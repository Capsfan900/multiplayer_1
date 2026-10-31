using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    [TestFixture,Category("Managed")]
    public sealed class PresentationEventHistoryTests
    {
        [Test]
        public void ReplayedEventsStaySilentAndNewLivesRemainDistinct()
        {
            var history=new PresentationEventHistory();
            var first=Event(10,2);
            Assert.That(history.Accept(first),Is.True);
            Assert.That(history.Accept(first),Is.False);
            Assert.That(history.Accept(Event(11,2,life:2)),Is.True);
            Assert.That(history.Accept(Event(11,2,authority:2)),Is.True);
            Assert.That(history.Accept(Event(11,2,kind:CombatEventKind.Hit)),Is.True);
            Assert.That(history.Accept(Event(11,2,target:8)),Is.True);
        }
        [Test]
        public void DenseRecentEvictionCannotReplayOldBurst()
        {
            var history=new PresentationEventHistory();
            var first=Event(10,1);
            Assert.That(history.Accept(first),Is.True);
            for(int i=0;i<2048;i++)
                Assert.That(history.Accept(Event(11+i/128,2+i)),Is.True);
            Assert.That(history.Accept(first),Is.False,"Capacity eviction must close the evicted tick, even inside the time window.");
            Assert.That(history.Accept(Event(10,9999)),Is.False,"The conservative watermark also closes unseen events on that tick.");
            Assert.That(history.Accept(Event(27,9999)),Is.True);
        }
        [Test]
        public void ActionIdentityKeepsAuthorityLifeAndSequence()
        {
            var one=new PresentationActionKey(Event(10,2).Action);
            Assert.That(one.Equals(new PresentationActionKey(Event(99,2).Action)),Is.True,"Tick is not part of a projectile's committed action identity.");
            Assert.That(one.Equals(new PresentationActionKey(Event(10,2,life:2).Action)),Is.False);
            Assert.That(one.Equals(new PresentationActionKey(Event(10,2,authority:2).Action)),Is.False);
            Assert.That(one.Equals(new PresentationActionKey(Event(10,3).Action)),Is.False);
        }
        static CombatEvent Event(int tick,int sequence,int life=1,int authority=1,CombatEventKind kind=CombatEventKind.Shot,int target=0)
        {
            var action=new CombatAction { Participant=7,LifeGeneration=life,AuthorityGeneration=authority,Sequence=sequence,Tick=tick };
            return new CombatEvent(kind,action,tick,target);
        }
        [Test]
        public void OpponentLocomotionIsMeasuredInTheFacingFrame()
        {
            var motor=new MotorState { Grounded=true };
            Assert.That(SoldierRig.LocalMove(motor,0),Is.EqualTo(Vector2.zero));
            motor.Velocity=Vector3.right*4;
            Assert.That(SoldierRig.LocalMove(motor,0).x,Is.EqualTo(4).Within(.001f),"Sideways travel drives the strafe axis.");
            motor.Velocity=Vector3.forward*4;
            Assert.That(SoldierRig.LocalMove(motor,90).x,Is.EqualTo(-4).Within(.001f),"Facing east, north travel is a left strafe.");
            Assert.That(SoldierRig.LocalMove(motor,180).y,Is.EqualTo(-4).Within(.001f),"Facing away, travel is a backpedal.");
            motor.Velocity=new Vector3(float.NaN,0,1);
            Assert.That(SoldierRig.LocalMove(motor,0),Is.EqualTo(Vector2.zero));
            // At full 11 m/s ground speed every direction plays its own run clip faster rather than skating it.
            foreach(var (direction,clipSpeed) in new[]{(Vector2.up,SoldierRig.RunCycleSpeed),(Vector2.down,SoldierRig.RunBackSpeed),
                (Vector2.left,SoldierRig.RunSideSpeed),(Vector2.right,SoldierRig.RunSideSpeed)})
            {
                float rate=SoldierRig.LocomotionScale(direction*11);
                Assert.That((direction*11/rate).magnitude,Is.EqualTo(clipSpeed).Within(.001f),direction.ToString());
            }
            Assert.That(SoldierRig.LocomotionScale(new Vector2(1,2)),Is.EqualTo(1),"Inside the run envelope the cycle plays at authored rate.");
            Assert.That(SoldierRig.LocomotionScale(new Vector2(float.PositiveInfinity,0)),Is.EqualTo(1));
            Assert.That(SoldierRig.StanceFor(WeaponKind.LaserSword),Is.EqualTo(2));
            Assert.That(SoldierRig.StanceFor(WeaponKind.LaserPistol),Is.EqualTo(1));
            Assert.That(SoldierRig.StanceFor(WeaponKind.LaserSMG),Is.EqualTo(0));
        }
        [Test]
        public void DeflectionHasItsOwnParryCue()
        {
            Assert.That(ArenaAudio.TryCue(new CombatEvent(CombatEventKind.Deflected,default,10),out var cue),Is.True);
            Assert.That(cue.Bank,Is.EqualTo("DeflectorParry"));
            foreach(var kind in new[]{CombatEventKind.ShieldBroken,CombatEventKind.Death})
            {
                ArenaAudio.TryCue(new CombatEvent(kind,default,10),out var other);
                Assert.That(cue.Pitch/other.Pitch,Is.GreaterThanOrEqualTo(1.2f),"Shares the parry recording with "+kind+" at a distinct pitch.");
            }
        }
        [Test]
        public void GrenadeFuseShowsAuthoritativeRemainingTime()
        {
            var shot=new ArenaProjectile { Action=new CombatAction { Tick=100 },ExpireTick=220,Weapon=WeaponKind.Grenade };
            Assert.That(ArenaEffects.FuseRemaining(shot,100),Is.EqualTo(1));
            Assert.That(ArenaEffects.FuseRemaining(shot,160),Is.EqualTo(.5f));
            Assert.That(ArenaEffects.FuseRemaining(shot,220),Is.EqualTo(0));
        }
    }
}
