#if WILDSTONE_SMOKE
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Fusion;
using Fusion.Statistics;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Match;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    public sealed partial class FusionSmokeDriver
    {
        NetworkRunner capacityRunner;
        FusionStatisticsManager capacityStats;
        int capacityAuthority,capacityLastSdkTick;
        double capacityObservedAt,capacitySdkProgressAt;
        FusionPlayer soakAvatar;
        bool soakHooked,soakWasRunning,soakRoundCoherent;
        int soakLife=-1,soakSequence,soakBeat,soakRematched,soakFinishRequested;
        double soakNextShot,soakRoundBegan,soakFinishedAt=-1,capacityNextPeerRead;
        bool capacityAllVerified,capacityClientsFinished;

        internal static int ParseCapacitySoakSeconds(string value)
        {
            if(value==null) return 10;
            if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int seconds) || seconds<10 || seconds>7200)
                throw new ArgumentException("Capacity soak duration must be an integer from 10 to 7200 seconds.");
            return seconds;
        }
        static int CapacityRoleIndex(string role)
        {
            if(role=="host") return 0;
            if(role=="client") return 1;
            if(role!=null && role.Length==7 && role.StartsWith("client",StringComparison.Ordinal) && role[6]>='2' && role[6]<='7') return role[6]-'0';
            return -1;
        }
        Status ReadCapacityStatus(int index)
        {
            string role=index==0?"host":index==1?"client":"client"+index;
            if(role==status.role) return status;
            string path=Path.Combine(Path.GetDirectoryName(output),role+".json");
            try
            {
                if(!File.Exists(path)) return null;
                var peer=ReadStatus(path);
                return peer!=null && peer.role==role && peer.scenario=="capacity" && peer.room==status.room?peer:null;
            }
            catch(IOException) { return null; }
        }
        void ObserveCapacity(FusionSession session,NetworkRunner runner,ArenaMatch model,bool playing,double now)
        {
            if(status.capacityTeamSizes==null) status.capacityTeamSizes=new int[4];
            status.managedHeapBytes=GC.GetTotalMemory(false); status.gcCollections=GC.CollectionCount(0);
            status.maxFrameGapSeconds=Math.Max(status.maxFrameGapSeconds,Time.unscaledDeltaTime);
            Array.Clear(status.capacityTeamSizes,0,4); status.connectedPlayerCount=0; status.connectedTeamCount=0;
            bool running=model!=null && model.Phase==ArenaMatchPhase.Running;
            bool finished=model!=null && model.Phase==ArenaMatchPhase.Finished && presentation.ExternalSimulation && (presentation.Flow.Screen==Wildstone.Game.MenuScreen.Results || presentation.Flow.Screen==Wildstone.Game.MenuScreen.Playing);
            bool coherent=(running && playing || finished && stableAt>=0) && runner!=null && runner.IsRunning &&
                session.Phase==Wildstone.Session.SessionPhase.Running && !session.Recovering && !session.SimulationFrozen &&
                model!=null && model.Players.Count==8 && status.avatarCount==8;
            if((model!=null && model.Phase==ArenaMatchPhase.Aborted) || session.Recovering || session.SimulationFrozen)
                throw new InvalidOperationException("Capacity soak cannot include recovery, frozen gameplay or an aborted match.");
            if(model!=null)
                foreach(var player in model.Players)
                {
                    if(!player.Connected) continue;
                    status.connectedPlayerCount++;
                    if(player.Team<0 || player.Team>3) throw new InvalidOperationException("Capacity smoke observed an invalid team.");
                    status.capacityTeamSizes[player.Team]++;
                    var lobby=session.Lobby;
                    if(lobby==null || player.Participant<1 || player.Participant>8) { coherent=false; continue; }
                    var seat=lobby.Seats[player.Participant-1];
                    if(!seat.Occupied || !seat.Connected || !seat.Ready || seat.Team!=player.Team || seat.ConnectionGeneration!=player.ConnectionGeneration) coherent=false;
                }
            foreach(int count in status.capacityTeamSizes)
            { if(count>0) status.connectedTeamCount++; if(count!=2) coherent=false; }
            coherent&=status.connectedPlayerCount==8 && status.connectedTeamCount==4;
            // Gameplay soak: from Results until the next round first becomes coherent (its avatars spawn after it starts
            // Running), only the eight retained seats in four teams of two must hold.
            if(!running) soakRoundCoherent=false; else if(coherent) soakRoundCoherent=true;
            bool between=status.soakGameplay && stableAt>=0 && !coherent && (!running || !soakRoundCoherent) && SeatsCoherent(session.Lobby);
            // Peer files are parsed twice a second, not every frame, so soak GC figures reflect the game rather than this harness.
            if(now>=capacityNextPeerRead)
            {
                capacityNextPeerRead=now+.5; capacityAllVerified=capacityClientsFinished=true;
                for(int i=0;i<8;i++)
                {
                    var peer=ReadCapacityStatus(i);
                    if(peer!=null && peer.finished && !peer.success) throw new InvalidOperationException("A capacity smoke peer failed.");
                    if(peer==null || !peer.success) capacityAllVerified=false;
                    if(i>0 && (peer==null || !peer.finished)) capacityClientsFinished=false;
                }
            }
            bool allVerified=capacityAllVerified,clientsFinished=capacityClientsFinished;
            if(status.soakGameplay && stableAt>=0) DriveSoak(session,runner,model,running && playing,finished,now);
            if(!status.success)
            {
                status.stage="eight_player_stability";
                if(!coherent && !between)
                {
                    if(stableAt>=0 && status.maxFrameGapSeconds>2)
                        throw new InvalidOperationException($"Capacity soak environment stalled ({status.maxFrameGapSeconds:0.0} s frame gap) and {8-status.connectedPlayerCount} peers dropped; rerun on an idle machine.");
                    if(stableAt>=0) throw new InvalidOperationException($"Eight-player capacity coherence was lost during the soak ({status.connectedPlayerCount}/8 connected, largest frame gap {status.maxFrameGapSeconds:0.00} s).");
                    status.stableSeconds=0; return;
                }
                if(stableAt<0)
                {
                    stableAt=capacityObservedAt=capacitySdkProgressAt=now; firstMatchTick=model.Tick;
                    capacityRunner=runner; capacityAuthority=model.AuthorityGeneration; capacityLastSdkTick=runner.Tick.Raw;
                    status.capacityHoldStartElapsed=(float)(now-began);
                    // Release DLLs compile statistics out; the hold still verifies, with traffic reported unavailable.
                    status.capacityStatsAvailable=runner.TryGetFusionStatistics(out capacityStats);
                }
                if(!ReferenceEquals(capacityRunner,runner) || (model!=null && capacityAuthority!=model.AuthorityGeneration) || runner.Tick.Raw<capacityLastSdkTick)
                    throw new InvalidOperationException("Capacity soak runner or authority changed.");
                int advanced=runner.Tick.Raw-capacityLastSdkTick;
                if(advanced>0) capacitySdkProgressAt=now;
                if(now-capacitySdkProgressAt>2) throw new InvalidOperationException("Capacity soak SDK ticks stopped advancing.");
                // The default updater runs one Fusion loop before each script Update, so each completed snapshot is read once.
                float bytesIn=0,bytesOut=0;
                if(now>capacityObservedAt && capacityStats?.SimulationSnapshot?.Stats is Dictionary<FusionStatType,float> stats)
                {
                    stats.TryGetValue(FusionStatType.InBandwidth,out bytesIn); stats.TryGetValue(FusionStatType.OutBandwidth,out bytesOut);
                    if(stats.TryGetValue(FusionStatType.ForwardTicks,out float forward)) status.capacityStatsForwardTicks+=(int)forward;
                }
                if(running)
                {
                    status.capacityRunningSeconds+=(float)(now-capacityObservedAt); status.capacityRunningSdkTicks+=advanced;
                    status.capacityRunningInBytes+=bytesIn; status.capacityRunningOutBytes+=bytesOut;
                    status.capacityRunningInBytesPerSecond=(float)(status.capacityRunningInBytes/Math.Max(.001,status.capacityRunningSeconds));
                    status.capacityRunningOutBytesPerSecond=(float)(status.capacityRunningOutBytes/Math.Max(.001,status.capacityRunningSeconds));
                }
                else
                {
                    status.capacityFinishedSeconds+=(float)(now-capacityObservedAt); status.capacityFinishedSdkTicks+=advanced;
                    status.capacityMatchFinished=true;
                    status.capacityFinishedInBytes+=bytesIn; status.capacityFinishedOutBytes+=bytesOut;
                    status.capacityFinishedInBytesPerSecond=(float)(status.capacityFinishedInBytes/Math.Max(.001,status.capacityFinishedSeconds));
                    status.capacityFinishedOutBytesPerSecond=(float)(status.capacityFinishedOutBytes/Math.Max(.001,status.capacityFinishedSeconds));
                    if(between) status.soakBetweenSeconds+=(float)(now-capacityObservedAt);
                }
                capacityLastSdkTick=runner.Tick.Raw; capacityObservedAt=now;
                status.stableSeconds=(float)(now-stableAt);
                if(now-stableAt<status.capacitySoakSeconds || !coherent || (!status.soakGameplay && model.Tick<=firstMatchTick)) return;
                status.capacityVerifiedPlayers=model.Players.Count; status.capacityVerifiedAvatars=status.avatarCount;
                status.capacityVerifiedConnected=status.connectedPlayerCount; status.capacityVerifiedTeams=status.connectedTeamCount;
                status.capacityVerifiedTeamSizes=(int[])status.capacityTeamSizes.Clone(); status.capacityVerifiedMatchTick=model.Tick;
                status.capacityHoldEndElapsed=(float)(now-began);
                status.success=true; status.stage="verified"; verifiedAt=now; WriteOutput();
                return;
            }
            status.stage="awaiting_all_eight_verified";
            if(allVerified && now-verifiedAt>=1 && (status.role!="host" || clientsFinished)) Finish(true,"");
        }
        static bool SeatsCoherent(FusionLobby lobby)
        {
            if(lobby==null) return false;
            var teams=new int[4]; int connected=0;
            foreach(var seat in lobby.Seats)
                if(seat.Occupied && seat.Connected && seat.Team>=0 && seat.Team<4) { connected++; teams[seat.Team]++; }
            return connected==8 && teams[0]==2 && teams[1]==2 && teams[2]==2 && teams[3]==2;
        }
        // Gameplay soak: weave, jump, dash and slide; fire at the nearest enemy and reload when empty. The host rematches
        // 3 s into each Results screen (or smoke-finishes a round after soakRoundSeconds) and the lobby flow readies all eight.
        void DriveSoak(FusionSession session,NetworkRunner runner,ArenaMatch model,bool running,bool finished,double now)
        {
            if(!soakHooked) { soakHooked=true; session.InputRequested+=SmokeSoakInput; }
            if(running && !soakWasRunning)
            {
                status.soakRounds++; soakAvatar=null; soakLife=-1; soakFinishedAt=-1; soakRoundBegan=now;
                capacityAuthority=model.AuthorityGeneration;
            }
            soakWasRunning=running;
            if(!running)
            {
                soakAvatar=null;
                if(!finished) return;
                if(soakFinishedAt<0) { soakFinishedAt=now; readyRequested=teamRequested=startRequested=false; }
                if(status.role=="host" && soakRematched!=status.soakRounds && now-soakFinishedAt>=3 && presentation.Flow.Screen==Wildstone.Game.MenuScreen.Results)
                { soakRematched=status.soakRounds; bridge.Rematch(); }
                return;
            }
            var local=presentation.LocalPlayer;
            if(status.role=="host" && status.soakRoundSeconds>0 && now-soakRoundBegan>=status.soakRoundSeconds && soakFinishRequested!=status.soakRounds)
            {
                var match=RematchMatch(runner);
                if(match==null || !match.SmokeFinish(local.Team)) throw new InvalidOperationException("The soak round could not finish.");
                soakFinishRequested=status.soakRounds;
            }
            if(!SoakAvatarLive(soakAvatar))
            {
                soakAvatar=null;
                foreach(var avatar in FindObjectsByType<FusionPlayer>())
                    if(avatar.Runner==runner && SoakAvatarLive(avatar) && avatar.Participant==local.Participant) soakAvatar=combatAvatar=avatar;
            }
            if(soakAvatar==null) return;
            if(local.LifeGeneration!=soakLife) { if(soakLife>=0) status.soakDeaths++; soakLife=local.LifeGeneration; soakSequence=0; }
            if(!local.Alive || now<soakNextShot) return;
            soakNextShot=now+.4;
            if(local.StarterAmmo==0)
            {
                if(local.ReloadUntil==0) { status.soakReloads++; SoakAction(model,local,CombatActionKind.Reload,Vector3.forward); }
                return;
            }
            CombatState target=null; float best=float.MaxValue;
            foreach(var p in model.Players)
            {
                float distance=(p.Feet-local.Feet).sqrMagnitude;
                if(p.Alive && p.Team!=local.Team && distance<best) { best=distance; target=p; }
            }
            if(target==null) return;
            status.soakShots++; SoakAction(model,local,CombatActionKind.Fire,(target.Eye-local.Eye).normalized);
        }
        void SoakAction(ArenaMatch model,CombatState local,CombatActionKind kind,Vector3 direction) =>
            combatAvatar.RPC_Combat(FusionCombatAction.FromCombat(new CombatAction {Participant=local.Participant,ConnectionGeneration=local.ConnectionGeneration,
                LifeGeneration=local.LifeGeneration,AuthorityGeneration=model.AuthorityGeneration,Sequence=soakSequence++,Kind=kind,Direction=direction,PickupId=-1}));
        static bool SoakAvatarLive(FusionPlayer avatar) => avatar!=null && avatar.Initialized && avatar.Object!=null && avatar.Object.IsValid;
        void SmokeSoakInput(NetworkInput output)
        {
            var avatar=soakAvatar; if(!SoakAvatarLive(avatar)) return;
            float t=(float)(Time.realtimeSinceStartupAsDouble-began)+status.roleIndex*.37f;
            combatInputSequence=checked(Math.Max(combatInputSequence,avatar.LastSampleSequence)+1);
            var pressed=MotorButtons.None; int beat=(int)(t*4);
            if(beat!=soakBeat) { soakBeat=beat; pressed=beat%8==0?MotorButtons.Jump:beat%20==5?MotorButtons.Dash:beat%14==9?MotorButtons.Slide:MotorButtons.None; }
            output.Set(new FusionInput {Present=true,Sequence=combatInputSequence,Generation=avatar.State.Generation,
                Move=new Vector2(Mathf.Sin(t*.9f)*.6f,1),Yaw=Mathf.Repeat(t*35+status.roleIndex*45,360)-180,Held=pressed,Pressed=pressed});
        }
    }
}
#endif
