using System;
using NUnit.Framework;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    public sealed class FusionPredictionBootstrapTests
    {
        [TestCase(9,16,319,328,6)]
        [TestCase(9,16,-9,0,6)]
        [TestCase(-1,0,100,99,0)]
        public void NewPredictionUsesConfirmedSnapshotWithoutChangingItsOrigin(int saved,int target,int origin,int confirmed,int expected)
        {
            Assert.That(FusionPlayer.InitialPredictionBootstrapTicks(saved,target,origin,confirmed,true),Is.EqualTo(expected));
        }
        [TestCase(9,16,319,327)]
        [TestCase(9,371,319,328)]
        [TestCase(-2,1,100,100)]
        [TestCase(0,2,-1,0)]
        public void FutureInvalidAndOverBudgetInitialCheckpointsAreRejected(int saved,int target,int origin,int confirmed)
        {
            Assert.Throws<InvalidOperationException>(()=>FusionPlayer.InitialPredictionBootstrapTicks(saved,target,origin,confirmed,true));
        }
        [Test]
        public void OrdinaryHostResimulationAndSubsequentTicksCannotUseBootstrapCatchup()
        {
            Assert.That(FusionPlayer.InitialPredictionBootstrapTicks(9,16,319,328,false),Is.Zero);
            Assert.That(FusionPlayer.InitialPredictionBootstrapTicks(16,16,319,335,true),Is.Zero,"A snapshot from the future must still reach the strict sequential check.");
        }
    }
}
