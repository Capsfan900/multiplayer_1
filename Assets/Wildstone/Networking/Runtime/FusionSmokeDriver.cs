#if WILDSTONE_SMOKE
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using Wildstone.Game;
using Wildstone.Combat;
using Wildstone.Match;
using Wildstone.Session;
using Wildstone.UI;

namespace Wildstone.Networking
{
    // Only compiled into explicitly requested smoke builds; every transition uses the normal game/menu components.
    public sealed partial class FusionSmokeDriver : MonoBehaviour
    {
        [Serializable] sealed class Status
        {
            public FusionSmokeNetworkProfile.Report networkProfile;
            public int sdkRemoteRttSamples;
            public double sdkRemoteRttMinMs, sdkRemoteRttMaxMs, sdkRemoteRttMeanMs;
            public CombatMetrics combat;
            public LaserMetrics[] lasers;
            public GunplayMetrics[] gunplay;
            public ProjectileMetrics projectile;
            public GrenadeMetrics grenade;
            public SpecialProjectileMetrics[] specialProjectiles;
            public DeflectionRound[] deflection;
            public StunClientMetrics stunClient;
            public RematchMetrics rematch;
            public string role,scenario,stage="boot",room="",error="",phase="Closed",screen="Title";
            // "stage@elapsed;" per stage change (whole seconds; the latest ~3000 chars), so a deadline failure shows which step took the time.
            public string stageTimes="";
            public bool success,finished,externalSimulation;
            public bool initialVerified,reconnectRequested,reconnectDisconnected,reconnectComplete,retainedAvatar;
            public bool migrationRequested,migrationComplete,sawRunnerChange,electedHost,isServer;
            public bool crashReady;
            public bool lateJoinComplete;
            public int lateJoinConnectedPlayers,lateJoinAvatars,lateJoinTeams,lateJoinParticipant;
            public float lateJoinStableSeconds;
            public long publishedTick=-1;
            public int originalAuthorityGeneration,originalParticipant,originalTeam,originalLifeGeneration,originalStarterAmmo;
            public int preExitTick,minimumPostRecoveryTick,rollbackTicks,newHostCount;
            public float originalHealth,migrationSeconds,postRecoveryStableSeconds;
            public string originalMatchId="",originalContent="";
            public int runnerTick,matchTick,playerCount,avatarCount,localParticipant,authorityGeneration,connectionGeneration;
            public int previousConnectionGeneration,previousLifeGeneration,lifeGeneration,starterAmmo;
            public int recoveredConnectionGeneration,recoveredLifeGeneration,recoveredStarterAmmo;
            public float elapsed,stableSeconds;
            public float reconnectSeconds,health,recoveredHealth;
            public Vector3 localPosition,remotePosition;
            public bool lobbyReady;
            public int teamSwitchStep,teamSwitchClearAck,teamSwitchReadyAck,teamSwitchInitialTeam,teamSwitchGuestPlayer;
            public long teamSwitchConnectionGeneration;
            public string teamSwitchGuestFingerprint="";
            public bool teamSwitchComplete;
            public int roleIndex,connectedPlayerCount,connectedTeamCount;
            public int capacityVerifiedPlayers,capacityVerifiedAvatars,capacityVerifiedConnected,capacityVerifiedTeams,capacityVerifiedMatchTick;
            public int[] capacityTeamSizes,capacityVerifiedTeamSizes;
            public int capacitySoakSeconds,capacityRunningSdkTicks,capacityFinishedSdkTicks;
            public float capacityRunningSeconds,capacityFinishedSeconds,capacityHoldStartElapsed,capacityHoldEndElapsed;
            public bool capacityMatchFinished;
            // Fusion Debug-DLL per-update In/OutBandwidth summed over the hold; ForwardTicks should track the SDK tick totals above.
            public bool capacityStatsAvailable;
            public int capacityStatsForwardTicks;
            public double capacityRunningInBytes,capacityRunningOutBytes,capacityFinishedInBytes,capacityFinishedOutBytes;
            public float capacityRunningInBytesPerSecond,capacityRunningOutBytesPerSecond,capacityFinishedInBytesPerSecond,capacityFinishedOutBytesPerSecond;
            // Gameplay soak (capacity plus --wildstone-smoke-soak-gameplay 1): every peer moves and fires; the host rematches.
            public bool soakGameplay;
            public int soakRoundSeconds,soakRounds,soakShots,soakReloads,soakDeaths;
            public float soakBetweenSeconds;
            // Managed heap in use and Gen0 collections so far; the runner copies them into each per-minute memory point.
            public long managedHeapBytes;
            // Largest frame gap seen by this process: a machine-wide stall drops peers and is not a game desync.
            public float maxFrameGapSeconds;
            public int gcCollections;
        }
        Status status;
        string output,peerOutput,joinRoom;
        FusionMenuController menu;
        OnlineMenuBridge bridge;
        PracticeSession presentation;
        bool connectedRequested,teamRequested,readyRequested,startRequested,finishing;
        double began,stableAt=-1,verifiedAt=-1,lastTickAt,nextWrite;
        string loggedStage;
        double nextRttSample;
        int firstMatchTick,lastMatchTick=-1;
        FusionSession retainedSession;
        bool retryRequested;
        int reconnectParticipant,reconnectTeam,baselineAmmo;
        float baselineHealth;
        double disconnectAt=-1, migrationAt=-1;
        NetworkRunner originalRunner;
        ContentVersion originalContent;
        string originalMatchId;
        int minimumPostRecoveryTick=int.MaxValue;
        NetworkRunner teamSwitchRunner;
        string teamSwitchGuestIdentity;
        bool teamSwitchInitialized,teamSwitchActionSent;
        double teamSwitchProgressAt;
        bool IsMigrationScenario => status.scenario=="migration" || status.scenario=="migration-crash";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartRequestedSmoke()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--wildstone-smoke-role")<0) return;
            try
            {
                var values=new Dictionary<string,string>(StringComparer.Ordinal);
                for(int i=1;i<args.Length;i++)
                    if(args[i].StartsWith("--wildstone-smoke-",StringComparison.Ordinal))
                    { if(i+1>=args.Length) throw new ArgumentException(); values.Add(args[i],args[++i]); }
                var go=new GameObject("Wildstone production smoke driver"); DontDestroyOnLoad(go);
                go.AddComponent<FusionSmokeDriver>().Configure(values);
            }
            catch(Exception exception) { Debug.LogError("Smoke setup failed: "+exception.GetType().Name); Application.Quit(2); }
        }
        void Configure(Dictionary<string,string> args)
        {
            output=CheckedPath(args["--wildstone-smoke-output"]);
            if(args.TryGetValue("--wildstone-smoke-peer-output",out var peer)) peerOutput=CheckedPath(peer);
            status=new Status {role=args["--wildstone-smoke-role"],scenario=args.TryGetValue("--wildstone-smoke-scenario",out var scenario)?scenario:"basic"};
            FusionSmokeNetworkProfile.ConfigureFromArgs(args);
            status.networkProfile=FusionSmokeNetworkProfile.Configuration;
            began=Time.realtimeSinceStartupAsDouble;
            bool capacity=status.scenario=="capacity";
            if(capacity?CapacityRoleIndex(status.role)<0:status.role!="host" && status.role!="client" && status.role!="client2") throw new ArgumentException();
            if(status.scenario!="basic" && status.scenario!="reconnect" && !IsMigrationScenario && status.scenario!="combat" && status.scenario!="projectile-combat" && status.scenario!="grenade-combat" && status.scenario!="special-projectiles" && status.scenario!="deflection" && status.scenario!="stun-client" && status.scenario!="laser-combat" && status.scenario!="laser-moving-cover" && status.scenario!="gunplay" && status.scenario!="late-join" && status.scenario!="team-switch" && status.scenario!="rematch" && !capacity) throw new NotSupportedException("This smoke scenario is not implemented.");
            if(status.role=="client2" && !IsMigrationScenario && status.scenario!="late-join" && !capacity) throw new ArgumentException();
            if((IsMigrationScenario || capacity || status.scenario=="late-join") && Path.GetFileName(output)!=status.role+".json") throw new ArgumentException("This scenario requires fixed sibling status filenames.");
            if(capacity)
            {
                status.roleIndex=CapacityRoleIndex(status.role);
                status.capacitySoakSeconds=ParseCapacitySoakSeconds(args.TryGetValue("--wildstone-smoke-soak-seconds",out var soak)?soak:null);
                status.soakGameplay=args.TryGetValue("--wildstone-smoke-soak-gameplay",out var gameplay) && gameplay=="1";
                if(args.TryGetValue("--wildstone-smoke-soak-round-seconds",out var round) &&
                    (!status.soakGameplay || !int.TryParse(round,NumberStyles.None,CultureInfo.InvariantCulture,out status.soakRoundSeconds) || status.soakRoundSeconds<20 || status.soakRoundSeconds>480))
                    throw new ArgumentException("Gameplay soak rounds must be 20 to 480 seconds.");
            }
            if((status.scenario=="reconnect" || status.scenario=="combat" || status.scenario=="projectile-combat" || status.scenario=="grenade-combat" || status.scenario=="special-projectiles" || status.scenario=="deflection" || status.scenario=="stun-client" || status.scenario=="laser-combat" || status.scenario=="laser-moving-cover" || status.scenario=="gunplay" || status.scenario=="team-switch" || status.scenario=="rematch") && peerOutput==null) throw new ArgumentException("This scenario requires the peer status file.");
            if(status.role!="host")
            {
                joinRoom=args["--wildstone-smoke-room"].Trim().ToUpperInvariant();
                if(!MenuFlow.ValidJoinCode(joinRoom)) throw new ArgumentException();
            }
            WriteOutput();
        }
        static string CheckedPath(string path)
        {
            if(!Path.IsPathRooted(path)) throw new ArgumentException();
            string full=Path.GetFullPath(path);
            if(full.Replace('\\','/').IndexOf("/Artifacts/Smoke/",StringComparison.OrdinalIgnoreCase)<0 ||
                !full.EndsWith(".json",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException();
            return full;
        }
        void Update()
        {
            if(status==null || finishing) return;
            try
            {
                double now=Time.realtimeSinceStartupAsDouble; status.elapsed=(float)(now-began);
                if(status.stage!=loggedStage)
                {
                    loggedStage=status.stage; status.stageTimes+=status.stage+"@"+((int)status.elapsed)+";";
                    if(status.stageTimes.Length>4000) status.stageTimes=status.stageTimes.Substring(status.stageTimes.Length-3000);
                }
                // Injected latency stretches every round-trip step; one moving-cover adverse150 run verified everything at 118 s.
                if(now-began>=(status.scenario=="capacity"?status.capacitySoakSeconds+120:IsMigrationScenario || status.networkProfile?.injected==true?180:120)) { Finish(false,"DeadlineExceeded"); return; }
                if(menu==null)
                {
                    menu=FindFirstObjectByType<FusionMenuController>();
                    if(menu==null) return;
                    bridge=menu.GetComponent<OnlineMenuBridge>(); presentation=menu.GetComponent<PracticeSession>();
                }
                status.screen=presentation.Flow.Screen.ToString();
                if(!connectedRequested)
                {
                    if(presentation.Settings==null || presentation.Catalog==null || presentation.Flow.ForegroundLoading) return;
                    if(!bridge.Available) throw new InvalidOperationException();
                    connectedRequested=true; presentation.Show(MenuScreen.Online); status.stage="connecting";
                    if(status.role=="host") bridge.HostRoom(); else bridge.Connect(joinRoom);
                }
                var session=menu.Session;
                if(session==null)
                {
                    if(!bridge.Busy) throw new InvalidOperationException();
                    PublishDue(now); return;
                }
                retainedSession=retainedSession??session;
                var peerStatus=IsMigrationScenario || status.scenario=="capacity" || status.scenario=="late-join"?null:ReadPeer();
                if(peerStatus!=null && peerStatus.finished && !peerStatus.success) throw new InvalidOperationException("The peer smoke process failed.");
                if(status.scenario=="reconnect" && status.role=="client" && peerStatus!=null && peerStatus.reconnectRequested && !status.reconnectRequested)
                {
                    if(!status.initialVerified) throw new InvalidOperationException();
                    status.reconnectRequested=true; disconnectAt=now; stableAt=-1; lastMatchTick=-1;
                    status.stableSeconds=0; status.stage="awaiting_disconnect"; WriteOutput();
                }
                if(!ReferenceEquals(session,retainedSession)) throw new InvalidOperationException("The retained session changed.");
                if(session.Failure!=null)
                {
                    if(status.scenario!="reconnect" || status.role!="client" || !status.reconnectRequested) throw new InvalidOperationException(session.Failure);
                    status.phase=session.Phase.ToString(); status.stage="reconnecting";
                    status.externalSimulation=presentation.ExternalSimulation;
                    if(!retryRequested && peerStatus!=null && peerStatus.reconnectDisconnected && bridge.CanReconnect && session.Completion.IsCompleted)
                    { retryRequested=true; bridge.RetryConnection(); }
                    else if(retryRequested && !bridge.Busy && session.Completion.IsCompleted) throw new InvalidOperationException("The reconnect attempt failed.");
                    PublishDue(now); return;
                }
                status.phase=session.Phase.ToString(); status.room=bridge.Room;
                var runner=session.Runner;
                if(runner!=null && runner.IsRunning) status.runnerTick=runner.Tick.Raw;
                var lobby=session.Lobby;
                if(status.scenario=="team-switch" && teamSwitchInitialized && !ReferenceEquals(runner,teamSwitchRunner))
                    throw new InvalidOperationException("Team switching replaced the runner.");
                if(status.scenario=="team-switch" && lobby!=null && runner!=null && runner.IsRunning && lobby.Phase==SessionPhase.Lobby)
                    DriveTeamSwitch(lobby,runner,peerStatus,now);
                if(status.scenario=="rematch" && lobby!=null && runner!=null && runner.IsRunning && lobby.Phase==SessionPhase.Lobby)
                    ObserveRematchLobby(lobby,runner);
                if(status.scenario!="team-switch" && lobby!=null && runner!=null && runner.IsRunning && lobby.Phase==SessionPhase.Lobby)
                {
                    status.stage="lobby"; int team=status.scenario=="capacity"?status.roleIndex%4:status.role=="host"?0:status.role=="client2"?2:1; bool localOnTeam=false;
                    int readyCount=0,teamMask=0;
                    for(int i=0;i<lobby.Seats.Length;i++)
                    {
                        var seat=lobby.Seats[i]; if(!seat.Occupied || !seat.Connected) continue;
                        if(seat.Player==runner.LocalPlayer)
                        {
                            localOnTeam=seat.Team==team;
                            if(status.scenario=="capacity") status.lobbyReady=localOnTeam && seat.Ready;
                            if(localOnTeam && !seat.Ready && !readyRequested) { readyRequested=true; bridge.ToggleReady(); }
                        }
                        if(seat.Ready) { readyCount++; teamMask|=1<<seat.Team; }
                    }
                    if(!localOnTeam && !teamRequested) { teamRequested=true; bridge.SelectTeam(team); }
                    if(status.role=="host" && (status.scenario=="capacity"?readyCount==8 && teamMask==15:IsMigrationScenario?readyCount>=3 && (teamMask&7)==7:readyCount>=2 && (teamMask&(teamMask-1))!=0) && !startRequested)
                    { startRequested=true; bridge.StartGame(); }
                }
                status.externalSimulation=presentation.ExternalSimulation;
                var model=presentation.Match; status.playerCount=model?.Players.Count??0;
                status.avatarCount=0;
                if(runner!=null && runner.IsRunning)
                {
                    // Reused list: a per-frame scene search would put the harness's own garbage into soak GC figures.
                    smokeAvatars.Clear(); runner.GetAllBehaviours(smokeAvatars);
                    foreach(var avatar in smokeAvatars)
                        if(avatar.Initialized && avatar.Object!=null && avatar.Object.IsValid) status.avatarCount++;
                }
                if(presentation.ExternalSimulation && presentation.Flow.Screen==MenuScreen.Pause) presentation.Show(MenuScreen.Playing);
                bool playing=presentation.ExternalSimulation && model!=null && model.Phase==ArenaMatchPhase.Running &&
                    session.Phase==SessionPhase.Running && status.playerCount>=2 && status.avatarCount>=2 && presentation.LocalPlayer!=null;
                if(playing)
                {
                    if(now>=nextRttSample && runner!=null && runner.IsRunning)
                    {
                        nextRttSample=now+.25;
                        foreach(var remote in runner.ActivePlayers)
                        {
                            // Fusion clients expose their server RTT through LocalPlayer; hosts expose each remote client.
                            if(runner.IsServer ? remote==runner.LocalPlayer : remote!=runner.LocalPlayer) continue;
                            double ms=runner.GetPlayerRtt(remote)*1000;
                            if(double.IsNaN(ms) || double.IsInfinity(ms) || ms<=0) continue;
                            status.sdkRemoteRttSamples++;
                            status.sdkRemoteRttMinMs=status.sdkRemoteRttSamples==1?ms:Math.Min(status.sdkRemoteRttMinMs,ms);
                            status.sdkRemoteRttMaxMs=Math.Max(status.sdkRemoteRttMaxMs,ms);
                            status.sdkRemoteRttMeanMs+=(ms-status.sdkRemoteRttMeanMs)/status.sdkRemoteRttSamples;
                        }
                    }
                    int connected=0;
                    foreach(var player in model.Players)
                    {
                        if(player.Connected) connected++;
                        if(player.Participant!=presentation.LocalParticipantId) status.remotePosition=player.Feet;
                    }
                    playing=connected>=2;
                    status.matchTick=model.Tick; status.authorityGeneration=model.AuthorityGeneration;
                    status.localParticipant=presentation.LocalParticipantId; status.connectionGeneration=presentation.LocalPlayer.ConnectionGeneration;
                    status.lifeGeneration=presentation.LocalPlayer.LifeGeneration; status.health=presentation.LocalPlayer.Health;
                    status.starterAmmo=presentation.LocalPlayer.StarterAmmo;
                    status.localPosition=presentation.LocalPlayer.Feet;
                    if(model.Tick!=lastMatchTick) { lastMatchTick=model.Tick; lastTickAt=now; }
                    if(now-lastTickAt>2 && !(status.scenario=="capacity" && model.Phase==ArenaMatchPhase.Finished) && !(IsMigrationScenario && (session.SimulationFrozen || session.Recovering || status.migrationRequested))) throw new InvalidOperationException();
                }
                if(IsMigrationScenario)
                {
                    ObserveMigration(session,runner,model,playing,now);
                    PublishDue(now); return;
                }
                if(status.scenario=="capacity")
                {
                    ObserveCapacity(session,runner,model,playing,now);
                    PublishDue(now); return;
                }
                if(status.scenario=="late-join")
                {
                    ObserveLateJoin(session,runner,model,playing,now);
                    PublishDue(now); return;
                }
                if(status.scenario=="rematch")
                {
                    ObserveRematch(session,runner,model,playing,peerStatus,now);
                    PublishDue(now); return;
                }
                if(status.scenario=="reconnect" && status.initialVerified && !status.reconnectRequested)
                {
                    status.stage="awaiting_disconnect";
                    if(status.role=="host" && peerStatus!=null && peerStatus.initialVerified)
                    {
                        var seat=lobby.Seats[reconnectParticipant-1];
                        if(!seat.Occupied || !seat.Connected || seat.Player==runner.LocalPlayer) throw new InvalidOperationException();
                        status.reconnectRequested=true; status.stage="disconnecting"; disconnectAt=now;
                        stableAt=-1; lastMatchTick=-1; status.stableSeconds=0;
                        WriteOutput(); runner.Disconnect(seat.Player);
                    }
                }
                if(status.scenario=="reconnect" && status.role=="host" && status.reconnectRequested && model!=null &&
                    model.Find(reconnectParticipant)!=null && !model.Find(reconnectParticipant).Connected) status.reconnectDisconnected=true;
                if(status.scenario=="reconnect" && status.reconnectRequested && !status.reconnectComplete && playing)
                {
                    var recovered=model.Find(reconnectParticipant);
                    if(recovered!=null && recovered.Connected && recovered.ConnectionGeneration>status.previousConnectionGeneration)
                    {
                        if(status.role=="client" && (peerStatus==null || !peerStatus.reconnectComplete)) { PublishDue(now); return; }
                        if(recovered.Team!=reconnectTeam || (status.role=="client" && presentation.LocalParticipantId!=reconnectParticipant))
                            throw new InvalidOperationException("The recovered seat changed.");
                        status.reconnectSeconds=status.role=="client"?peerStatus.reconnectSeconds:(float)(now-disconnectAt);
                        status.recoveredConnectionGeneration=recovered.ConnectionGeneration; status.recoveredLifeGeneration=recovered.LifeGeneration;
                        status.recoveredHealth=recovered.Health; status.recoveredStarterAmmo=recovered.StarterAmmo;
                        status.retainedAvatar=recovered.LifeGeneration==status.previousLifeGeneration;
                        if(status.reconnectSeconds<5 && (!status.retainedAvatar || recovered.Health!=baselineHealth || recovered.StarterAmmo!=baselineAmmo))
                            throw new InvalidOperationException("Recovery inside the avatar hold changed its life or inventory.");
                        if(!status.retainedAvatar && (recovered.LifeGeneration<=status.previousLifeGeneration || recovered.Health!=WeaponCatalog.Health || recovered.StarterAmmo!=WeaponCatalog.StarterAmmo))
                            throw new InvalidOperationException("Expired-avatar recovery did not use ordinary starter respawn.");
                        status.reconnectComplete=true; status.stage="reconnected"; stableAt=-1; lastMatchTick=-1; lastTickAt=now;
                        WriteOutput();
                    }
                }
                bool combatReady=status.scenario=="grenade-combat" ? playing && DriveGrenadeCombat(model,runner,peerStatus,now) :
                    status.scenario=="special-projectiles" ? playing && DriveSpecialProjectiles(model,runner,peerStatus,now) :
                    status.scenario=="projectile-combat" ? playing && DriveProjectileCombat(model,runner,peerStatus,now) :
                    status.scenario=="stun-client" ? playing && DriveStunClient(model,runner,peerStatus,now) :
                    status.scenario=="deflection" ? playing && DriveDeflection(model,runner,peerStatus,now) :
                    (status.scenario=="laser-combat" || status.scenario=="laser-moving-cover") ? playing && DriveLaserCombat(model,runner,peerStatus,now) :
                    status.scenario=="gunplay" ? playing && DriveGunplay(model,runner,peerStatus,now) :
                    status.scenario!="combat" || (playing && DriveCombat(session,model,runner,peerStatus,now));
                bool observeStability=(status.scenario=="basic" || !status.initialVerified || status.reconnectComplete) && combatReady;
                if(playing && !status.success && observeStability)
                {
                    status.stage="playing";
                    if(stableAt<0) { stableAt=now; firstMatchTick=model.Tick; }
                    status.stableSeconds=(float)(now-stableAt);
                    if(now-stableAt>=10 && model.Tick>firstMatchTick)
                    {
                        if(status.scenario=="reconnect" && !status.initialVerified)
                        {
                            var target=status.role=="client"?presentation.LocalPlayer:RemotePlayer(model);
                            if(target==null) throw new InvalidOperationException();
                            reconnectParticipant=target.Participant; reconnectTeam=target.Team;
                            status.previousConnectionGeneration=target.ConnectionGeneration; status.previousLifeGeneration=target.LifeGeneration;
                            baselineHealth=target.Health; baselineAmmo=target.StarterAmmo;
                            status.initialVerified=true; status.stage="awaiting_disconnect"; stableAt=-1; status.stableSeconds=0; WriteOutput();
                        }
                        else { status.success=true; status.stage="verified"; verifiedAt=now; WriteOutput(); }
                    }
                }
                else if(!status.success) { stableAt=-1; status.stableSeconds=0; }
                if(status.success)
                {
                    status.stage="verified";
                    bool peerReady=peerOutput==null || PeerVerified();
                    double hold=status.role=="host"?5:1;
                    if(peerReady && now-verifiedAt>=hold) { Finish(true,""); return; }
                }
                PublishDue(now);
            }
            catch(Exception exception) { Debug.LogException(exception); Finish(false,exception.GetType().Name+"@"+status.stage); }
        }
        void DriveTeamSwitch(FusionLobby lobby,NetworkRunner runner,Status peer,double now)
        {
            status.stage="team_switch_lobby";
            FusionLobbySeat guest=default,host=default;
            int connected=0;
            foreach(var seat in lobby.Seats)
            {
                if(!seat.Occupied || !seat.Connected) continue;
                connected++;
                if(seat.Player==lobby.HostPlayer) host=seat; else guest=seat;
            }
            if(connected!=2) { if(teamSwitchInitialized) throw new InvalidOperationException("Team switching lost a peer."); return; }
            if(!teamSwitchInitialized)
            {
                teamSwitchInitialized=true; teamSwitchRunner=runner; teamSwitchProgressAt=now;
                status.teamSwitchInitialTeam=guest.Team; status.teamSwitchGuestPlayer=guest.Player.RawEncoded;
                status.teamSwitchConnectionGeneration=guest.ConnectionGeneration;
                teamSwitchGuestIdentity=runner.GetPlayerUserId(guest.Player);
                if(string.IsNullOrEmpty(teamSwitchGuestIdentity)) throw new InvalidOperationException("Missing guest identity.");
                using(var sha=SHA256.Create())
                    status.teamSwitchGuestFingerprint=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(teamSwitchGuestIdentity)));
            }
            if(guest.Player.RawEncoded!=status.teamSwitchGuestPlayer || guest.ConnectionGeneration!=status.teamSwitchConnectionGeneration ||
                runner.GetPlayerUserId(guest.Player)!=teamSwitchGuestIdentity)
                throw new InvalidOperationException("Team switching changed the guest connection identity.");
            if(now-teamSwitchProgressAt>15) throw new InvalidOperationException("Team switch handshake timed out.");
            if(peer==null) return;
            if(peer.teamSwitchInitialTeam!=status.teamSwitchInitialTeam || peer.teamSwitchGuestPlayer!=status.teamSwitchGuestPlayer ||
                peer.teamSwitchConnectionGeneration!=status.teamSwitchConnectionGeneration || peer.teamSwitchGuestFingerprint!=status.teamSwitchGuestFingerprint) return;
            if(status.role=="host")
            {
                int step=peer.teamSwitchStep;
                if(step<0 || step>4) throw new InvalidOperationException("Invalid team switch step.");
                int target=(status.teamSwitchInitialTeam+step)%4;
                if(guest.Team!=target) return;
                if(step>status.teamSwitchClearAck && !guest.Ready)
                { status.teamSwitchStep=step; status.teamSwitchClearAck=step; teamSwitchProgressAt=now; }
                if(guest.Ready && (step==0 || status.teamSwitchClearAck==step) && status.teamSwitchReadyAck==step)
                { status.teamSwitchReadyAck=step+1; teamSwitchProgressAt=now; }
                if(step==4 && status.teamSwitchReadyAck==5 && peer.teamSwitchComplete)
                {
                    status.teamSwitchComplete=true;
                    if(!host.Ready && !readyRequested) { readyRequested=true; bridge.ToggleReady(); }
                    if(host.Ready && guest.Ready && host.Team!=guest.Team && !startRequested)
                    { startRequested=true; bridge.StartGame(); }
                }
                return;
            }
            int current=status.teamSwitchStep;
            int expected=(status.teamSwitchInitialTeam+current)%4;
            if(guest.Team!=expected) return;
            if(current>0 && !guest.Ready && peer.teamSwitchClearAck==current)
            { status.teamSwitchClearAck=current; teamSwitchProgressAt=now; }
            if(!guest.Ready && (current==0 || status.teamSwitchClearAck==current) && !teamSwitchActionSent)
            { teamSwitchActionSent=true; bridge.ToggleReady(); }
            if(!guest.Ready || peer.teamSwitchReadyAck!=current+1) return;
            status.teamSwitchReadyAck=current+1;
            if(current==4) { status.teamSwitchComplete=true; teamSwitchProgressAt=now; return; }
            status.teamSwitchStep=current+1; teamSwitchActionSent=false; teamSwitchProgressAt=now;
            bridge.SelectTeam((status.teamSwitchInitialTeam+status.teamSwitchStep)%4);
        }
        void ObserveMigration(FusionSession session,NetworkRunner runner,ArenaMatch model,bool playing,double now)
        {
            var host=ReadMigrationStatus("host"); var client=ReadMigrationStatus("client"); var client2=ReadMigrationStatus("client2");
            foreach(var peer in new[]{host,client,client2})
                if(peer!=null && peer.finished && !peer.success) throw new InvalidOperationException("A migration smoke peer failed.");
            status.isServer=runner!=null && runner.IsRunning && runner.IsServer;
            if(!status.initialVerified)
            {
                int connected=0; if(model!=null) foreach(var player in model.Players) if(player.Connected) connected++;
                if(!playing || connected<3 || status.avatarCount<3) { stableAt=-1; status.stableSeconds=0; return; }
                if(stableAt<0) { stableAt=now; firstMatchTick=model.Tick; }
                status.stage="initial_three_player_stability"; status.stableSeconds=(float)(now-stableAt);
                if(now-stableAt<10 || model.Tick<=firstMatchTick) return;
                originalRunner=runner; originalContent=session.Content; originalMatchId=session.MatchId;
                status.originalMatchId=originalMatchId;
                status.originalContent=originalContent.Protocol+"/"+originalContent.Build+"/"+originalContent.Map+"/"+originalContent.Ruleset;
                status.originalAuthorityGeneration=model.AuthorityGeneration;
                status.originalParticipant=presentation.LocalParticipantId; status.originalTeam=presentation.LocalPlayer.Team;
                status.originalLifeGeneration=presentation.LocalPlayer.LifeGeneration;
                status.originalHealth=presentation.LocalPlayer.Health; status.originalStarterAmmo=presentation.LocalPlayer.StarterAmmo;
                status.initialVerified=true; status.stage="awaiting_original_host_departure";
                stableAt=-1; status.stableSeconds=0; WriteOutput(); return;
            }
            if(status.role=="host")
            {
                if(status.crashReady) { status.stage="crash_ready"; return; }
                if(client==null || client2==null || !client.initialVerified || !client2.initialVerified) return;
                if(!playing || model==null || runner==null || !runner.IsServer) throw new InvalidOperationException("Original host lost its running session.");
                status.publishedTick=session.LastPublishedTick;
                if(status.publishedTick<0) throw new InvalidOperationException("The original host has no successful migration snapshot upload.");
                status.preExitTick=model.Tick; status.migrationRequested=true; status.stage="requesting_graceful_migration";
                if(status.scenario=="migration-crash")
                {
                    status.crashReady=true; status.stage="crash_ready"; WriteOutput(); return;
                }
                WriteOutput(); Finish(true,""); return;
            }
            if(host==null || !host.migrationRequested) return;
            if(!status.migrationRequested)
            {
                status.migrationRequested=true; migrationAt=now; stableAt=-1; status.stableSeconds=0;
                status.preExitTick=host.preExitTick; lastMatchTick=-1; lastTickAt=now; status.stage="awaiting_sdk_migration";
            }
            if(runner!=null && !ReferenceEquals(runner,originalRunner)) status.sawRunnerChange=true;
            status.migrationSeconds=(float)(now-migrationAt);
            if(!status.migrationComplete && now-migrationAt>SessionState.RecoveryTimeoutSeconds)
                throw new InvalidOperationException("SDK host migration exceeded thirty seconds.");
            var other=status.role=="client"?client2:client;
            if(status.success)
            {
                status.stage="verified";
                if(other!=null && other.success && now-verifiedAt>=1 && (!status.electedHost || other.finished)) Finish(true,"");
                return;
            }
            if(!status.sawRunnerChange || runner==null || !runner.IsRunning) return;
            int recoveredMatches=0;
            foreach(var networkMatch in FindObjectsByType<FusionMatch>())
                if(networkMatch.Runner==runner && networkMatch.Initialized && networkMatch.Header.AuthorityGeneration==status.originalAuthorityGeneration+1)
                { recoveredMatches++; minimumPostRecoveryTick=Math.Min(minimumPostRecoveryTick,networkMatch.Header.Tick); }
            if(recoveredMatches>1) throw new InvalidOperationException("Multiple matches appeared after SDK migration.");
            if(minimumPostRecoveryTick!=int.MaxValue)
            {
                status.minimumPostRecoveryTick=minimumPostRecoveryTick;
                status.rollbackTicks=Math.Max(0,status.preExitTick-minimumPostRecoveryTick);
                if(status.rollbackTicks>360) throw new InvalidOperationException("Observed migration rollback exceeded three seconds.");
            }
            if(!playing || model==null || session.SimulationFrozen || session.Recovering)
            { stableAt=-1; status.postRecoveryStableSeconds=0; status.stage="sdk_recovering"; return; }
            if(!ReferenceEquals(session,retainedSession) || session.MatchId!=originalMatchId || !session.Content.Matches(originalContent) ||
                model.AuthorityGeneration!=status.originalAuthorityGeneration+1 || presentation.LocalParticipantId!=status.originalParticipant ||
                presentation.LocalPlayer.Team!=status.originalTeam || minimumPostRecoveryTick==int.MaxValue)
                throw new InvalidOperationException("Recovered match identity, authority or local seat changed.");
            var formerHost=model.Find(host.originalParticipant);
            if(formerHost!=null && formerHost.Connected) throw new InvalidOperationException("The departed original host still controls a seat.");
            int connectedTeams=0,mask=0;
            foreach(var player in model.Players) if(player.Connected) mask|=1<<player.Team;
            for(int bits=mask;bits!=0;bits&=bits-1) connectedTeams++;
            if(connectedTeams<2) { stableAt=-1; status.postRecoveryStableSeconds=0; return; }
            status.electedHost=runner.IsServer;
            if(other==null || !other.sawRunnerChange || other.authorityGeneration!=status.originalAuthorityGeneration+1) return;
            status.newHostCount=(status.electedHost?1:0)+(other.electedHost?1:0);
            if(status.newHostCount!=1) { stableAt=-1; status.postRecoveryStableSeconds=0; return; }
            if(!status.migrationComplete)
            {
                status.migrationComplete=true; status.stage="recovered"; stableAt=-1; lastMatchTick=-1; lastTickAt=now; WriteOutput();
            }
            if(stableAt<0) { stableAt=now; firstMatchTick=model.Tick; }
            status.postRecoveryStableSeconds=(float)(now-stableAt); status.stage="post_migration_stability";
            if(!status.success && now-stableAt>=10 && model.Tick>firstMatchTick)
            { status.success=true; verifiedAt=now; status.stage="verified"; WriteOutput(); }
        }
        Status ReadMigrationStatus(string role)
        {
            string path=Path.Combine(Path.GetDirectoryName(output),role+".json");
            try
            {
                if(!File.Exists(path)) return null;
                var peer=ReadStatus(path);
                return peer!=null && peer.role==role && peer.scenario==status.scenario && peer.room==status.room?peer:null;
            }
            catch(IOException) { return null; }
        }
        bool PeerVerified()
        {
            var peer=ReadPeer();
            return peer!=null && peer.success && (status.role!="host" || peer.finished);
        }
        // A transient read failure (the peer replacing or a scanner holding its file) returns the last good copy. A null
        // there used to reset 10 s stability windows, stretching loaded runs into DeadlineExceeded.
        Status lastPeer;
        Status ReadPeer()
        {
            if(peerOutput==null) return null;
            try
            {
                if(!File.Exists(peerOutput)) return lastPeer;
                var peer=ReadStatus(peerOutput);
                return lastPeer=peer!=null && peer.room==status.room && peer.role!=status.role && peer.scenario==status.scenario?peer:null;
            }
            catch(IOException) { return lastPeer; }
        }
        CombatState RemotePlayer(ArenaMatch model)
        {
            foreach(var player in model.Players) if(player.Participant!=presentation.LocalParticipantId && player.Connected) return player;
            return null;
        }
        static Status ReadStatus(string path)
        {
            // Readers must permit atomic replacement of the status file on Windows.
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(file)) return JsonUtility.FromJson<Status>(reader.ReadToEnd());
        }
        readonly List<FusionPlayer> smokeAvatars=new List<FusionPlayer>();
        void PublishDue(double now) { if(now>=nextWrite) { nextWrite=now+(status.scenario=="capacity"?1:.25); WriteOutput(); } }
        void WriteOutput()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output)); string temporary=output+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(status,true),new UTF8Encoding(false));
            // Windows readers/indexers can briefly hold the destination; retry only this diagnostic write.
            for(int attempt=0;;attempt++)
            {
                try { if(File.Exists(output)) File.Replace(temporary,output,null); else File.Move(temporary,output); return; }
                catch(IOException) { if(attempt==9) throw; System.Threading.Thread.Sleep(10); }
            }
        }
        async void Finish(bool success,string error)
        {
            if(finishing) return; finishing=true; StopCombatInput(); status.success=success; status.error=error; status.stage="leaving";
            try
            {
                WriteOutput(); var owned=menu?.Session; bridge?.Exit();
                if(owned!=null)
                {
                    var stop=owned.Completion; var completed=await Task.WhenAny(stop,Task.Delay(TimeSpan.FromSeconds(15)));
                    if(completed!=stop || stop.IsFaulted || stop.IsCanceled) { status.success=false; status.error="CleanupFailed"; }
                }
            }
            catch(Exception exception) { status.success=false; status.error=exception.GetType().Name+"@cleanup"; }
            status.finished=true; status.stage=status.success?"passed":"failed";
            try { WriteOutput(); }
            catch(Exception) { status.success=false; }
            Application.Quit(status.success?0:1);
        }
    }
}
#endif
