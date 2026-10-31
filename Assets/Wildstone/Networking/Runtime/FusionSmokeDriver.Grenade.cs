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
        [Serializable] sealed class GrenadeMetrics
        {
            public bool pickupPlaced,pickupSent,picked;
            public int pickupId,pickupGeneration,pickupSequence;
            public string fixture="Synthetic elevated floor/backstop inside Atrium bounds; lateral cover on second throw";
            public GrenadeVerdict[] throws={new GrenadeVerdict(),new GrenadeVerdict()};
        }
        [Serializable] sealed class GrenadeVerdict
        {
            public bool placed,ready,fireSent,gravityObserved,bounceObserved,despawned,coverBlocks,observed,verified;
            public int connection,authority,sequence,resends,shooter,life,target,targetLife,ammo,samples,firstTick=-1,lastTick=-1,actionTick=-1,expireTick=-1,explosionTick=-1,shotCount,explosions,hits;
            public float baselineHealth,health,damage,eventDamage,firstTravel,lastTravel,expectedDamage,blastDistance;
            public string identity="",shotId="",explosionId="",hitId="";
            public Vector3 explosion,targetEye,previousVelocity;
        }
        int grenadeIndex;
        GameObject grenadeFloor,grenadeBackstop,grenadeCover;
        double grenadeBegan=-1,grenadeStable=-1,grenadeAck=-1,grenadeThrown=-1;
        bool DriveGrenadeCombat(ArenaMatch model,NetworkRunner runner,Status peer,double now)
        {
            // Completion already includes both peers; transient status-file reads must not revoke it.
            if(grenadeIndex==2) return true;
            var spec=WeaponCatalog.Get(WeaponKind.Grenade);
            if(status.grenade==null)
            {
                status.grenade=new GrenadeMetrics();
                foreach(var candidate in FindObjectsByType<FusionMatch>()) if(candidate.Runner==runner && candidate.Initialized) combatMatch=candidate;
                combatAvatar=LaserAvatar(runner,presentation.LocalParticipantId);
                if(combatMatch==null) throw new InvalidOperationException("Missing grenade match.");
            }
            if(peer?.grenade==null) return false;
            var p=status.grenade; var q=peer.grenade;
            if(grenadeBegan<0) grenadeBegan=now;
            if(now-grenadeBegan>25) throw new TimeoutException("Grenade fixture timed out at "+status.stage);
            var shooter=status.role=="client"?presentation.LocalPlayer:RemotePlayer(model);
            var target=status.role=="host"?presentation.LocalPlayer:RemotePlayer(model);
            if(shooter==null || target==null || !shooter.Alive || !target.Alive) throw new InvalidOperationException("Grenade fixture lost living participants.");
            var host=status.role=="host"?p:q; var client=status.role=="client"?p:q;
            PickupState pickup=null;
            foreach(var item in model.Pickups) if(WeaponCatalog.FromPickup(item.Item)==WeaponKind.Grenade) { pickup=item; break; }
            if(pickup==null) throw new InvalidOperationException("Missing grenade pickup.");
            status.stage="grenade_pickup"; p.pickupId=pickup.Id;
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
            p.picked|=client.pickupSent && shooter.GrenadeAmmo==spec.Ammo && pickup.Generation==host.pickupGeneration+1 && pickup.AvailableTick>model.Tick;
            if(!p.picked || !q.picked) return false;
            var m=p.throws[grenadeIndex]; var remote=q.throws[grenadeIndex];
            if(m.verified && remote.verified)
            { grenadeIndex++; grenadeBegan=grenadeStable=grenadeAck=-1; WriteOutput(); return grenadeIndex==2; }
            if(m.life!=0 && (m.shooter!=shooter.Participant || m.life!=shooter.LifeGeneration || m.target!=target.Participant || m.targetLife!=target.LifeGeneration))
                throw new InvalidOperationException("Grenade fixture changed life identity.");
            m.connection=shooter.ConnectionGeneration; m.authority=model.AuthorityGeneration; m.shooter=shooter.Participant; m.life=shooter.LifeGeneration; m.target=target.Participant; m.targetLife=target.LifeGeneration;
            var shooterPoint=new Vector3(-30,12.02f,-4); var targetPoint=new Vector3(-27,12.02f,-18);
            status.stage="grenade_fixture_"+grenadeIndex;
            if(!m.placed)
            {
                // Both peers get identical diagnostic colliders. Only prethrow avatar placement/health is synthetic.
                if(grenadeFloor==null)
                {
                    grenadeFloor=GrenadeBox("Smoke grenade elevated floor",new Vector3(-27,11.75f,-12),new Vector3(10,.5f,30));
                    grenadeBackstop=GrenadeBox("Smoke grenade backstop",new Vector3(-30,15,-22),new Vector3(2,6,.2f));
                }
                if(grenadeIndex==1) grenadeCover=GrenadeBox("Smoke grenade lateral blast cover",new Vector3(-28.5f,14,-18),new Vector3(.15f,4,8));
                Physics.SyncTransforms();
                if(status.role=="host")
                {
                    var state=combatMatch.Model.Find(target.Participant); state.Health=WeaponCatalog.Health; state.Shield=0;
                    LaserAvatar(runner,target.Participant).Teleport(targetPoint,0);
                    LaserAvatar(runner,shooter.Participant).Teleport(shooterPoint,180);
                }
                m.placed=true; WriteOutput();
            }
            if(!remote.placed) return false;
            if(!m.ready)
            {
                if((shooter.Feet-shooterPoint).sqrMagnitude>.04f || (target.Feet-targetPoint).sqrMagnitude>.04f || target.Health!=WeaponCatalog.Health) { grenadeStable=-1; return false; }
                if(grenadeStable<0) grenadeStable=now;
                if(now-grenadeStable<1) return false;
                if(!Physics.Raycast(shooter.Eye,Vector3.back,out var obstacle,19,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore) || obstacle.collider.gameObject!=grenadeBackstop)
                    throw new InvalidOperationException("Grenade flight corridor does not reach the diagnostic backstop.");
                m.baselineHealth=target.Health; m.targetEye=target.Eye; m.ready=true; WriteOutput();
            }
            if(!remote.ready) return false;
            var firing=status.role=="client"?m:remote;
            if(status.role=="client" && !m.fireSent)
            {
                if(model.Tick<shooter.GrenadeReadyTick) return false;
                m.sequence=shooter.LastActionSequence+1; m.fireSent=true; grenadeThrown=now; WriteOutput();
                SendLaserAction(model,shooter,m.sequence,CombatActionKind.Grenade,Vector3.back);
            }
            else if(status.role=="client" && shooter.LastActionSequence<m.sequence && now-grenadeThrown>1)
            {
                // A loss-resent RPC can reach the host past the 150 ms attack window and is dropped unseen
                // (FusionPlayer.AcceptsCombatTick); press again with the same sequence, which the host dedups.
                m.resends++; grenadeThrown=now; SendLaserAction(model,shooter,m.sequence,CombatActionKind.Grenade,Vector3.back);
            }
            if(!firing.fireSent) return false;
            m.sequence=firing.sequence; m.ammo=shooter.GrenadeAmmo; m.health=target.Health; m.damage=m.baselineHealth-m.health;
            bool present=false;
            foreach(var shot in model.Projectiles)
            {
                if(shot.Action.Participant!=m.shooter || shot.Action.LifeGeneration!=m.life || shot.Action.Sequence!=m.sequence) continue;
                if(present || shot.Weapon!=WeaponKind.Grenade || shot.Action.ConnectionGeneration!=m.connection || shot.Action.AuthorityGeneration!=m.authority) throw new InvalidOperationException("Duplicate/wrong grenade.");
                present=true; m.identity=shot.Action.AuthorityGeneration+"/"+shot.Action.ConnectionGeneration+"/"+m.shooter+"/"+m.life+"/"+m.sequence;
                if(m.expireTick!=-1 && (m.expireTick!=shot.ExpireTick || m.actionTick!=shot.Action.Tick)) throw new InvalidOperationException("Grenade fuse changed.");
                m.actionTick=shot.Action.Tick; m.expireTick=shot.ExpireTick;
                if(model.Tick!=m.lastTick)
                {
                    if(m.samples==0) { m.firstTick=model.Tick; m.firstTravel=shot.Travel; }
                    else
                    {
                        m.gravityObserved|=shot.Velocity.y<m.previousVelocity.y-.01f;
                        m.bounceObserved|=m.previousVelocity.z< -1 && shot.Velocity.z>1;
                    }
                    m.previousVelocity=shot.Velocity; m.samples++; m.lastTick=model.Tick; m.lastTravel=shot.Travel;
                }
            }
            m.shotCount=CountSmokeShots(m.shooter,m.life,m.sequence,out m.shotId);
            m.hits=CountSmokeHits(m.shooter,m.life,m.sequence,m.target,m.targetLife,out m.hitId,out m.eventDamage);
            m.explosions=0;
            for(int i=0;i<combatMatch.EventRing.Length;i++)
            {
                var e=combatMatch.EventRing[i];
                if(e.Sequence<1 || e.Action.Participant!=m.shooter || e.Action.LifeGeneration!=m.life || e.Action.Sequence!=m.sequence) continue;
                if(e.Action.ConnectionGeneration!=m.connection || e.Action.AuthorityGeneration!=m.authority || e.Action.Kind!=CombatActionKind.Grenade || e.Kind==CombatEventKind.Shot && e.Weapon!=WeaponKind.Grenade) throw new InvalidOperationException("Grenade event changed action identity.");
                if(e.Kind!=CombatEventKind.Explosion) continue;
                m.explosions++; m.explosionId=e.ToCombat().Id; m.explosion=e.Position; m.explosionTick=e.Tick;
            }
            if(m.explosions==1)
            {
                if(m.expireTick<0 || m.explosionTick!=m.expireTick || m.expireTick-m.actionTick!=WeaponCatalog.Ticks(spec.Lifetime,model.TickRate))
                    throw new InvalidOperationException("Grenade exploded before/after its production fuse.");
                m.blastDistance=(m.targetEye-m.explosion).magnitude;
                m.expectedDamage=spec.Damage+(spec.EdgeDamage-spec.Damage)*(m.blastDistance/spec.Radius);
                bool blocked=Physics.Raycast(m.explosion,(m.targetEye-m.explosion).normalized,out var hit,m.blastDistance,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore);
                m.coverBlocks=blocked && hit.collider.gameObject==grenadeCover;
                if(grenadeIndex==0 && blocked) throw new InvalidOperationException("Uncovered grenade blast is obstructed.");
            }
            if(m.ammo<spec.Ammo-grenadeIndex-1 || m.shotCount>1 || m.explosions>1 || m.hits>1 || shooter.StarterAmmo!=WeaponCatalog.StarterAmmo || shooter.Health!=WeaponCatalog.Health || grenadeIndex==1 && (m.damage!=0 || m.hits!=0))
                throw new InvalidOperationException("Grenade duplicated spending/events or changed protected health.");
            m.despawned=m.samples>=2 && !present && m.explosions==1;
            bool inside=m.blastDistance<spec.Radius-.5f && m.expectedDamage>spec.EdgeDamage;
            bool damageValid=grenadeIndex==0 ? m.hits==1 && Mathf.Abs(m.damage-m.expectedDamage)<.01f && Mathf.Abs(m.eventDamage-m.expectedDamage)<.01f : m.coverBlocks && m.damage==0 && m.hits==0;
            m.observed=m.despawned && m.gravityObserved && m.bounceObserved && m.lastTick>m.firstTick && m.lastTravel-m.firstTravel>1 && m.shotCount==1 && m.ammo==spec.Ammo-grenadeIndex-1 && shooter.LastActionSequence==m.sequence && inside && damageValid;
            status.stage="grenade_verdict_"+grenadeIndex;
            if(!m.observed || !remote.observed) return false;
            if(m.identity!=remote.identity || m.shotId!=remote.shotId || m.explosionId!=remote.explosionId || m.hitId!=remote.hitId || m.expireTick!=remote.expireTick || m.explosionTick!=remote.explosionTick || Mathf.Abs(m.damage-remote.damage)>.01f || (m.explosion-remote.explosion).sqrMagnitude>.0001f)
                throw new InvalidOperationException("Peers disagree on grenade terminal verdict.");
            if(grenadeAck<0) grenadeAck=now;
            m.verified=now-grenadeAck>=1; return false;
        }
        static GameObject GrenadeBox(string name,Vector3 position,Vector3 size)
        { var box=new GameObject(name); box.layer=0; box.transform.position=position; box.AddComponent<BoxCollider>().size=size; return box; }
        void CleanupGrenadeFixture()
        {
            if(grenadeCover!=null) Destroy(grenadeCover);
            if(grenadeBackstop!=null) Destroy(grenadeBackstop);
            if(grenadeFloor!=null) Destroy(grenadeFloor);
            grenadeCover=grenadeBackstop=grenadeFloor=null;
        }
    }
}
#endif
