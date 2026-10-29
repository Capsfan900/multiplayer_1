using System;
using System.Linq;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using Wildstone.Networking;
using Wildstone.Session;

namespace Wildstone.Tests
{
    public sealed class FusionRecoveryStateTests
    {
        static readonly ContentVersion Content = new ContentVersion("protocol", "build", "map", "rules");
        static byte[] Credential(byte value) { var bytes = new byte[32]; bytes[0] = value; return bytes; }

        [Test]
        public void CredentialHashUsesSha256AndNeverStoresTheReconnectSecret()
        {
            var hash = FusionCredentialHash.FromCredential(new byte[32]);
            Assert.That(hash.A, Is.EqualTo(0x77bd62f8ad7a6866UL));
            Assert.That(hash.MatchesCredential(new byte[32]), Is.True);
            Assert.That(hash.MatchesCredential(Credential(1)), Is.False);
            var changed = new byte[32]; changed[31] = 1;
            Assert.That(hash.MatchesCredential(changed), Is.False);
            Assert.That(hash.MatchesCredential(null), Is.False);
            Assert.That(hash.MatchesCredential(new byte[31]), Is.False);
            Assert.Throws<ArgumentException>(() => FusionCredentialHash.FromCredential(new byte[31]));
            Assert.That(typeof(FusionCredentialHash).GetFields().All(f => f.FieldType == typeof(ulong)), Is.True);
        }

        [Test]
        public void RecoverySeatCoversEveryCanonicalSeatFieldAndRoundTripsExactly()
        {
            const BindingFlags fields = BindingFlags.Public | BindingFlags.Instance;
            CollectionAssert.AreEquivalent(typeof(SeatSnapshot).GetFields(fields).Select(f => f.Name),
                typeof(FusionRecoverySeat).GetFields(fields).Select(f => f.Name).Except(new[] { "CredentialHash", "LoadDeadline", "VulnerableUntil" }));
            var source = new SeatSnapshot("account", 3, 1, 9123456789L, false, false, true, 30.123456789012345, 60.123456789012345);
            var hash = FusionCredentialHash.FromCredential(Credential(8));
            var wire = FusionRecoverySeat.FromSeat(source, hash, 57.25, 34.123456789);
            Assert.That(wire.LoadDeadline, Is.EqualTo(57.25));
            Assert.That(wire.VulnerableUntil, Is.EqualTo(34.123456789));
            var restored = wire.ToSeat();
            foreach (var field in typeof(SeatSnapshot).GetFields(fields)) Assert.That(field.GetValue(restored), Is.EqualTo(field.GetValue(source)), field.Name);
            Assert.That(wire.CredentialHash.MatchesCredential(Credential(8)), Is.True);
        }

        [Test]
        public void RecoveryHeaderPreservesExactContentClockDeadlineAndWatermarkMetadata()
        {
            var original = new SessionCheckpoint("match", Content, 7, 1234, SessionPhase.Running, 55.125,
                new SeatTableSnapshot(new[] { new SeatSnapshot("account", 0, 0, 12, false, false, true, 4, 34) }, 45, 30.125), 29.75, 20.125);
            var header = FusionRecoveryState.FromCheckpoint(original, 1177);
            var seats = new[] { FusionRecoverySeat.FromSeat(original.Seats.Seats[0], FusionCredentialHash.FromCredential(Credential(1))) };
            var restored = header.ToCheckpoint(seats);
            Assert.That(header.MatchTick, Is.EqualTo(1177));
            Assert.That(restored.MatchId, Is.EqualTo(original.MatchId));
            Assert.That(restored.Content.Matches(original.Content), Is.True);
            Assert.That(restored.AuthorityGeneration, Is.EqualTo(original.AuthorityGeneration));
            Assert.That(restored.SimulationTick, Is.EqualTo(original.SimulationTick));
            Assert.That(restored.Phase, Is.EqualTo(original.Phase));
            Assert.That(restored.AbsenceDeadline, Is.EqualTo(original.AbsenceDeadline));
            Assert.That(restored.OppositionStableSince, Is.EqualTo(original.OppositionStableSince));
            Assert.That(restored.AbsenceResolvedAt, Is.EqualTo(original.AbsenceResolvedAt));
            Assert.That(restored.Seats.CapturedAt, Is.EqualTo(original.Seats.CapturedAt));
            Assert.That(restored.Seats.LastConnectionGeneration, Is.EqualTo(original.Seats.LastConnectionGeneration));
            Assert.That(restored.Seats.Seats[0].HoldUntil, Is.EqualTo(34));
            Assert.Throws<ArgumentException>(() => header.ToCheckpoint(new FusionRecoverySeat[0]));
        }

