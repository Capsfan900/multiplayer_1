using NUnit.Framework;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    public sealed class FusionCombatTickTests
    {
        [TestCase(120,120,true),TestCase(120,102,true),TestCase(120,101,false)]
        [TestCase(120,121,false),TestCase(0,-1,false),TestCase(int.MaxValue,0,false)]
        public void CombatRpcRejectsExpiredFutureAndOverflowingSdkTimestamps(int now,int sent,bool accepted)
        {
            Assert.That(FusionPlayer.AcceptsCombatTick(now,sent),Is.EqualTo(accepted));
        }
    }
}
