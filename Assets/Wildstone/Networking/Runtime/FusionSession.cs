using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using Photon.Realtime;
using UnityEngine;
using Wildstone.Session;

namespace Wildstone.Networking
{
    // Call from Unity's main thread. Anonymous Photon peers and credential-protected host seat admission.
    public sealed class FusionSession : INetworkRunnerCallbacks
    {
        SessionState rules;
        readonly string matchId;
        readonly FusionRecovery recovery;
        FusionRecoveryClock logicalClock;
        FusionAppSettings savedSettings;
        AuthenticationValues savedAuthentication;
        string savedRoom;
        bool recovering, gracefulFreeze, awaitingMigrationToken;
        NetworkRunner disconnectedRunner;
        public bool SimulationFrozen => gracefulFreeze || !ReferenceEquals(runner,null) && ReferenceEquals(runner,disconnectedRunner) || Lobby!=null && Lobby.Object!=null && Lobby.Object.IsValid && Lobby.FinalFrozen;
        double recoveryDeadline;
        Task<bool> migrating;
        public double Now => logicalClock==null?Time.realtimeSinceStartupAsDouble:logicalClock.Now(Time.realtimeSinceStartupAsDouble);
        public bool Recovering => recovering;
        public double RecoverySecondsRemaining => recovering?Math.Max(0,recoveryDeadline-Time.realtimeSinceStartupAsDouble):0;
        public string MatchId => matchId;
        internal long LastPublishedTick => recovery.LastPublishedTick;
        readonly ContentVersion content;
        readonly byte[] contentToken;
        // One random secret per app run, like the guest ID; never persisted or logged. Each room derives its own credential from it,
        // so leaving and rejoining a room reclaims the held seat, while another room's host learns nothing that works here.
        static readonly byte[] appRunSecret = NewSecret();
        readonly byte[] reconnectCredential;
        static byte[] NewSecret() { var secret = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(secret); return secret; }
        static readonly Dictionary<NetworkRunner, FusionSession> owners = new Dictionary<NetworkRunner, FusionSession>();
        public NetworkRunner Runner => runner;
        public FusionLobby Lobby { get; private set; }
        public SessionState Rules => rules;
        public ContentVersion Content => content;
        internal byte[] ConnectionToken
        {
            get { var token = new byte[64]; Array.Copy(contentToken, token, 32); Array.Copy(reconnectCredential, 0, token, 32, 32); return token; }
        }
        internal static FusionSession OwnerOf(NetworkRunner r) => !ReferenceEquals(r,null) && owners.TryGetValue(r, out var owner) ? owner : null;
        internal void AttachLobby(FusionLobby lobby) { Lobby = lobby; Changed?.Invoke(); }
        internal void NotifyChanged() => Changed?.Invoke();
        readonly Dictionary<NetworkRunner, long> attempts = new Dictionary<NetworkRunner, long>();
        NetworkRunner runner;
        CancellationTokenSource operation;
        Task<bool> starting;
        Task stopping;
        long generation;
        public string Failure { get; private set; }
        public string RecoveryFailureDetail { get; private set; }
        public SessionPhase Phase => Failure != null ? SessionPhase.Failed : recovering?SessionPhase.Migrating:Lobby != null && Lobby.Object != null && Lobby.Object.IsValid ? Lobby.Phase : rules.Phase;
        public Task Completion => stopping ?? (Task)migrating ?? starting ?? Task.CompletedTask;
        public event Action Changed;
        public event Action<NetworkInput> InputRequested;

        public FusionSession(ContentVersion content, string matchId)
        {
            rules = new SessionState(content, matchId);
            this.matchId=matchId;
            recovery=new FusionRecovery(this);
            this.content = content;
            contentToken = ContentToken(content);
            using (var mac = new HMACSHA256(appRunSecret)) reconnectCredential = mac.ComputeHash(Encoding.UTF8.GetBytes(matchId));
        }

