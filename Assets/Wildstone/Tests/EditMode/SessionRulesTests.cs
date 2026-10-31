using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Wildstone.Session;

namespace Wildstone.Tests
{
    [TestFixture, Category("Managed")]
    public class SessionRulesTests
    {
        [Test]
        public void FinalSeatClaimsAreAtomicAndDuplicateAccountsNeverTakeOver()
        {
            var seats = new SeatTable();
            for (int i = 0; i < 7; i++) Assert.That(seats.TryJoin("p" + i, i / 2, i + 1, 0), Is.True);
            var claims = new bool[2];
            Parallel.Invoke(() => claims[0] = seats.TryJoin("a", 3, 8, 0),
                            () => claims[1] = seats.TryJoin("b", 3, 9, 0));
            Assert.That(claims.Count(x => x), Is.EqualTo(1));
            Assert.That(seats.Capture(0).Seats.Length, Is.EqualTo(8));
            Assert.That(seats.TryJoin("p0", 2, 2, 0), Is.False);
            Assert.That(seats.TryReconnect("p0", 2, 0), Is.False);
        }

        [Test]
        public void DisconnectSeparatesVulnerabilityHoldAndFreshRespawn()
        {
            var seats = new SeatTable();
            Assert.That(seats.TryJoin("a", 0, 1, 0), Is.True);
            Assert.That(seats.MarkSpawned("a", 1), Is.True);
            Assert.That(seats.Disconnect("a", 1, 10), Is.True);
            Assert.That(seats.Disconnect("a", 1, 20), Is.False);
            Assert.That(seats.Capture(14.99).Seats[0].HasVulnerableAvatar(14.99), Is.True);
            Assert.That(seats.Capture(15).Seats[0].HasVulnerableAvatar(15), Is.False);
            Assert.That(seats.TryReconnect("a", 1, 15), Is.False);
            Assert.That(seats.TryReconnect("a", 2, 15), Is.True);
            Assert.That(seats.Capture(15).Seats[0].RequiresRespawn, Is.True);
            Assert.That(seats.Disconnect("a", 1, 16), Is.False);
            Assert.That(seats.Disconnect("a", 2, 16), Is.True);
            Assert.That(seats.TryReconnect("a", 3, 46), Is.False);
            Assert.That(seats.Expire(46), Is.EqualTo(1));
            Assert.That(seats.TryJoin("new", 0, 3, 46), Is.True);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1)]
        public void BadTimesCannotMutateSeatState(double now)
        {
            var seats = new SeatTable();
            Assert.That(seats.TryJoin("a", 0, 1, now), Is.False);
            Assert.That(seats.Capture(0).Seats, Is.Empty);
        }

        [Test]
        public void InvalidOrDuplicateRestoreDoesNotPartiallyReplaceLiveSeats()
        {
            var seats = new SeatTable();
            seats.TryJoin("original", 0, 1, 0);
            var source = new SeatTable();
            source.TryJoin("a", 0, 1, 0);
            var item = source.Capture(0).Seats[0];
            Assert.That(seats.TryRestore(new SeatTableSnapshot(new[] { item, item }, 1, 0), 0), Is.False);
            Assert.That(seats.Capture(0).Seats[0].VerifiedSubject, Is.EqualTo("original"));
            source.Disconnect("a", 1, 1);
            Assert.That(seats.TryRestore(source.Capture(2), 30), Is.True);
            Assert.That(seats.Capture(30).Seats[0].HoldUntil, Is.EqualTo(31));
            Assert.That(seats.TryReconnect("a", 2, 31), Is.False);
        }

        private static readonly ContentVersion Content = new ContentVersion("1", "build", "courtyard", "rules");

