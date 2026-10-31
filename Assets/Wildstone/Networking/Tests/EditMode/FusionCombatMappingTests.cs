using System;
using System.Linq;
using System.Reflection;
using Fusion;
using Assert = NUnit.Framework.Assert;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Match;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    public sealed class FusionCombatMappingTests
    {
        const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public;

        [TestCase(typeof(CombatAction), typeof(FusionCombatAction))]
        [TestCase(typeof(PendingAttack), typeof(FusionPendingAttack))]
        [TestCase(typeof(CombatState), typeof(FusionCombatState))]
        [TestCase(typeof(ArenaProjectile), typeof(FusionProjectileState))]
        [TestCase(typeof(PickupState), typeof(FusionPickupState))]
        public void WireCoversEveryPublicField(Type source, Type wire)
        {
            Assert.That(typeof(INetworkStruct).IsAssignableFrom(wire), Is.True);
            var wireFields = wire.GetFields(Fields).Where(f => !f.IsLiteral && f.Name != "HasPending").ToArray();
            CollectionAssert.AreEquivalent(source.GetFields(Fields).Select(f => f.Name), wireFields.Select(f => f.Name));
            foreach (var field in source.GetFields(Fields))
            {
                var expected = field.FieldType;
                if (expected == typeof(bool)) expected = typeof(NetworkBool);
                else if (expected == typeof(CombatAction)) expected = typeof(FusionCombatAction);
                else if (expected == typeof(PendingAttack)) expected = typeof(FusionPendingAttack);
                else if (expected == typeof(string)) expected = typeof(NetworkString<_32>);
                Assert.That(wire.GetField(field.Name).FieldType, Is.EqualTo(expected), field.Name);
            }
        }

        [TestCase(0), TestCase(1), TestCase(2)]
        public void RoundTripIsLosslessAndCopiesNestedState(int sample)
        {
            var action = (CombatAction)Populate(typeof(CombatAction), sample);
            AssertWire(action, FusionCombatAction.FromCombat(action));
            Exact(action, FusionCombatAction.FromCombat(action).ToCombat());
            var pending = (PendingAttack)Populate(typeof(PendingAttack), sample);
            AssertWire(pending, FusionPendingAttack.FromCombat(pending));
            Exact(pending, FusionPendingAttack.FromCombat(pending).ToCombat());
            var combat = (CombatState)Populate(typeof(CombatState), sample);
            AssertWire(combat, FusionCombatState.FromCombat(combat));
            var restored = FusionCombatState.FromCombat(combat).ToCombat();
            Exact(combat, restored);
            Assert.That(restored.Pending, Is.Not.SameAs(combat.Pending));
            var projectile = (ArenaProjectile)Populate(typeof(ArenaProjectile), sample);
            AssertWire(projectile, FusionProjectileState.FromProjectile(projectile));
            Exact(projectile, FusionProjectileState.FromProjectile(projectile).ToProjectile());
            var pickup = (PickupState)Populate(typeof(PickupState), sample);
            AssertWire(pickup, FusionPickupState.FromPickup(pickup));
            Exact(pickup, FusionPickupState.FromPickup(pickup).ToPickup());
        }

        [TestCase(WeaponKind.LaserPistol)] [TestCase(WeaponKind.LaserShotgun)] [TestCase(WeaponKind.LaserSniper)]
        public void ExpandedSpecialRoundTripsWithoutChangingEnumIds(WeaponKind kind)
        {
            var player=new CombatState {Participant=1,Team=0,LifeGeneration=1,ConnectionGeneration=1,
                SpecialWeapon=kind,SpecialAmmo=WeaponCatalog.PickupAmmo(kind),SelectedSpecial=true};
            var restored=FusionCombatState.FromCombat(player).ToCombat();
            Assert.That(CombatRules.ValidPlayer(restored),Is.True);
            Assert.That(restored.SpecialWeapon,Is.EqualTo(kind)); Assert.That(restored.SpecialAmmo,Is.EqualTo(player.SpecialAmmo));
            restored.SpecialAmmo++;
            Assert.That(CombatRules.ValidPlayer(restored),Is.False);
        }
        [Test]
        public void NullPendingIsDistinctFromAnAttackWithDefaultFields()
        {
            var source = new CombatState { Pending = null };
            var wire = FusionCombatState.FromCombat(source);
            Assert.That((bool)wire.HasPending, Is.False);
            Assert.That(wire.ToCombat().Pending, Is.Null);
            source.Pending = new PendingAttack();
            wire = FusionCombatState.FromCombat(source);
            Assert.That((bool)wire.HasPending, Is.True);
            Exact(source.Pending, wire.ToCombat().Pending);
        }

        [Test]
        public void HeaderCoversEverySnapshotFieldOrBoundedCollection()
        {
            var collectionNames = new[] { "Players", "Pickups", "Projectiles", "ScoreTicks" };
            var source = typeof(MatchSnapshot).GetFields(Fields).Select(f => f.Name).Except(collectionNames);
            var wire = typeof(FusionMatchState).GetFields(Fields).Where(f => !f.IsLiteral).Select(f => f.Name)
                .Except(new[] { "PlayerCount", "PickupCount", "ProjectileCount", "Captor", "Contested" });
            CollectionAssert.AreEquivalent(source, wire);
        }

        [Test]
        public void SnapshotMetadataAndMaximumCollectionsRoundTrip()
        {
            var source = Snapshot(8, 32, 64);
            source.MapRevision = 17; source.CatalogRevision = 3; source.TickRate = 120;
            source.Tick = 567; source.StartTick = 11; source.AuthorityGeneration = 9;
            source.AbsenceSince = 523; source.OppositionStableSince = 545; source.Winner = 2;
            source.Practice = true; source.Phase = ArenaMatchPhase.Finished;
            var header = FusionMatchState.FromSnapshot(source);
            Exact(source, header.ToSnapshot(source.Players, source.Pickups, source.Projectiles, source.ScoreTicks));
            Assert.That(header.PlayerCount, Is.EqualTo(8));
            Assert.That(header.PickupCount, Is.EqualTo(32));
            Assert.That(header.ProjectileCount, Is.EqualTo(64));
            var empty = Snapshot(0, 0, 0);
            Exact(empty, FusionMatchState.FromSnapshot(empty).ToSnapshot(empty.Players, empty.Pickups, empty.Projectiles, empty.ScoreTicks));
        }

        [TestCase(9, 0, 0), TestCase(0, 33, 0), TestCase(0, 0, 65)]
        public void OversizedCollectionsAreRejected(int players, int pickups, int projectiles)
        {
            Assert.Throws<ArgumentException>(() => FusionMatchState.FromSnapshot(Snapshot(players, pickups, projectiles)));
        }

        [Test]
        public void MissingCollectionsElementsAndCountMismatchAreRejected()
        {
            var source = Snapshot(1, 1, 1);
            source.Players[0] = null;
            Assert.Throws<ArgumentException>(() => FusionMatchState.FromSnapshot(source));
            source = Snapshot(1, 1, 1); source.Pickups[0] = null;
            Assert.Throws<ArgumentException>(() => FusionMatchState.FromSnapshot(source));
            source = Snapshot(1, 1, 1); source.Projectiles[0] = null;
            Assert.Throws<ArgumentException>(() => FusionMatchState.FromSnapshot(source));
            source = Snapshot(0, 0, 0); source.ScoreTicks = new int[3];
            Assert.Throws<ArgumentException>(() => FusionMatchState.FromSnapshot(source));
            source = Snapshot(0, 0, 0); source.Players = null;
            Assert.Throws<ArgumentException>(() => FusionMatchState.FromSnapshot(source));
            var header = FusionMatchState.FromSnapshot(Snapshot(1, 0, 0));
            Assert.Throws<ArgumentException>(() => header.ToSnapshot(new CombatState[0], new PickupState[0], new ArenaProjectile[0], new int[4]));
        }

        [TestCase(null), TestCase("123456789012345678901234567890123")]
        public void InvalidTextIsRejectedBeforeFusionCanTruncate(string text)
        {
            var source = Snapshot(0, 0, 0); source.MapId = text;
            Assert.Throws<ArgumentException>(() => FusionMatchState.FromSnapshot(source));
            Assert.Throws<ArgumentException>(() => FusionPickupState.FromPickup(new PickupState { Item = text }));
        }

        [Test]
        public void UnpairedSurrogateIsRejectedAtRuntime()
        {
            InvalidTextIsRejectedBeforeFusionCanTruncate(new string((char)0xd800, 1));
        }

        [TestCase("12345678901234567890123456789012"), TestCase("arena-\ud83d\ude00")]
        public void MaximumAndUnicodeTextRoundTrip(string text)
        {
            var source = Snapshot(0, 0, 0); source.MapId = text;
            var header = FusionMatchState.FromSnapshot(source);
            Assert.That(header.MapId.ToString(), Is.EqualTo(text));
            Assert.That(FusionPickupState.FromPickup(new PickupState { Item = text }).ToPickup().Item, Is.EqualTo(text));
        }

        [TestCase(typeof(FusionCombatAction)), TestCase(typeof(FusionPendingAttack)), TestCase(typeof(FusionCombatState))]
        [TestCase(typeof(FusionProjectileState)), TestCase(typeof(FusionPickupState)), TestCase(typeof(FusionMatchState))]
        [Category("Native")]
        public void WireHasActualFusionWeavingMetadata(Type wire)
        {
            var attribute = Attribute.GetCustomAttribute(wire, typeof(NetworkStructWeavedAttribute)) as NetworkStructWeavedAttribute;
            Assert.That(attribute, Is.Not.Null, wire.Name);
            Assert.That(attribute.WordCount, Is.GreaterThan(0), wire.Name);
        }

        [Test, Category("Native")]
        public void MatchAndCollectionCapacitiesAreActuallyWeaved()
        {
            var attribute = Attribute.GetCustomAttribute(typeof(FusionMatch), typeof(NetworkBehaviourWeavedAttribute)) as NetworkBehaviourWeavedAttribute;
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.WordCount*4, Is.LessThan(32*1024-256), "Match state must fit Fusion's per-object allocation including its header");
            foreach (string property in new[] { "Players", "Pickups", "Projectiles", "Scores" })
            {
                var member = typeof(FusionMatch).GetProperty(property);
                Assert.That(Attribute.IsDefined(member, typeof(NetworkedAttribute)), Is.True, property);
                Assert.That(Attribute.IsDefined(member, typeof(CapacityAttribute)), Is.True, property);
            }
        }

        static MatchSnapshot Snapshot(int players, int pickups, int projectiles) => new MatchSnapshot
        {
            MapId = "atrium", ScoreTicks = new[] { 0, 1, 2, 3 },
            Players = Enumerable.Range(0, players).Select(_ => new CombatState()).ToArray(),
            Pickups = Enumerable.Range(0, pickups).Select(_ => new PickupState { Item = "shield" }).ToArray(),
            Projectiles = Enumerable.Range(0, projectiles).Select(_ => new ArenaProjectile()).ToArray()
        };

        static object Populate(Type type, int sample)
        {
            object value = Activator.CreateInstance(type);
            int index = 0;
            foreach (var field in type.GetFields(Fields))
            {
                ++index;
                var t = field.FieldType;
                object item;
                if (t == typeof(int)) item = sample == 0 ? -index : sample == 1 ? int.MaxValue - index : int.MinValue + index;
                else if (t == typeof(float)) item = sample == 0 ? -index * 1.234567f : sample == 1 ? float.Epsilon * index : -0f;
                else if (t == typeof(bool)) item = (sample + index) % 2 == 0;
                else if (t == typeof(Vector3)) item = new Vector3(-index * 1.234567f, float.Epsilon, -0f);
                else if (t == typeof(string)) item = "shield";
                else if (t.IsEnum) item = Enum.ToObject(t, sample + 1);
                else item = Populate(t, sample);
                field.SetValue(value, item);
            }
            return value;
        }

        static void AssertWire(object source, object wire)
        {
            foreach (var field in source.GetType().GetFields(Fields))
            {
                var expected = field.GetValue(source);
                var actual = wire.GetType().GetField(field.Name).GetValue(wire);
                if (field.FieldType == typeof(bool)) actual = (bool)(NetworkBool)actual;
                else if (field.FieldType == typeof(string)) actual = actual.ToString();
                else if (field.FieldType == typeof(PendingAttack) || field.FieldType == typeof(CombatAction))
                {
                    AssertWire(expected, actual);
                    continue;
                }
                Exact(expected, actual);
            }
        }

        static void Exact(object expected, object actual)
        {
            if (expected == null) { Assert.That(actual, Is.Null); return; }
            Assert.That(actual, Is.Not.Null);
            if (expected is float f) CollectionAssert.AreEqual(BitConverter.GetBytes(f), BitConverter.GetBytes((float)actual));
            else if (expected is Vector3 v)
            {
                var a = (Vector3)actual; Exact(v.x, a.x); Exact(v.y, a.y); Exact(v.z, a.z);
            }
            else if (expected is Array array)
            {
                var a = (Array)actual; Assert.That(a.Length, Is.EqualTo(array.Length));
                for (int i = 0; i < array.Length; i++) Exact(array.GetValue(i), a.GetValue(i));
            }
            else if (expected.GetType().IsPrimitive || expected is string || expected.GetType().IsEnum)
                Assert.That(actual, Is.EqualTo(expected));
            else foreach (var field in expected.GetType().GetFields(Fields)) Exact(field.GetValue(expected), field.GetValue(actual));
        }
    }
}
