using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.Session;

namespace Wildstone.Networking
{
    // One coherent resume plan is validated in isolation before the replacement runner spawns any gameplay object.
    public sealed class FusionRecovery
    {
        readonly FusionSession session;
        FusionRecoveryState latestHeader;
        FusionRecoverySeat[] latestSeats;
        long lastObservedTick=-1;
        double lastObservedAt, nextSnapshot;
        SessionCheckpoint baseline;
        double migrationBegan;
        Task<bool> publishing;
        internal long LastPublishedTick { get; private set; } = -1;
        Plan validated;
        sealed class Plan
        {
            public NetworkObject LobbySource, MatchSource;
            public readonly List<NetworkObject> Avatars=new List<NetworkObject>(8);
            public ArenaDefinition Map;
            public MatchSnapshot Match;
            public FusionRecoveryState Header;
            public FusionRecoverySeat[] Seats;
        }
        public FusionRecovery(FusionSession session) { this.session=session; }
        public void Reset()
        {
            latestSeats=null; baseline=null; validated=null; lastObservedTick=-1; nextSnapshot=0; publishing=null; LastPublishedTick=-1;
        }
        public void FinishedMigration() { Reset(); }
        public void ObserveAndPublish(NetworkRunner runner)
        {
            if(session.Recovering || !runner.IsRunning) return;
            var lobby=session.Lobby;
            if(lobby==null || lobby.Object==null || !lobby.Object.IsValid) return;
            long authoritativeTick=runner.IsServer?runner.Tick.Raw:
                lobby.TryGetSnapshotsBuffers(out _,out var received,out _)?(int)received.Tick:
                lobby.RecoveryHeader.SeatCount>0?lobby.RecoveryHeader.SimulationTick:-1;
            if (lobby.FinalFrozen)
            {
                if (!lobby.TryCaptureRecovery(out var frozenHeader, out var frozenSeats)) throw new InvalidOperationException("Frozen host checkpoint is incomplete.");
                if (frozenHeader.SimulationTick > authoritativeTick) return;
                ObserveFinalFrozen(frozenHeader, frozenSeats, authoritativeTick, Time.realtimeSinceStartupAsDouble);
                return;
            }
            ObserveAuthoritativeTick(authoritativeTick,Time.realtimeSinceStartupAsDouble);
            bool live=lobby.Phase==SessionPhase.Running || lobby.Phase==SessionPhase.Warmup;
            if(runner.IsServer && live && !session.Recovering && Time.realtimeSinceStartupAsDouble>=nextSnapshot &&
                (publishing==null || publishing.IsCompleted))
            {
                nextSnapshot=Time.realtimeSinceStartupAsDouble+SessionState.SnapshotIntervalSeconds;
                publishing=Publish(runner);
            }
            if(lobby.RecoveryHeader.SeatCount>0 && lobby.RecoveryHeader.SimulationTick<=lastObservedTick && (latestSeats==null ||
                latestHeader.SimulationTick!=lobby.RecoveryHeader.SimulationTick || latestHeader.AuthorityGeneration!=lobby.RecoveryHeader.AuthorityGeneration))
            {
                if(!lobby.TryCaptureRecovery(out var header,out var seats)) throw new InvalidOperationException("Invalid replicated recovery header.");
                FusionRecoveryValidation.ValidateSession(header,seats,session.Content,session.MatchId,lastObservedTick);
                latestHeader=header; latestSeats=seats;
            }
        }
        internal void ObserveFinalFrozen(FusionRecoveryState header, FusionRecoverySeat[] seats, long sdkTick, double observedAt)
        {
            if (header.SimulationTick > sdkTick || latestSeats != null && (header.AuthorityGeneration != latestHeader.AuthorityGeneration ||
                header.SimulationTick < latestHeader.SimulationTick)) throw new InvalidOperationException("Frozen checkpoint generation or clock is invalid.");
            FusionRecoveryValidation.ValidateSession(header, seats, session.Content, session.MatchId, Math.Max(lastObservedTick, header.SimulationTick));
            // Send ticks keep advancing after gameplay freezes; account for upload wait even on a late first receipt.
            double frozenObservedAt=Math.Max(0,observedAt-(sdkTick-header.SimulationTick)/120d);
            ObserveAuthoritativeTick(header.SimulationTick, frozenObservedAt);
            latestHeader=header; latestSeats=seats;
        }
        internal void ObserveAuthoritativeTick(long tick,double observedAt)
        {
            if(tick<=lastObservedTick) return;
            lastObservedTick=tick; lastObservedAt=observedAt;
        }
        async Task<bool> Publish(NetworkRunner runner)
        {
            try
            {
                if(!PrepareSnapshot(runner))
                { return false; }
                long snapshotTick=session.Lobby.RecoveryHeader.SimulationTick;
                bool published=await runner.PushHostMigrationSnapshot();
                if(published) LastPublishedTick=snapshotTick;
#if WILDSTONE_SMOKE
                Debug.Log("WILDSTONE RECOVERY PUBLISH stage=periodic result="+published+" tick="+snapshotTick);
#endif
                return published;
            }
            catch(Exception exception)
            { Debug.LogWarning("WILDSTONE RECOVERY PUBLISH "+FusionSession.FormatRecoveryFailure("periodic-prepare-or-upload",exception)); return false; }
        }
        public async Task PublishBeforeLeave(NetworkRunner runner)
        {
            try
            {
                FreezeSimulation(runner);
                if (!PrepareSnapshot(runner)) { Debug.Log("WILDSTONE RECOVERY PUBLISH stage=final-prepare result=false"); return; }
                long snapshotTick=session.Lobby.RecoveryHeader.SimulationTick;
                session.Lobby.FinalFrozen=true;
                var timeout=Task.Delay(TimeSpan.FromSeconds(5));
                // Serialize behind an in-flight periodic upload so it cannot overwrite the final snapshot later.
                if (publishing != null && !publishing.IsCompleted && await Task.WhenAny(publishing,timeout) != publishing)
                { Debug.LogWarning("WILDSTONE RECOVERY PUBLISH stage=final-wait-periodic result=timeout"); return; }
                publishing=runner.PushHostMigrationSnapshot();
                if(await Task.WhenAny(publishing,timeout)!=publishing)
                { Debug.LogWarning("WILDSTONE RECOVERY PUBLISH stage=final-upload result=timeout"); return; }
                bool published=await publishing;
                if(published) LastPublishedTick=snapshotTick;
                Debug.Log("WILDSTONE RECOVERY PUBLISH stage=final result="+published+" tick="+snapshotTick);
            }
            catch(Exception exception)
            { Debug.LogWarning("WILDSTONE RECOVERY PUBLISH "+FusionSession.FormatRecoveryFailure("final-prepare-or-upload",exception)); }
        }
        internal static void FreezeSimulation(NetworkRunner runner)
        {
            foreach (var obj in runner.GetAllNetworkObjects())
                if (obj != null && obj.IsValid) runner.SetIsSimulated(obj, false);
        }
        bool PrepareSnapshot(NetworkRunner runner)
        {
            if(session.Lobby==null || !runner.IsServer) return false;
            var objects=runner.GetAllNetworkObjects();
            FusionMatch match=null;
            foreach(var obj in objects)
                if(obj.GetComponent<FusionMatch>() is FusionMatch candidate)
                { if(match!=null) throw new InvalidOperationException("Multiple matches cannot share one checkpoint."); match=candidate; }
            if(match==null || !match.Initialized || match.Header.Phase!=ArenaMatchPhase.Running) return false;
            if(!session.Lobby.PublishRecovery(runner.Tick.Raw,match.Header.Tick)) return false;
            if(!session.Lobby.TryCaptureRecovery(out var header,out var seats)) return false;
            // Validate exactly the same complete object set that is about to be uploaded.
            ValidateWorld(objects,header,seats,runner.Tick.Raw);
            return true;
        }
        public FusionRecoveryClock Begin(NetworkRunner retired)
        {
            if(baseline!=null || latestSeats==null || lastObservedTick<0 ||
                session.Lobby==null || (session.Lobby.Phase!=SessionPhase.Running && session.Lobby.Phase!=SessionPhase.Warmup))
                throw new InvalidOperationException("A live replicated recovery baseline is required.");
            baseline=FusionRecoveryValidation.ValidateSession(latestHeader,latestSeats,session.Content,session.MatchId,lastObservedTick);
            var clock=new FusionRecoveryClock(latestHeader.CapturedAt,latestHeader.SimulationTick,lastObservedTick,120,lastObservedAt);
            migrationBegan=clock.Now(Time.realtimeSinceStartupAsDouble);
            return clock;
        }
        public SessionState ValidateResume(NetworkRunner replacement)
        {
            if(baseline==null) throw new InvalidOperationException("No migration baseline.");
            foreach(var ignored in replacement.GetResumeSnapshotNetworkSceneObjects())
                throw new InvalidOperationException("Scene network objects are not part of this recovery contract.");
            var objects=new List<NetworkObject>(); FusionLobby lobby=null;
            foreach(var obj in replacement.GetResumeSnapshotNetworkObjects())
            {
                objects.Add(obj);
                if(obj.GetComponent<FusionLobby>() is FusionLobby candidate)
                { if(lobby!=null) throw new InvalidOperationException("Duplicate recovery lobby."); lobby=candidate; }
            }
            if(lobby==null || !lobby.TryCaptureRecovery(out var header,out var seats)) throw new InvalidOperationException("Missing recovery lobby.");
            validated=ValidateWorld(objects,header,seats,lastObservedTick);
            var cloud=FusionRecoveryValidation.ValidateSession(header,seats,session.Content,session.MatchId,lastObservedTick);
            var isolated=new SessionState(session.Content,session.MatchId);
            if(!isolated.TryInstallMigrationBaseline(baseline,migrationBegan) || !isolated.BeginMigration(lastObservedTick,120,migrationBegan) ||
                !isolated.RestoreCheckpoint(cloud,session.Now)) throw new InvalidOperationException("Session recovery failed validation.");
            PrepareDisconnectedSnapshot(validated.Match,isolated.CaptureSeats(session.Now),session.Now);
            var model=new ArenaMatch(validated.Map,120);
            if(!model.TryRestore(validated.Match) || !model.AdvanceAuthorityGeneration(checked((int)isolated.AuthorityGeneration)))
                throw new InvalidOperationException("Recovered gameplay failed validation.");
            return isolated;
        }
        Plan ValidateWorld(IEnumerable<NetworkObject> objects,FusionRecoveryState header,FusionRecoverySeat[] seats,long observedTick)
        {
            var checkpoint=FusionRecoveryValidation.ValidateSession(header,seats,session.Content,session.MatchId,observedTick);
            var plan=new Plan {Header=header,Seats=seats}; int count=0;
            foreach(var obj in objects)
            {
                if(obj==null || ++count>10) throw new InvalidOperationException("Recovery object set exceeds the complete match contract.");
                int supported=0;
                if(obj.GetComponent<FusionLobby>()!=null)
                { if(plan.LobbySource!=null) throw new InvalidOperationException("Duplicate recovery lobby."); plan.LobbySource=obj; supported++; }
                if(obj.GetComponent<FusionMatch>() is FusionMatch match)
                {
                    if(plan.MatchSource!=null || !match.Initialized) throw new InvalidOperationException("Duplicate or uninitialized recovery match.");
                    match.ValidateOrbCheckpoint(header.SimulationTick);
                    plan.MatchSource=obj; plan.Match=match.CaptureSnapshot(); supported++;
                }
                if(obj.GetComponent<FusionPlayer>()!=null) { plan.Avatars.Add(obj); supported++; }
                if(supported!=1) throw new InvalidOperationException("Unsupported or combined network object in recovery snapshot.");
            }
            if(plan.LobbySource==null || plan.MatchSource==null || plan.Match==null || plan.Match.Tick!=header.MatchTick ||
                plan.Match.AuthorityGeneration!=header.AuthorityGeneration || plan.Match.Phase!=ArenaMatchPhase.Running || plan.Match.Practice)
                throw new InvalidOperationException("Recovery snapshot is incomplete or contains incompatible gameplay clocks.");
            var catalog=Resources.Load<ArenaCatalog>("ArenaCatalog");
            if(catalog==null) throw new InvalidOperationException("Arena content is unavailable.");
            plan.Map=catalog.Find(plan.Match.MapId);
            if(plan.Map.Id+"-"+plan.Map.Revision!=session.Content.Map) throw new InvalidOperationException("Recovery map revision mismatch.");
            var model=new ArenaMatch(plan.Map,120);
            if(!model.TryRestore(plan.Match)) throw new InvalidOperationException("Invalid gameplay state.");
            int mask=0;
            foreach(var obj in plan.Avatars)
            {
                var avatar=obj.GetComponent<FusionPlayer>(); int participant=avatar.Participant;
                if(participant<1 || participant>8 || (mask&(1<<participant))!=0 || !avatar.Initialized)
                    throw new InvalidOperationException("Invalid or duplicate recovery participant.");
                mask|=1<<participant;
                var player=model.Find(participant);
                if(player==null) throw new InvalidOperationException("An avatar has no gameplay state.");
                ValidateAvatar(avatar.State,avatar.LastSampleSequence,avatar.PoseEpoch,avatar.FirstRunnerTick,player,header.SimulationTick);
            }
            ValidateCoverage(plan.Match.Players,checkpoint.Seats.Seats,mask);
            return plan;
        }
        internal static void ValidateCoverage(CombatState[] players,SeatSnapshot[] seats,int avatarMask)
        {
            foreach(var player in players)
            {
                SeatSnapshot? binding=null;
                foreach(var seat in seats) if(seat.Team*2+seat.Slot+1==player.Participant) binding=seat;
                bool avatar=(avatarMask&(1<<player.Participant))!=0;
                if((player.Alive || player.Connected) && (!binding.HasValue || !avatar))
                    throw new InvalidOperationException("Live or connected gameplay requires its seat and avatar.");
                if(!binding.HasValue) continue;
                var bound=binding.Value;
                bool pendingReplacement=!player.Alive && !player.Connected && !avatar && bound.RequiresRespawn && !bound.Ready;
                if(!pendingReplacement && (bound.Team!=player.Team || bound.ConnectionGeneration!=player.ConnectionGeneration || bound.Connected!=player.Connected))
                    throw new InvalidOperationException("Recovery seat and gameplay binding disagree.");
            }
            foreach(var seat in seats)
            {
                int participant=seat.Team*2+seat.Slot+1;
                if(Array.Exists(players,player=>player.Participant==participant)) continue;
                if(!seat.RequiresRespawn || seat.Ready || (avatarMask&(1<<participant))!=0)
                    throw new InvalidOperationException("Only a pending spawn may have a reserved seat without gameplay state.");
            }
        }
        internal static void ValidateAvatar(FusionMotorState state,int lastSample,int epoch,int origin,CombatState player,long snapshotTick)
        {
            if(player==null || !ArenaMotor.ValidSnapshot(state.ToMotor()) || state.Generation!=player.LifeGeneration ||
                Vector3.Distance(state.Position,player.Feet)>.001f || Math.Abs(state.CapsuleHeight-player.CapsuleHeight)>.0001f ||
                (long)origin+state.Tick!=snapshotTick || lastSample< -1 || lastSample>=int.MaxValue-1 || epoch<0 || epoch>=int.MaxValue-1)
                throw new InvalidOperationException("Invalid or incoherent recovered motor state.");
        }
        internal static void PrepareDisconnectedSnapshot(MatchSnapshot match,SeatTableSnapshot restoredSeats,double now)
        {
            foreach(var player in match.Players)
            {
                SeatSnapshot? binding=null;
                foreach(var seat in restoredSeats.Seats) if(seat.Team*2+seat.Slot+1==player.Participant) binding=seat;
                if(!binding.HasValue)
                {
                    if(player.Alive || player.Connected) throw new InvalidOperationException("A live recovered player lost its seat binding.");
                    continue;
                }
                player.Connected=false;
                double remaining=Math.Max(0,binding.Value.DisconnectedAt+5-now);
                player.DisconnectUntil=checked(match.Tick+(int)Math.Ceiling(remaining*120));
                if(player.Alive && remaining<=0)
                {
                    player.Alive=false; player.Health=0; player.RespawnTick=checked(match.Tick+(int)Math.Ceiling(WeaponCatalog.RespawnSeconds*120));
                    CombatRules.ClearLife(player);
                }
            }
        }
        public void RestoreValidated(NetworkRunner replacement)
        {
            var plan=validated??throw new InvalidOperationException("A complete plan must be validated before spawning.");
            try
            {
                var lobbyObject=replacement.Spawn(plan.LobbySource,onBeforeSpawned:(r,obj)=>
                {
                    obj.CopyStateFrom(plan.LobbySource);
                    obj.GetComponent<FusionLobby>().RestoreRecovery(plan.Header,plan.Seats);
                });
                var matchObject=replacement.Spawn(plan.MatchSource,onBeforeSpawned:(r,obj)=>
                {
                    obj.CopyStateFrom(plan.MatchSource);
                    obj.GetComponent<FusionMatch>().RestoreCheckpoint(plan.Map,plan.Match,checked((int)session.Rules.AuthorityGeneration));
                    obj.GetComponent<FusionMatch>().RecoveryWarmup=true;
                });
                var match=matchObject.GetComponent<FusionMatch>();
                foreach(var source in plan.Avatars)
                {
                    var old=source.GetComponent<FusionPlayer>();
                    var restored=replacement.Spawn(source,old.State.Position,Quaternion.Euler(0,old.State.Yaw,0),PlayerRef.None,(r,obj)=>
                    {
                        obj.CopyStateFrom(source);
                        obj.GetComponent<FusionPlayer>().RestoreCheckpoint(old.State,old.LastSampleSequence,old.PoseEpoch,old.Participant,
                            checked(r.Tick.Raw-old.State.Tick));
                    }).GetComponent<FusionPlayer>();
                    restored.Object.RemoveInputAuthority();
                    match.BindRecoveredPlayer(old.Participant,restored);
                }
                var lobby=lobbyObject.GetComponent<FusionLobby>();
                foreach(var player in replacement.ActivePlayers)
                    if(!lobby.Admit(player) && lobby.Rejection!=FusionLobby.IdentityPending)
                    {
                        if(player==replacement.LocalPlayer) throw new InvalidOperationException("Replacement host identity could not be rebound.");
                        replacement.Disconnect(player);
                    }
            }
            finally { validated=null; }
        }
    }
}
