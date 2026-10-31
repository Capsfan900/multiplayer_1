using NUnit.Framework;
using Wildstone.Combat;
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

        // A pickup resent after 2% loss arrived 21 ticks old and vanished (adverse gunplay smoke): inventory actions,
        // judged on current host state, get 500 ms; attacks keep 150 ms.
        [TestCase(CombatActionKind.Pickup,60,true),TestCase(CombatActionKind.Pickup,61,false)]
        [TestCase(CombatActionKind.Reload,21,true),TestCase(CombatActionKind.SelectStarter,60,true),TestCase(CombatActionKind.SelectSpecial,60,true)]
        [TestCase(CombatActionKind.Fire,19,false),TestCase(CombatActionKind.Grenade,19,false)]
        [TestCase(CombatActionKind.Blink,19,false),TestCase(CombatActionKind.Shield,19,false),TestCase(CombatActionKind.Shield,18,true)]
        [TestCase(CombatActionKind.Pickup,-1,false)]
        public void InventoryActionsSurviveAResendButAttacksKeepTheirWindow(CombatActionKind kind,int age,bool accepted)
        {
            Assert.That(FusionPlayer.AcceptsCombatTick(1000,1000-age,kind),Is.EqualTo(accepted));
        }
    }
}
