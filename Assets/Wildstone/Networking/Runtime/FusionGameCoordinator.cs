using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.Session;
using Wildstone.UI;

namespace Wildstone.Networking
{
    [RequireComponent(typeof(FusionMenuController), typeof(PracticeSession))]
    public sealed class FusionGameCoordinator : MonoBehaviour
    {
        FusionMenuController menu;
        PracticeSession presentation;
        FusionSession session;
        NetworkRunner observedRunner;
        FusionMatch match;
        ArenaMatch clientModel;
        string mapId;
        bool loading, loaded, loadAcknowledged, attached;
        double loadDeadline;
        FusionLobby subscribedLobby;
        int actionSequence;
        float nextPresentation, nextDiscovery;
        int eventCursor;
        readonly List<CombatEvent> presentationEvents = new List<CombatEvent>(FusionCombatEvent.Capacity);
        readonly Dictionary<int,Transform> renderPoses = new Dictionary<int,Transform>();
        readonly Dictionary<int,ArenaMotor> motors = new Dictionary<int,ArenaMotor>();
        readonly Dictionary<int,FusionPlayer> avatars = new Dictionary<int,FusionPlayer>();
        readonly Dictionary<int,NetworkId> avatarNetworkIds = new Dictionary<int,NetworkId>();
        readonly List<CombatActionKind> commands = new List<CombatActionKind>();
        readonly List<FusionMatch> discoveredMatches = new List<FusionMatch>(1);
        readonly List<FusionPlayer> discoveredPlayers = new List<FusionPlayer>(8);