        // Keep the random guest ID for this app lifetime. It identifies a Photon peer, never a verified account.
        // A held seat additionally requires its secret reconnect credential; neither value is persisted or logged.
        public Task<bool> StartAsync(bool host, string room, FusionAppSettings settings,
            AuthenticationValues authentication, CancellationToken cancellation = default)
        {
            if (runner != null || starting != null || stopping != null || migrating!=null || recovering)
                throw new InvalidOperationException("Stop the current session before starting another.");
            if (cancellation.IsCancellationRequested)
            {
                rules.Leave(); Failure = null;
                return Task.FromResult(false);
            }
            ValidateStart(room, settings, authentication);
            var config = NetworkProjectConfig.Global;
#if WILDSTONE_SMOKE
            config = FusionSmokeNetworkProfile.Apply(config);
#endif
            ValidateProjectConfig(config);
            savedRoom=room; savedSettings=settings.GetCopy(); savedAuthentication=authentication;
            if (!rules.BeginConnect(Now))
                throw new InvalidOperationException("Session cannot begin a new connection attempt.");
            generation = rules.AttemptGeneration;
            Failure = null;
            operation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            operation.CancelAfter(TimeSpan.FromSeconds(SessionState.OperationTimeoutSeconds));
            starting = StartCore(host, room, settings, authentication, config, generation, operation.Token);
            return starting;
        }

        async Task<bool> StartCore(bool host, string room, FusionAppSettings settings,
            AuthenticationValues authentication, NetworkProjectConfig config, long attempt, CancellationToken cancellation)
        {
            await Task.Yield();
            NetworkRunner owned = null;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                owned = new GameObject("Wildstone Fusion " + attempt).AddComponent<NetworkRunner>();
                UnityEngine.Object.DontDestroyOnLoad(owned.gameObject);
                runner = owned;
                owned.ProvideInput = true;
                attempts.Add(owned, attempt);
                owners.Add(owned, this);
                owned.AddCallbacks(this);
                owned.gameObject.AddComponent<FusionRecoveryTicker>().Session=this;
                Changed?.Invoke();
                var result = await owned.StartGame(new StartGameArgs
                {
                    GameMode = host ? GameMode.Host : GameMode.Client,
                    SessionName = room,
                    IsVisible = false,
                    IsOpen = true,
                    EnableClientSessionCreation = false,
                    PlayerCount = SeatTable.Capacity,
                    Config = config,
                    CustomPhotonAppSettings = settings.GetCopy(),
                    AuthValues = authentication,
                    ConnectionToken = ConnectionToken,
                    SessionProperties = Properties(content),
                    StartGameCancellationToken = cancellation
                });
                if (!Current(owned) || attempt != generation || cancellation.IsCancellationRequested || !result.Ok || Failure != null)
                {
                    if (Failure == null) Failure = cancellation.IsCancellationRequested ? "Connection cancelled or timed out." : "Connection failed. Please retry.";
                    return false;
                }
                if (!MatchesProperties(owned.SessionInfo.Properties, content) ||
                    host && string.IsNullOrWhiteSpace(owned.GetPlayerUserId(owned.LocalPlayer)))
                {
                    Failure = "Anonymous peer or content verification failed.";
                    return false;
                }
                if (!rules.EnterLobby(attempt, content, Now))
                {
                    Failure = "The connection attempt expired.";
                    return false;
                }
                if (host)
                {
                    var prefab = Resources.Load<NetworkObject>("WildstoneFusionLobby");
                    if (prefab == null) throw new InvalidOperationException("Registered lobby prefab is missing.");
                    var spawned = owned.Spawn(prefab);
                    Lobby = spawned.GetComponent<FusionLobby>();
                    if (Lobby == null) throw new InvalidOperationException("Registered lobby component is missing.");
                    foreach (var player in owned.ActivePlayers)
                        if (!Lobby.Admit(player) && Lobby.Rejection != FusionLobby.IdentityPending)
                        {
                            if (player == owned.LocalPlayer) throw new InvalidOperationException("Host guest admission failed.");
                            LogRemoval(Lobby.Rejection, Lobby.Phase); owned.Disconnect(player);
                        }
                }
                return true;
            }
            catch (OperationCanceledException) { Failure = "Connection cancelled or timed out."; return false; }
            catch (Exception) { Failure = "Connection failed. Please retry."; return false; }
            finally
            {
                try
                {
                    if (rules.Phase != SessionPhase.Lobby || Failure != null || cancellation.IsCancellationRequested)
                    {
                        try { await ShutdownOwned(owned); }
                        finally { rules.Leave(); }
                    }
                }
                finally
                {
                    starting = null;
                    operation?.Dispose(); operation = null;
                    Changed?.Invoke();
                }
            }
        }

