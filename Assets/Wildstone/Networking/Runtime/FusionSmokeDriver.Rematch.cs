#if WILDSTONE_SMOKE
using System;
using Fusion;
using UnityEngine.UIElements;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Match;
using Wildstone.Session;
using Wildstone.UI;

namespace Wildstone.Networking
{
    public sealed partial class FusionSmokeDriver
    {
        // Two rounds in one room: play, smoke-finish and verify Results; the host rematches; both re-ready on the retained
        // seats; a fresh second round plays and finishes. Only the 480 s clock is skipped (FusionMatch.SmokeFinish).
        [Serializable] sealed class RematchMetrics
        {
            public int playedRounds,finishedRounds,resultsVerified,participant,team;
            public int[] winners={-1,-1};
            public long connectionGeneration,firstMatchId,secondMatchId,publishedTickAtRematch=-1,secondRoundPublishedTick=-1;
            public string room="",matchId="";
            public bool rematchRequested,clientRematchRefused,lobbyReturned,readyCleared,freshVerified,newHud;
            public int freshPlayers,freshProjectiles,freshScoreTicks,freshPickups,freshPickupCount,freshElapsedTicks;
            public float freshMinHealth;
        }
        NetworkRunner rematchRunner;
        ArenaHud rematchHud;
        double rematchStable=-1;
        int rematchFirstTick,rematchFinishRequested;

