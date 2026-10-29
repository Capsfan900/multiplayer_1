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
        [Serializable] sealed class CombatMetrics
        {
            public bool ready,movementComplete,fireSent,verified;
            public bool dedupVerified,fixtureTeleported,targetSetupObserved,hitFireSent,hitObserved,hitVerified;
            public int hitFireSequence,hitShotCount,hitEventCount,hitTargetParticipant,hitTargetLife,hitShooterAmmo,hitTargetAmmo;
            public float hitBaselineHealth,hitHealth,hitDamage,hitEventAmount,hitShooterBaselineHealth,fixtureStableSeconds;
            public Vector3 fixtureShooter,fixtureTarget;
            public string hitShotId="",hitId="";
            public int participant,remoteParticipant,lifeGeneration,remoteLifeGeneration,fireSequence;
            public int movementSamples,localStarterAmmo,remoteStarterAmmo,localShotCount,remoteShotCount;
            public int hostSampleSequence;
            public float localDistance,remoteDistance;
            public Vector3 baselineLocal,baselineRemote;
            public string localShotId="",remoteShotId="";
        }
        FusionSession combatSession;
        FusionPlayer combatAvatar;
        FusionMatch combatMatch;
        int combatMoveFrom,combatMoveUntil,combatInputSequence;
        bool combatStoppedInput;
        double combatMoveBegan=-1,combatFireBegan=-1,combatAcknowledgedAt=-1;
        double combatFixtureBegan=-1,combatFixtureStableAt=-1,combatHitFireBegan=-1,combatHitAcknowledgedAt=-1;

        bool DriveCombat(FusionSession session,ArenaMatch model,NetworkRunner runner,Status peer,double now)
        {
            if(status.combat==null)
            {
                var local=presentation.LocalPlayer; var remote=RemotePlayer(model);
                if(local==null || remote==null || local.StarterAmmo!=WeaponCatalog.StarterAmmo || remote.StarterAmmo!=WeaponCatalog.StarterAmmo || local.SelectedSpecial || remote.SelectedSpecial)
                    throw new InvalidOperationException("Combat smoke requires fresh starter clips.");
                foreach(var avatar in FindObjectsByType<FusionPlayer>())
                    if(avatar.Runner==runner && avatar.Initialized && avatar.Participant==local.Participant && avatar.Object.InputAuthority==runner.LocalPlayer)
                        combatAvatar=avatar;
                foreach(var candidate in FindObjectsByType<FusionMatch>()) if(candidate.Runner==runner && candidate.Initialized) combatMatch=candidate;
                if(combatAvatar==null || combatMatch==null) throw new InvalidOperationException("The admitted combat avatar is missing.");
                status.combat=new CombatMetrics {ready=true,participant=local.Participant,remoteParticipant=remote.Participant,
                    lifeGeneration=local.LifeGeneration,remoteLifeGeneration=remote.LifeGeneration,
                    baselineLocal=local.Feet,baselineRemote=remote.Feet};
                combatSession=session; session.InputRequested+=SmokeCombatInput;
                WriteOutput();
            }
            var metrics=status.combat;
            var own=model.Find(metrics.participant); var other=model.Find(metrics.remoteParticipant);
            if(own==null || other==null || own.LifeGeneration!=metrics.lifeGeneration || other.LifeGeneration!=metrics.remoteLifeGeneration)
                throw new InvalidOperationException("Combat smoke changed avatar life unexpectedly.");
            if(peer?.combat==null || !peer.combat.ready) { status.stage="combat_wait_peer"; return false; }
            if(combatMoveBegan<0)
            {
                combatMoveBegan=now; combatMoveFrom=runner.Tick.Raw+2; combatMoveUntil=combatMoveFrom+30;
                combatInputSequence=combatAvatar.LastSampleSequence;
            }
            metrics.hostSampleSequence=combatAvatar.LastSampleSequence;
            if(!metrics.movementComplete)
            {
                metrics.localDistance=HorizontalDistance(own.Feet,metrics.baselineLocal);
                metrics.remoteDistance=HorizontalDistance(other.Feet,metrics.baselineRemote);
                status.stage="combat_move";
                if(now-combatMoveBegan>5) throw new TimeoutException("Authoritative horizontal movement was not observed.");
                if(!combatStoppedInput || metrics.localDistance<=.2f || metrics.remoteDistance<=.2f || runner.Tick.Raw<combatMoveUntil+10) return false;
                metrics.movementComplete=true; WriteOutput();
            }
            if(!peer.combat.movementComplete) { status.stage="combat_wait_movement"; return false; }
            if(metrics.dedupVerified) return DriveControlledHit(model,runner,peer,now);
            if(!metrics.fireSent)
            {
                metrics.fireSequence=checked(own.LastActionSequence+1);
                var packet=FusionCombatAction.FromCombat(new CombatAction {Participant=own.Participant,
                    ConnectionGeneration=own.ConnectionGeneration,LifeGeneration=own.LifeGeneration,AuthorityGeneration=model.AuthorityGeneration,
                    Sequence=metrics.fireSequence,Kind=CombatActionKind.Fire,
                    Direction=Quaternion.Euler(combatAvatar.State.Pitch,combatAvatar.State.Yaw,0)*Vector3.forward,PickupId=-1});
                metrics.fireSent=true; combatFireBegan=now; WriteOutput();
                combatAvatar.RPC_Combat(packet); combatAvatar.RPC_Combat(packet);
            }
            status.stage="combat_confirm";
            metrics.localStarterAmmo=own.StarterAmmo; metrics.remoteStarterAmmo=other.StarterAmmo;
            metrics.localShotCount=CountSmokeShots(metrics.participant,metrics.lifeGeneration,metrics.fireSequence,out metrics.localShotId);
            if(peer.combat.fireSent)
                metrics.remoteShotCount=CountSmokeShots(metrics.remoteParticipant,metrics.remoteLifeGeneration,peer.combat.fireSequence,out metrics.remoteShotId);
            if(metrics.localShotCount>1 || metrics.remoteShotCount>1 || own.StarterAmmo<WeaponCatalog.StarterAmmo-1 || other.StarterAmmo<WeaponCatalog.StarterAmmo-1)
                throw new InvalidOperationException("Duplicate RPC spent ammunition or emitted a second shot.");
            if(now-combatFireBegan>5 && !metrics.verified) throw new TimeoutException("Replicated shot confirmation was not observed.");
            bool acknowledged=peer.combat.fireSent && own.StarterAmmo==WeaponCatalog.StarterAmmo-1 && other.StarterAmmo==WeaponCatalog.StarterAmmo-1 &&
                own.LastActionSequence==metrics.fireSequence && other.LastActionSequence==peer.combat.fireSequence &&
                metrics.localShotCount==1 && metrics.remoteShotCount==1;
            if(!acknowledged) return false;
            if(peer.combat.remoteShotId!="" && peer.combat.remoteShotId!=metrics.localShotId ||
                peer.combat.localShotId!="" && peer.combat.localShotId!=metrics.remoteShotId)
                throw new InvalidOperationException("Peers disagreed on the replicated shot identity.");
            if(peer.combat.localShotId=="" || peer.combat.remoteShotId=="") return false;
            if(combatAcknowledgedAt<0) combatAcknowledgedAt=now;
            metrics.dedupVerified=now-combatAcknowledgedAt>=1;
            if(metrics.dedupVerified) return DriveControlledHit(model,runner,peer,now);
            return false;
        }
        bool DriveControlledHit(ArenaMatch model,NetworkRunner runner,Status peer,double now)
        {
            var metrics=status.combat;
            if(combatFixtureBegan<0) combatFixtureBegan=now;
            if(now-combatFixtureBegan>15 && !metrics.hitVerified) throw new TimeoutException("Controlled hit fixture was not confirmed.");
            if(!peer.combat.dedupVerified) { status.stage="combat_wait_dedup"; return false; }
            int shooterId=status.role=="client"?metrics.participant:metrics.remoteParticipant;
            int targetId=status.role=="host"?metrics.participant:metrics.remoteParticipant;
            var shooter=model.Find(shooterId); var target=model.Find(targetId);
            metrics.hitTargetParticipant=targetId; metrics.hitTargetLife=target.LifeGeneration;
            metrics.fixtureTarget=new Vector3(-30,.02f,-12); metrics.fixtureShooter=new Vector3(-30,.02f,-6);
            if(status.role=="host" && !metrics.fixtureTeleported)
            {
                // Controlled target placement follows the independently verified real input movement above.
                FusionPlayer targetAvatar=null,shooterAvatar=null;
                foreach(var avatar in FindObjectsByType<FusionPlayer>())
                    if(avatar.Runner==runner && avatar.Initialized)
                    {
                        if(avatar.Participant==targetId) targetAvatar=avatar;
                        if(avatar.Participant==shooterId) shooterAvatar=avatar;
                    }
                if(targetAvatar==null || shooterAvatar==null || !runner.IsServer) throw new InvalidOperationException("Controlled target authority is missing.");
                var ray=metrics.fixtureTarget-metrics.fixtureShooter;
                if(Physics.Raycast(metrics.fixtureShooter+Vector3.up*1.65f,ray.normalized,ray.magnitude,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("The controlled Atrium firing corridor is occluded.");
                targetAvatar.Teleport(metrics.fixtureTarget,0); shooterAvatar.Teleport(metrics.fixtureShooter,180);
                metrics.fixtureTeleported=true; WriteOutput();
            }
            if(!(status.role=="host"?metrics.fixtureTeleported:peer.combat.fixtureTeleported))
            { status.stage="combat_wait_target_setup"; return false; }
            bool placed=(target.Feet-metrics.fixtureTarget).sqrMagnitude<.04f && (shooter.Feet-metrics.fixtureShooter).sqrMagnitude<.04f;
            if(!placed) { combatFixtureStableAt=-1; status.stage="combat_target_replication"; return false; }
            if(combatFixtureStableAt<0) combatFixtureStableAt=now;
            metrics.fixtureStableSeconds=(float)(now-combatFixtureStableAt);
            if(!metrics.targetSetupObserved)
            {
                status.stage="combat_target_history_warmup";
                if(now-combatFixtureStableAt<1) return false;
                metrics.hitBaselineHealth=target.Health; metrics.hitShooterBaselineHealth=shooter.Health;
                metrics.targetSetupObserved=true; WriteOutput();
            }
            if(!peer.combat.targetSetupObserved) { status.stage="combat_wait_target_history"; return false; }
            if(status.role=="client" && !metrics.hitFireSent)
            {
                metrics.hitFireSequence=checked(shooter.LastActionSequence+1);
                var packet=FusionCombatAction.FromCombat(new CombatAction {Participant=shooter.Participant,
                    ConnectionGeneration=shooter.ConnectionGeneration,LifeGeneration=shooter.LifeGeneration,AuthorityGeneration=model.AuthorityGeneration,
                    Sequence=metrics.hitFireSequence,Kind=CombatActionKind.Fire,Direction=(target.Eye-shooter.Eye).normalized,PickupId=-1});
                metrics.hitFireSent=true; combatHitFireBegan=now; WriteOutput(); combatAvatar.RPC_Combat(packet);
            }
            if(status.role=="host" && !peer.combat.hitFireSent) { status.stage="combat_wait_target_shot"; return false; }
            int sequence=status.role=="client"?metrics.hitFireSequence:peer.combat.hitFireSequence;
            if(combatHitFireBegan<0) combatHitFireBegan=now;
            metrics.hitFireSequence=sequence; status.stage="combat_hit_replication";
            metrics.hitHealth=target.Health; metrics.hitDamage=metrics.hitBaselineHealth-target.Health;
            metrics.hitShooterAmmo=shooter.StarterAmmo; metrics.hitTargetAmmo=target.StarterAmmo;
            metrics.hitShotCount=CountSmokeShots(shooterId,shooter.LifeGeneration,sequence,out metrics.hitShotId);
            metrics.hitEventCount=CountSmokeHits(shooterId,shooter.LifeGeneration,sequence,targetId,target.LifeGeneration,out metrics.hitId,out metrics.hitEventAmount);
            float expected=WeaponCatalog.Get(WeaponKind.LineDriver).Damage;
            if(metrics.hitShotCount>1 || metrics.hitEventCount>1 || shooter.StarterAmmo<WeaponCatalog.StarterAmmo-2 || target.StarterAmmo!=WeaponCatalog.StarterAmmo-1 ||
                shooter.Health!=metrics.hitShooterBaselineHealth || metrics.hitDamage>expected+.001f)
                throw new InvalidOperationException("Controlled shot duplicated a verdict or changed an unrelated combat value.");
            if(now-combatHitFireBegan>5 && !metrics.hitVerified) throw new TimeoutException("Host-authoritative damage did not replicate to both peers.");
            bool hit=shooter.StarterAmmo==WeaponCatalog.StarterAmmo-2 && shooter.LastActionSequence==sequence && metrics.hitShotCount==1 && metrics.hitEventCount==1 &&
                Mathf.Abs(metrics.hitDamage-expected)<.001f && Mathf.Abs(metrics.hitEventAmount-expected)<.001f;
            metrics.hitObserved=hit;
            if(!hit || !peer.combat.hitObserved || peer.combat.hitId=="" || peer.combat.hitShotId=="") return false;
            if(peer.combat.hitId!=metrics.hitId || peer.combat.hitShotId!=metrics.hitShotId ||
                peer.combat.hitTargetParticipant!=targetId || peer.combat.hitTargetLife!=target.LifeGeneration || Mathf.Abs(peer.combat.hitDamage-expected)>.001f)
                throw new InvalidOperationException("Peers disagreed on the controlled hit, life or replicated damage.");
            if(combatHitAcknowledgedAt<0) combatHitAcknowledgedAt=now;
            metrics.hitVerified=now-combatHitAcknowledgedAt>=1; metrics.verified=metrics.hitVerified;
            return metrics.verified;
        }
        int CountSmokeHits(int shooter,int life,int sequence,int target,int targetLife,out string id,out float amount)
        {
            int count=0; id=""; amount=0;
            for(int i=0;i<combatMatch.EventRing.Length;i++)
            {
                var entry=combatMatch.EventRing[i];
                if(entry.Sequence<1 || entry.Kind!=CombatEventKind.Hit || entry.Action.Participant!=shooter ||
                    entry.Action.LifeGeneration!=life || entry.Action.Sequence!=sequence || entry.Target!=target || entry.TargetLife!=targetLife) continue;
                count++; id=entry.ToCombat().Id; amount=entry.Amount;
            }
            return count;
        }
        void SmokeCombatInput(NetworkInput output)
        {
            if(combatMoveBegan<0 || combatAvatar==null || combatSession?.Runner==null) return;
            int tick=combatSession.Runner.Tick.Raw;
            bool moving=tick>=combatMoveFrom && tick<combatMoveUntil;
            combatInputSequence=checked(Math.Max(combatInputSequence,combatAvatar.LastSampleSequence)+1);
            output.Set(new FusionInput {Present=true,Sequence=combatInputSequence,Generation=combatAvatar.State.Generation,
                Move=moving?Vector2.right:Vector2.zero,Yaw=combatAvatar.State.Yaw,Pitch=combatAvatar.State.Pitch});
            if(moving) status.combat.movementSamples++;
            else if(tick>=combatMoveUntil) combatStoppedInput=true;
        }
        static float HorizontalDistance(Vector3 point,Vector3 baseline)
        { var delta=point-baseline; delta.y=0; return delta.magnitude; }
        int CountSmokeShots(int participant,int life,int sequence,out string id)
        {
            int count=0; id="";
            for(int i=0;i<combatMatch.EventRing.Length;i++)
            {
                var entry=combatMatch.EventRing[i];
                if(entry.Sequence<1 || entry.Kind!=CombatEventKind.Shot || entry.Action.Participant!=participant ||
                    entry.Action.LifeGeneration!=life || entry.Action.Sequence!=sequence) continue;
                count++; id=entry.ToCombat().Id;
            }
            return count;
        }
        [Serializable] sealed class LaserMetrics
        {
            public string weapon,shotId="",hitIds="";
            public bool pickupPlaced,pickupSent,picked,equipSent,equipped,fixturePlaced,ready,fireSent,observed,verified;
            public int pickupId,pickupGeneration,pickupSequence,equipSequence,fireSequence,shooter,life,target,targetLife,ammo,shotCount,hitCount;
            public float baselineHealth,health,damage,eventDamage;
            public bool movingObserved,movingVerified,coverPlaced,coverReady,coverFireSent,coverObserved;
            public int movementSamples,coverSequence,coverAmmo,coverShots,coverHits;
            public float movingDistance,movingSpeed,movingFireDistance,movingFireSpeed,coverBaselineHealth,coverHealth;
            public string coverShotId="";
        }
        int laserIndex;
        bool MovingLaserScenario=>status.scenario=="laser-moving-cover";
        bool laserMoveActive;
        float laserMoveDirection=1;
        GameObject laserCover;
        double laserCoverStable=-1;
        double laserBegan=-1,laserStable=-1,laserAck=-1;
        static readonly WeaponKind[] LaserWeapons={WeaponKind.LaserPistol,WeaponKind.LaserShotgun,WeaponKind.LaserSniper};
        bool DriveLaserCombat(ArenaMatch model,NetworkRunner runner,Status peer,double now)
        {
            if(status.lasers==null)
            {
                if(MovingLaserScenario) { combatSession=menu.Session; combatSession.InputRequested+=SmokeLaserMovingInput; }
                status.lasers=new LaserMetrics[LaserWeapons.Length];
                for(int i=0;i<LaserWeapons.Length;i++) status.lasers[i]=new LaserMetrics {weapon=LaserWeapons[i].ToString()};
                foreach(var candidate in FindObjectsByType<FusionMatch>()) if(candidate.Runner==runner && candidate.Initialized) combatMatch=candidate;
                foreach(var avatar in FindObjectsByType<FusionPlayer>())
                    if(avatar.Runner==runner && avatar.Initialized && avatar.Participant==presentation.LocalParticipantId && avatar.Object.InputAuthority==runner.LocalPlayer) combatAvatar=avatar;
                if(combatMatch==null || combatAvatar==null) throw new InvalidOperationException("Laser fixture is missing admitted network objects.");
            }
            if(peer?.lasers==null || peer.lasers.Length!=LaserWeapons.Length) return false;
            if(laserIndex==LaserWeapons.Length) return true;
            var m=status.lasers[laserIndex]; var remote=peer.lasers[laserIndex];
            if(m.verified && remote.verified)
            { laserMoveActive=false; if(laserCover!=null) Destroy(laserCover); laserCover=null;
                laserIndex++; laserBegan=laserStable=laserAck=laserCoverStable=-1; WriteOutput(); return laserIndex==LaserWeapons.Length; }
            if(laserBegan<0) laserBegan=now;
            if(now-laserBegan>25) throw new TimeoutException("Laser fixture timed out: "+m.weapon+"@"+status.stage);
            var weapon=LaserWeapons[laserIndex]; var spec=WeaponCatalog.Get(weapon);
            var shooter=status.role=="client"?presentation.LocalPlayer:RemotePlayer(model);
            var target=status.role=="host"?presentation.LocalPlayer:RemotePlayer(model);
            if(shooter==null || target==null || !shooter.Alive || !target.Alive) throw new InvalidOperationException("Laser fixture lost a living participant.");
            if(m.life!=0 && (m.shooter!=shooter.Participant || m.life!=shooter.LifeGeneration || m.target!=target.Participant || m.targetLife!=target.LifeGeneration))
                throw new InvalidOperationException("Laser fixture changed participant or life identity.");
            m.shooter=shooter.Participant; m.life=shooter.LifeGeneration; m.target=target.Participant; m.targetLife=target.LifeGeneration;
            PickupState pickup=null;
            foreach(var item in model.Pickups) if(WeaponCatalog.FromPickup(item.Item)==weapon) { pickup=item; break; }
            if(pickup==null) throw new InvalidOperationException("Map is missing laser pickup: "+weapon);
            m.pickupId=pickup.Id;
            if(status.role=="host" && !m.pickupPlaced)
            {
                // Diagnostic-only reset between weapons; inventory is acquired only by the real pickup RPC.
                combatMatch.Model.Find(target.Participant).Health=WeaponCatalog.Health;
                combatMatch.Model.Find(target.Participant).Shield=0;
                LaserAvatar(runner,shooter.Participant).Teleport(pickup.Position+Vector3.right,0);
                m.pickupGeneration=pickup.Generation; m.pickupPlaced=true; WriteOutput();
            }
            var host=status.role=="host"?m:remote;
            var client=status.role=="client"?m:remote;
            status.stage="laser_pickup_"+weapon;
            if(!host.pickupPlaced) return false;
            if(status.role=="client" && !m.pickupSent)
            {
                if((shooter.Feet-pickup.Position).sqrMagnitude>4 || pickup.Generation!=host.pickupGeneration) return false;
                m.pickupGeneration=pickup.Generation; m.pickupSequence=shooter.LastActionSequence+1; m.pickupSent=true;
                SendLaserAction(model,shooter,m.pickupSequence,CombatActionKind.Pickup,Vector3.forward,pickup.Id,pickup.Generation);
            }
            m.picked=m.picked || client.pickupSent && shooter.SpecialWeapon==weapon && shooter.SpecialAmmo==spec.Ammo && pickup.Generation==host.pickupGeneration+1 && pickup.AvailableTick>model.Tick;
            if(!m.picked || !remote.picked) return false;
            // Explicit selection exercises the production equip RPC even though pickup auto-selects.
            if(status.role=="client" && !m.equipSent)
            {
                m.equipSequence=shooter.LastActionSequence+1; m.equipSent=true;
                SendLaserAction(model,shooter,m.equipSequence,CombatActionKind.SelectSpecial,Vector3.forward);
            }
            m.equipped=m.equipped || client.equipSent && shooter.LastActionSequence>=client.equipSequence && shooter.SelectedSpecial && shooter.SpecialWeapon==weapon;
            if(!m.equipped || !remote.equipped) return false;
            var targetPoint=new Vector3(-30,.02f,-12); var shooterPoint=new Vector3(-30,.02f,-10);
            if(status.role=="host" && !m.fixturePlaced)
            {
                if(Physics.Raycast(shooterPoint+Vector3.up*.9f,Vector3.back,2,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Laser fixture corridor is occluded.");
                LaserAvatar(runner,target.Participant).Teleport(targetPoint,0);
                LaserAvatar(runner,shooter.Participant).Teleport(shooterPoint,180);
                m.fixturePlaced=true; WriteOutput();
            }
            status.stage="laser_history_"+weapon;
            if(!host.fixturePlaced || !m.ready && ((shooter.Feet-shooterPoint).sqrMagnitude>.04f || (target.Feet-targetPoint).sqrMagnitude>.04f)) { laserStable=-1; return false; }
            if(laserStable<0) laserStable=now;
            if(!m.ready)
            {
                if(now-laserStable<1 || target.Health!=WeaponCatalog.Health) return false;
                m.baselineHealth=target.Health; m.ready=true; WriteOutput();
            }
            if(!remote.ready) return false;
            if(MovingLaserScenario)
            {
                laserMoveActive=true;
                var state=LaserAvatar(runner,target.Participant).State;
                m.movingDistance=HorizontalDistance(target.Feet,targetPoint);
                m.movingSpeed=new Vector2(state.Velocity.x,state.Velocity.z).magnitude;
                m.movingObserved|=m.movingDistance>.2f && m.movingSpeed>.2f;
                if(!m.movingObserved || !remote.movingObserved || host.movementSamples<5) return false;
                if(m.movingVerified) return DriveLaserCover(model,runner,peer,m,remote,shooter,target,spec,now);
            }
            if(status.role=="client" && !m.fireSent)
            {
                if(model.Tick<shooter.FireReadyTick) return false;
                if(MovingLaserScenario)
                {
                    if(m.movingDistance<=.2f || m.movingSpeed<=.2f) return false;
                    m.movingFireDistance=m.movingDistance; m.movingFireSpeed=m.movingSpeed;
                }
                m.fireSequence=shooter.LastActionSequence+1; m.fireSent=true;
                SendLaserAction(model,shooter,m.fireSequence,CombatActionKind.Fire,(target.Feet+Vector3.up*.9f-shooter.Eye).normalized);
            }
            if(!client.fireSent) return false;
            if(MovingLaserScenario)
            { m.movingFireDistance=client.movingFireDistance; m.movingFireSpeed=client.movingFireSpeed; }
            m.fireSequence=client.fireSequence; m.ammo=shooter.SpecialAmmo; m.health=target.Health; m.damage=m.baselineHealth-m.health;
            m.shotCount=CountSmokeShots(shooter.Participant,shooter.LifeGeneration,m.fireSequence,out m.shotId);
            m.hitCount=0; m.hitIds=""; m.eventDamage=0;
            for(int i=0;i<combatMatch.EventRing.Length;i++)
            {
                var e=combatMatch.EventRing[i];
                if(e.Sequence<1 || e.Kind!=CombatEventKind.Hit || e.Action.Participant!=shooter.Participant || e.Action.LifeGeneration!=shooter.LifeGeneration || e.Action.Sequence!=m.fireSequence) continue;
                if(e.Weapon!=weapon || e.Target!=target.Participant || e.TargetLife!=target.LifeGeneration) throw new InvalidOperationException("Laser hit has wrong weapon or target identity.");
                m.hitCount++; m.eventDamage+=e.Amount; m.hitIds+=e.ToCombat().Id+";";
            }
            float expected=spec.Damage*WeaponCatalog.HitscanPellets(weapon);
            if(m.ammo<spec.Ammo-1 || m.shotCount>1 || m.hitCount>WeaponCatalog.HitscanPellets(weapon) || m.damage>expected+.001f || shooter.StarterAmmo!=WeaponCatalog.StarterAmmo)
                throw new InvalidOperationException("Laser shot duplicated ammo, damage or events.");
            m.observed=m.ammo==spec.Ammo-1 && m.shotCount==1 && m.hitCount==WeaponCatalog.HitscanPellets(weapon) && Mathf.Abs(m.damage-expected)<.001f && Mathf.Abs(m.eventDamage-expected)<.001f;
            status.stage="laser_verdict_"+weapon;
            if(!m.observed || !remote.observed) return false;
            if(m.shotId!=remote.shotId || m.hitIds!=remote.hitIds || m.life!=remote.life || m.targetLife!=remote.targetLife) throw new InvalidOperationException("Peers disagree on laser event identity.");
            if(laserAck<0) laserAck=now;
            if(MovingLaserScenario) m.movingVerified=now-laserAck>=1;
            else m.verified=now-laserAck>=1;
            return false;
        }
        void SmokeLaserMovingInput(NetworkInput output)
        {
            if(status.role!="host" || !laserMoveActive || laserIndex>=LaserWeapons.Length || combatAvatar==null) return;
            float x=combatAvatar.State.Position.x;
            if(x> -29.55f) laserMoveDirection=-1;
            if(x< -30.45f) laserMoveDirection=1;
            combatInputSequence=checked(Math.Max(combatInputSequence,combatAvatar.LastSampleSequence)+1);
            output.Set(new FusionInput {Present=true,Sequence=combatInputSequence,Generation=combatAvatar.State.Generation,
                Move=new Vector2(laserMoveDirection*.2f,0),Yaw=0,Pitch=0});
            status.lasers[laserIndex].movementSamples++;
        }
        bool DriveLaserCover(ArenaMatch model,NetworkRunner runner,Status peer,LaserMetrics m,LaserMetrics remote,
            CombatState shooter,CombatState target,WeaponSpec spec,double now)
        {
            status.stage="laser_cover_"+m.weapon;
            if(!remote.movingVerified) return false;
            // A bounded diagnostic-only world collider; movement and all verdicts still use production simulation.
            if(!m.coverPlaced)
            {
                laserCover=new GameObject("Smoke laser cover"); laserCover.layer=0;
                laserCover.transform.position=new Vector3(-30,1.5f,-11);
                laserCover.AddComponent<BoxCollider>().size=new Vector3(4,3,.15f);
                Physics.SyncTransforms();
                m.coverPlaced=true; laserCoverStable=now; WriteOutput();
            }
            if(!remote.coverPlaced || now-laserCoverStable<1) return false;
            if(!m.coverReady)
            {
                if(!Physics.Raycast(shooter.Eye,(target.Feet+Vector3.up*.9f-shooter.Eye).normalized,out var obstruction,3,
                    MotorTuning.WorldMask,QueryTriggerInteraction.Ignore) || obstruction.collider.gameObject!=laserCover)
                    throw new InvalidOperationException("Diagnostic cover does not obstruct the moving target.");
                m.coverBaselineHealth=target.Health; m.coverReady=true; WriteOutput();
            }
            if(!remote.coverReady) return false;
            var client=status.role=="client"?m:remote;
            if(status.role=="client" && !m.coverFireSent)
            {
                if(model.Tick<shooter.FireReadyTick) return false;
                m.coverSequence=shooter.LastActionSequence+1; m.coverFireSent=true;
                SendLaserAction(model,shooter,m.coverSequence,CombatActionKind.Fire,(target.Feet+Vector3.up*.9f-shooter.Eye).normalized);
            }
            if(!client.coverFireSent) return false;
            m.coverSequence=client.coverSequence; m.coverAmmo=shooter.SpecialAmmo; m.coverHealth=target.Health;
            m.coverShots=CountSmokeShots(shooter.Participant,shooter.LifeGeneration,m.coverSequence,out m.coverShotId);
            m.coverHits=CountSmokeHits(shooter.Participant,shooter.LifeGeneration,m.coverSequence,target.Participant,target.LifeGeneration,out _,out _);
            if(m.coverHits!=0 || m.coverHealth!=m.coverBaselineHealth || m.coverShots>1 || m.coverAmmo<spec.Ammo-2 ||
                shooter.StarterAmmo!=WeaponCatalog.StarterAmmo)
                throw new InvalidOperationException("Cover shot damaged its protected target or duplicated ammunition/events.");
            m.coverObserved=m.coverShots==1 && m.coverAmmo==spec.Ammo-2 && shooter.LastActionSequence==m.coverSequence;
            if(!m.coverObserved || !remote.coverObserved) return false;
            if(m.coverShotId!=remote.coverShotId || m.coverSequence!=remote.coverSequence || m.coverHealth!=remote.coverHealth)
                throw new InvalidOperationException("Peers disagree on the blocked moving-target verdict.");
            m.verified=true; WriteOutput(); return false;
        }
        FusionPlayer LaserAvatar(NetworkRunner runner,int participant)
        {
            foreach(var avatar in FindObjectsByType<FusionPlayer>()) if(avatar.Runner==runner && avatar.Initialized && avatar.Participant==participant) return avatar;
            throw new InvalidOperationException("Laser fixture avatar is missing.");
        }
        void SendLaserAction(ArenaMatch model,CombatState shooter,int sequence,CombatActionKind kind,Vector3 direction,int pickup=-1,int generation=0)
        {
            combatAvatar.RPC_Combat(FusionCombatAction.FromCombat(new CombatAction {Participant=shooter.Participant,ConnectionGeneration=shooter.ConnectionGeneration,
                LifeGeneration=shooter.LifeGeneration,AuthorityGeneration=model.AuthorityGeneration,Sequence=sequence,Kind=kind,Direction=direction,PickupId=pickup,PickupGeneration=generation}));
            WriteOutput();
        }
        void StopCombatInput()
        {
            if(combatSession!=null) { combatSession.InputRequested-=SmokeCombatInput; combatSession.InputRequested-=SmokeLaserMovingInput; }
            if(laserCover!=null) Destroy(laserCover);
            combatSession=null;
        }
    }
}
#endif
