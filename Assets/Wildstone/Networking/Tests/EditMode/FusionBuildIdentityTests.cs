using System;
using System.IO;
using NUnit.Framework;
using Wildstone.Networking;
using BuildIdentity=Wildstone.Networking.Editor.FusionBuildIdentity;
using Assert=NUnit.Framework.Assert;

namespace Wildstone.Tests
{
    public sealed class FusionBuildIdentityTests
    {
        [Test] public void SamePathsAndBytesAreOrderIndependentButChangedSourceOrPathChangesIdentity()
        {
            string root=Path.Combine(Path.GetTempPath(),"wildstone-fingerprint-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root,"a.cs"),"source one"); File.WriteAllText(Path.Combine(root,"b.asset"),"content");
                string first=BuildIdentity.Compute(root,new[]{"a.cs","b.asset"});
                Assert.That(BuildIdentity.Compute(root,new[]{"b.asset","a.cs"}),Is.EqualTo(first));
                Assert.That(FusionBuildIdentity.Parse(first),Is.EqualTo(first));
                File.WriteAllText(Path.Combine(root,"a.cs"),"source two");
                Assert.That(BuildIdentity.Compute(root,new[]{"a.cs","b.asset"}),Is.Not.EqualTo(first));
                File.WriteAllText(Path.Combine(root,"renamed.cs"),"source one");
                Assert.That(BuildIdentity.Compute(root,new[]{"renamed.cs","b.asset"}),Is.Not.EqualTo(first));
            }
            finally { Directory.Delete(root,true); }
        }
        [TestCase("Assets/Photon/Fusion/Resources/PhotonAppSettings.asset")]
        [TestCase("Assets/Photon/Fusion/Resources/PhotonAppSettings.asset.meta")]
        [TestCase("Assets/Wildstone/Resources/WildstoneOnline.json")]
        [TestCase("Assets/Wildstone/Resources/LocalServiceConfig.json")]
        [TestCase("Assets/Wildstone/Resources/WildstoneBuild.txt")]
        [TestCase("Assets/Wildstone/Resources/WildstoneBuild.txt.meta")]
        public void LocalProviderSettingsAndGeneratedIdentityCannotAffectContentFingerprint(string path)
        { Assert.That(BuildIdentity.IncludePath(path),Is.False); }
        [TestCase(null)] [TestCase("")] [TestCase("version-1")]
        [TestCase("fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffg")]
        public void MissingOrMalformedBuildIdentityFailsClosed(string value)
        { Assert.Throws<InvalidOperationException>(()=>FusionBuildIdentity.Parse(value)); }
    }
}
