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
        void StopCombatInput()
        {
            if(combatSession!=null) combatSession.InputRequested-=SmokeCombatInput;
            combatSession=null;
        }
    }
}
#endif