        [Test]
        public void CrossPeerClockPreservesElapsedLossDetectionAndRecoveryWithoutNegativeDeadlines()
        {
            var clock = new FusionRecoveryClock(1000, 120, 480, 120, 2);
            Assert.That(clock.Now(2), Is.EqualTo(1003));
            Assert.That(clock.Now(7), Is.EqualTo(1008));
            const double originalHoldDeadline = 1012;
            Assert.That(originalHoldDeadline - clock.Now(8), Is.EqualTo(3), "The original host deadline consumes elapsed local time");
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Now(7));
        }

        [TestCase(0, 360, 120), TestCase(14, 374, 120)]
        public void ExactThreeSecondRollbackIsAccepted(long snapshot, long observed, int rate)
        {
            Assert.DoesNotThrow(() => FusionRecoveryClock.ValidateRollback(snapshot, observed, rate));
        }

        [TestCase(-1, 100, 120), TestCase(101, 100, 120), TestCase(0, 361, 120), TestCase(0, 10, 0)]
        public void MissingFutureAndStaleSnapshotsFailClosed(long snapshot, long observed, int rate)
        {
            Assert.Throws<ArgumentException>(() => FusionRecoveryClock.ValidateRollback(snapshot, observed, rate));
        }

        [Test]
        public void IsolatedClientBootstrapThenCanonicalRestoreAdvancesAuthorityOnceAndRetainsDeadlines()
        {
            var host = Running();
            Assert.That(host.Disconnect("a", 1, 4), Is.True);
            var cloud = host.CaptureCheckpoint(240, 5);
            var latest = host.CaptureCheckpoint(360, 6);
            var clock = new FusionRecoveryClock(latest.Seats.CapturedAt, latest.SimulationTick, latest.SimulationTick, 120, 1000);
            var replacement = new SessionState(Content, "match");
            Assert.That(replacement.TryInstallMigrationBaseline(latest, clock.Now(1000)), Is.True);
            Assert.That(replacement.AuthorityGeneration, Is.EqualTo(1));
            Assert.That(replacement.BeginMigration(360, 120, clock.Now(1000)), Is.True);
            Assert.That(replacement.RestoreCheckpoint(cloud, clock.Now(1019)), Is.True);
            Assert.That(replacement.AuthorityGeneration, Is.EqualTo(2));
            Assert.That(replacement.CaptureSeats(clock.Now(1019)).Seats.Single(s => s.VerifiedSubject == "a").HoldUntil, Is.EqualTo(34));
            Assert.That(replacement.AbsenceDeadline, Is.EqualTo(34));
            Assert.That(replacement.TryInstallMigrationBaseline(latest, clock.Now(1019)), Is.False, "Never overwrite an installed or live rules instance");
            replacement.Tick(clock.Now(1028));
            Assert.That(replacement.Phase, Is.EqualTo(SessionPhase.Aborted));
        }

        [Test]
        public void InvalidBootstrapDoesNotMutateTheFreshRulesInstance()
        {
            var valid = Running().CaptureCheckpoint(240, 5);
            var invalid = new SessionCheckpoint("wrong", valid.Content, valid.AuthorityGeneration, valid.SimulationTick,
                valid.Phase, valid.AbsenceDeadline, valid.Seats);
            var replacement = new SessionState(Content, "match");
            Assert.That(replacement.TryInstallMigrationBaseline(invalid, 6), Is.False);
            Assert.That(replacement.Phase, Is.EqualTo(SessionPhase.Closed));
            Assert.That(replacement.AuthorityGeneration, Is.Zero);
            Assert.That(replacement.TryInstallMigrationBaseline(valid, 6), Is.True);
            Assert.That(replacement.TryInstallMigrationBaseline(valid, 6), Is.False);
        }

