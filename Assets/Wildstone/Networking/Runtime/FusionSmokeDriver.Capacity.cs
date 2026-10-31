#if WILDSTONE_SMOKE
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Fusion;
using Fusion.Statistics;
using Wildstone.Match;

namespace Wildstone.Networking
{
    public sealed partial class FusionSmokeDriver
    {
        NetworkRunner capacityRunner;
        FusionStatisticsManager capacityStats;
        int capacityAuthority,capacityLastSdkTick;
        double capacityObservedAt,capacitySdkProgressAt;

        internal static int ParseCapacitySoakSeconds(string value)
        {
            if(value==null) return 10;
            if(!int.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out int seconds) || seconds<10 || seconds>1800)
                throw new ArgumentException("Capacity soak duration must be an integer from 10 to 1800 seconds.");
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
            bool allVerified=true,clientsFinished=true;
            for(int i=0;i<8;i++)
            {
                var peer=ReadCapacityStatus(i);
                if(peer!=null && peer.finished && !peer.success) throw new InvalidOperationException("A capacity smoke peer failed.");
                if(peer==null || !peer.success) allVerified=false;
                if(i>0 && (peer==null || !peer.finished)) clientsFinished=false;
            }
            if(!status.success)
            {
                status.stage="eight_player_stability";
                if(!coherent)
                {
                    if(stableAt>=0) throw new InvalidOperationException("Eight-player capacity coherence was lost during the soak.");
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
                if(!ReferenceEquals(capacityRunner,runner) || capacityAuthority!=model.AuthorityGeneration || runner.Tick.Raw<capacityLastSdkTick)
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
                }
                capacityLastSdkTick=runner.Tick.Raw; capacityObservedAt=now;
                status.stableSeconds=(float)(now-stableAt);
                if(now-stableAt<status.capacitySoakSeconds || model.Tick<=firstMatchTick) return;
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
    }
}
#endif
