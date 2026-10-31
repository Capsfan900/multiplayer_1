#if WILDSTONE_SMOKE
using System;
using Fusion;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Match;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    public sealed partial class FusionSmokeDriver
    {
        // D037: a Pulse Lobber orb reflected by a Push Lens. Round 0 the host pushes; round 1 the client, whose push RPC
        // crosses the attack age window. Pickups, fire, push, flight and damage use production rules.
        [Serializable] sealed class DeflectionRound
        {
            public bool pickupPlaced,pickupSent,picked,placed,ready,fireSent,pushSent,observed,verified,deflectedFlag;
            public int shooter,shooterLife,shooterTeam,pusher,pusherLife,pusherTeam,pickupSequence,fireSequence,pushSequence,pushLeadTicks;
            public int samples,deflectedSamples,launchTick=-1,deflectedLaunchTick=-1,ownerTeam=-1,remaining=-1;
            public int fireShots,pushShots,deflections,explosions,shooterExplosions,hits,pusherHits,shooterAmmo,pusherAmmo;
            public float pushObservedDistance,pushPredictedDistance,resolveDistance,velocityDot,shooterBaselineHealth,shooterHealth,pusherHealth;
            public float damage,eventDamage,expectedDamage,blastDistance;
            public string fireShotId="",pushShotId="",deflectedId="",explosionId="",hitId="";
            public Vector3 shooterEye,pusherEye,originalVelocity,deflectedVelocity,explosion;
        }
        int deflectionIndex,deflectionTick=-1;
        double deflectionBegan=-1,deflectionStable=-1,deflectionAck=-1;
        bool DriveDeflection(ArenaMatch model,NetworkRunner runner,Status peer,double now)
        {
            // Completion already includes both peers; transient status-file reads must not revoke it.
            if(deflectionIndex==2) return true;
            if(status.deflection==null)
            {
                status.deflection=new[]{new DeflectionRound(),new DeflectionRound()};
                foreach(var candidate in FindObjectsByType<FusionMatch>()) if(candidate.Runner==runner && candidate.Initialized) combatMatch=candidate;
                combatAvatar=LaserAvatar(runner,presentation.LocalParticipantId);
                if(combatMatch==null) throw new InvalidOperationException("Missing deflection match.");
            }
            if(peer?.deflection==null || peer.deflection.Length!=2) return false;
            var d=status.deflection[deflectionIndex]; var r=peer.deflection[deflectionIndex];
            if(d.verified && r.verified)
            { deflectionIndex++; deflectionBegan=deflectionStable=deflectionAck=-1; deflectionTick=-1; WriteOutput(); return deflectionIndex==2; }
            // The peer may already be claiming round-1 pickups; keep the published verdict frozen.
            if(d.verified) return false;
            if(deflectionBegan<0) deflectionBegan=now;
            // Round 1 also waits out the 15 s respawn of both pickups claimed in round 0.
            if(now-deflectionBegan>40) throw new TimeoutException("Deflection fixture timed out at "+status.stage);
            bool localPushes=(status.role=="host")==(deflectionIndex==0);
            var local=presentation.LocalPlayer; var remote=RemotePlayer(model);
            var pusher=localPushes?local:remote; var shooter=localPushes?remote:local;
            if(shooter==null || pusher==null || !shooter.Alive || !pusher.Alive) throw new InvalidOperationException("Deflection fixture lost living participants.");
            if(d.shooterLife!=0 && (d.shooter!=shooter.Participant || d.shooterLife!=shooter.LifeGeneration || d.pusher!=pusher.Participant || d.pusherLife!=pusher.LifeGeneration))
                throw new InvalidOperationException("Deflection fixture changed life identity.");
            d.shooter=shooter.Participant; d.shooterLife=shooter.LifeGeneration; d.shooterTeam=shooter.Team;
            d.pusher=pusher.Participant; d.pusherLife=pusher.LifeGeneration; d.pusherTeam=pusher.Team;
            var host=status.role=="host"?d:r; var shooterRecord=localPushes?r:d; var pusherRecord=localPushes?d:r;
            var lobber=WeaponCatalog.Get(WeaponKind.PulseLobber); var lens=WeaponCatalog.Get(WeaponKind.PushLens);
            var lobberPickup=DeflectionPickup(model,WeaponKind.PulseLobber); var lensPickup=DeflectionPickup(model,WeaponKind.PushLens);
            status.stage="deflection_pickup_"+deflectionIndex;
            if(status.role=="host" && !d.pickupPlaced)
            {
                LaserAvatar(runner,shooter.Participant).Teleport(lobberPickup.Position+Vector3.right,0);
                LaserAvatar(runner,pusher.Participant).Teleport(lensPickup.Position+Vector3.right,0);
                d.pickupPlaced=true; WriteOutput();
            }
            if(!host.pickupPlaced) return false;
            // Each peer claims its own weapon through the production pickup RPC; the claim auto-selects the special.
            var own=localPushes?lensPickup:lobberPickup;
            if(!d.pickupSent)
            {
                if((local.Feet-own.Position).sqrMagnitude>4 || model.Tick<own.AvailableTick) return false;
                d.pickupSequence=local.LastActionSequence+1; d.pickupSent=true;
                SendLaserAction(model,local,d.pickupSequence,CombatActionKind.Pickup,Vector3.forward,own.Id,own.Generation);
            }
            d.picked|=r.pickupSent && shooter.SpecialWeapon==WeaponKind.PulseLobber && shooter.SpecialAmmo==lobber.Ammo && shooter.SelectedSpecial &&
                pusher.SpecialWeapon==WeaponKind.PushLens && pusher.SpecialAmmo==lens.Ammo && pusher.SelectedSpecial;
            if(!d.picked || !r.picked) return false;
            var shooterPoint=new Vector3(-30,.02f,-3); var pusherPoint=new Vector3(-30,.02f,-15);
            status.stage="deflection_fixture_"+deflectionIndex;
            if(!d.placed)
            {
                if(status.role=="host")
                {
                    // Explicit diagnostic placement/reset only.
                    foreach(var id in new[]{shooter.Participant,pusher.Participant}) { var state=combatMatch.Model.Find(id); state.Health=WeaponCatalog.Health; state.Shield=0; }
                    LaserAvatar(runner,shooter.Participant).Teleport(shooterPoint,180);
                    LaserAvatar(runner,pusher.Participant).Teleport(pusherPoint,0);
                }
                d.placed=true; WriteOutput();
            }
            if(!r.placed) return false;
            if(!d.ready)
            {
                if((shooter.Feet-shooterPoint).sqrMagnitude>.04f || (pusher.Feet-pusherPoint).sqrMagnitude>.04f || shooter.Health!=WeaponCatalog.Health ||
                    pusher.Health!=WeaponCatalog.Health || model.Tick<shooter.FireReadyTick || model.Tick<pusher.FireReadyTick) { deflectionStable=-1; return false; }
                if(deflectionStable<0) deflectionStable=now;
                if(now-deflectionStable<1) return false;
                if(Physics.Raycast(shooter.Eye,(pusher.Eye-shooter.Eye).normalized,(pusher.Eye-shooter.Eye).magnitude,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Deflection corridor is occluded.");
                d.shooterEye=shooter.Eye; d.pusherEye=pusher.Eye; d.shooterBaselineHealth=shooter.Health; d.ready=true; WriteOutput();
            }
            if(!r.ready) return false;
            status.stage="deflection_fire_"+deflectionIndex;
            if(!localPushes && !d.fireSent)
            {
                d.fireSequence=shooter.LastActionSequence+1; d.fireSent=true; WriteOutput();
                SendLaserAction(model,shooter,d.fireSequence,CombatActionKind.Fire,(d.pusherEye-d.shooterEye).normalized);
            }
            if(!shooterRecord.fireSent) return false;
            d.fireSequence=shooterRecord.fireSequence;
            bool original=false,deflected=false; int remaining=0; Vector3 orb=default;
            foreach(var shot in model.Projectiles)
            {
                remaining++; var a=shot.Action;
                if(a.Participant==d.shooter && a.LifeGeneration==d.shooterLife && a.Sequence==d.fireSequence)
                {
                    if(original || shot.Weapon!=WeaponKind.PulseLobber || shot.Deflected || shot.OwnerTeam!=d.shooterTeam) throw new InvalidOperationException("Duplicate/wrong original orb.");
                    original=true; orb=shot.Position; d.launchTick=a.Tick; d.originalVelocity=shot.Velocity;
                    if(model.Tick!=deflectionTick) d.samples++;
                }
                else if(pusherRecord.pushSent && a.Participant==d.pusher && a.LifeGeneration==d.pusherLife && a.Sequence==pusherRecord.pushSequence)
                {
                    d.deflectedFlag=shot.Deflected; d.ownerTeam=shot.OwnerTeam; d.deflectedLaunchTick=a.Tick; d.deflectedVelocity=shot.Velocity;
                    if(deflected || shot.Weapon!=WeaponKind.PulseLobber || !shot.Deflected || shot.OwnerTeam!=d.pusherTeam || d.launchTick>=0 && a.Tick!=d.launchTick)
                        throw new InvalidOperationException("Deflected orb has the wrong identity, owner or launch tick.");
                    deflected=true; if(model.Tick!=deflectionTick) d.deflectedSamples++;
                }
            }
            if(original && deflected) throw new InvalidOperationException("Original and deflected orbs coexist.");
            deflectionTick=model.Tick; d.remaining=remaining;
            status.stage="deflection_push_"+deflectionIndex;
            if(localPushes && !d.pushSent)
            {
                if(!original) { if(d.samples>0) throw new InvalidOperationException("Orb ended before the push was sent."); return false; }
                // The orb flies straight at constant speed, so predict its distance on the host's resolve tick from the stamp
                // this request will carry (NextActionTick): replication and RPC delay are measured, not assumed.
                int lead=combatMatch.NextActionTick+WeaponCatalog.Ticks(lens.Windup,model.TickRate)-1-model.Tick;
                float distance=(orb-d.pusherEye).magnitude, predicted=distance-lobber.Speed*lead/model.TickRate;
                if(predicted>4.5f) return false;
                if(predicted<1.5f) throw new InvalidOperationException("Deflection window missed: predicted "+predicted+" m at lead "+lead);
                d.pushLeadTicks=lead; d.pushObservedDistance=distance; d.pushPredictedDistance=predicted;
                d.pushSequence=pusher.LastActionSequence+1; d.pushSent=true; WriteOutput();
                SendLaserAction(model,pusher,d.pushSequence,CombatActionKind.Fire,(d.shooterEye-d.pusherEye).normalized);
            }
            if(!pusherRecord.pushSent) return false;
            d.pushSequence=pusherRecord.pushSequence;
            d.fireShots=CountSmokeShots(d.shooter,d.shooterLife,d.fireSequence,out d.fireShotId);
            d.pushShots=CountSmokeShots(d.pusher,d.pusherLife,d.pushSequence,out d.pushShotId);
            d.deflections=d.explosions=d.shooterExplosions=d.hits=d.pusherHits=0;
            for(int i=0;i<combatMatch.EventRing.Length;i++)
            {
                var e=combatMatch.EventRing[i];
                if(e.Sequence<1) continue;
                bool fire=e.Action.Participant==d.shooter && e.Action.LifeGeneration==d.shooterLife && e.Action.Sequence==d.fireSequence;
                bool push=e.Action.Participant==d.pusher && e.Action.LifeGeneration==d.pusherLife && e.Action.Sequence==d.pushSequence;
                if(!fire && !push) continue;
                if(e.Kind==CombatEventKind.Hit && e.Target==d.pusher) d.pusherHits++;
                else if(e.Kind==CombatEventKind.Explosion)
                { if(fire) d.shooterExplosions++; else { d.explosions++; d.explosionId=e.ToCombat().Id; d.explosion=e.Position; } }
                else if(push && e.Kind==CombatEventKind.Deflected)
                {
                    if(e.Target!=d.shooter || e.TargetLife!=d.shooterLife || e.Weapon!=WeaponKind.PulseLobber) throw new InvalidOperationException("Deflected event names the wrong shooter or weapon.");
                    d.deflections++; d.deflectedId=e.ToCombat().Id; d.resolveDistance=(e.Position-d.pusherEye).magnitude;
                }
                else if(push && e.Kind==CombatEventKind.Hit)
                {
                    if(e.Target!=d.shooter || e.TargetLife!=d.shooterLife) throw new InvalidOperationException("Deflected hit changed target life.");
                    d.hits++; d.hitId=e.ToCombat().Id; d.eventDamage=e.Amount;
                }
            }
            d.shooterHealth=shooter.Health; d.pusherHealth=pusher.Health; d.damage=d.shooterBaselineHealth-shooter.Health;
            d.shooterAmmo=shooter.SpecialAmmo; d.pusherAmmo=pusher.SpecialAmmo;
            if(d.explosions==1)
            { d.blastDistance=(d.shooterEye-d.explosion).magnitude; d.expectedDamage=lobber.Damage+(lobber.EdgeDamage-lobber.Damage)*(d.blastDistance/lobber.Radius); }
            if(d.samples>0 && d.deflectedSamples>0) d.velocityDot=Vector3.Dot(d.originalVelocity.normalized,d.deflectedVelocity.normalized);
            if(d.fireShots>1 || d.pushShots>1 || d.deflections>1 || d.explosions>1 || d.hits>1 || d.shooterExplosions>0 || d.pusherHits>0 ||
                d.pusherHealth!=WeaponCatalog.Health || d.shooterAmmo<lobber.Ammo-1 || d.pusherAmmo<lens.Ammo-1 ||
                shooter.StarterAmmo!=WeaponCatalog.StarterAmmo || pusher.StarterAmmo!=WeaponCatalog.StarterAmmo)
                throw new InvalidOperationException("Deflection duplicated events/spending, credited the shooter or damaged the pusher.");
            d.observed=d.samples>0 && d.deflectedSamples>0 && d.velocityDot<0 && d.deflectedFlag && d.ownerTeam==d.pusherTeam && d.deflectedLaunchTick==d.launchTick &&
                d.fireShots==1 && d.pushShots==1 && d.deflections==1 && d.explosions==1 && d.hits==1 && d.remaining==0 &&
                d.expectedDamage>lobber.EdgeDamage && Mathf.Abs(d.damage-d.expectedDamage)<.01f && Mathf.Abs(d.eventDamage-d.expectedDamage)<.01f &&
                d.shooterAmmo==lobber.Ammo-1 && d.pusherAmmo==lens.Ammo-1 && shooter.LastActionSequence==d.fireSequence && pusher.LastActionSequence==d.pushSequence;
            status.stage="deflection_verdict_"+deflectionIndex;
            if(!d.observed || !r.observed) return false;
            if(d.fireShotId!=r.fireShotId || d.pushShotId!=r.pushShotId || d.deflectedId!=r.deflectedId || d.explosionId!=r.explosionId || d.hitId!=r.hitId ||
                d.launchTick!=r.launchTick || Mathf.Abs(d.damage-r.damage)>.01f || (d.explosion-r.explosion).sqrMagnitude>.0001f)
                throw new InvalidOperationException("Peers disagree on the deflection verdict.");
            if(deflectionAck<0) deflectionAck=now;
            d.verified=now-deflectionAck>=1; return false;
        }
        static PickupState DeflectionPickup(ArenaMatch model,WeaponKind weapon)
        {
            foreach(var item in model.Pickups) if(WeaponCatalog.FromPickup(item.Item)==weapon) return item;
            throw new InvalidOperationException("Missing pickup: "+weapon);
        }
    }
}
#endif
