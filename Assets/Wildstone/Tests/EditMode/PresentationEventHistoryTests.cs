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
        public void OpponentPoseUsesCombatFacingAndLateralMotion()
        {
            var player=new CombatState { Aim=Vector3.forward,Alive=true };
            var motor=new MotorState { Grounded=true };
            Assert.That(PlayerVisual.SelectAnimation(player,motor),Is.EqualTo("Aim"));
            motor.Velocity=Vector3.right*4;
            Assert.That(PlayerVisual.SelectAnimation(player,motor),Is.EqualTo("Strafe"));
            motor.Velocity=Vector3.forward*4;
            Assert.That(PlayerVisual.SelectAnimation(player,motor),Is.EqualTo("Run"));
            player.Alive=false;
            Assert.That(PlayerVisual.SelectAnimation(player,motor),Is.EqualTo("Death"));
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
