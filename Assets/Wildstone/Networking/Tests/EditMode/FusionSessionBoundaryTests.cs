using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Photon.Realtime;
using Photon.Realtime;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using Wildstone.Networking;
using Wildstone.Session;

namespace Wildstone.Tests
{
    [TestFixture, Category("Managed")]
    public sealed class FusionSessionBoundaryTests
    {
        static ContentVersion Version => new ContentVersion("p1", "b1", "atrium1", "r1");
        static FusionAppSettings Settings => new FusionAppSettings
        { AppIdFusion = "11111111-1111-1111-1111-111111111111", FixedRegion = "us" };
        static AuthenticationValues Auth()
        {
            var auth = new AuthenticationValues { AuthType = CustomAuthenticationType.Custom };
            auth.AddAuthParameter("token", "test-only-not-a-provider-token");
            return auth;
        }
        [Test] public void AbsentProviderAndCallerIdentityDoNotAuthorizeStartup()
        {
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings, Auth(), false));
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings, null, true));
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings,
                new AuthenticationValues("unverified-caller-id"), true));
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings,
                new AuthenticationValues { AuthType = CustomAuthenticationType.Custom }, true));
        }
        [Test] public void InvalidAppRegionAndRoomAreRejectedBeforeRunnerCreation()
        {
            Assert.Throws<ArgumentException>(() => FusionSession.ValidateStart("../ROOM", Settings, Auth(), true));
            Assert.Throws<ArgumentException>(() => FusionSession.ValidateStart("ABC", Settings, Auth(), true));
            Assert.Throws<ArgumentException>(() => FusionSession.ValidateStart("ABCDEFGHIJKLM", Settings, Auth(), true));
            Assert.DoesNotThrow(() => FusionSession.ValidateStart("ABCD", Settings, Auth(), true));
            Assert.DoesNotThrow(() => FusionSession.ValidateStart("ABCDEFGHIJKL", Settings, Auth(), true));
            var settings = Settings; settings.AppIdFusion = "missing";
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", settings, Auth(), true));
            settings = Settings; settings.FixedRegion = "";
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", settings, Auth(), true));
            Assert.DoesNotThrow(() => FusionSession.ValidateStart("ROOM1", Settings, Auth(), true));
        }
        [Test] public void ContentFingerprintIncludesEveryVersionWithoutDelimiterAmbiguity()
        {
            CollectionAssert.AreEqual(FusionSession.ContentToken(Version), FusionSession.ContentToken(Version));
            foreach (var other in new[] { new ContentVersion("p2","b1","atrium1","r1"),
                new ContentVersion("p1","b2","atrium1","r1"), new ContentVersion("p1","b1","atrium2","r1"),
                new ContentVersion("p1","b1","atrium1","r2") })
                CollectionAssert.AreNotEqual(FusionSession.ContentToken(Version), FusionSession.ContentToken(other));
            CollectionAssert.AreNotEqual(FusionSession.ContentToken(new ContentVersion("a|b","c","d","e")),
                FusionSession.ContentToken(new ContentVersion("a","b|c","d","e")));
            Assert.Throws<ArgumentException>(() => FusionSession.ContentToken(default));
        }
        [Test] public void MissingWrongTypedAndMismatchedPropertiesFailClosed()
        {
            var properties = new Dictionary<string, SessionProperty>
            { ["protocol"]="p1", ["build"]="b1", ["map"]="atrium1", ["rules"]="r1" };
            Assert.That(FusionSession.MatchesProperties(properties, Version), Is.True);
            properties["map"]="different";
            Assert.That(FusionSession.MatchesProperties(properties, Version), Is.False);
            properties["map"]=5;
            Assert.That(FusionSession.MatchesProperties(properties, Version), Is.False);
            properties.Remove("map");
            Assert.That(FusionSession.MatchesProperties(properties, Version), Is.False);
            Assert.That(FusionSession.MatchesProperties(null, Version), Is.False);
        }
        [Test] public void AbsentRunnerCallbacksCannotChangeClosedSession()
        {
            var session = new FusionSession(Version,"test-match");
            session.OnShutdown(null, default);
            session.OnDisconnectedFromServer(null, default);
            session.OnConnectFailed(null, default, default);
            session.OnHostMigration(null, null);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Closed));
            Assert.That(session.Failure, Is.Null);
        }
    }
}

