using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    [TestFixture, Category("Managed")]
    public sealed class FusionPlayerInputTests
    {
        static MotorState State => new MotorState { Tick = 40, Generation = 7, LastInputSequence = 17 };
        static FusionInput Input => new FusionInput
        {
            Present = true, Tick = int.MaxValue, Sequence = 1234, Generation = 7,
            Move = new Vector2(.25f, 1), Yaw = 23, Pitch = -5, Held = MotorButtons.Jump, Pressed = MotorButtons.Dash
        };

        [Test]
        public void ClientTimeAndSequenceCannotSetMotorSchedule()
        {
            var input = FusionMotorInput.Schedule(true, Input, State, 12, out int consumed);
            Assert.That(input.Present, Is.True);
            Assert.That(input.Tick, Is.EqualTo(41)); Assert.That(input.Sequence, Is.EqualTo(18));
            Assert.That(input.Generation, Is.EqualTo(7)); Assert.That(consumed, Is.EqualTo(1234));
            Assert.That(input.Pressed, Is.EqualTo(MotorButtons.Dash));
            Assert.That(input.Move, Is.EqualTo(Input.Move));
        }

        [Test]
        public void AimBitIsAcceptedFromTheWireButNeverReachesTheMotor()
        {
            var sample = Input; sample.Held = MotorButtons.Jump | MotorButtons.Aim; sample.Pressed = MotorButtons.Aim | MotorButtons.Dash;
            var input = FusionMotorInput.Schedule(true, sample, State, 12, out int consumed);
            Assert.That(input.Present, Is.True); Assert.That(consumed, Is.EqualTo(1234));
            Assert.That(input.Held, Is.EqualTo(MotorButtons.Jump)); Assert.That(input.Pressed, Is.EqualTo(MotorButtons.Dash));
            sample.Held = (MotorButtons)16;
            Assert.That(FusionMotorInput.Schedule(true, sample, State, 12, out _).Present, Is.False, "Bits above Aim stay invalid.");
        }

        [TestCase(false, 12), TestCase(true, 1234), TestCase(true, 1235)]
        public void MissingRepeatedOrOlderSampleCannotRefreshHoldOrRepeatAnEdge(bool available, int consumedBefore)
        {
            var input = FusionMotorInput.Schedule(available, Input, State, consumedBefore, out int consumed);
            Assert.That(input.Present, Is.False); Assert.That(input.Pressed, Is.EqualTo(MotorButtons.None));
            Assert.That(consumed, Is.EqualTo(consumedBefore));
        }

        [Test]
        public void RewindingTheSnapshotCursorAllowsTheSameEdgeAtItsOriginalReplayTick()
        {
            var first = FusionMotorInput.Schedule(true, Input, State, 12, out int consumed);
            Assert.That(FusionMotorInput.Schedule(true, Input, State, consumed, out _).Present, Is.False);
            var replay = FusionMotorInput.Schedule(true, Input, State, 12, out _);
            Assert.That(replay.Pressed, Is.EqualTo(first.Pressed)); Assert.That(replay.Sequence, Is.EqualTo(first.Sequence));
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        public void InvalidOrStaleLifePayloadIsMissingInputWithoutAdvancingCursor(int failure)
        {
            var sample = Input;
            switch (failure)
            {
                case 0: sample.Generation = 6; break;
                case 1: sample.Move.x = float.NaN; break;
                case 2: sample.Move.y = 2; break;
                case 3: sample.Yaw = float.PositiveInfinity; break;
                case 4: sample.Pitch = 90; break;
                case 5: sample.Pressed = (MotorButtons)128; break;
            }
            Assert.That(FusionMotorInput.Schedule(true, sample, State, 12, out int consumed).Present, Is.False);
            Assert.That(consumed, Is.EqualTo(12));
        }
    }
}
