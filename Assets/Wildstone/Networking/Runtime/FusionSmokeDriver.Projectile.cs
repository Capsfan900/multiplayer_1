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
        [Serializable] sealed class ProjectileMetrics
        {
            public bool pickupPlaced,pickupSent,picked,equipSent,equipped;
            public int pickupId,pickupGeneration,pickupSequence,equipSequence;
            public ProjectileVerdict[] shots={new ProjectileVerdict(),new ProjectileVerdict()};
        }
        [Serializable] sealed class ProjectileVerdict
        {
            public bool placed,ready,fireSent,observed,verified,despawned,coverBlocks;
            public int connection,authority,sequence,resends,shooter,life,target,targetLife,ammo,samples,firstTick=-1,lastTick=-1,shotCount,explosions,hits;
            public float baselineHealth,health,damage,eventDamage,firstTravel,lastTravel,expectedDamage,blastDistance;
            public string identity="",shotId="",explosionId="",hitId="";
            public Vector3 explosion,targetEye;
        }
        int projectileIndex;
        GameObject projectileCover;
        double projectileBegan=-1,projectileStable=-1,projectileAck=-1,projectileFired=-1;
        bool DriveProjectileCombat(ArenaMatch model,NetworkRunner runner,Status peer,double now)
        {
            const WeaponKind weapon=WeaponKind.PulseLobber;
            var spec=WeaponCatalog.Get(weapon);
            if(status.projectile==null)
            {
                status.projectile=new ProjectileMetrics();
                foreach(var candidate in FindObjectsByType<FusionMatch>()) if(candidate.Runner==runner && candidate.Initialized) combatMatch=candidate;
                combatAvatar=LaserAvatar(runner,presentation.LocalParticipantId);
                if(combatMatch==null) throw new InvalidOperationException("Missing projectile match.");
            }
            if(peer?.projectile==null) return false;
            var p=status.projectile; var q=peer.projectile;
            if(projectileIndex==2) return true;
            if(projectileBegan<0) projectileBegan=now;
            if(now-projectileBegan>25) throw new TimeoutException("Projectile fixture timed out at "+status.stage);
            var shooter=status.role=="client"?presentation.LocalPlayer:RemotePlayer(model);
            var target=status.role=="host"?presentation.LocalPlayer:RemotePlayer(model);
            if(shooter==null || target==null || !shooter.Alive || !target.Alive) throw new InvalidOperationException("Projectile fixture lost living participants.");
            var host=status.role=="host"?p:q; var client=status.role=="client"?p:q;
            PickupState pickup=null;
            foreach(var item in model.Pickups) if(WeaponCatalog.FromPickup(item.Item)==weapon) { pickup=item; break; }
            if(pickup==null) throw new InvalidOperationException("Missing Pulse Lobber pickup.");
            status.stage="projectile_pickup"; p.pickupId=pickup.Id;
            if(status.role=="host" && !p.pickupPlaced)
            {
                LaserAvatar(runner,shooter.Participant).Teleport(pickup.Position+Vector3.right,0);
                p.pickupGeneration=pickup.Generation; p.pickupPlaced=true; WriteOutput();
            }
            if(!host.pickupPlaced) return false;
            if(status.role=="client" && !p.pickupSent)
            {
                if((shooter.Feet-pickup.Position).sqrMagnitude>4 || pickup.Generation!=host.pickupGeneration) return false;
                p.pickupSequence=shooter.LastActionSequence+1; p.pickupGeneration=pickup.Generation; p.pickupSent=true;
                SendLaserAction(model,shooter,p.pickupSequence,CombatActionKind.Pickup,Vector3.forward,pickup.Id,pickup.Generation);
            }
            p.picked|=client.pickupSent && shooter.SpecialWeapon==weapon && shooter.SpecialAmmo==spec.Ammo && pickup.Generation==host.pickupGeneration+1 && pickup.AvailableTick>model.Tick;
            if(!p.picked || !q.picked) return false;
            if(status.role=="client" && !p.equipSent)
            { p.equipSequence=shooter.LastActionSequence+1; p.equipSent=true; SendLaserAction(model,shooter,p.equipSequence,CombatActionKind.SelectSpecial,Vector3.forward); }
            p.equipped|=client.equipSent && shooter.LastActionSequence>=client.equipSequence && shooter.SelectedSpecial && shooter.SpecialWeapon==weapon;
            if(!p.equipped || !q.equipped) return false;
            var m=p.shots[projectileIndex]; var remote=q.shots[projectileIndex];
            if(m.verified && remote.verified)
            { projectileIndex++; projectileBegan=projectileStable=projectileAck=-1; WriteOutput(); return projectileIndex==2; }
            if(m.life!=0 && (m.shooter!=shooter.Participant || m.life!=shooter.LifeGeneration || m.target!=target.Participant || m.targetLife!=target.LifeGeneration))
                throw new InvalidOperationException("Projectile fixture changed life identity.");
            m.connection=shooter.ConnectionGeneration; m.authority=model.AuthorityGeneration; m.shooter=shooter.Participant; m.life=shooter.LifeGeneration; m.target=target.Participant; m.targetLife=target.LifeGeneration;
            var shooterPoint=new Vector3(-30,.02f,-4); var targetPoint=new Vector3(-30,.02f,-12);
            status.stage="projectile_fixture_"+projectileIndex;
            if(!m.placed)
            {
                if(status.role=="host")
                {
                    // Explicit diagnostic placement/reset only; pickup, firing and all damage use production rules.
                    var state=combatMatch.Model.Find(target.Participant); state.Health=WeaponCatalog.Health; state.Shield=0;
                    LaserAvatar(runner,target.Participant).Teleport(targetPoint,0);
                    LaserAvatar(runner,shooter.Participant).Teleport(shooterPoint,180);
                }
                if(projectileIndex==1)
                {
                    projectileCover=new GameObject("Smoke projectile blast cover"); projectileCover.layer=0;
                    projectileCover.transform.position=new Vector3(-30,1.5f,-10);
                    projectileCover.AddComponent<BoxCollider>().size=new Vector3(4,3,.15f); Physics.SyncTransforms();
                }
                m.placed=true; WriteOutput();
            }
            if(!remote.placed) return false;
            if(!m.ready)
            {
                if((shooter.Feet-shooterPoint).sqrMagnitude>.04f || (target.Feet-targetPoint).sqrMagnitude>.04f || target.Health!=WeaponCatalog.Health) { projectileStable=-1; return false; }
                if(projectileStable<0) projectileStable=now;
                if(now-projectileStable<1) return false;
                bool obstructed=Physics.Raycast(shooter.Eye,(target.Eye-shooter.Eye).normalized,out var obstruction,8,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore);
                if(projectileIndex==0 && obstructed || projectileIndex==1 && (!obstructed || obstruction.collider.gameObject!=projectileCover)) throw new InvalidOperationException("Projectile fixture corridor/cover is invalid.");
                m.baselineHealth=target.Health; m.targetEye=target.Eye; m.ready=true; WriteOutput();
            }
            if(!remote.ready) return false;
            var firing=status.role=="client"?m:remote;
            if(status.role=="client" && !m.fireSent)
            {
                if(model.Tick<shooter.FireReadyTick) return false;
                m.sequence=shooter.LastActionSequence+1; m.fireSent=true; projectileFired=now; WriteOutput();
                SendLaserAction(model,shooter,m.sequence,CombatActionKind.Fire,(target.Eye-shooter.Eye).normalized);
            }
            else if(status.role=="client" && shooter.LastActionSequence<m.sequence && now-projectileFired>1)
            {
                // A loss-resent RPC can reach the host past the 150 ms attack window and is dropped unseen
                // (FusionPlayer.AcceptsCombatTick); press again with the same sequence, which the host dedups.
                m.resends++; projectileFired=now; SendLaserAction(model,shooter,m.sequence,CombatActionKind.Fire,(target.Eye-shooter.Eye).normalized);
            }
            if(!firing.fireSent) return false;
            m.sequence=firing.sequence; m.ammo=shooter.SpecialAmmo; m.health=target.Health; m.damage=m.baselineHealth-m.health;
            bool present=false;
            foreach(var shot in model.Projectiles)
            {
                if(shot.Action.Participant!=m.shooter || shot.Action.LifeGeneration!=m.life || shot.Action.Sequence!=m.sequence) continue;
                if(present || shot.Weapon!=weapon || shot.Action.ConnectionGeneration!=m.connection || shot.Action.AuthorityGeneration!=m.authority) throw new InvalidOperationException("Duplicate/wrong projectile.");
                present=true; string identity=shot.Action.AuthorityGeneration+"/"+shot.Action.ConnectionGeneration+"/"+m.shooter+"/"+m.life+"/"+m.sequence;
                if(m.identity!="" && m.identity!=identity) throw new InvalidOperationException("Projectile identity changed.");
                m.identity=identity;
                if(model.Tick!=m.lastTick)
                { if(m.samples==0) { m.firstTick=model.Tick; m.firstTravel=shot.Travel; } m.samples++; m.lastTick=model.Tick; m.lastTravel=shot.Travel; }
            }
            m.shotCount=CountSmokeShots(m.shooter,m.life,m.sequence,out m.shotId);
            m.hits=CountSmokeHits(m.shooter,m.life,m.sequence,m.target,m.targetLife,out m.hitId,out m.eventDamage);
            m.explosions=0;
            for(int i=0;i<combatMatch.EventRing.Length;i++)
            {
                var e=combatMatch.EventRing[i];
                if(e.Sequence<1 || e.Action.Participant!=m.shooter || e.Action.LifeGeneration!=m.life || e.Action.Sequence!=m.sequence) continue;
                if(e.Action.ConnectionGeneration!=m.connection || e.Action.AuthorityGeneration!=m.authority) throw new InvalidOperationException("Projectile event changed action epoch.");
                if(e.Kind!=CombatEventKind.Explosion) continue;
                m.explosions++; m.explosionId=e.ToCombat().Id; m.explosion=e.Position;
            }
            if(m.explosions==1)
            {
                m.blastDistance=(m.targetEye-m.explosion).magnitude;
                m.expectedDamage=spec.Damage+(spec.EdgeDamage-spec.Damage)*(m.blastDistance/spec.Radius);
                if(projectileIndex==1)
                    m.coverBlocks=Physics.Raycast(m.explosion,(m.targetEye-m.explosion).normalized,out var coverHit,m.blastDistance,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore) && coverHit.collider.gameObject==projectileCover;
            }
            if(m.ammo<spec.Ammo-projectileIndex-1 || m.shotCount>1 || m.explosions>1 || m.hits>1 || shooter.StarterAmmo!=WeaponCatalog.StarterAmmo || shooter.Health!=WeaponCatalog.Health || projectileIndex==1 && (m.damage!=0 || m.hits!=0))
                throw new InvalidOperationException("Projectile duplicated spending/events or changed protected health.");
            m.despawned=m.samples>=2 && !present && m.explosions==1;
            bool damageValid=projectileIndex==0 ? m.hits==1 && m.expectedDamage>spec.EdgeDamage && Mathf.Abs(m.damage-m.expectedDamage)<.01f && Mathf.Abs(m.eventDamage-m.expectedDamage)<.01f :
                m.coverBlocks && m.blastDistance<spec.Radius-.5f && m.expectedDamage>spec.EdgeDamage && m.damage==0 && m.hits==0;
            m.observed=m.despawned && m.lastTick>m.firstTick && m.lastTravel-m.firstTravel>.2f && m.shotCount==1 && m.ammo==spec.Ammo-projectileIndex-1 && shooter.LastActionSequence==m.sequence && damageValid;
            status.stage="projectile_verdict_"+projectileIndex;
            if(!m.observed || !remote.observed) return false;
            if(m.identity!=remote.identity || m.shotId!=remote.shotId || m.explosionId!=remote.explosionId || m.hitId!=remote.hitId || Mathf.Abs(m.damage-remote.damage)>.01f || (m.explosion-remote.explosion).sqrMagnitude>.0001f)
                throw new InvalidOperationException("Peers disagree on projectile terminal verdict.");
            if(projectileAck<0) projectileAck=now;
            m.verified=now-projectileAck>=1; return false;
        }
    }
}
#endif
