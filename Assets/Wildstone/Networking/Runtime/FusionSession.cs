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
    // Call from Unity's main thread. Owns transport only; no gameplay admission or reconnect identity binding yet.
    public sealed class FusionSession : INetworkRunnerCallbacks
    {
        readonly SessionState rules;
        readonly ContentVersion content;
        readonly byte[] contentToken;
        readonly Dictionary<NetworkRunner, long> attempts = new Dictionary<NetworkRunner, long>();
        NetworkRunner runner;
        CancellationTokenSource operation;
        Task<bool> starting;
        Task stopping;
        long generation;
        public string Failure { get; private set; }
        public SessionPhase Phase => Failure == null ? rules.Phase : SessionPhase.Failed;
        public Task Completion => stopping ?? (Task)starting ?? Task.CompletedTask;
        public event Action Changed;

        public FusionSession(ContentVersion content, string matchId)
        {
            rules = new SessionState(content, matchId);
            this.content = content;
            contentToken = ContentToken(content);
        }

        // The caller must complete real provider configuration; AuthValues are sent to Photon for verification.
        // No supplied UserId is accepted as proof of identity, and no credentials are persisted or logged.
        public Task<bool> StartAsync(bool host, string room, FusionAppSettings settings,
            AuthenticationValues authentication, bool verifiedProviderConfigured, CancellationToken cancellation = default)
        {
            if (runner != null || starting != null || stopping != null)
                throw new InvalidOperationException("Stop the current session before starting another.");
            if (cancellation.IsCancellationRequested)
            {
                rules.Leave(); Failure = null;
                return Task.FromResult(false);
            }
            ValidateStart(room, settings, authentication, verifiedProviderConfigured);
            var config = NetworkProjectConfig.Global;
            ValidateProjectConfig(config);
            if (!rules.BeginConnect(Time.realtimeSinceStartupAsDouble))
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
                runner = owned;
                attempts.Add(owned, attempt);
                owned.AddCallbacks(this);
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
                    ConnectionToken = (byte[])contentToken.Clone(),
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
                    Failure = "Authenticated session or content verification failed.";
                    return false;
                }
                if (!rules.EnterLobby(attempt, content, Time.realtimeSinceStartupAsDouble))
                {
                    Failure = "The connection attempt expired.";
                    return false;
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
                var pending = starting;
                try { if (pending != null) await pending; }
                finally { try { await ShutdownOwned(runner); } finally { rules.Leave(); } }
            }
            catch (Exception) { Failure = "Session cleanup failed. Please retry."; }
            finally { stopping = null; Changed?.Invoke(); }
        }
        async Task ShutdownOwned(NetworkRunner owned)
        {
            if (ReferenceEquals(owned, null)) return;
            if (ReferenceEquals(runner, owned)) runner = null;
            attempts.Remove(owned);
            if (owned == null) return;
            try { owned.RemoveCallbacks(this); await owned.Shutdown(); }
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

        public static void ValidateStart(string room, FusionAppSettings settings, AuthenticationValues authentication, bool providerConfigured)
        {
            if (string.IsNullOrWhiteSpace(room) || room.Length < 4 || room.Length > 12 ||
                !Array.TrueForAll(room.ToCharArray(), c => c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'))
                throw new ArgumentException("Room code must contain 4-12 uppercase letters or digits.", nameof(room));
            if (!providerConfigured || authentication == null || authentication.AuthType != CustomAuthenticationType.Custom ||
                string.IsNullOrWhiteSpace(authentication.AuthGetParameters))
                throw new InvalidOperationException("Configured recoverable provider authentication is required.");
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
        static Dictionary<string, SessionProperty> Properties(ContentVersion v) => new Dictionary<string, SessionProperty>
        { ["protocol"] = v.Protocol, ["build"] = v.Build, ["map"] = v.Map, ["rules"] = v.Ruleset };
        public static bool MatchesProperties(IReadOnlyDictionary<string, SessionProperty> properties, ContentVersion v) =>
            v.IsValid && properties != null && properties.TryGetValue("protocol", out var protocol) && protocol.IsString && (string)protocol == v.Protocol &&
            properties.TryGetValue("build", out var build) && build.IsString && (string)build == v.Build &&
            properties.TryGetValue("map", out var map) && map.IsString && (string)map == v.Map &&
            properties.TryGetValue("rules", out var ruleset) && ruleset.IsString && (string)ruleset == v.Ruleset;
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // This token checks content only, never identity. Photon custom authentication remains mandatory.
            if (!Current(r) || token == null || token.Length != contentToken.Length) { request.Refuse(); return; }
            for (int i = 0; i < token.Length; i++) if (token[i] != contentToken[i]) { request.Refuse(); return; }
            request.Accept();
        }
        public void OnShutdown(NetworkRunner r, ShutdownReason reason) => Fail(r, "The session has closed.");
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) => Fail(r, "Connection lost. Please retry.");
        public void OnConnectFailed(NetworkRunner r, NetAddress address, NetConnectFailedReason reason) => Fail(r, "Connection failed. Please retry.");
        public void OnHostMigration(NetworkRunner r, HostMigrationToken token) => Fail(r, "Host recovery is not available in this transport stage.");
        public void OnPlayerJoined(NetworkRunner r, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner r, PlayerRef player) { }
        public void OnInput(NetworkRunner r, NetworkInput input) { }
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