        private static SessionState Running()
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
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Running));
            return session;
        }

        [Test]
        public void StartRequiresReadyOppositionAndExactContentAndLocksTeams()
        {
            var session = new SessionState(Content, "match");
            session.BeginConnect(0); session.EnterLobby(session.AttemptGeneration, Content, 0);
            Assert.That(session.TryAdmit("bad", 1, 1, new ContentVersion("1", "other", "courtyard", "rules"), 0), Is.False);
            session.TryAdmit("a", 0, 1, Content, 0); session.TryAdmit("b", 0, 2, Content, 0);
            session.SetReady("a", 1, true); session.SetReady("b", 2, true);
            Assert.That(session.StartLoading(1), Is.False);
            Assert.That(session.SelectTeam("b", 2, 1), Is.True);
            Assert.That(session.StartLoading(1), Is.False, "Team change clears readiness");
            session.SetReady("b", 2, true);
            Assert.That(session.StartLoading(1), Is.True);
            Assert.That(session.SelectTeam("a", 1, 3), Is.False);
            Assert.That(session.FinishLoading(2), Is.False);
            session.Tick(31);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Failed));
            Assert.That(session.CaptureSeats(31).Seats, Is.Empty);
        }

        [Test]
        public void CancelRejectsObsoleteConnectCallbackAndTimeoutReleasesSeats()
        {
            var session = new SessionState(Content, "match");
            session.BeginConnect(0);
            long oldAttempt = session.AttemptGeneration;
            session.Leave(); session.BeginConnect(1);
            Assert.That(session.EnterLobby(oldAttempt, Content, 2), Is.False);
            session.Tick(31);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Failed));
            Assert.That(session.EnterLobby(session.AttemptGeneration, Content, 31), Is.False);
        }

        [Test]
        public void OppositionReconnectLoopsDoNotExtendAbsenceDeadline()
        {
            var session = Running();
            session.Disconnect("b", 2, 4);
            Assert.That(session.ScoringAllowed, Is.False);
            Assert.That(session.AbsenceDeadline, Is.EqualTo(34));
            Assert.That(session.Reconnect("b", 3, 20), Is.True);
            Assert.That(session.ScoringAllowed, Is.True);
            session.Disconnect("b", 3, 24.9);
            Assert.That(session.AbsenceDeadline, Is.EqualTo(34));
            session.Tick(34);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Aborted));
        }

        [Test]
        public void RematchReturnsToTheLobbyWithSeatsTeamsAndHoldsButNoReadinessOrMatchDeadline()
        {
            var session = Running();
            Assert.That(session.MarkSpawned("a", 1) && session.MarkSpawned("b", 2), Is.True);
            var before = session.CaptureSeats(3.5).Seats.Select(s => (s.VerifiedSubject, s.Team, s.Slot, s.ConnectionGeneration)).ToArray();
            session.Disconnect("b", 2, 4);
            Assert.That(session.AbsenceDeadline, Is.EqualTo(34));
            Assert.That(session.ReturnToLobby(5), Is.True);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Lobby));
            Assert.That(session.ReturnToLobby(6), Is.False, "Only a running session can return to its lobby.");
            Assert.That(session.AbsenceDeadline, Is.Zero);
            var seats = session.CaptureSeats(6).Seats;
            Assert.That(seats.Select(s => (s.VerifiedSubject, s.Team, s.Slot, s.ConnectionGeneration)), Is.EqualTo(before));
            Assert.That(seats.All(s => !s.Ready && s.RequiresRespawn), Is.True, "Every seat readies again and spawns a fresh avatar.");
            Assert.That(seats.Single(s => s.VerifiedSubject == "b").HoldUntil, Is.EqualTo(34), "The reconnect hold survives the rematch.");
            Assert.That(session.Reconnect("b", 3, 20), Is.True);
            session.Tick(40);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Lobby), "The retired match's absence deadline cannot abort the lobby.");
            Assert.That(session.StartLoading(40), Is.False, "Readiness was cleared.");
            Assert.That(session.SetReady("a", 1, true) && session.SetReady("b", 3, true), Is.True);
            Assert.That(session.StartLoading(41), Is.True);
            session.SetLoaded("a", 1); session.SetLoaded("b", 3);
            Assert.That(session.FinishLoading(42), Is.True);
            session.Tick(42.2);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Running));
            Assert.That(session.CaptureSeats(42.2).Seats.Select(s => (s.VerifiedSubject, s.Team, s.Slot)), Is.EqualTo(before.Select(s => (s.VerifiedSubject, s.Team, s.Slot))));
        }

        [Test]
        public void RematchIsRefusedOutsideARunningSessionAndAfterAnAbortedRound()
        {
            var session = new SessionState(Content, "match");
            Assert.That(session.ReturnToLobby(0), Is.False);
            session.BeginConnect(0);
            Assert.That(session.ReturnToLobby(0), Is.False);
            session.EnterLobby(session.AttemptGeneration, Content, 1);
            session.TryAdmit("a", 0, 1, Content, 1); session.TryAdmit("b", 1, 2, Content, 1);
            Assert.That(session.ReturnToLobby(1), Is.False);
            session.SetReady("a", 1, true); session.SetReady("b", 2, true); session.StartLoading(2);
            Assert.That(session.ReturnToLobby(2), Is.False);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Loading));
            var running = Running();
            Assert.That(running.ReturnToLobby(double.NaN), Is.False);
            Assert.That(running.Phase, Is.EqualTo(SessionPhase.Running));
            running.Disconnect("b", 2, 4);
            Assert.That(running.ReturnToLobby(34), Is.False, "The absence deadline aborts the round first.");
            Assert.That(running.Phase, Is.EqualTo(SessionPhase.Aborted));
        }

        [Test]
        public void RecoveryPreservesDeadlinesRejectsOldAuthorityAndRequiresFreshBindings()
        {
            var session = Running();
            session.Disconnect("b", 2, 4);
            var checkpoint = session.CaptureCheckpoint(240, 5);
            Assert.That(session.BeginMigration(360, 120, 6), Is.True);
            Assert.That(session.RestoreCheckpoint(checkpoint, 7), Is.True);
            Assert.That(session.AuthorityGeneration, Is.EqualTo(2));
            Assert.That(session.AbsenceDeadline, Is.EqualTo(34));
            Assert.That(session.CaptureSeats(7).Seats.All(s => !s.Connected), Is.True);
            Assert.That(session.Reconnect("a", 3, 8), Is.True);
            Assert.That(session.Reconnect("b", 4, 8), Is.True);
            session.Tick(8.2);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Warmup), "Recovered clients must acknowledge loaded content before simulation resumes");
            Assert.That(session.SetLoaded("a",3), Is.True);
            Assert.That(session.SetLoaded("b",4), Is.True);
            session.Tick(8.21);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Running));
            Assert.That(session.AcceptsAuthority(1), Is.False);
            Assert.That(session.AcceptsAuthority(2), Is.True);
        }

        [TestCase(601, 120, 6)]
        [TestCase(360, 120, 36)]
        public void RecoveryAbortsStaleSnapshotOrExpiredAttempt(long observedTick, int tickRate, double restoreAt)
        {
            var session = Running();
            var checkpoint = session.CaptureCheckpoint(240, 5);
            session.BeginMigration(observedTick, tickRate, 6);
            Assert.That(session.RestoreCheckpoint(checkpoint, restoreAt), Is.False);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Aborted));
        }

        [Test]
        public void SecondHostLossAbortsRecovery()
        {
            var session = Running();
            session.BeginMigration(360, 120, 6);
            Assert.That(session.BeginMigration(360, 120, 7), Is.False);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Aborted));
        }

        [Test]
        public void MigrationCannotRestartHoldOrAbsenceTimersAtRestoreTime()
        {
            var session = Running();
            var checkpoint = session.CaptureCheckpoint(240, 5);
            session.BeginMigration(360, 120, 6);
            Assert.That(session.RestoreCheckpoint(checkpoint, 25), Is.True);
            Assert.That(session.CaptureSeats(25).Seats[0].HoldUntil, Is.EqualTo(36));
            Assert.That(session.AbsenceDeadline, Is.EqualTo(36));
            session.Tick(36);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Aborted));
        }

        [Test]
        public void ExpiredAccountCannotReuseRetiredGenerationAndSnapshotsAreDetached()
        {
            var seats = new SeatTable();
            seats.TryJoin("a", 0, 10, 0);
            var copy = seats.Capture(0);
            copy.Seats[0] = default;
            Assert.That(seats.Capture(0).Seats[0].VerifiedSubject, Is.EqualTo("a"));
            seats.Disconnect("a", 10, 1); seats.Expire(31);
            Assert.That(seats.TryJoin("a", 0, 10, 31), Is.False);
            var other = new SeatTable();
            Assert.That(other.TryRestore(seats.Capture(31), 31), Is.True);
            Assert.That(other.TryJoin("a", 0, 10, 31), Is.False);
            Assert.That(other.TryJoin("a", 0, 11, 31), Is.True);
        }

        [Test]
        public void MalformedSeatDeadlineOrSlotCannotEnterSnapshot()
        {
            var seats = new SeatTable();
            seats.TryJoin("original", 0, 1, 0);
            var invalid = new[] {
                new SeatSnapshot("a", 4, 0, 1, true, false, false, 0, 0),
                new SeatSnapshot("a", 0, 2, 1, true, false, false, 0, 0),
                new SeatSnapshot("a", 0, 0, 1, false, false, false, 0, double.NaN),
                new SeatSnapshot("a", 0, 0, 1, false, false, false, 0, 31),
                new SeatSnapshot("a", 0, 0, 1, false, true, false, 0, 30),
                new SeatSnapshot("a", 0, 0, 0, true, false, false, 0, 0),
                new SeatSnapshot("a\n", 0, 0, 1, true, false, false, 0, 0)
            };
            foreach (var seat in invalid)
                Assert.That(seats.TryRestore(new SeatTableSnapshot(new[] { seat }, 1, 0), 0), Is.False);
            Assert.That(seats.Capture(0).Seats.Single().VerifiedSubject, Is.EqualTo("original"));
        }

        [Test]
        public void WrongMatchOrContentOrAuthorityCannotRecover()
        {
            for (int kind = 0; kind < 3; kind++)
            {
                var session = Running();
                var good = session.CaptureCheckpoint(240, 5);
                var bad = new SessionCheckpoint(kind == 0 ? "wrong" : good.MatchId,
                    kind == 1 ? default : good.Content, kind == 2 ? 2 : good.AuthorityGeneration,
                    good.SimulationTick, good.Phase, good.AbsenceDeadline, good.Seats);
                session.BeginMigration(360, 120, 6);
                Assert.That(session.RestoreCheckpoint(bad, 7), Is.False);
                Assert.That(session.Phase, Is.EqualTo(SessionPhase.Aborted));
                Assert.That(session.CaptureSeats(7).Seats, Is.Empty);
            }
        }

        [Test]
        public void NewConnectAttemptRetiresPriorMatchAuthority()
        {
            var session = Running();
            long oldAuthority = session.AuthorityGeneration;
            session.Leave(); session.BeginConnect(10);
            Assert.That(session.AuthorityGeneration, Is.GreaterThan(oldAuthority));
        }

        [Test]
        public void RecoveryRemainsBoundedUntilPeersRebindAndHistoryWarmsUp()
        {
            var session = Running();
            var checkpoint = session.CaptureCheckpoint(240, 5);
            session.BeginMigration(360, 120, 6);
            session.RestoreCheckpoint(checkpoint, 7);
            session.Reconnect("a", 3, 35.9); session.Reconnect("b", 4, 35.9);
            session.Tick(35.9);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Warmup));
            session.Tick(36);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Aborted));
        }

        [Test]
        public void SecondHostLossDuringRecoveryWarmupAborts()
        {
            var session = Running();
            var checkpoint = session.CaptureCheckpoint(240, 5);
            session.BeginMigration(360, 120, 6); session.RestoreCheckpoint(checkpoint, 7);
            Assert.That(session.BeginMigration(360, 120, 8), Is.False);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Aborted));
        }

        [Test]
        public void AdmissionsRejectMalformedIdsTeamsAndGenerationBounds()
        {
            var seats = new SeatTable();
            foreach (var subject in new[] { null, "", " ", "bad\rname", new string('x', 129) })
                Assert.That(seats.TryJoin(subject, 0, 1, 0), Is.False);
            Assert.That(seats.TryJoin("a", -1, 1, 0), Is.False);
            Assert.That(seats.TryJoin("a", 4, 1, 0), Is.False);
            Assert.That(seats.TryJoin("a", 0, 0, 0), Is.False);
            Assert.That(seats.TryJoin("a", 0, long.MaxValue, 0), Is.False);
            Assert.That(seats.TryJoin("a", 0, 1, double.MaxValue), Is.False);
            Assert.That(seats.TryJoin("a", 0, 1, 1), Is.True);
            Assert.That(seats.Disconnect("a", 1, 0), Is.False);
            Assert.That(seats.Capture(1).Seats.Single().Connected, Is.True);
        }

        [Test]
        public void ExactlyThreeSecondsRollbackIsAccepted()
        {
            var session = Running();
            var checkpoint = session.CaptureCheckpoint(240, 5);
            session.BeginMigration(600, 120, 6);
            Assert.That(session.RestoreCheckpoint(checkpoint, 7), Is.True);
        }

        [Test]
        public void MigrationPreservesConnectionGenerationsIssuedAfterCheckpoint()
        {
            var session = Running();
            var checkpoint = session.CaptureCheckpoint(240, 5);
            session.Disconnect("b", 2, 5.2); session.Reconnect("b", 3, 5.3);
            session.BeginMigration(360, 120, 6); session.RestoreCheckpoint(checkpoint, 7);
            Assert.That(session.Reconnect("b", 3, 8), Is.False);
            Assert.That(session.Reconnect("b", 4, 8), Is.True);
            Assert.That(session.Disconnect("b", 3, 8.1), Is.False);
        }

        [Test]
        public void MigrationPreservesKnownDisconnectAfterCheckpointWithoutOppositionLoss()
        {
            var session = Running();
            session.TryAdmit("c", 2, 3, Content, 4);
            var checkpoint = session.CaptureCheckpoint(240, 5);
            session.Disconnect("b", 2, 5.5);
            session.BeginMigration(360, 120, 6); session.RestoreCheckpoint(checkpoint, 7);
            Assert.That(session.CaptureSeats(7).Seats.Single(s => s.VerifiedSubject == "b").HoldUntil, Is.EqualTo(35.5));
            session.Reconnect("a", 4, 8); session.Reconnect("c", 5, 8); session.Tick(8.2);
            Assert.That(session.Reconnect("b", 6, 35.75), Is.False);
        }

        [Test]
        public void FiveContinuousSecondsEndsEpisodeAndLaterAbsenceGetsNewDeadline()
        {
            var session = Running();
            session.Disconnect("b", 2, 4); session.Reconnect("b", 3, 20);
            Assert.That(session.ScoringAllowed, Is.True);
            session.Tick(24.999);
            Assert.That(session.AbsenceDeadline, Is.EqualTo(34));
            session.Tick(25);
            Assert.That(session.AbsenceDeadline, Is.Zero);
            session.Disconnect("b", 3, 100);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Running));
            Assert.That(session.AbsenceDeadline, Is.EqualTo(130));
        }

        [Test]
        public void LaterDisconnectAccountsForStableTimeBeforeChangingPresence()
        {
            var session = Running();
            session.Disconnect("b", 2, 4); session.Reconnect("b", 3, 20);
            session.Disconnect("b", 3, 100);
            Assert.That(session.AbsenceDeadline, Is.EqualTo(130));
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Running));
        }

        [Test]
        public void CheckpointCarriesPendingStabilityButMigrationInterruptsIt()
        {
            var session = Running();
            session.Disconnect("b", 2, 4); session.Reconnect("b", 3, 20);
            var checkpoint = session.CaptureCheckpoint(240, 22);
            Assert.That(checkpoint.OppositionStableSince, Is.EqualTo(20));
            Assert.That(checkpoint.AbsenceDeadline, Is.EqualTo(34));
            session.BeginMigration(360, 120, 23); session.RestoreCheckpoint(checkpoint, 24);
            session.Reconnect("a", 4, 25); session.Reconnect("b", 5, 25);
            session.Tick(29.999);
            Assert.That(session.AbsenceDeadline, Is.EqualTo(34));
            session.Tick(30);
            Assert.That(session.AbsenceDeadline, Is.Zero);
        }

        [Test]
        public void ResolvedEpisodeAfterCheckpointIsNotResurrectedByMigration()
        {
            var session = Running();
            session.Disconnect("b", 2, 4); session.Reconnect("b", 3, 20);
            var checkpoint = session.CaptureCheckpoint(240, 22);
            session.Tick(25);
            session.BeginMigration(360, 120, 26); session.RestoreCheckpoint(checkpoint, 27);
            Assert.That(session.AbsenceDeadline, Is.EqualTo(56));
        }

        [Test]
        public void MigrationDoesNotForgetHoldExpiredWhileWaitingForSnapshot()
        {
            var session = Running();
            session.TryAdmit("c", 2, 3, Content, 4);
            var checkpoint = session.CaptureCheckpoint(240, 5);
            session.Disconnect("b", 2, 5.5); session.BeginMigration(360, 120, 6);
            session.Tick(35.6);
            Assert.That(session.RestoreCheckpoint(checkpoint, 35.7), Is.True);
            Assert.That(session.CaptureSeats(35.7).Seats.Any(s => s.VerifiedSubject == "b"), Is.False);
            Assert.That(session.Reconnect("b", 4, 35.7), Is.False);
        }

        [Test]
        public void MalformedCheckpointStabilityCannotResumeRecovery()
        {
            foreach (double stableSince in new[] { double.NaN, -2, 8 })
            {
                var session = Running();
                var good = session.CaptureCheckpoint(240, 5);
                var bad = new SessionCheckpoint(good.MatchId, good.Content, good.AuthorityGeneration,
                    good.SimulationTick, good.Phase, 35, good.Seats, stableSince);
                session.BeginMigration(360, 120, 6);
                Assert.That(session.RestoreCheckpoint(bad, 7), Is.False);
                Assert.That(session.Phase, Is.EqualTo(SessionPhase.Aborted));
            }
        }

        [Test]
        public void ResolvedEpisodeCheckpointKeepsResolutionTime()
        {
            var session = Running();
            session.Disconnect("b", 2, 4); session.Reconnect("b", 3, 20);
            var checkpoint = session.CaptureCheckpoint(240, 25);
            Assert.That(checkpoint.AbsenceDeadline, Is.Zero);
            Assert.That(checkpoint.OppositionStableSince, Is.EqualTo(-1));
            Assert.That(checkpoint.AbsenceResolvedAt, Is.EqualTo(25));
        }

        [Test]
        public void FractionalClockCanStartAnotherEpisodeAtStableRecoveryBoundary()
        {
            var session = Running();
            session.Disconnect("b", 2, 4); session.Reconnect("b", 3, 20.05);
            session.Tick(25.05); session.Disconnect("b", 3, 25.05);
            var checkpoint = session.CaptureCheckpoint(240, 25.1);
            session.BeginMigration(360, 120, 26);
            Assert.That(session.RestoreCheckpoint(checkpoint, 27), Is.True);
            Assert.That(session.AbsenceDeadline, Is.EqualTo(55.05));
        }

        [TestCase(9, true, 37)]
        [TestCase(34.25, true, 37)]
        [TestCase(34.25, false, 38)]
        public void NewerConnectionPreservesItsCurrentHoldAcrossOlderCheckpoint(double restoreAt, bool disconnectAgain, double holdUntil)
        {
            var session = Running();
            Assert.That(session.TryAdmit("c", 2, 3, Content, 3.5), Is.True);
            session.Disconnect("b", 2, 4);
            var checkpoint = session.CaptureCheckpoint(240, 5);
            Assert.That(session.Reconnect("b", 4, 6), Is.True);
            if (disconnectAgain) session.Disconnect("b", 4, 7);
            session.BeginMigration(360, 120, 8);
            Assert.That(session.RestoreCheckpoint(checkpoint, restoreAt), Is.True);
            Assert.That(session.CaptureSeats(restoreAt).Seats.Single(s => s.VerifiedSubject == "b").HoldUntil, Is.EqualTo(holdUntil));
            session.Reconnect("a", 5, restoreAt); session.Reconnect("c", 6, restoreAt);
            session.Tick(restoreAt + 0.2);
            Assert.That(session.Reconnect("b", 7, 34.5), Is.True);
        }
    }
}

