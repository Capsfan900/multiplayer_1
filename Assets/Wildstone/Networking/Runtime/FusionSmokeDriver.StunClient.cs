#if WILDSTONE_SMOKE
using System;
using Fusion;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Match;

namespace Wildstone.Networking
{
    public sealed partial class FusionSmokeDriver
    {
        [Serializable] sealed class StunClientMetrics
        {
            public bool placed,moving,stunned,suppressionObserved,settled,released,verified;
            public int participant,life,liftTick,lastSuppressedTick,releasedTick,releaseLagTicks,stunnedSamples;
            public float movingTravel,stunnedTravel,releasedTravel=-1,maxReleaseJump;
        }
        double stunClientBegan=-1,stunClientReleasedAt=-1;
        Vector3 stunClientOrigin,stunClientReleaseOrigin,stunClientPrevious;
        bool stunClientInput;
        void SmokeStunClientInput(NetworkInput output)
        {
            if(!stunClientInput || combatAvatar==null) return;
            combatInputSequence=checked(Math.Max(combatInputSequence,combatAvatar.LastSampleSequence)+1);
            output.Set(new FusionInput {Present=true,Sequence=combatInputSequence,Generation=combatAvatar.State.Generation,
                Move=Vector2.up,Yaw=combatAvatar.State.Yaw,Pitch=combatAvatar.State.Pitch});
        }
        bool DriveStunClient(ArenaMatch model,NetworkRunner runner,Status peer,double now)
        {
            var point=new Vector3(-30,.02f,-15);
            if(status.stunClient==null)
            {
                status.stunClient=new StunClientMetrics(); stunClientBegan=now;
                foreach(var candidate in FindObjectsByType<FusionMatch>()) if(candidate.Runner==runner && candidate.Initialized) combatMatch=candidate;
                if(combatMatch==null) throw new InvalidOperationException("Missing stun-client match.");
                combatAvatar=LaserAvatar(runner,presentation.LocalParticipantId);
            }
            var m=status.stunClient; var q=peer?.stunClient;
            if(m.verified && q?.verified==true) return true;
            if(now-stunClientBegan>30) throw new TimeoutException("Stun-client fixture timed out at "+status.stage);
            if(m.verified) return false;
            bool host=status.role=="host";
            if(host) model=combatMatch.Model;
            var target=host?RemotePlayer(model):model.Find(presentation.LocalParticipantId);
            if(target==null || !target.Alive) throw new InvalidOperationException("Stun-client fixture lost its living client.");
            if(m.life!=0 && (m.participant!=target.Participant || m.life!=target.LifeGeneration))
                throw new InvalidOperationException("Stun-client fixture changed client life identity.");
            m.participant=target.Participant; m.life=target.LifeGeneration;
            status.stage="stun_client_placement";
            if(host && !m.placed)
            { LaserAvatar(runner,target.Participant).Teleport(point,0); m.placed=true; WriteOutput(); }
            if(q==null) return false;
            if(!host && !m.placed)
            {
                if(!q.placed || combatAvatar==null || (combatAvatar.State.Position-point).sqrMagnitude>.04f) return false;
                m.placed=true; combatSession=menu.Session; combatSession.InputRequested+=SmokeStunClientInput; stunClientInput=true;
            }
            if(!m.placed || !q.placed) return false;
            status.stage="stun_client_moving";
            if(!host && !m.suppressionObserved)
            { m.movingTravel=HorizontalDistance(combatAvatar.State.Position,point); m.moving|=m.movingTravel>1; }
            if(host && !m.stunned && q.moving && q.movingTravel>1)
            {
                if(!CombatRules.Stun(model.Find(target.Participant),model.Tick,model.TickRate))
                    throw new InvalidOperationException("Stun-client diagnostic stun was rejected.");
                m.stunned=true; m.liftTick=target.StunUntil; WriteOutput();
            }
            status.stage="stun_client_measure";
            if(host)
            {
                if(!m.stunned) return false;
                if(model.Tick<m.liftTick)
                {
                    // Stun removes input, not momentum: a full-speed runner coasts ~8 cm in the first 0.15 s, under 1 cm after 0.3 s.
                    if(model.Tick<m.liftTick-WeaponCatalog.Ticks(.15f,model.TickRate)) return false;
                    if(!m.settled) { m.settled=true; stunClientOrigin=target.Feet; }
                    m.stunnedSamples++; m.stunnedTravel=Mathf.Max(m.stunnedTravel,HorizontalDistance(target.Feet,stunClientOrigin));
                }
                else
                {
                    if(!m.released) { m.released=true; m.releasedTick=model.Tick; stunClientReleaseOrigin=target.Feet; }
                    if(model.Tick<m.liftTick+WeaponCatalog.Ticks(.4f,model.TickRate)) return false;
                    m.releasedTravel=HorizontalDistance(target.Feet,stunClientReleaseOrigin);
                }
            }
            else
            {
                var state=combatAvatar.State; var position=state.Position;
                if(m.liftTick==0 && state.SuppressedUntil!=0) m.liftTick=state.SuppressedUntil;
                if(!m.suppressionObserved && state.Suppressed && m.liftTick!=0)
                { m.suppressionObserved=true; stunClientPrevious=position; }
                if(!m.suppressionObserved) return false;
                if(!m.released && state.Suppressed) m.lastSuppressedTick=state.Tick;
                if(!m.released && !state.Suppressed)
                {
                    m.released=true; m.releasedTick=state.Tick; m.releaseLagTicks=m.releasedTick-m.liftTick;
                    stunClientReleasedAt=now; stunClientReleaseOrigin=position;
                    m.maxReleaseJump=HorizontalDistance(position,stunClientPrevious);
                    // A frame can advance several ticks; release is exact when the lift tick falls inside the observed release step.
                    if(m.lastSuppressedTick>=m.liftTick || m.releasedTick<m.liftTick)
                        throw new InvalidOperationException("Stun-client predicted release missed lift tick "+m.liftTick+": suppressed at "+m.lastSuppressedTick+", released at "+m.releasedTick);
                }
                if(!m.released && state.Tick>=m.liftTick-WeaponCatalog.Ticks(.15f,model.TickRate))
                {
                    if(!m.settled) { m.settled=true; stunClientOrigin=position; }
                    m.stunnedSamples++; m.stunnedTravel=Mathf.Max(m.stunnedTravel,HorizontalDistance(position,stunClientOrigin));
                }
                if(!m.released) stunClientPrevious=position;
                if(m.released)
                {
                    m.maxReleaseJump=Mathf.Max(m.maxReleaseJump,HorizontalDistance(position,stunClientPrevious)); stunClientPrevious=position;
                    if(now-stunClientReleasedAt<.4) return false;
                    m.releasedTravel=HorizontalDistance(position,stunClientReleaseOrigin);
                }
            }
            if(m.releasedTravel<0) return false;
            if(!m.settled || m.stunnedSamples<2) throw new InvalidOperationException("Stun-client lacks settled suppression samples: "+status.role);
            if(!(m.stunnedTravel<.05f)) throw new InvalidOperationException("Stun-client moved while stunned: "+status.role+" "+m.stunnedTravel);
            if(!(m.releasedTravel>.3f)) throw new InvalidOperationException("Stun-client failed to move after release: "+status.role+" "+m.releasedTravel);
            if(!host && !(m.maxReleaseJump<.5f)) throw new InvalidOperationException("Stun-client predicted release jump exceeded bound: "+m.maxReleaseJump);
            m.verified=true; WriteOutput(); return q.verified;
        }
    }
}
#endif
