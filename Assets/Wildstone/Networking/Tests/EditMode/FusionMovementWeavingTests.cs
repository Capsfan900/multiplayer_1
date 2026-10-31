using System;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    // Run after integration in Unity with this assembly registered for Fusion weaving.
    [TestFixture, Category("Native")]
    public sealed class FusionMovementWeavingTests
    {
        [Test]
        public void InputHasActualFusionWeavingMetadata()
        {
            var metadata = Attribute.GetCustomAttribute(typeof(FusionInput), typeof(NetworkInputWeavedAttribute)) as NetworkInputWeavedAttribute;
            Assert.That(metadata, Is.Not.Null, "Fusion must weave the movement wire assembly");
            Assert.That(metadata.WordCount, Is.EqualTo(10));
        }

        [Test]
        public void StateHasActualFusionWeavingMetadata()
        {
            var metadata = Attribute.GetCustomAttribute(typeof(FusionMotorState), typeof(NetworkStructWeavedAttribute)) as NetworkStructWeavedAttribute;
            Assert.That(metadata, Is.Not.Null, "Fusion must weave the movement wire assembly");
            Assert.That(metadata.WordCount, Is.EqualTo(100));
        }

        [Test]
        public void PlayerRollbackFieldsHaveActualFusionWeavingMetadata()
        {
            var metadata = Attribute.GetCustomAttribute(typeof(FusionPlayer), typeof(NetworkBehaviourWeavedAttribute)) as NetworkBehaviourWeavedAttribute;
            Assert.That(metadata, Is.Not.Null, "Fusion must weave the player behaviour");
            Assert.That(metadata.WordCount, Is.EqualTo(106));
        }
    }
}

