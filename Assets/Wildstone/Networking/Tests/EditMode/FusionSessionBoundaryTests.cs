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
        static AuthenticationValues Auth() => new AuthenticationValues("11111111111141118111111111111111");
        [Test] public void AnonymousGuestStartupRejectsMissingMalformedAndCustomAuthentication()
        {
            Assert.DoesNotThrow(() => FusionSession.ValidateStart("ROOM1", Settings, Auth()));
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings, null));
            foreach(var id in new[]{"", "display-name", "11111111-1111-4111-8111-111111111111",new string('0',32)})
                Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings,new AuthenticationValues(id)));
            var custom=Auth(); custom.AuthType=CustomAuthenticationType.Custom;
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings,custom));
            var parameters=Auth(); parameters.AddAuthParameter("token","test-only");
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings,parameters));
            var post=Auth(); post.SetAuthPostData("test-only");
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", Settings,post));
            Assert.DoesNotThrow(() => FusionSession.ValidateStart("ROOM1", Settings,Auth()),"Anonymous Photon needs no external account or provider configuration.");
        }
        [Test] public void InvalidAppRegionAndRoomAreRejectedBeforeRunnerCreation()
        {
            Assert.Throws<ArgumentException>(() => FusionSession.ValidateStart("../ROOM", Settings, Auth()));
            Assert.Throws<ArgumentException>(() => FusionSession.ValidateStart("ABC", Settings, Auth()));
            Assert.Throws<ArgumentException>(() => FusionSession.ValidateStart("ABCDEFGHIJKLM", Settings, Auth()));
            Assert.DoesNotThrow(() => FusionSession.ValidateStart("ABCD", Settings, Auth()));
            Assert.DoesNotThrow(() => FusionSession.ValidateStart("ABCDEFGHIJKL", Settings, Auth()));
            var settings = Settings; settings.AppIdFusion = "missing";
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", settings, Auth()));
            settings = Settings; settings.FixedRegion = "";
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateStart("ROOM1", settings, Auth()));
            Assert.DoesNotThrow(() => FusionSession.ValidateStart("ROOM1", Settings, Auth()));
        }
        [Test] public void OnlyAnActiveClientTimeoutWaitsForSdkMigrationInsteadOfPreemptivelyClosingCloud()
        {
            Assert.That(FusionSession.CanAwaitMigrationToken(Fusion.Sockets.NetDisconnectReason.Timeout,false,SessionPhase.Running),Is.True);
            Assert.That(FusionSession.CanAwaitMigrationToken(Fusion.Sockets.NetDisconnectReason.Timeout,false,SessionPhase.Warmup),Is.True);
            Assert.That(FusionSession.CanAwaitMigrationToken(Fusion.Sockets.NetDisconnectReason.Timeout,true,SessionPhase.Running),Is.False);
            Assert.That(FusionSession.CanAwaitMigrationToken(Fusion.Sockets.NetDisconnectReason.Timeout,false,SessionPhase.Lobby),Is.False);
            Assert.That(FusionSession.CanAwaitMigrationToken(Fusion.Sockets.NetDisconnectReason.ByRemote,false,SessionPhase.Running),Is.False,"Server kicks must keep immediate reconnect availability.");
            Assert.That(FusionSession.CanAwaitMigrationToken(Fusion.Sockets.NetDisconnectReason.Requested,false,SessionPhase.Running),Is.False);
            Assert.That(FusionSession.CanAwaitMigrationToken(Fusion.Sockets.NetDisconnectReason.ProtocolError,false,SessionPhase.Running),Is.False);
        }
        [Test] public void RecoveryDiagnosticsExposeLocalFailureWithoutLoggingExternalCredentials()
        {
            var external=new InvalidOperationException("secret-token-and-guest-id");
            string hidden=FusionSession.FormatRecoveryFailure("start-replacement",external);
            StringAssert.Contains("stage=start-replacement",hidden); StringAssert.Contains("InvalidOperationException",hidden);
            StringAssert.DoesNotContain("secret-token",hidden);
            try { FusionRecoveryState.FromCheckpoint(null,0); Assert.Fail("Expected invalid checkpoint."); }
            catch(ArgumentException exception)
            {
                string local=FusionSession.FormatRecoveryFailure("validate-resume",exception);
                StringAssert.Contains("complete bounded recovery checkpoint",local);
                StringAssert.Contains("FusionRecoveryState",local);
            }
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
            session.OnDisconnectedFromServer(null,Fusion.Sockets.NetDisconnectReason.Timeout);
            session.OnConnectFailed(null, default, default);
            session.OnHostMigration(null, null);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Closed));
            Assert.That(session.Failure, Is.Null);
        }
    }
}

