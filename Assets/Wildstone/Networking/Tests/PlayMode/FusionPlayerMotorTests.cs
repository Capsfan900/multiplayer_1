using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    // Native motor boundary checks; real runner prediction remains a separate integration gate.
    [TestFixture, Category("Native")]
    public sealed class FusionPlayerMotorTests
    {
        [Test]
        public void RepeatedSdkSampleCannotRepeatDashOrExtendTheInputTimeout()
        {
            var go = new GameObject("Fusion input boundary fixture");
            try
            {
                go.transform.position = Vector3.up * 20;
                var motor = go.AddComponent<ArenaMotor>();
                var state = motor.CaptureState();
                var sample = FusionInput.FromMotor(new MotorInput
                {
                    Present = true, Sequence = 23, Tick = int.MaxValue, Generation = state.Generation,
                    Move = Vector2.up, Held = MotorButtons.Dash, Pressed = MotorButtons.Dash
                });
                var input = FusionMotorInput.Schedule(true, sample, state, -1, out int cursor);
                Assert.That(motor.Simulate(input, MotorTuning.TickDelta, 0), Is.True);
                int events = motor.CaptureState().EventSequence;
                Assert.That(motor.CaptureState().AirDashUsed, Is.True);
                for (int tick = 1; tick <= 14; tick++)
                {
                    state = motor.CaptureState();
                    input = FusionMotorInput.Schedule(true, sample, state, cursor, out cursor);
                    Assert.That(motor.Simulate(input, MotorTuning.TickDelta, tick), Is.True);
                }
                state = motor.CaptureState();
                Assert.That(state.LastFreshInputTick, Is.Zero);
                Assert.That(state.LastMove, Is.EqualTo(Vector2.zero));
                Assert.That(state.Held, Is.EqualTo(MotorButtons.None));
                Assert.That(state.EventSequence, Is.EqualTo(events));
                Assert.That(cursor, Is.EqualTo(23));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
