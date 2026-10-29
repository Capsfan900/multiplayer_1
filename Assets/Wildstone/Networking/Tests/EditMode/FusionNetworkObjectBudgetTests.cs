using System;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    public sealed class FusionNetworkObjectBudgetTests
    {
        [TestCase(typeof(FusionMatch)), TestCase(typeof(FusionLobby)), TestCase(typeof(FusionPlayer))]
        [Category("Native")]
        public void CompleteNetworkObjectsRemainBelowSdkStateLimit(Type type)
        {
            var metadata = Attribute.GetCustomAttribute(type, typeof(NetworkBehaviourWeavedAttribute)) as NetworkBehaviourWeavedAttribute;
            Assert.That(metadata, Is.Not.Null, type.Name);
            int bytes = checked(metadata.WordCount * 4 + NetworkObjectHeader.SIZE);
            Assert.That(bytes, Is.LessThan(32768), type.Name + " serialized state plus SDK object header must fit the native object limit");
        }
    }
}
