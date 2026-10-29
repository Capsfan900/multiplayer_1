#if WILDSTONE_SMOKE
using System;
using System.IO;
using Fusion;
using Wildstone.Match;

namespace Wildstone.Networking
{
    public sealed partial class FusionSmokeDriver
    {
        Status ReadLateJoinStatus(string role)
        {
            if(role==status.role) return status;
            string path=Path.Combine(Path.GetDirectoryName(output),role+".json");
            try
            {
                if(!File.Exists(path)) return null;
                var peer=ReadStatus(path);
                return peer!=null && peer.role==role && peer.scenario=="late-join" && peer.room==status.room?peer:null;
            }
            catch(IOException) { return null; }
        }
        void ObserveLateJoin(FusionSession session,NetworkRunner runner,ArenaMatch model,bool playing,double now)
        {
            var host=ReadLateJoinStatus("host"); var client=ReadLateJoinStatus("client"); var late=ReadLateJoinStatus("client2");
            foreach(var peer in new[]{host,client,late})
                if(peer!=null && peer.finished && !peer.success) throw new InvalidOperationException("A late-join smoke peer failed.");
            if(status.success)
            {
                status.stage="awaiting_three_peer_success";
                if(host!=null && client!=null && late!=null && host.success && client.success && late.success && now-verifiedAt>=1 &&
                    (status.role!="host" || client.finished && late.finished)) Finish(true,"");
                return;
            }
            int connected=0,teamMask=0;
            if(model!=null) foreach(var player in model.Players) if(player.Connected) { connected++; teamMask|=1<<player.Team; }
            if(!status.initialVerified && status.role!="client2")
            {
                status.stage="initial_two_player_stability";
                if(!playing || connected!=2 || model.Players.Count!=2 || status.avatarCount!=2) { stableAt=-1; status.stableSeconds=0; return; }
                if(stableAt<0) { stableAt=now; firstMatchTick=model.Tick; }
                status.stableSeconds=(float)(now-stableAt);
                if(now-stableAt<10 || model.Tick<=firstMatchTick) return;
                status.originalAuthorityGeneration=model.AuthorityGeneration; status.originalParticipant=presentation.LocalParticipantId;
                status.originalTeam=presentation.LocalPlayer.Team; status.previousConnectionGeneration=presentation.LocalPlayer.ConnectionGeneration;
                status.originalMatchId=session.MatchId; status.initialVerified=true; status.stage="waiting_late_peer";
                stableAt=-1; status.stableSeconds=0; WriteOutput(); return;
            }
            if(host==null || client==null || !host.initialVerified || !client.initialVerified) return;
            if(host.originalAuthorityGeneration!=client.originalAuthorityGeneration || host.originalMatchId!=client.originalMatchId)
                throw new InvalidOperationException("The initial peers disagree on the running match.");
            if(!playing || connected!=3 || model.Players.Count!=3 || status.avatarCount!=3 || teamMask!=7)
            { stableAt=-1; status.lateJoinStableSeconds=0; status.stage="waiting_late_peer"; return; }
            if(!status.initialVerified)
            {
                if(presentation.LocalPlayer.Team!=2) throw new InvalidOperationException("The late peer did not enter the unused team.");
                status.originalAuthorityGeneration=model.AuthorityGeneration; status.originalParticipant=presentation.LocalParticipantId;
                status.originalTeam=presentation.LocalPlayer.Team; status.previousConnectionGeneration=presentation.LocalPlayer.ConnectionGeneration;
                status.originalMatchId=session.MatchId; status.initialVerified=true;
            }
            if(model.AuthorityGeneration!=host.originalAuthorityGeneration || session.MatchId!=host.originalMatchId ||
                presentation.LocalParticipantId!=status.originalParticipant || presentation.LocalPlayer.Team!=status.originalTeam ||
                presentation.LocalPlayer.ConnectionGeneration!=status.previousConnectionGeneration)
                throw new InvalidOperationException("Late admission changed the match authority or an existing local seat.");
            var hostPlayer=model.Find(host.originalParticipant); var firstClient=model.Find(client.originalParticipant);
            if(hostPlayer==null || firstClient==null || hostPlayer.Team!=host.originalTeam || firstClient.Team!=client.originalTeam ||
                hostPlayer.ConnectionGeneration!=host.previousConnectionGeneration || firstClient.ConnectionGeneration!=client.previousConnectionGeneration)
                throw new InvalidOperationException("Late admission replaced an existing peer binding.");
            bool boundLocal=false;
            foreach(var avatar in FindObjectsByType<FusionPlayer>())
                if(avatar.Runner==runner && avatar.Initialized && avatar.Participant==presentation.LocalParticipantId &&
                    avatar.Object!=null && avatar.Object.IsValid && avatar.Object.InputAuthority==runner.LocalPlayer) boundLocal=true;
            if(!boundLocal) { stableAt=-1; status.lateJoinStableSeconds=0; return; }
            foreach(var player in model.Players)
            {
                var seat=session.Lobby.Seats[player.Participant-1];
                if(!seat.Occupied || !seat.Connected || !seat.Ready || seat.Team!=player.Team || seat.ConnectionGeneration!=player.ConnectionGeneration)
                { stableAt=-1; status.lateJoinStableSeconds=0; return; }
                if(player.Team==2) status.lateJoinParticipant=player.Participant;
            }
            if(stableAt<0) { stableAt=now; firstMatchTick=model.Tick; }
            status.stage="three_player_late_join_stability"; status.lateJoinStableSeconds=(float)(now-stableAt);
            if(now-stableAt<10 || model.Tick<=firstMatchTick) return;
            status.lateJoinConnectedPlayers=connected; status.lateJoinAvatars=status.avatarCount; status.lateJoinTeams=3;
            status.lateJoinComplete=true; status.success=true; status.stage="verified"; verifiedAt=now; WriteOutput();
        }
    }
}
#endif
