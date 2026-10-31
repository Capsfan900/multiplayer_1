using System;
using System.Linq;
using System.Reflection;
using Fusion;
using Assert = NUnit.Framework.Assert;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    public sealed class FusionCombatEventTests
    {
        [Test]
        public void WireCoversAllEventFieldsExceptUnusedDiagnosticReason()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var expected = typeof(CombatEvent).GetFields(flags).Select(f => f.Name).Except(new[] { "Reason" });
            var actual = typeof(FusionCombatEvent).GetFields(flags).Select(f => f.Name).Except(new[] { "Sequence", "AuthorityGeneration" });
            CollectionAssert.AreEquivalent(expected, actual);
            Assert.That(typeof(INetworkStruct).IsAssignableFrom(typeof(FusionCombatEvent)), Is.True);
        }

        [TestCase(CombatEventKind.Accepted), TestCase(CombatEventKind.Rejected)]
        public void DiagnosticAcknowledgementsNeverOccupyPresentationRing(CombatEventKind kind)
        {
            Assert.That(FusionCombatEvent.IsPresentationEvent(kind), Is.False);
            Assert.Throws<ArgumentException>(() => FusionCombatEvent.FromCombat(new CombatEvent(kind, default, 1, reason: "diagnostic"), 1, 1));
        }

        [TestCase(CombatEventKind.Ricochet), TestCase(CombatEventKind.Deflected)]
        public void ProjectileCuesReachClients(CombatEventKind kind)
        {
            Assert.That(FusionCombatEvent.IsPresentationEvent(kind), Is.True);
        }

        [Test] public void ShotgunPelletHitIdentitiesSurviveWireAndPresentationDeduplication()
        {
            var action=new CombatAction {Participant=1,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1,Tick=1,Sequence=1,Kind=CombatActionKind.Fire,Direction=Vector3.forward};
            var history=new Wildstone.Presentation.PresentationEventHistory();
            var ids=new System.Collections.Generic.HashSet<string>();
            for(int pellet=0;pellet<8;pellet++)
            {
                var original=new CombatEvent(CombatEventKind.Hit,action,1,2,1,Vector3.forward*5,amount:9,subIndex:pellet,weapon:WeaponKind.LaserShotgun);
                var copy=FusionCombatEvent.FromCombat(original,pellet+1,1).ToCombat();
                Assert.That(copy.Id,Is.EqualTo(original.Id)); Assert.That(copy.SubIndex,Is.EqualTo(pellet));
                Assert.That(copy.Weapon,Is.EqualTo(WeaponKind.LaserShotgun));
                Assert.That(ids.Add(copy.Id),Is.True); Assert.That(history.Accept(copy),Is.True);
                Assert.That(history.Accept(copy),Is.False);
            }
        }
        [Test]
        public void EveryGameplayEventPreservesIdentityAndExactValues()
        {
            var action = new CombatAction
            {
                Participant = 8, ConnectionGeneration = 17, LifeGeneration = 91, AuthorityGeneration = 3,
                Tick = 51, Sequence = 21, Kind = CombatActionKind.Grenade, Direction = new Vector3(.25f, -.5f, .75f),
                PickupId = 4, PickupGeneration = 8
            };
            foreach (CombatEventKind kind in Enum.GetValues(typeof(CombatEventKind)))
            {
                if (!FusionCombatEvent.IsPresentationEvent(kind)) continue;
                var original = new CombatEvent(kind, action, 57, 6, 19, new Vector3(-1.234567f, float.Epsilon, -0f),
                    new Vector3(4, -5, 6), -0f, subIndex: 39, weapon: WeaponKind.Grenade);
                var wire = FusionCombatEvent.FromCombat(original, 107, 5);
                var copy = wire.ToCombat();
                Assert.That(wire.Sequence, Is.EqualTo(107));
                Assert.That(wire.AuthorityGeneration, Is.EqualTo(5), "Publication epoch differs from a committed projectile's original authority epoch");
                Assert.That(wire.Action.AuthorityGeneration, Is.EqualTo(3));
                Assert.That(copy.Id, Is.EqualTo(original.Id));
                foreach (var field in typeof(CombatEvent).GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var expected = field.GetValue(original); var actual = field.GetValue(copy);
                    if (expected is float f) CollectionAssert.AreEqual(BitConverter.GetBytes(f), BitConverter.GetBytes((float)actual), field.Name);
                    else Assert.That(actual, Is.EqualTo(expected), field.Name);
                }
            }
        }

        [Test]
        public void PresentationEventsDeliberatelyOmitDiagnosticText()
        {
            var value = new CombatEvent(CombatEventKind.Shot, default, 1, reason: "not rendered");
            Assert.That(FusionCombatEvent.FromCombat(value, 1, 1).ToCombat().Reason, Is.Null);
        }

        [TestCase(0, 0, 0), TestCase(0, 1, 1), TestCase(1, 1, 0), TestCase(5, 3, 0)]
        [TestCase(0, FusionCombatEvent.Capacity, 1), TestCase(0, FusionCombatEvent.Capacity+1, 2), TestCase(250, 520, 393)]
        [TestCase(500, 520, 501), TestCase(int.MaxValue - 1, int.MaxValue, int.MaxValue)]
        public void CursorHandlesFirstReadEvictionRepeatRollbackAndSequenceBoundary(int cursor, int latest, int expected)
        {
            Assert.That(FusionCombatEvent.FirstAvailableSequence(cursor, latest), Is.EqualTo(expected));
        }

        [TestCase(-1, 0), TestCase(0, -1)]
        public void NegativeCursorsAreRejected(int cursor, int latest)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FusionCombatEvent.FirstAvailableSequence(cursor, latest));
        }

        [TestCase(0, 1), TestCase(1, 0)]
        public void InvalidEnvelopeEpochsAreRejected(int sequence, int authority)
        {
            Assert.Throws<ArgumentException>(() => FusionCombatEvent.FromCombat(new CombatEvent(CombatEventKind.Shot, default, 1), sequence, authority));
        }

        [Test, Category("Native")]
        public void PresentationWireIsActuallyFusionWeaved()
        {
            var attribute = Attribute.GetCustomAttribute(typeof(FusionCombatEvent), typeof(NetworkStructWeavedAttribute)) as NetworkStructWeavedAttribute;
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.WordCount, Is.GreaterThan(0));
        }
    }
}
