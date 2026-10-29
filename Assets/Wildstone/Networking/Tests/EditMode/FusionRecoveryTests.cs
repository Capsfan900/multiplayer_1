using System;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.Networking;
using Wildstone.Session;
using Assert = NUnit.Framework.Assert;

namespace Wildstone.Tests
{
    public sealed class FusionRecoveryTests
    {
        [Test] public void FinalFreezeCapsOnlyValidatedGameplayTicksAndKeepsTheOriginalObservationTime()
        {
            var content=new ContentVersion("p","b","m","r");
            var session=new FusionSession(content,"match"); var recovery=new FusionRecovery(session);
            var checkpoint=new SessionCheckpoint("match",content,1,240,SessionPhase.Running,0,
                new SeatTableSnapshot(new[]{new SeatSnapshot("peer",0,0,1,true,true,false,0,0)},1,5));
            var header=FusionRecoveryState.FromCheckpoint(checkpoint,200);
            var seats=new[]{FusionRecoverySeat.FromSeat(checkpoint.Seats.Seats[0],FusionCredentialHash.FromCredential(new byte[32]))};
            recovery.ObserveAuthoritativeTick(240,1);
            recovery.ObserveFinalFrozen(header,seats,240,1);
            recovery.ObserveFinalFrozen(header,seats,960,7);
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Assert.That(typeof(FusionRecovery).GetField("lastObservedTick",flags).GetValue(recovery),Is.EqualTo(240));
            Assert.That(typeof(FusionRecovery).GetField("lastObservedAt",flags).GetValue(recovery),Is.EqualTo(1),"Upload wait consumes original deadlines instead of moving the clock anchor.");
            var delayed=new FusionRecovery(session); delayed.ObserveAuthoritativeTick(239,.99);
            delayed.ObserveFinalFrozen(header,seats,840,6);
            Assert.That(typeof(FusionRecovery).GetField("lastObservedAt",flags).GetValue(delayed),Is.EqualTo(1),"Late first receipt consumes transport ticks elapsed during upload wait.");
            var later=new FusionRecovery(session); later.ObserveAuthoritativeTick(601,1);
            Assert.Throws<ArgumentException>(()=>later.ObserveFinalFrozen(header,seats,960,7),"A final flag cannot hide already observed gameplay older than the rollback budget.");
            header.AuthorityGeneration=2;
            Assert.Throws<InvalidOperationException>(()=>recovery.ObserveFinalFrozen(header,seats,960,7),"A different authority cannot replace a frozen baseline.");
        }
        [Test] public void WholeAvatarValidationRejectsStaleClockWrongLifeAndInvalidMotorBeforeSpawn()
        {
            var motor=FusionMotorState.FromMotor(ArenaMotor.InitialState(new Vector3(1,0,2),0));
            motor.Tick=10; var player=new CombatState {Participant=1,Team=0,ConnectionGeneration=1,LifeGeneration=1,Feet=motor.Position};
            Assert.DoesNotThrow(()=>FusionRecovery.ValidateAvatar(motor,-1,1,100,player,110));
            motor.LastInputSequence=1000;
            Assert.DoesNotThrow(()=>FusionRecovery.ValidateAvatar(motor,-1,1,100,player,110),"A new client sample cursor does not reset the host motor sequence.");
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateAvatar(motor,-1,1,100,player,109));
            player.LifeGeneration=2;
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateAvatar(motor,-1,1,100,player,110));
            player.LifeGeneration=1; motor.Velocity=new Vector3(float.NaN,0,0);
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateAvatar(motor,-1,1,100,player,110));
        }
        [Test] public void DespawnedDeadAndExpiredPlayersDoNotRequireAnAvatarOrRetainedSeat()
        {
            var dead=new CombatState {Participant=1,Team=0,ConnectionGeneration=1,LifeGeneration=1,Alive=false,Connected=false,Health=0};
            var held=new SeatSnapshot("held",0,0,1,false,false,true,10,40);
            Assert.DoesNotThrow(()=>FusionRecovery.ValidateCoverage(new[]{dead},new[]{held},0));
            Assert.DoesNotThrow(()=>FusionRecovery.ValidateCoverage(new[]{dead},Array.Empty<SeatSnapshot>(),0));
            dead.Alive=true;
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateCoverage(new[]{dead},new[]{held},0));
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateCoverage(new[]{dead},Array.Empty<SeatSnapshot>(),1<<1));
            dead.Alive=false; dead.Connected=true;
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateCoverage(new[]{dead},new[]{held},0));
        }
        [Test] public void OnlyAnUnreadyPendingSpawnCanReserveASeatWithoutGameplayState()
        {
            var pending=new SeatSnapshot("pending",0,0,2,true,false,true,0,0);
            Assert.DoesNotThrow(()=>FusionRecovery.ValidateCoverage(Array.Empty<CombatState>(),new[]{pending},0));
            var ready=new SeatSnapshot("pending",0,0,2,true,true,true,0,0);
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateCoverage(Array.Empty<CombatState>(),new[]{ready},0));
            var ordinary=new SeatSnapshot("pending",0,0,2,true,false,false,0,0);
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateCoverage(Array.Empty<CombatState>(),new[]{ordinary},0));
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateCoverage(Array.Empty<CombatState>(),new[]{pending},1<<1));
            var oldDead=new CombatState {Participant=1,Team=0,ConnectionGeneration=1,LifeGeneration=1,Alive=false,Connected=false,Health=0};
            Assert.DoesNotThrow(()=>FusionRecovery.ValidateCoverage(new[]{oldDead},new[]{pending},0));
            Assert.Throws<InvalidOperationException>(()=>FusionRecovery.ValidateCoverage(new[]{oldDead},new[]{pending},1<<1));
        }
        [Test] public void RecoveryConsumesAvatarHoldWithoutRestoringInventoryOrResettingActionCursor()
        {
            var player=new CombatState {Participant=1,Team=0,ConnectionGeneration=1,LifeGeneration=3,Health=37,StarterAmmo=2,
                SpecialWeapon=WeaponKind.PulseLobber,SpecialAmmo=1,LastActionSequence=19};
            var match=new MatchSnapshot {Tick=600,Players=new[]{player}};
            var seats=new SeatTableSnapshot(new[]{new SeatSnapshot("account",0,0,1,false,false,false,10,40)},1,13);
            FusionRecovery.PrepareDisconnectedSnapshot(match,seats,13);
            Assert.That(player.Connected,Is.False); Assert.That(player.Alive,Is.True); Assert.That(player.DisconnectUntil,Is.EqualTo(840));
            Assert.That(player.Health,Is.EqualTo(37)); Assert.That(player.StarterAmmo,Is.EqualTo(2));
            Assert.That(player.SpecialAmmo,Is.EqualTo(1)); Assert.That(player.LifeGeneration,Is.EqualTo(3)); Assert.That(player.LastActionSequence,Is.EqualTo(19));
        }
        [Test] public void ExpiredVulnerableAvatarIsRemovedWithoutInventingAReconnectedLife()
        {
            var player=new CombatState {Participant=1,Team=0,ConnectionGeneration=1,LifeGeneration=3,Health=37,StarterAmmo=2,
                SpecialWeapon=WeaponKind.PulseLobber,SpecialAmmo=1};
            var match=new MatchSnapshot {Tick=600,Players=new[]{player}};
            var seats=new SeatTableSnapshot(new[]{new SeatSnapshot("account",0,0,1,false,false,false,10,40)},1,18);
            FusionRecovery.PrepareDisconnectedSnapshot(match,seats,18);
            Assert.That(player.Alive,Is.False); Assert.That(player.Health,Is.Zero); Assert.That(player.DisconnectUntil,Is.EqualTo(600));
            Assert.That(player.LifeGeneration,Is.EqualTo(3)); Assert.That(player.SpecialWeapon,Is.EqualTo(WeaponKind.None));
            Assert.That(player.RespawnTick,Is.GreaterThan(600));
        }
    }
}
