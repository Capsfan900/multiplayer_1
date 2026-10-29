using System;
using NUnit.Framework;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    public sealed class FusionOrbStateTests
    {
        [Test]
        public void OneWinnerPerRunnerTickAndExactCooldownBoundary()
        {
            var state = new FusionOrbState { Id = 71 };
            Assert.That(state.TryConsume(100, out int first), Is.True);
            Assert.That(first, Is.EqualTo(1));
            Assert.That(state.TryConsume(100, out _), Is.False);
            Assert.That(state.TryConsume(399, out _), Is.False);
            Assert.That(state.TryConsume(400, out int second), Is.True);
            Assert.That(second, Is.EqualTo(2));
        }
        [Test]
        public void RestoringWholeWireStateReplaysIdenticalConsumption()
        {
            var before = new FusionOrbState { Id = 71, ReadyTick = 120, ActionSequence = 9 };
            var replay = before;
            Assert.That(before.TryConsume(120, out int first), Is.True);
            Assert.That(replay.TryConsume(120, out int second), Is.True);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(replay.ReadyTick, Is.EqualTo(before.ReadyTick));
            Assert.That(replay.ActionSequence, Is.EqualTo(before.ActionSequence));
        }
        [Test]
        public void MigrationPreservesRemainingCooldownAndActionSequenceAcrossRunnerOrigins()
        {
            var state = new FusionOrbState { Id = 71, ReadyTick = 1100, ActionSequence = 19 };
            state.Rebase(1000, 10);
            Assert.That(state.ReadyTick, Is.EqualTo(110));
            Assert.That(state.TryConsume(109, out _), Is.False);
            Assert.That(state.TryConsume(110, out int action), Is.True);
            Assert.That(action, Is.EqualTo(20));
        }
        [Test]
        public void InvalidAndExhaustedStateCannotConsumeOrWrap()
        {
            var invalid = new FusionOrbState();
            Assert.That(invalid.TryConsume(0, out _), Is.False);
            var exhausted = new FusionOrbState { Id = 71, ActionSequence = int.MaxValue };
            Assert.That(exhausted.TryConsume(0, out _), Is.False);
            var late = new FusionOrbState { Id = 71 };
            Assert.That(late.TryConsume(int.MaxValue, out _), Is.False);
            Assert.Throws<ArgumentException>(() => invalid.Rebase(0, 0));
        }
    }
}