        public Task StopAsync()
        {
            if (stopping != null) return stopping;
            stopping = StopCore();
            return stopping;
        }
        async Task StopCore()
        {
            await Task.Yield();
            try
            {
                operation?.Cancel();
                var recoveryTask=migrating;
                if(recoveryTask!=null) await recoveryTask;
                var pending = starting;
                try { if (pending != null) await pending; }
                finally
                {
                    try
                    {
                        if(!recovering && runner!=null && runner.IsServer && Failure==null)
                        {
                            gracefulFreeze=true;
                            await recovery.PublishBeforeLeave(runner);
                        }
                        await ShutdownOwned(runner);
                    }
                    finally { rules.Leave(); recovering=false; gracefulFreeze=false; awaitingMigrationToken=false; disconnectedRunner=null; recovery.Reset(); }
                }
            }
            catch (Exception) { Failure = "Session cleanup failed. Please retry."; }
            finally { stopping = null; Changed?.Invoke(); }
        }
        async Task ShutdownOwned(NetworkRunner owned, ShutdownReason reason = ShutdownReason.Ok)
        {
            if (ReferenceEquals(owned, null)) return;
            if (ReferenceEquals(runner, owned)) { runner = null; Lobby = null; }
            owners.Remove(owned);
            attempts.Remove(owned);
            if (owned == null) return;
            try { owned.RemoveCallbacks(this); await owned.Shutdown(shutdownReason:reason); }
            catch (Exception) { Failure = "Session cleanup failed. Please retry."; }
            finally { if (owned != null) UnityEngine.Object.Destroy(owned.gameObject); }
        }
        bool Current(NetworkRunner candidate) => !ReferenceEquals(candidate, null) && ReferenceEquals(runner, candidate) && attempts.TryGetValue(candidate, out var capturedAttempt) &&
            capturedAttempt == generation && generation == rules.AttemptGeneration;
        void Fail(NetworkRunner candidate, string message)
        {
            if (!Current(candidate)) return;
            Failure = message;
            operation?.Cancel();
            // StopAsync retains the task; the owner awaits Completion before disposing this session.
            StopAsync();
        }
        internal void ServiceRecovery(NetworkRunner current)
        {
            if(!Current(current) || Failure!=null || gracefulFreeze) return;
            try
            {
                if(recovering)
                {
                    if(Time.realtimeSinceStartupAsDouble>=recoveryDeadline) { Fail(current,"Host recovery timed out. The match was aborted."); return; }
                    if(awaitingMigrationToken || migrating!=null || !current.IsRunning) return;
                    if(Lobby!=null && Lobby.Phase==SessionPhase.Running)
                    { recovering=false; recovery.FinishedMigration(); Changed?.Invoke(); }
                    else return;
                }
                recovery.ObserveAndPublish(current);
            }
            catch(Exception) { Fail(current,"The complete recovery checkpoint could not be validated. The match was aborted."); }
        }
        async Task<bool> MigrateCore(NetworkRunner retired,HostMigrationToken token)
        {
            await Task.Yield();
            NetworkRunner replacement=null;
            var source=new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(.001,recoveryDeadline-Time.realtimeSinceStartupAsDouble)));
            operation=source;
            string stage="shutdown-retired";
            RecoveryFailureDetail=null;
            try
            {
                // The retired runner is destroyed before constructing the replacement; no old callbacks or histories survive.
                await ShutdownOwned(retired,ShutdownReason.HostMigration);
                source.Token.ThrowIfCancellationRequested();
                stage="create-replacement";
                replacement=new GameObject("Wildstone recovered Fusion").AddComponent<NetworkRunner>();
                UnityEngine.Object.DontDestroyOnLoad(replacement.gameObject);
                runner=replacement; replacement.ProvideInput=true; attempts.Add(replacement,generation); owners.Add(replacement,this);
                replacement.AddCallbacks(this); replacement.gameObject.AddComponent<FusionRecoveryTicker>().Session=this;
                var sceneManager=replacement.gameObject.AddComponent<NetworkSceneManagerDefault>();
                var objectProvider=replacement.gameObject.AddComponent<NetworkObjectProviderDefault>();
                bool hostRestored=false;
                var resumeCompletion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                stage="start-replacement";
                var result=await replacement.StartGame(new StartGameArgs
                {
                    GameMode=token.GameMode,HostMigrationToken=token,
                    HostMigrationResume=resuming=>CompleteRecoveryResume(()=>
                    {
                        try
                        {
                            stage="validate-resume";
                            source.Token.ThrowIfCancellationRequested();
                            if(!Current(resuming)) throw new InvalidOperationException("Retired recovery attempt.");
                            var isolated=recovery.ValidateResume(resuming);
                            rules=isolated; generation=rules.AttemptGeneration; attempts[resuming]=generation;
                            stage="restore-world";
                            recovery.RestoreValidated(resuming); hostRestored=true;
                            stage="resume-complete";
                            resumeCompletion.TrySetResult(true);
                        }
                        catch(Exception exception)
                        {
                            RecordRecoveryFailure(stage,exception); throw;
                        }
                    },resumeCompletion),
                    SessionName=savedRoom,IsVisible=false,IsOpen=true,EnableClientSessionCreation=false,PlayerCount=SeatTable.Capacity,
                    Config=NetworkProjectConfig.Global,CustomPhotonAppSettings=savedSettings.GetCopy(),AuthValues=savedAuthentication,
                    SceneManager=sceneManager,ObjectProvider=objectProvider,
                    ConnectionToken=ConnectionToken,SessionProperties=Properties(content),StartGameCancellationToken=source.Token
                });
                source.Token.ThrowIfCancellationRequested();
                Debug.Log("WILDSTONE RECOVERY START mode="+token.GameMode+" ok="+result.Ok+" isResume="+replacement.IsResume);
                if(result.Ok && token.GameMode==GameMode.Host)
                {
                    stage="await-resume-callback";
                    using(source.Token.Register(()=>resumeCompletion.TrySetCanceled())) await resumeCompletion.Task;
                }
                else if(resumeCompletion.Task.IsFaulted) _=resumeCompletion.Task.Exception;
                source.Token.ThrowIfCancellationRequested();
                if(!result.Ok) stage="start-result-"+result.ShutdownReason;
                else stage="validate-start-result";
                bool current=Current(replacement), propertiesMatch=MatchesProperties(replacement.SessionInfo.Properties,content);
                if(!result.Ok || Failure!=null || !current || token.GameMode==GameMode.Host && !hostRestored || !propertiesMatch)
                    throw new InvalidOperationException($"Recovery startup failed: mode={token.GameMode}, ok={result.Ok}, restored={hostRestored}, current={current}, failure={Failure!=null}, properties={propertiesMatch}, isResume={replacement.IsResume}.");
                Changed?.Invoke(); return true;
            }
            catch(Exception exception)
            {
                RecordRecoveryFailure(stage,exception);
                Failure="Host recovery failed. The match was aborted; no winner was awarded."; return false;
            }
            finally
            {
                if(Failure!=null || source.IsCancellationRequested)
                { await ShutdownOwned(replacement); rules.Leave(); recovering=false; }
                if(ReferenceEquals(operation,source)) operation=null;
                source.Dispose(); migrating=null; Changed?.Invoke();
            }
        }

        internal static void CompleteRecoveryResume(Action restore,TaskCompletionSource<bool> completion)
        {
            try { restore(); }
            catch(Exception exception)
            {
                // Let Fusion finish disposing its migration snapshot before MigrateCore owns shutdown.
                completion.TrySetException(exception);
            }
        }

        void RecordRecoveryFailure(string stage,Exception exception)
        {
            if(RecoveryFailureDetail!=null) return;
            RecoveryFailureDetail=FormatRecoveryFailure(stage,exception);
            Debug.LogError("WILDSTONE RECOVERY FAILURE "+RecoveryFailureDetail);
        }
        internal static string FormatRecoveryFailure(string stage,Exception exception)
        {
            var method=exception.TargetSite;
            var owner=method?.DeclaringType;
            var scope=owner?.DeclaringType??owner;
            bool local=scope==typeof(FusionSession) || scope==typeof(FusionRecovery) || scope==typeof(FusionRecoveryValidation) ||
                scope==typeof(FusionRecoveryState) || scope==typeof(FusionRecoveryClock) || scope==typeof(FusionLobby) ||
                scope==typeof(FusionPlayer) || scope==typeof(FusionMatch) || scope==typeof(SessionState);
            string detail=local?exception.Message:"External exception message omitted.";
            if(detail.Length>256) detail=detail.Substring(0,256);
            detail=new string(Array.ConvertAll(detail.ToCharArray(),c=>char.IsControl(c)?' ':c));
            return "stage="+stage+" type="+exception.GetType().Name+" at="+(owner?.FullName??"unknown")+"."+(method?.Name??"unknown")+" detail="+detail;
        }
        public static void ValidateStart(string room, FusionAppSettings settings, AuthenticationValues authentication)
        {
            if (string.IsNullOrWhiteSpace(room) || room.Length < 4 || room.Length > 12 ||
                !Array.TrueForAll(room.ToCharArray(), c => c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'))
                throw new ArgumentException("Room code must contain 4-12 uppercase letters or digits.", nameof(room));
            if (authentication == null || authentication.AuthType != CustomAuthenticationType.None ||
                !Guid.TryParseExact(authentication.UserId, "N", out var guestId) || guestId == Guid.Empty ||
                !string.IsNullOrEmpty(authentication.AuthGetParameters) || authentication.AuthPostData != null)
                throw new InvalidOperationException("An anonymous Photon guest ID without custom authentication is required.");
            if (settings == null || !Guid.TryParse(settings.AppIdFusion, out var appId) || appId == Guid.Empty || string.IsNullOrWhiteSpace(settings.FixedRegion))
                throw new InvalidOperationException("A real Fusion App ID and common region are required.");
        }
        public static void ValidateProjectConfig(NetworkProjectConfig config)
        {
            if (config == null) throw new InvalidOperationException("Fusion project configuration is required.");
            var rates = TickRate.Resolve(config.Simulation.TickRateSelection);
            if (config.Simulation.PlayerCount != SeatTable.Capacity || rates.Client != 120 || rates.Server != 120 || rates.ServerSend != 60)
                throw new InvalidOperationException("Wildstone requires 120 Hz host/client simulation, 60 Hz snapshots and eight players.");
        }
        public static byte[] ContentToken(ContentVersion version)
        {
            if (!version.IsValid) throw new ArgumentException("Exact content versions are required.");
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true))
                { writer.Write(version.Protocol); writer.Write(version.Build); writer.Write(version.Map); writer.Write(version.Ruleset); }
                using (var sha = SHA256.Create()) return sha.ComputeHash(bytes.ToArray());
            }
        }
        public static bool TryReadCredential(byte[] token, ContentVersion content, out byte[] credential)
        {
            credential = null;
            if (!content.IsValid || token == null || token.Length != 64) return false;
            var expected = ContentToken(content);
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= token[i] ^ expected[i];
            if (difference != 0) return false;
            credential = new byte[32]; Array.Copy(token, 32, credential, 0, 32);
            return true;
        }
        static Dictionary<string, SessionProperty> Properties(ContentVersion v) => new Dictionary<string, SessionProperty>
        { ["protocol"] = v.Protocol, ["build"] = v.Build, ["map"] = v.Map, ["rules"] = v.Ruleset };
        public static bool MatchesProperties(IReadOnlyDictionary<string, SessionProperty> properties, ContentVersion v) =>
            v.IsValid && properties != null && properties.TryGetValue("protocol", out var protocol) && protocol.IsString && (string)protocol == v.Protocol &&
            properties.TryGetValue("build", out var build) && build.IsString && (string)build == v.Build &&
            properties.TryGetValue("map", out var map) && map.IsString && (string)map == v.Map &&
            properties.TryGetValue("rules", out var ruleset) && ruleset.IsString && (string)ruleset == v.Ruleset;
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // This checks content and carries a reconnect secret; admission binds the anonymous SDK peer separately.
            if (!Current(r) || SimulationFrozen || !TryReadCredential(token, content, out _)) { request.Refuse(); return; }
            request.Accept();
        }
        // Host-side reason for removing a peer, written to the player log. Fixed codes only: no player, user or session IDs.
        internal static void LogRemoval(string reason, SessionPhase phase) =>
            Debug.LogWarning("WILDSTONE HOST REMOVED PEER reason=" + (reason ?? "unknown") + " phase=" + phase);
        public void OnShutdown(NetworkRunner r, ShutdownReason reason)
        {
            if(!Current(r)) return;
#if WILDSTONE_SMOKE
            Debug.Log("WILDSTONE TRANSPORT EVENT shutdown="+reason+" awaitingMigration="+awaitingMigrationToken);
#endif
            if(awaitingMigrationToken && reason==ShutdownReason.HostMigration) return;
            Fail(r,"The session has closed.");
        }
        internal static bool CanAwaitMigrationToken(NetDisconnectReason reason,bool server,SessionPhase phase) =>
            reason==NetDisconnectReason.Timeout && !server && (phase==SessionPhase.Running || phase==SessionPhase.Warmup);
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
        {
            if(!Current(r)) return;
#if WILDSTONE_SMOKE
            Debug.Log("WILDSTONE TRANSPORT EVENT disconnected="+reason+" server="+r.IsServer+" awaitingMigration="+awaitingMigrationToken);
#endif
            if(awaitingMigrationToken && reason==NetDisconnectReason.Timeout) return;
            if(!recovering && starting==null && stopping==null && savedSettings!=null && savedAuthentication!=null && CanAwaitMigrationToken(reason,r.IsServer,Phase))
            {
                try
                {
                    RecoveryFailureDetail=null;
                    logicalClock=recovery.Begin(r);
                    recoveryDeadline=Time.realtimeSinceStartupAsDouble+SessionState.RecoveryTimeoutSeconds;
                    disconnectedRunner=r; awaitingMigrationToken=true; recovering=true;
                    Changed?.Invoke(); return;
                }
                catch(Exception exception) { RecordRecoveryFailure("transport-timeout-baseline",exception); }
            }
            Fail(r,"Connection lost. Please retry.");
        }
        public void OnConnectFailed(NetworkRunner r, NetAddress address, NetConnectFailedReason reason) => Fail(r, "Connection failed. Please retry.");
        public void OnHostMigration(NetworkRunner r, HostMigrationToken token)
        {
            if(!Current(r)) return;
            if(awaitingMigrationToken && Time.realtimeSinceStartupAsDouble>=recoveryDeadline)
            { Fail(r,"Host recovery timed out. The match was aborted."); return; }
            if(recovering && !awaitingMigrationToken || migrating!=null || starting!=null || stopping!=null || token==null ||
                token.GameMode!=GameMode.Host && token.GameMode!=GameMode.Client ||
                savedSettings==null || savedAuthentication==null)
            { Fail(r,"Host recovery could not start or was interrupted. The match was aborted."); return; }
            try
            {
                ValidateStart(savedRoom,savedSettings,savedAuthentication);
                if(!awaitingMigrationToken)
                {
                    logicalClock=recovery.Begin(r);
                    recoveryDeadline=Time.realtimeSinceStartupAsDouble+SessionState.RecoveryTimeoutSeconds;
                }
                awaitingMigrationToken=false; recovering=true;
                Changed?.Invoke(); migrating=MigrateCore(r,token);
            }
            catch(Exception) { Fail(r,"A valid complete host checkpoint is unavailable. The match was aborted."); }
        }
        public void OnPlayerJoined(NetworkRunner r, PlayerRef player)
        {
            // Startup callbacks can precede EnterLobby/spawn; StartCore admits all current players afterward.
            if (!Current(r) || !r.IsServer || Lobby == null) return;
            if (SimulationFrozen || !Lobby.Admit(player) && Lobby.Rejection != FusionLobby.IdentityPending) { LogRemoval(SimulationFrozen ? "simulation-frozen" : Lobby.Rejection, Lobby.Phase); r.Disconnect(player); }
            Changed?.Invoke();
        }
        public void OnPlayerLeft(NetworkRunner r, PlayerRef player)
        {
            if (!Current(r) || !r.IsServer || Lobby == null) return;
            if (!SimulationFrozen) { Lobby.Disconnect(player); Changed?.Invoke(); }
        }
        public void OnInput(NetworkRunner r, NetworkInput input) { if (Current(r) && !recovering && !SimulationFrozen) InputRequested?.Invoke(input); }
        public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessions) { }
        public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
        public void OnSceneLoadDone(NetworkRunner r) { }
        public void OnSceneLoadStart(NetworkRunner r) { }
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
    }
}




