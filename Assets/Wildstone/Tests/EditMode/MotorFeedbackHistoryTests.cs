using NUnit.Framework;
using Wildstone.Movement;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    [Category("Managed")]
    public class MotorFeedbackHistoryTests
    {
        [Test] public void ReplaySuppressesTheSameIdentityButAllowsADifferentLaterAction()
        {
            var history=new MotorFeedbackHistory();
            Assert.That(history.Consume(1,10,MotorEvents.Dash),Is.EqualTo(MotorEvents.Dash));
            Assert.That(history.Consume(1,10,MotorEvents.Dash),Is.EqualTo(MotorEvents.None));
            Assert.That(history.Consume(1,9,MotorEvents.None),Is.EqualTo(MotorEvents.None));
            Assert.That(history.Consume(1,11,MotorEvents.Jump),Is.EqualTo(MotorEvents.Jump));
            Assert.That(history.Consume(1,11,MotorEvents.Jump),Is.EqualTo(MotorEvents.None));
            Assert.That(history.Consume(1,12,MotorEvents.Dash),Is.EqualTo(MotorEvents.Dash));
        }
        [Test] public void ACorrectionCanAddADifferentEventKindAtTheSameTick()
        {
            var history=new MotorFeedbackHistory();
            history.Consume(1,10,MotorEvents.Dash);
            Assert.That(history.Consume(1,10,MotorEvents.Dash|MotorEvents.WallJump),Is.EqualTo(MotorEvents.WallJump));
            Assert.That(history.Consume(1,10,MotorEvents.WallJump),Is.EqualTo(MotorEvents.None));
        }
        [Test] public void NewLifeCanReuseTicksButOldLifeCannotReplayFeedback()
        {
            var history=new MotorFeedbackHistory();
            history.Consume(1,10,MotorEvents.Dash);
            Assert.That(history.Consume(2,10,MotorEvents.Dash),Is.EqualTo(MotorEvents.Dash));
            Assert.That(history.Consume(1,11,MotorEvents.Jump),Is.EqualTo(MotorEvents.None));
        }
        [Test] public void BoundedHistoryRejectsExpiredReplayInsteadOfReplayingEvictedActions()
        {
            var history=new MotorFeedbackHistory(); history.Consume(1,0,MotorEvents.Dash);
            Assert.That(history.Consume(1,512,MotorEvents.Jump),Is.EqualTo(MotorEvents.Jump));
            Assert.That(history.Consume(1,0,MotorEvents.Dash),Is.EqualTo(MotorEvents.None));
            Assert.That(history.Consume(1,1,MotorEvents.Dash),Is.EqualTo(MotorEvents.Dash));
            Assert.That(history.Consume(1,1,MotorEvents.Dash),Is.EqualTo(MotorEvents.None));
        }
    }
}