        void ObserveRematchLobby(FusionLobby lobby,NetworkRunner runner)
        {
            var m=status.rematch;
            if(m==null || m.finishedRounds!=1 || m.lobbyReturned) return;
            if(status.role=="host" && !m.rematchRequested) throw new InvalidOperationException("The session returned to its lobby without the host's rematch.");
            var seat=lobby.Seats[m.participant-1];
            if(!seat.Occupied || !seat.Connected || seat.Player!=runner.LocalPlayer || seat.Team!=m.team || seat.ConnectionGeneration!=m.connectionGeneration ||
                seat.Ready || bridge.Room!=m.room || menu.Session.MatchId!=m.matchId || !ReferenceEquals(runner,rematchRunner))
                throw new InvalidOperationException("The rematch lobby changed the room, seat or readiness.");
            m.lobbyReturned=m.readyCleared=true; lastMatchTick=-1; rematchStable=-1;
            // The ordinary lobby flow then readies both peers and the host starts the second round.
            readyRequested=teamRequested=startRequested=false;
            status.stage="rematch_lobby"; WriteOutput();
        }
        void ObserveRematch(FusionSession session,NetworkRunner runner,ArenaMatch model,bool playing,Status peer,double now)
        {
            var m=status.rematch??(status.rematch=new RematchMetrics()); var q=peer?.rematch;
            if(status.success)
            {
                status.stage="verified";
                if(PeerVerified() && now-verifiedAt>=(status.role=="host"?5:1)) Finish(true,"");
                return;
            }
            var lobby=session.Lobby;
            if(m.playedRounds==m.finishedRounds+1 && model!=null && model.Phase==ArenaMatchPhase.Finished &&
                presentation.ExternalSimulation && presentation.Flow.Screen==MenuScreen.Results)
            {
                VerifyRematchResults(lobby,model,m);
                if(++m.finishedRounds==2) { status.success=true; verifiedAt=now; status.stage="verified"; }
                WriteOutput(); return;
            }
            if(m.finishedRounds==1 && !m.lobbyReturned)
            {
                status.stage="rematch_results";
                if(status.role!="host" || m.rematchRequested || q==null || q.finishedRounds!=1 || !q.clientRematchRefused) return;
                m.rematchRequested=true; m.publishedTickAtRematch=session.LastPublishedTick;
                bridge.Rematch();
                if(lobby.Phase!=SessionPhase.Lobby || presentation.Match!=null || presentation.Flow.Screen!=MenuScreen.Online)
                    throw new InvalidOperationException("The host rematch did not return the session to its lobby.");
                foreach(var seat in lobby.Seats) if(seat.Occupied && seat.Ready) throw new InvalidOperationException("The rematch kept a ready seat.");
                WriteOutput(); return;
            }
            status.stage=m.finishedRounds==0?"rematch_first_round":"rematch_second_round";
            if(!playing) { rematchStable=-1; return; }
            if(m.playedRounds==m.finishedRounds)
            {
                if(m.finishedRounds==1 && !m.freshVerified) VerifyRematchFresh(session,runner,model,m);
                if(rematchStable<0) { rematchStable=now; rematchFirstTick=model.Tick; }
                status.stableSeconds=(float)(now-rematchStable);
                if(now-rematchStable<3 || model.Tick<=rematchFirstTick) return;
                if(m.finishedRounds==0) RecordRematchIdentity(session,runner,m);
                m.playedRounds++; WriteOutput(); return;
            }
            // Both peers watched this round run; only the host ends it.
            status.stage="rematch_finishing";
            if(status.role!="host" || rematchFinishRequested==m.playedRounds || q==null || q.playedRounds!=m.playedRounds || m.finishedRounds==1 && !q.freshVerified) return;
            if(m.finishedRounds==1)
            {
                // The second match must still upload a complete recovery checkpoint.
                status.stage="rematch_awaiting_checkpoint";
                if(session.LastPublishedTick<=m.publishedTickAtRematch) return;
                m.secondRoundPublishedTick=session.LastPublishedTick;
            }
            var match=RematchMatch(runner);
            if(match==null || !match.SmokeFinish(presentation.LocalPlayer.Team)) throw new InvalidOperationException("The smoke match could not finish.");
            rematchFinishRequested=m.playedRounds; WriteOutput();
        }
        FusionMatch RematchMatch(NetworkRunner runner)
        {
            foreach(var candidate in FindObjectsByType<FusionMatch>())
                if(candidate.Object!=null && candidate.Object.IsValid && candidate.Runner==runner && candidate.Initialized) return candidate;
            return null;
        }
        void RecordRematchIdentity(FusionSession session,NetworkRunner runner,RematchMetrics m)
        {
            var match=RematchMatch(runner); var local=presentation.LocalPlayer; var arena=menu.GetComponent<ArenaMenu>();
            rematchHud=arena==null?null:arena.Hud;
            if(match==null || local==null || rematchHud==null) throw new InvalidOperationException("The first rematch round has no live match.");
            m.firstMatchId=match.Object.Id.Raw; m.participant=presentation.LocalParticipantId; m.team=local.Team;
            m.connectionGeneration=local.ConnectionGeneration; m.room=bridge.Room; m.matchId=session.MatchId; rematchRunner=runner;
        }
        void VerifyRematchResults(FusionLobby lobby,ArenaMatch model,RematchMetrics m)
        {
            var root=menu.GetComponent<UIDocument>().rootVisualElement;
            var buttons=root.Query<Button>().ToList(); var labels=root.Query<Label>().ToList();
            bool host=status.role=="host", rematch=buttons.Exists(b=>b.text=="Rematch"), leave=buttons.Exists(b=>b.text=="Leave match"),
                offline=buttons.Exists(b=>b.text=="Play again"), waiting=labels.Exists(l=>l.text!=null && l.text.StartsWith("Waiting for the host",StringComparison.Ordinal));
            if(!leave || offline || rematch!=host || waiting==host || lobby==null || lobby.Phase!=SessionPhase.Running)
                throw new InvalidOperationException("Online results did not offer the host rematch and the client wait.");
            m.winners[m.finishedRounds]=model.Winner; m.resultsVerified++;
            if(!host && m.finishedRounds==0)
            {
                // A client request is ignored locally and has no message to the host.
                bridge.Rematch();
                if(presentation.Flow.Screen!=MenuScreen.Results || presentation.Match==null || lobby.Phase!=SessionPhase.Running)
                    throw new InvalidOperationException("A client rematch request changed the session.");
                m.clientRematchRefused=true;
            }
        }
        void VerifyRematchFresh(FusionSession session,NetworkRunner runner,ArenaMatch model,RematchMetrics m)
        {
            var match=RematchMatch(runner); var local=presentation.LocalPlayer; var arena=menu.GetComponent<ArenaMenu>();
            m.secondMatchId=match==null?0:match.Object.Id.Raw;
            m.freshPlayers=model.Players.Count; m.freshProjectiles=model.Projectiles.Count; m.freshElapsedTicks=model.ElapsedTicks;
            m.freshPickupCount=model.Pickups.Count; m.freshScoreTicks=m.freshPickups=0; m.freshMinHealth=float.MaxValue;
            foreach(int score in model.ScoreTicks) m.freshScoreTicks+=score;
            foreach(var item in model.Pickups) if(item.Generation==1 && item.AvailableTick<=model.Tick) m.freshPickups++;
            bool firstLives=true;
            foreach(var player in model.Players)
            {
                m.freshMinHealth=Math.Min(m.freshMinHealth,player.Health);
                firstLives&=player.Alive && player.LifeGeneration==1 && player.StarterAmmo==WeaponCatalog.StarterAmmo;
            }
            m.newHud=arena!=null && arena.Hud!=null && rematchHud!=null && !ReferenceEquals(arena.Hud,rematchHud);
            if(match==null || m.secondMatchId==m.firstMatchId || m.freshPlayers!=2 || m.freshProjectiles!=0 || m.freshScoreTicks!=0 || model.Winner!=-1 ||
                m.freshPickupCount==0 || m.freshPickups!=m.freshPickupCount || m.freshMinHealth!=WeaponCatalog.Health || !firstLives || !m.newHud ||
                m.freshElapsedTicks>10*model.TickRate || presentation.LocalParticipantId!=m.participant || local.Team!=m.team ||
                local.ConnectionGeneration!=m.connectionGeneration || session.MatchId!=m.matchId || bridge.Room!=m.room || !ReferenceEquals(runner,rematchRunner))
                throw new InvalidOperationException("The rematch did not start a fresh match on the retained seats.");
            m.freshVerified=true;
        }
    }
}
#endif
