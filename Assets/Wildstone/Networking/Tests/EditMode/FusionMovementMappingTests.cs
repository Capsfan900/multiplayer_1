using System;
using System.Linq;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using Wildstone.Movement;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    [TestFixture, Category("Managed")]
    public sealed class FusionMovementMappingTests
    {
        const BindingFlags Fields = BindingFlags.Public | BindingFlags.Instance;

        [Test]
        public void InputWireCoversEverySourceFieldWithSupportedTypes()
        {
            Assert.That(typeof(INetworkInput).IsAssignableFrom(typeof(FusionInput)), Is.True);
            AssertCoverage(typeof(MotorInput), typeof(FusionInput));
        }

        [Test]
        public void StateWireCoversEverySourceFieldWithSupportedTypes()
        {
            Assert.That(typeof(INetworkStruct).IsAssignableFrom(typeof(FusionMotorState)), Is.True);
            AssertCoverage(typeof(MotorState), typeof(FusionMotorState));
        }

        [TestCase(0), TestCase(1), TestCase(2)]
        public void InputRoundTripPreservesEveryFieldExactly(int sample)
        {
            var source = (MotorInput)Populate(typeof(MotorInput), sample);
            var wire = FusionInput.FromMotor(source);
            AssertWire(source, wire);
            AssertExact(source, wire.ToMotor());
        }

        [TestCase(0), TestCase(1), TestCase(2)]
        public void StateRoundTripPreservesEveryFieldExactly(int sample)
        {
            var source = (MotorState)Populate(typeof(MotorState), sample);
            var wire = FusionMotorState.FromMotor(source);
            AssertWire(source, wire);
            AssertExact(source, wire.ToMotor());
        }

        static void AssertCoverage(Type source, Type wire)
        {
            CollectionAssert.AreEquivalent(source.GetFields(Fields).Select(f => f.Name), wire.GetFields(Fields).Select(f => f.Name));
            Assert.That(wire.GetProperties(Fields), Is.Empty, "Use raw fields; no auto-properties or compressed network properties");
            foreach (var field in source.GetFields(Fields))
            {
                var expected = field.FieldType == typeof(bool) ? typeof(NetworkBool) : field.FieldType;
                Assert.That(wire.GetField(field.Name).FieldType, Is.EqualTo(expected), field.Name);
                Assert.That(expected == typeof(NetworkBool) || expected == typeof(int) || expected == typeof(float) ||
                    expected == typeof(double) || expected == typeof(Vector2) || expected == typeof(Vector3) || expected.IsEnum,
                    Is.True, "Review SDK support before mapping new field " + field.Name);
            }
        }

        static object Populate(Type type, int sample)
        {
            object result = Activator.CreateInstance(type);
            int index = 0;
            foreach (var field in type.GetFields(Fields)) field.SetValue(result, Sample(field.FieldType, sample, ++index));
            return result;
        }

        static object Sample(Type type, int sample, int index)
        {
            if (type == typeof(bool)) return (index + sample) % 2 == 0;
            if (type == typeof(int)) return sample == 0 ? -index : sample == 1 ? int.MaxValue - index : int.MinValue + index;
            if (type == typeof(float)) return sample == 0 ? -index * 1.234567f : sample == 1 ? float.Epsilon * index : -0f;
            if (type == typeof(double)) return sample == 0 ? -index * 1.2345678901234567 : sample == 1 ? 16777216.125 + index : -0d;
            if (type == typeof(Vector2)) return new Vector2((float)Sample(typeof(float), sample, index), index + .125f);
            if (type == typeof(Vector3)) return new Vector3((float)Sample(typeof(float), sample, index), index + .125f, -index - .875f);
            if (type.IsEnum) return Enum.ToObject(type, sample == 0 ? 0 : sample == 1 ? 1 : 7);
            throw new NotSupportedException(type.FullName);
        }

        static void AssertWire(object source, object wire)
        {
            foreach (var field in source.GetType().GetFields(Fields))
            {
                var actual = wire.GetType().GetField(field.Name).GetValue(wire);
                if (field.FieldType == typeof(bool)) actual = (bool)(NetworkBool)actual;
                AssertValue(field.GetValue(source), actual, field.Name);
            }
        }

        static void AssertExact(object expected, object actual)
        {
            foreach (var field in expected.GetType().GetFields(Fields))
                AssertValue(field.GetValue(expected), field.GetValue(actual), field.Name);
        }

        static void AssertValue(object expected, object actual, string field)
        {
            if (expected is float f) CollectionAssert.AreEqual(BitConverter.GetBytes(f), BitConverter.GetBytes((float)actual), field);
            else if (expected is double d) CollectionAssert.AreEqual(BitConverter.GetBytes(d), BitConverter.GetBytes((double)actual), field);
            else if (expected is Vector2 v2)
            {
                var value = (Vector2)actual;
                AssertValue(v2.x, value.x, field + ".x"); AssertValue(v2.y, value.y, field + ".y");
            }
            else if (expected is Vector3 v3)
            {
                var value = (Vector3)actual;
                AssertValue(v3.x, value.x, field + ".x"); AssertValue(v3.y, value.y, field + ".y"); AssertValue(v3.z, value.z, field + ".z");
            }
            else Assert.That(actual, Is.EqualTo(expected), field);
        }
    }
}