        void Awake()
        {
            menu=GetComponent<FusionMenuController>(); presentation=GetComponent<PracticeSession>();
            menu.SessionCreated+=SetSession;
        }
        void SetSession(FusionSession value)
        {
            ResetSession(); session=value; session.InputRequested+=CollectInput;
            mapId=GetComponent<OnlineMenuBridge>().Map;
        }
        void ResetSession()
        {
            if(session!=null) session.InputRequested-=CollectInput;
            ResetWorld(); session=null;
        }
        void ResetWorld()
        {
            if(subscribedLobby!=null) subscribedLobby.PlayerDisconnected-=PeerDisconnected;
            subscribedLobby=null;
            if(presentation!=null)
            {
                if(loading && !attached) presentation.CancelLoad();
                presentation.DetachNetworkMatch();
            }
            observedRunner=null; match=null; clientModel=null;
            avatars.Clear(); avatarNetworkIds.Clear(); motors.Clear(); renderPoses.Clear(); presentationEvents.Clear();
            discoveredMatches.Clear(); discoveredPlayers.Clear();
            loading=loaded=loadAcknowledged=attached=false; loadDeadline=0; actionSequence=eventCursor=0; nextDiscovery=nextPresentation=0;
        }
        void Update()
        {
            if(session==null) return;
            if(menu.Session!=session || session.Phase==SessionPhase.Closed || session.Phase==SessionPhase.Failed)
            { ResetSession(); return; }
            if(session.Phase==SessionPhase.Aborted) { CloseOnline(); return; }
            if(!ReferenceEquals(observedRunner,session.Runner))
            {
                ResetWorld(); observedRunner=session.Runner;
            }
            if(session.SimulationFrozen) return;
            var lobby=session.Lobby;
            if(lobby==null) return;
            if(subscribedLobby!=lobby)
            {
                if(subscribedLobby!=null) subscribedLobby.PlayerDisconnected-=PeerDisconnected;
                subscribedLobby=lobby; subscribedLobby.PlayerDisconnected+=PeerDisconnected;
            }
            try
            {
                if(!loading && (lobby.Phase==SessionPhase.Loading || lobby.Phase==SessionPhase.Warmup || lobby.Phase==SessionPhase.Running))
                {
                    loading=true; loadDeadline=Time.realtimeSinceStartupAsDouble+SessionState.OperationTimeoutSeconds;
                    if(session.Recovering && presentation.TryRetainNetworkMap(mapId)) loaded=true;
                    else presentation.Load(mapId,false);
                }
                if(loading && presentation.Failure!=null) throw new InvalidOperationException("Online arena load failed.");
                if(loading && !loaded && !presentation.Flow.ForegroundLoading && presentation.MapId==mapId)
                {
                    if(presentation.Failure!=null) throw new InvalidOperationException("Arena load failed.");
                    loaded=true;
                }
                if(loading && !loadAcknowledged && Time.realtimeSinceStartupAsDouble>=loadDeadline)
                    throw new InvalidOperationException("Online arena load acknowledgement timed out.");
                if(loaded && !loadAcknowledged && (lobby.Phase==SessionPhase.Loading || lobby.Phase==SessionPhase.Warmup || lobby.Phase==SessionPhase.Running))
                { lobby.RequestLoaded(); loadAcknowledged=true; }
                if(!loaded) return;
                var runner=session.Runner;
                if(runner==null || !runner.IsRunning) return;
                // shortcut: bounded discovery scans until spawn/despawn callbacks own the presentation registry.
                if(Time.unscaledTime>=nextDiscovery)
                {
                    nextDiscovery=Time.unscaledTime+.2f;
                    if(match==null)
                    {
                        discoveredMatches.Clear(); runner.GetAllBehaviours(discoveredMatches);
                        foreach(var candidate in discoveredMatches)
                            if(candidate!=null && candidate.gameObject.activeInHierarchy && candidate.Runner==runner && candidate.Initialized)
                            { match=candidate; break; }
                    }
                    RefreshAvatars(runner);
                }
                if(runner.IsServer && (lobby.Phase==SessionPhase.Warmup || lobby.Phase==SessionPhase.Running))
                    HostMatch(lobby,runner);
                if(match==null || !match.Initialized) return;
                var model=match.Model;
                if(!runner.IsServer && Time.unscaledTime>=nextPresentation)
                {
                    nextPresentation=Time.unscaledTime+.05f;
                    clientModel=clientModel??new ArenaMatch(presentation.Catalog.Find(mapId),120);
                    // shortcut: render snapshots allocate at 20 Hz; replace with reusable buffers before performance acceptance.
                    if(!clientModel.TryRestore(match.CaptureSnapshot())) throw new InvalidOperationException("Invalid replicated match state.");
                }
                model=model??clientModel;
                FusionPlayer local=null;
                foreach(var avatar in avatars.Values)
                    if(avatar!=null && avatar.Object!=null && avatar.Object.IsValid && avatar.Object.InputAuthority==runner.LocalPlayer) { local=avatar; break; }
                if(model==null || local==null || model.Find(local.Participant)==null) return;
                if(!attached)
                {
                    presentation.AttachNetworkMatch(model,local.Participant,motors);
                    presentation.RefreshNetworkAvatars(motors,renderPoses); attached=true;
                    eventCursor=match.EventSequence;
                }
                match.CopyPresentationEvents(presentationEvents,ref eventCursor);
                presentation.PresentNetworkTick(presentationEvents,match.Header.Captor,match.Header.Contested);
                if(model.Phase==ArenaMatchPhase.Finished && presentation.Flow.Screen!=MenuScreen.Results) presentation.Show(MenuScreen.Results);
                else if(model.Phase==ArenaMatchPhase.Aborted) throw new InvalidOperationException("Match aborted.");
                SendCombat(local,model);
            }
            catch(Exception exception)
            {
                Debug.LogWarning("Online match stopped: "+exception.GetType().Name);
                CloseOnline();
            }
        }
        void CloseOnline()
        {
            var bridge=GetComponent<OnlineMenuBridge>();
            bridge.Status="The online match could not continue. Return to the lobby and retry.";
            menu.LeaveWithFailure(bridge.Status); ResetSession(); bridge.Refresh();
        }
        void HostMatch(FusionLobby lobby,NetworkRunner runner)
        {
            if(match==null)
            {
                var prefab=Resources.Load<NetworkObject>("WildstoneFusionMatch");
                if(prefab==null) throw new InvalidOperationException("Network match prefab missing.");
                match=runner.Spawn(prefab,onBeforeSpawned:(r,obj)=>obj.GetComponent<FusionMatch>().Initialize(presentation.Catalog.Find(mapId))).GetComponent<FusionMatch>();
            }
            match.RecoveryWarmup = lobby.Phase != SessionPhase.Running;
            for(int i=0;i<lobby.Seats.Length;i++)
            {
                var seat=lobby.Seats[i]; int participant=i+1;
                var existing=match.Model.Find(participant);
                avatars.TryGetValue(participant,out var previousAvatar);
                if(existing!=null && !existing.Connected && !existing.Alive && match.Model.Tick>=existing.DisconnectUntil && previousAvatar!=null)
                {
                    if(!match.UnbindAvatar(participant,previousAvatar)) throw new InvalidOperationException("Retired avatar could not unbind.");
                    runner.Despawn(previousAvatar.Object);
                    avatars.Remove(participant); avatarNetworkIds.Remove(participant); motors.Remove(participant); renderPoses.Remove(participant);
                    if(attached) presentation.RefreshNetworkAvatars(motors,renderPoses);
                }
                if(!seat.Occupied) continue;
                if(seat.ConnectionGeneration>int.MaxValue-1) throw new InvalidOperationException("Connection generation exhausted.");
                if(!seat.Connected)
                {
                    if(existing!=null && existing.Connected) RetirePeer(participant,existing.ConnectionGeneration);
                    continue;
                }
                if(!seat.Ready) continue;
                if(avatars.TryGetValue(participant,out var current) && current!=null)
                {
                    var state=match.Model.Find(participant);
                    if(state!=null && state.ConnectionGeneration!=(int)seat.ConnectionGeneration)
                    {
                        current.Object.AssignInputAuthority(seat.Player);
                        if(!match.BindPlayer(participant,current,(int)seat.ConnectionGeneration,seat.Team,lobby.Admission.RequiresFreshLife(seat.Player,session.Now))) throw new InvalidOperationException("Reconnect avatar rejected.");
                        runner.SetPlayerObject(seat.Player,current.Object);
                        lobby.Admission.MarkControlRestored(seat.Player);
                        if(lobby.Admission.TryGetBinding(seat.Player,out var reconnect)) lobby.HostRules.MarkSpawned(reconnect.VerifiedSubject,reconnect.ConnectionGeneration);
                    }
                    continue;
                }
                var map=presentation.Catalog.Find(mapId); int slot=0; ArenaSpawn spawn=default;
                foreach(var candidate in map.Spawns) if(candidate.Team==seat.Team && slot++==seat.Slot) { spawn=candidate; break; }
                var playerPrefab=Resources.Load<NetworkObject>("WildstoneFusionPlayer");
                if(playerPrefab==null) throw new InvalidOperationException("Network player prefab missing.");
                int firstTick=runner.Tick.Raw+1;
                var networkObject=runner.Spawn(playerPrefab,spawn.Position,Quaternion.Euler(0,spawn.Yaw,0),seat.Player,
                    (r,obj)=>
                    {
                        var initial=obj.GetComponent<ArenaMotor>().CaptureState();
                        initial.Position=spawn.Position; initial.Yaw=spawn.Yaw; initial.Generation=1;
                        obj.GetComponent<FusionPlayer>().Initialize(initial,firstTick);
                    });
                var player=networkObject.GetComponent<FusionPlayer>();
                if(!match.BindPlayer(participant,player,(int)seat.ConnectionGeneration,seat.Team,lobby.Admission.RequiresFreshLife(seat.Player,session.Now))) throw new InvalidOperationException("Admitted avatar could not bind.");
                avatars[participant]=player; avatarNetworkIds[participant]=networkObject.Id;
                motors[participant]=player.GetComponent<ArenaMotor>();
                renderPoses[participant]=player.VisualRoot;
                if(attached) presentation.RefreshNetworkAvatars(motors,renderPoses);
                runner.SetPlayerObject(seat.Player,networkObject);
                lobby.Admission.MarkControlRestored(seat.Player);
                if(lobby.Admission.TryGetBinding(seat.Player,out var binding)) lobby.HostRules.MarkSpawned(binding.VerifiedSubject,binding.ConnectionGeneration);
            }
            if(lobby.Phase==SessionPhase.Running && match.Model.Phase==ArenaMatchPhase.Lobby && !match.Begin())
                throw new InvalidOperationException("Ready match could not begin.");
        }
        void PeerDisconnected(PlayerRef peer)
        {
            if(match==null || session?.Runner==null || !session.Runner.IsServer) return;
            foreach(var pair in avatars)
                if(pair.Value!=null && pair.Value.Object!=null && pair.Value.Object.InputAuthority==peer)
                {
                    var player=match.Model.Find(pair.Key);
                    if(player!=null && player.Connected) RetirePeer(pair.Key,player.ConnectionGeneration);
                    break;
                }
        }
        void RetirePeer(int participant,int generation)
        {
            if(avatars.TryGetValue(participant,out var avatar) && avatar!=null && avatar.Object!=null)
                avatar.Object.RemoveInputAuthority();
            if(!match.SetConnection(participant,generation,false)) throw new InvalidOperationException("Disconnected avatar could not retire.");
        }
        void RefreshAvatars(NetworkRunner runner)
        {
            discoveredPlayers.Clear(); runner.GetAllBehaviours(discoveredPlayers);
            bool changed=false;
            for(int i=discoveredPlayers.Count-1;i>=0;i--)
            {
                var player=discoveredPlayers[i];
                if(player==null || !player.gameObject.activeInHierarchy || player.Runner!=runner || !player.Initialized ||
                    player.Participant<=0 || player.Object==null || !player.Object.IsValid)
                { discoveredPlayers.RemoveAt(i); continue; }
                if(!avatars.TryGetValue(player.Participant,out var known) || !ReferenceEquals(known,player) ||
                    !avatarNetworkIds.TryGetValue(player.Participant,out var networkId) || networkId!=player.Object.Id ||
                    !renderPoses.TryGetValue(player.Participant,out var pose) || pose!=player.VisualRoot)
                    changed=true;
            }
            if(!changed && discoveredPlayers.Count==avatars.Count) return;
            avatars.Clear(); avatarNetworkIds.Clear(); motors.Clear(); renderPoses.Clear();
            foreach(var player in discoveredPlayers)
            {
                avatars[player.Participant]=player; avatarNetworkIds[player.Participant]=player.Object.Id;
                motors[player.Participant]=player.GetComponent<ArenaMotor>(); renderPoses[player.Participant]=player.VisualRoot;
            }
            if(attached) presentation.RefreshNetworkAvatars(motors,renderPoses);
        }
        void CollectInput(NetworkInput output)
        {
            if(!attached || presentation.Motor==null || presentation.View==null) return;
            var state=presentation.Motor.CaptureState();
            output.Set(FusionInput.FromMotor(presentation.Input.ConsumeMovement(state.Tick+1,state.Generation,presentation.View.Yaw,presentation.View.Pitch)));
        }
        void SendCombat(FusionPlayer local,ArenaMatch model)
        {
            presentation.Input.ConsumeActions(commands,model.Tick+1,model.TickRate,model.Find(local.Participant),presentation.Flow.GameplayInput && model.Phase==ArenaMatchPhase.Running);
            if(!presentation.Flow.GameplayInput || model.Phase!=ArenaMatchPhase.Running) return;
            var player=model.Find(local.Participant);
            actionSequence=Math.Max(actionSequence,player.LastActionSequence);
            foreach(var kind in commands)
            {
                var action=new CombatAction { Participant=player.Participant, ConnectionGeneration=player.ConnectionGeneration,
                    LifeGeneration=player.LifeGeneration,AuthorityGeneration=model.AuthorityGeneration,Sequence=++actionSequence,
                    Kind=kind, Direction=Quaternion.Euler(presentation.View.Pitch,presentation.View.Yaw,0)*Vector3.forward,PickupId=-1 };
                if(kind==CombatActionKind.Pickup)
                {
                    float distance=6.25f;
                    foreach(var item in model.Pickups)
                    { float next=(item.Position-player.Feet).sqrMagnitude; if(next<distance && item.AvailableTick<=model.Tick) { distance=next; action.PickupId=item.Id; action.PickupGeneration=item.Generation; } }
                }
                local.RPC_Combat(FusionCombatAction.FromCombat(action));
            }
        }
        void OnDestroy() { if(menu!=null) menu.SessionCreated-=SetSession; ResetSession(); }
    }
}