        [Test]
        public void ValidationRejectsWrongContentMissingHashAndMalformedSeatBeforeRulesInstall()
        {
            var checkpoint = Running().CaptureCheckpoint(240, 5);
            var header = FusionRecoveryState.FromCheckpoint(checkpoint, 200);
            var seats = checkpoint.Seats.Seats.Select(s => FusionRecoverySeat.FromSeat(s, FusionCredentialHash.FromCredential(Credential((byte)s.ConnectionGeneration)))).ToArray();
            Assert.That(FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360).Seats.Seats.Length, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => FusionRecoveryValidation.ValidateSession(header, seats, default, "match", 360));
            seats[0].CredentialHash = default;
            Assert.Throws<ArgumentException>(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360));
            seats[0].CredentialHash = FusionCredentialHash.FromCredential(Credential(1));
            seats[0].Slot = 3;
            Assert.Throws<ArgumentException>(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360));
        }

        [Test]
        public void RecoveryLoadDeadlineIsBoundedAndNeverRestartedByTheCodec()
        {
            var checkpoint = Running().CaptureCheckpoint(240, 5);
            var header = FusionRecoveryState.FromCheckpoint(checkpoint, 200);
            var seats = checkpoint.Seats.Seats.Select(s => FusionRecoverySeat.FromSeat(s, FusionCredentialHash.FromCredential(Credential((byte)s.ConnectionGeneration)), 35)).ToArray();
            Assert.DoesNotThrow(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360));
            Assert.That(seats[0].LoadDeadline, Is.EqualTo(35));
            seats[0].LoadDeadline = 35.0001;
            Assert.Throws<ArgumentException>(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360));
            seats[0].LoadDeadline = double.NaN;
            Assert.Throws<ArgumentException>(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360));
            Assert.Throws<ArgumentOutOfRangeException>(() => FusionRecoverySeat.FromSeat(checkpoint.Seats.Seats[0], default, -1));
        }

        [Test]
        public void RecoveryVulnerabilityDeadlineRetainsExpiredOriginalAndRejectsExtendedOrMalformedWindow()
        {
            var checkpoint = Running().CaptureCheckpoint(240, 5);
            var header = FusionRecoveryState.FromCheckpoint(checkpoint, 200);
            var seats = checkpoint.Seats.Seats.Select(s => FusionRecoverySeat.FromSeat(s,
                FusionCredentialHash.FromCredential(Credential((byte)s.ConnectionGeneration)), vulnerableUntil: 10)).ToArray();
            Assert.DoesNotThrow(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360));
            Assert.That(seats[0].VulnerableUntil, Is.EqualTo(10));
            seats[0].VulnerableUntil = 4;
            Assert.DoesNotThrow(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360), "Expired original deadline is retained to force a fresh life.");
            seats[0].VulnerableUntil = 10.0001;
            Assert.Throws<ArgumentException>(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360));
            seats[0].VulnerableUntil = double.NaN;
            Assert.Throws<ArgumentException>(() => FusionRecoveryValidation.ValidateSession(header, seats, Content, "match", 360));
            Assert.Throws<ArgumentOutOfRangeException>(() => FusionRecoverySeat.FromSeat(checkpoint.Seats.Seats[0], default, vulnerableUntil: -1));
        }

        [TestCase(typeof(FusionCredentialHash)), TestCase(typeof(FusionRecoverySeat)), TestCase(typeof(FusionRecoveryState))]
        [Category("Native")]
        public void RecoveryWireIsActuallyFusionWeaved(Type type)
        {
            Assert.That(Attribute.GetCustomAttribute(type, typeof(NetworkStructWeavedAttribute)), Is.Not.Null);
        }

        static SessionState Running()
        {
            var session = new SessionState(Content, "match");
            Assert.That(session.BeginConnect(0), Is.True);
            Assert.That(session.EnterLobby(session.AttemptGeneration, Content, 1), Is.True);
            Assert.That(session.TryAdmit("a", 0, 1, Content, 1), Is.True);
            Assert.That(session.TryAdmit("b", 1, 2, Content, 1), Is.True);
            session.SetReady("a", 1, true); session.SetReady("b", 2, true);
            Assert.That(session.StartLoading(2), Is.True);
            session.SetLoaded("a", 1); session.SetLoaded("b", 2);
            Assert.That(session.FinishLoading(3), Is.True);
            session.Tick(3.2);
            return session;
        }
    }
}
