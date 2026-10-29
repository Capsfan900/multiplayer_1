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
        [Serializable] sealed class SpecialProjectileMetrics
        {
            public bool pickupPlaced,pickupSent,picked,equipSent,equipped,placed,ready,verified;
            public int pickupGeneration,pickupSequence,equipSequence,shooter,life,target,targetLife;
            public float baselineHealth;
            public string weapon="";
            public SpecialProjectileShot[] shots;
        }
        [Serializable] sealed class SpecialProjectileShot
        {
            public bool sent,observed,verified,absent,stunExpired,suppressionObserved;
            public int sequence,connection,authority,ammo,samples,lastTick=-1,firstTick=-1,shots,hits,ricochets,stuns,stunUntil,immunityUntil,maxBounces,ricochetMask,stunTick,expireTick,firstDespawnTick=-1,directionReversals,lastZSign,directionMask;
            public float firstTravel,lastTravel,damage,eventDamage,firstRicochetZ,secondRicochetZ;
            public string identity="",events="";
        }
        int specialWeaponIndex,specialShotIndex;
        double specialBegan=-1,specialStable=-1,specialAck=-1;
        GameObject specialWallA,specialWallB;
        static readonly WeaponKind[] SpecialSmokeWeapons={WeaponKind.ReboundDisc,WeaponKind.StunGun};
        bool DriveSpecialProjectiles(ArenaMatch model,NetworkRunner runner,Status peer,double now)
        {
            // Completion already includes both peers; transient status-file reads must not revoke it.
            if(specialWeaponIndex==SpecialSmokeWeapons.Length) return true;
            if(status.specialProjectiles==null)
            {
                status.specialProjectiles=new SpecialProjectileMetrics[2];
                for(int i=0;i<2;i++)
                {
                    var entry=new SpecialProjectileMetrics {weapon=SpecialSmokeWeapons[i].ToString(),shots=new SpecialProjectileShot[i==0?1:3]};
                    for(int j=0;j<entry.shots.Length;j++) entry.shots[j]=new SpecialProjectileShot();
                    status.specialProjectiles[i]=entry;
                }
                foreach(var candidate in FindObjectsByType<FusionMatch>()) if(candidate.Runner==runner && candidate.Initialized) combatMatch=candidate;
                combatAvatar=LaserAvatar(runner,presentation.LocalParticipantId);
                if(combatMatch==null) throw new InvalidOperationException("Missing special projectile match.");
            }
            if(peer?.specialProjectiles==null || peer.specialProjectiles.Length==0) return false;
            if(peer.specialProjectiles.Length!=2) throw new InvalidOperationException("Malformed special projectile peer metrics.");
            for(int i=0;i<2;i++)
                if(peer.specialProjectiles[i]==null || peer.specialProjectiles[i].shots==null || peer.specialProjectiles[i].shots.Length!=(i==0?1:3))
                    throw new InvalidOperationException("Malformed special projectile peer shots.");
            if(specialBegan<0) specialBegan=now;
            if(now-specialBegan>35) throw new TimeoutException("Special projectile fixture timed out: "+status.stage);
            var m=status.specialProjectiles[specialWeaponIndex]; var q=peer.specialProjectiles[specialWeaponIndex];
            if(m.verified && q.verified)
            {
                StopSpecialProjectiles(); specialWeaponIndex++; specialShotIndex=0; specialBegan=specialStable=specialAck=-1;
                WriteOutput(); return specialWeaponIndex==2;
            }
            if(m.verified) return false;
            var weapon=SpecialSmokeWeapons[specialWeaponIndex]; var spec=WeaponCatalog.Get(weapon);
            var shooter=status.role=="client"?presentation.LocalPlayer:RemotePlayer(model);
            var target=status.role=="host"?presentation.LocalPlayer:RemotePlayer(model);
            if(shooter==null || target==null || !shooter.Alive || !target.Alive) throw new InvalidOperationException("Special fixture lost living participants.");
            if(m.life!=0 && (m.shooter!=shooter.Participant || m.life!=shooter.LifeGeneration || m.target!=target.Participant || m.targetLife!=target.LifeGeneration))
                throw new InvalidOperationException("Special fixture changed life identity.");
            m.shooter=shooter.Participant; m.life=shooter.LifeGeneration; m.target=target.Participant; m.targetLife=target.LifeGeneration;
            var host=status.role=="host"?m:q; var client=status.role=="client"?m:q;
            PickupState pickup=null;
            foreach(var item in model.Pickups) if(WeaponCatalog.FromPickup(item.Item)==weapon) { pickup=item; break; }
            if(pickup==null) throw new InvalidOperationException("Missing special pickup: "+weapon);
            status.stage="special_pickup_"+weapon;
            if(status.role=="host" && !m.pickupPlaced)
            {
                LaserAvatar(runner,shooter.Participant).Teleport(pickup.Position+Vector3.right,0);
                m.pickupGeneration=pickup.Generation; m.pickupPlaced=true; WriteOutput();
            }
            if(!host.pickupPlaced) return false;
            if(status.role=="client" && !m.pickupSent)
            {
                if((shooter.Feet-pickup.Position).sqrMagnitude>4 || pickup.Generation!=host.pickupGeneration) return false;
                m.pickupSequence=shooter.LastActionSequence+1; m.pickupSent=true;
                SendLaserAction(model,shooter,m.pickupSequence,CombatActionKind.Pickup,Vector3.forward,pickup.Id,pickup.Generation);
            }
            m.picked|=client.pickupSent && shooter.SpecialWeapon==weapon && shooter.SpecialAmmo==spec.Ammo && pickup.Generation==host.pickupGeneration+1;
            if(!m.picked || !q.picked) return false;
            if(status.role=="client" && !m.equipSent)
            { m.equipSequence=shooter.LastActionSequence+1; m.equipSent=true; SendLaserAction(model,shooter,m.equipSequence,CombatActionKind.SelectSpecial,Vector3.forward); }
            m.equipped|=client.equipSent && shooter.LastActionSequence>=client.equipSequence && shooter.SelectedSpecial && shooter.SpecialWeapon==weapon;
            if(!m.equipped || !q.equipped) return false;
            // The stun bolt needs enough flight time for multiple replicated snapshots under loss.
            var shooterPoint=new Vector3(-30,.02f,specialWeaponIndex==0?-6:-3);
            var targetPoint=new Vector3(-30,.02f,specialWeaponIndex==0?-12:-15);
            if(!m.placed)
            {
                // Only diagnostic geometry and placement: inventory, flight, health and statuses use production rules.
                if(status.role=="host")
                {
                    LaserAvatar(runner,shooter.Participant).Teleport(shooterPoint,180);
                    LaserAvatar(runner,target.Participant).Teleport(targetPoint,0);
                }
                if(specialWeaponIndex==0)
                {
                    specialWallA=SpecialWall("Smoke disc far wall",-16);
                    specialWallB=SpecialWall("Smoke disc near wall",-2);
                    Physics.SyncTransforms();
                }
                m.placed=true; WriteOutput();
            }
            if(!q.placed) return false;
            status.stage="special_ready_"+weapon;
            if(!m.ready)
            {
                if((shooter.Feet-shooterPoint).sqrMagnitude>.04f || (target.Feet-targetPoint).sqrMagnitude>.04f) { specialStable=-1; return false; }
                if(specialStable<0) specialStable=now;
                if(now-specialStable<1) return false;
                m.baselineHealth=target.Health; m.ready=true; WriteOutput();
            }
            if(!q.ready) return false;
            var s=m.shots[specialShotIndex]; var r=q.shots[specialShotIndex];
            if(s.verified && r.verified)
            {
                specialShotIndex++; specialAck=-1;
                if(specialShotIndex==m.shots.Length) { m.verified=true; WriteOutput(); return false; }
                return false;
            }
            var firing=status.role=="client"?s:r;
            status.stage="special_fire_"+weapon+"_"+specialShotIndex;
            if(status.role=="client" && !s.sent)
            {
                if(model.Tick<shooter.FireReadyTick) return false;
                if(specialWeaponIndex==1 && specialShotIndex==2 && model.Tick<=m.shots[0].immunityUntil+2) return false;
                if(specialWeaponIndex==1 && specialShotIndex==1 && model.Tick>=m.shots[0].immunityUntil-10)
                    throw new InvalidOperationException("Missed legitimate immunity fire window.");
                s.sequence=shooter.LastActionSequence+1; s.sent=true;
                SendLaserAction(model,shooter,s.sequence,CombatActionKind.Fire,(target.Eye-shooter.Eye).normalized);
                SendLaserAction(model,shooter,s.sequence,CombatActionKind.Fire,(target.Eye-shooter.Eye).normalized);
            }
            if(!firing.sent) return false;
            s.sequence=firing.sequence; s.connection=shooter.ConnectionGeneration; s.authority=model.AuthorityGeneration;
            string identity=s.authority+"/"+s.connection+"/"+m.shooter+"/"+m.life+"/"+s.sequence;
            if(s.identity!="" && s.identity!=identity) throw new InvalidOperationException("Special action epoch changed.");
            s.identity=identity;
            bool present=false;
            foreach(var shot in model.Projectiles)
            {
                if(shot.Action.Participant!=m.shooter || shot.Action.LifeGeneration!=m.life || shot.Action.Sequence!=s.sequence) continue;
                if(present || shot.Weapon!=weapon || shot.Action.ConnectionGeneration!=s.connection || shot.Action.AuthorityGeneration!=s.authority) throw new InvalidOperationException("Special projectile identity duplicated/changed.");
                present=true;
                if(model.Tick!=s.lastTick)
                { if(s.samples==0) { s.firstTick=model.Tick; s.firstTravel=shot.Travel; } s.samples++; s.lastTick=model.Tick; s.lastTravel=shot.Travel; }
                if(s.expireTick!=0 && s.expireTick!=shot.ExpireTick) throw new InvalidOperationException("Special projectile expiry changed.");
                s.expireTick=shot.ExpireTick;
                int sign=shot.Velocity.z>1?1:shot.Velocity.z< -1?-1:0;
                if(sign!=0)
                {
                    if(s.lastZSign!=0 && sign!=s.lastZSign) s.directionReversals++;
                    s.lastZSign=sign;
                    if(shot.Bounces==0 && sign<0 || shot.Bounces==1 && sign>0 || shot.Bounces==2 && sign<0) s.directionMask|=1<<shot.Bounces;
                }
                s.maxBounces=Math.Max(s.maxBounces,shot.Bounces);
            }
            s.shots=s.hits=s.stuns=s.ricochets=s.ricochetMask=0; s.eventDamage=0; s.events="";
            for(int i=0;i<combatMatch.EventRing.Length;i++)
            {
                var e=combatMatch.EventRing[i];
                if(e.Sequence<1 || e.Action.Participant!=m.shooter || e.Action.LifeGeneration!=m.life || e.Action.Sequence!=s.sequence) continue;
                if(e.Action.ConnectionGeneration!=s.connection || e.Action.AuthorityGeneration!=s.authority) throw new InvalidOperationException("Special event epoch changed.");
                if(e.Kind==CombatEventKind.Shot)
                {
                    if(e.Weapon!=weapon) throw new InvalidOperationException("Special shot weapon identity changed.");
                    s.shots++;
                }
                                else if(e.Kind==CombatEventKind.Ricochet)
                {
                    s.ricochets++; s.ricochetMask|=1<<e.SubIndex;
                    if(e.SubIndex==1) s.firstRicochetZ=e.Position.z;
                    if(e.SubIndex==2) s.secondRicochetZ=e.Position.z;
                }
                else if(e.Kind==CombatEventKind.Hit || e.Kind==CombatEventKind.Stunned)
                {
                    if(e.Target!=m.target || e.TargetLife!=m.targetLife) throw new InvalidOperationException("Special target life changed.");
                    if(e.Kind==CombatEventKind.Hit) { s.hits++; s.eventDamage+=e.Amount; } else { s.stuns++; s.stunTick=e.Tick; }
                }
                else continue;
                s.events+=e.ToCombat().Id+";";
            }
            s.ammo=shooter.SpecialAmmo; s.damage=m.baselineHealth-target.Health;
            if(s.shots>1 || s.hits>1 || s.ricochets>2 || s.stuns>1 || s.ammo<spec.Ammo-specialShotIndex-1 || shooter.StarterAmmo!=WeaponCatalog.StarterAmmo)
                throw new InvalidOperationException("Special projectile duplicated spending/events.");
            // Event-ring and avatar snapshots can arrive separately. Keep sampling until the exact event/timer contract agrees.
            if(s.hits==1 && specialWeaponIndex==1)
            { s.stunUntil=target.StunUntil; s.immunityUntil=target.ImmunityUntil; }
            bool statusValid=true;
            if(specialWeaponIndex==1 && s.hits==1)
            {
                s.suppressionObserved|=model.Tick<target.StunUntil && target.Suppressed(model.Tick);
                if(specialShotIndex==1)
                {
                    var first=m.shots[0];
                    statusValid=s.stuns==0 && s.stunUntil==first.stunUntil && s.immunityUntil==first.immunityUntil;
                    if(!statusValid) throw new InvalidOperationException("Immune stun hit refreshed control loss.");
                }
                else statusValid=s.stuns==1 && s.stunUntil==s.stunTick+WeaponCatalog.Ticks(.45f,model.TickRate) && s.immunityUntil==s.stunUntil+WeaponCatalog.Ticks(1.75f,model.TickRate);
                s.stunExpired=model.Tick>=s.stunUntil && !target.Suppressed(model.Tick);
                statusValid&=s.stunExpired && (specialShotIndex==1 || (status.role=="host"?s:r).suppressionObserved);
            }
            s.absent=s.samples>=2 && !present;
            if(s.absent && s.firstDespawnTick<0) s.firstDespawnTick=model.Tick;
            // Early disappearance plus bounded travel excludes lifetime/range expiry as the terminal cause.
            // The event positions provide an independent path witness if a snapshot misses a reversal.
            bool discPath=s.directionReversals>=2 && s.directionMask==7 ||
                Mathf.Abs(s.firstRicochetZ+15.8f)<.2f && Mathf.Abs(s.secondRicochetZ+2.2f)<.2f && s.lastTravel>30;
            bool discTerminal=s.firstDespawnTick>s.lastTick && s.firstDespawnTick<s.expireTick &&
                s.lastTravel+spec.Speed*(s.firstDespawnTick-s.lastTick)/(float)model.TickRate<spec.Range-1;
            s.observed=s.absent && s.lastTick>s.firstTick && s.lastTravel>s.firstTravel+.2f && s.shots==1 && s.hits==1 &&
                s.ammo==spec.Ammo-specialShotIndex-1 && Mathf.Abs(s.eventDamage-spec.Damage)<.01f &&
                Mathf.Abs(s.damage-spec.Damage*(specialShotIndex+1))<.01f && statusValid &&
                (specialWeaponIndex!=0 || s.ricochets==2 && s.ricochetMask==6 && s.maxBounces==2 && discPath && discTerminal);
            status.stage="special_verdict_"+weapon+"_"+specialShotIndex;
            if(!s.observed || !r.observed) return false;
            if(s.identity!=r.identity || s.events!=r.events || s.ammo!=r.ammo || Mathf.Abs(s.damage-r.damage)>.01f || s.stunUntil!=r.stunUntil || s.immunityUntil!=r.immunityUntil)
                throw new InvalidOperationException("Peers disagree on special projectile verdict.");
            if(specialAck<0) specialAck=now;
            s.verified=now-specialAck>=.1; return false;
        }
        GameObject SpecialWall(string label,float z)
        {
            var wall=new GameObject(label); wall.layer=0; wall.transform.position=new Vector3(-30,2,z);
            wall.AddComponent<BoxCollider>().size=new Vector3(4,4,.15f); return wall;
        }
        void StopSpecialProjectiles()
        {
            if(specialWallA!=null) Destroy(specialWallA);
            if(specialWallB!=null) Destroy(specialWallB);
            specialWallA=specialWallB=null;
        }
    }
}
#endif
