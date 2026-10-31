using System;
namespace Wildstone.Session
{
    public enum SessionPhase { Closed, Connecting, Lobby, Loading, Warmup, Running, Completed, Reconnecting, Migrating, Failed, Aborted }
    public readonly struct ContentVersion
    {
        public readonly string Protocol, Build, Map, Ruleset;
        public ContentVersion(string protocol, string build, string map, string ruleset)
        { Protocol = protocol; Build = build; Map = map; Ruleset = ruleset; }
        public bool IsValid => SessionBounds.Id(Protocol) && SessionBounds.Id(Build) && SessionBounds.Id(Map) && SessionBounds.Id(Ruleset);
        public bool Matches(ContentVersion other) => IsValid && other.IsValid && Protocol == other.Protocol && Build == other.Build && Map == other.Map && Ruleset == other.Ruleset;
    }
    public sealed class SessionCheckpoint
    {
        public readonly string MatchId;
        public readonly ContentVersion Content;
        public readonly long AuthorityGeneration, SimulationTick;
        public readonly SessionPhase Phase;
        public readonly double AbsenceDeadline, OppositionStableSince, AbsenceResolvedAt;
        public readonly SeatTableSnapshot Seats;
        public SessionCheckpoint(string matchId, ContentVersion content, long authorityGeneration, long simulationTick,
            SessionPhase phase, double absenceDeadline, SeatTableSnapshot seats,
            double oppositionStableSince = -1, double absenceResolvedAt = 0)
        { MatchId = matchId; Content = content; AuthorityGeneration = authorityGeneration; SimulationTick = simulationTick;
          Phase = phase; AbsenceDeadline = absenceDeadline; Seats = seats;
          OppositionStableSince = oppositionStableSince; AbsenceResolvedAt = absenceResolvedAt; }
    }
    // Call on the authority simulation thread. The SDK must reject callbacks from retired runner/attempt epochs;
    // gameplay commands must carry AuthorityGeneration and the peer's verified connection binding.
    public sealed class SessionState
    {
        public const double OperationTimeoutSeconds = 30, RecoveryTimeoutSeconds = 30;
        public const double SnapshotIntervalSeconds = 2, MaximumRollbackSeconds = 3, HistoryWarmupSeconds = 0.15;
        public const double OppositionStabilitySeconds = 5;
        private readonly ContentVersion content;
        private readonly string matchId;
        private SeatTable seats = new SeatTable();
        private SeatTableSnapshot migrationSeats;
        private double lastTime, phaseDeadline, migrationStarted;
        private double oppositionStableSince = -1, absenceResolvedAt;
        private long observedTick;
        private int tickRate;
        private bool recovering;
        public SessionState(ContentVersion content, string matchId)
        {
            if (!content.IsValid || !SessionBounds.Id(matchId)) throw new ArgumentException("A bounded match ID and exact content versions are required.");
            this.content = content; this.matchId = matchId;
        }
        public SessionPhase Phase { get; private set; } = SessionPhase.Closed;
        public long AttemptGeneration { get; private set; }
        public long AuthorityGeneration { get; private set; }
        public bool ScoringAllowed => Phase == SessionPhase.Running && seats.ConnectedTeamCount >= 2;
        public double AbsenceDeadline { get; private set; }

        public bool BeginConnect(double now)
        {
            if (!Time(now) || (Phase != SessionPhase.Closed && Phase != SessionPhase.Failed && Phase != SessionPhase.Aborted) ||
                AttemptGeneration >= long.MaxValue - 1 || AuthorityGeneration >= long.MaxValue - 1) return false;
            seats = new SeatTable(); AbsenceDeadline = 0;
            oppositionStableSince = -1; absenceResolvedAt = 0; migrationSeats = null;
            AttemptGeneration++; AuthorityGeneration++; recovering = false;
            Phase = SessionPhase.Connecting; phaseDeadline = now + OperationTimeoutSeconds;
            return true;
        }

        public bool EnterLobby(long attempt, ContentVersion remoteContent, double now)
        {
            if (!Time(now) || attempt != AttemptGeneration || Phase != SessionPhase.Connecting) return false;
            if (now >= phaseDeadline || !content.Matches(remoteContent)) { End(SessionPhase.Failed); return false; }
            Phase = SessionPhase.Lobby;
            return true;
        }

        public bool TryAdmit(string subject, int team, long generation, ContentVersion remoteContent, double now)
        {
            if (!Time(now) || (Phase != SessionPhase.Lobby && Phase != SessionPhase.Running) || !content.Matches(remoteContent)) return false;
            Tick(now);
            if (Phase != SessionPhase.Lobby && Phase != SessionPhase.Running) return false;
            bool joined = seats.TryJoin(subject, team, generation, now);
            EvaluateOpposition(now);
            return joined;
        }

        public bool SetReady(string subject, long generation, bool ready) => Phase == SessionPhase.Lobby && seats.TrySetReady(subject, generation, ready);
        public bool SetLoaded(string subject, long generation) => (Phase == SessionPhase.Loading || Phase == SessionPhase.Warmup || Phase == SessionPhase.Running) && seats.TrySetReady(subject, generation, true);
        public bool SelectTeam(string subject, long generation, int team) => Phase == SessionPhase.Lobby && seats.TrySelectTeam(subject, generation, team);
        public bool MarkSpawned(string subject, long generation) =>
            (Phase == SessionPhase.Warmup || Phase == SessionPhase.Running) && seats.MarkSpawned(subject, generation);

        public bool StartLoading(double now)
        {
            if (!Time(now) || Phase != SessionPhase.Lobby || seats.ConnectedTeamCount < 2 || !seats.AllConnectedReady) return false;
            seats.ClearReadiness(); Phase = SessionPhase.Loading; phaseDeadline = now + OperationTimeoutSeconds;
            return true;
        }

        public bool FinishLoading(double now)
        {
            if (!Time(now) || Phase != SessionPhase.Loading) return false;
            if (now >= phaseDeadline) { End(SessionPhase.Failed); return false; }
            if (seats.ConnectedTeamCount < 2 || !seats.AllConnectedReady) return false;
            Phase = SessionPhase.Warmup; phaseDeadline = now + HistoryWarmupSeconds;
            return true;
        }

        public void Tick(double now)
        {
            if (!Time(now)) return;
            seats.Expire(now);
            if (recovering && now >= migrationStarted + RecoveryTimeoutSeconds) { End(SessionPhase.Aborted); return; }
            if ((Phase == SessionPhase.Connecting || Phase == SessionPhase.Loading) && now >= phaseDeadline) End(SessionPhase.Failed);
            else if (Phase == SessionPhase.Migrating && now >= phaseDeadline) End(SessionPhase.Aborted);
            else if (Phase == SessionPhase.Warmup && now >= phaseDeadline && seats.ConnectedTeamCount >= 2 && (!recovering || seats.AllConnectedReady))
            { Phase = SessionPhase.Running; recovering = false; }
            EvaluateOpposition(now);
        }

        // Host rematch: the same room, seats, teams and reconnect holds return to the lobby; readiness and match deadlines restart.
        // The caller verifies the match finished normally; an aborted round has already ended the session.
        public bool ReturnToLobby(double now)
        {
            if (!Time(now) || Phase != SessionPhase.Running) return false;
            Tick(now);
            if (Phase != SessionPhase.Running) return false;
            seats.ClearReadiness(true);
            AbsenceDeadline = 0; oppositionStableSince = -1; absenceResolvedAt = 0;
            Phase = SessionPhase.Lobby;
            return true;
        }

        public void Leave() => End(SessionPhase.Closed);
        public bool Complete(long authorityGeneration)
        {
            if (!ScoringAllowed || !AcceptsAuthority(authorityGeneration)) return false;
            Phase = SessionPhase.Completed;
            return true;
        }
        public SeatTableSnapshot CaptureSeats(double now)
        {
            if (!Time(now)) throw new ArgumentOutOfRangeException(nameof(now));
            return seats.Capture(now);
        }
        public bool Disconnect(string subject, long generation, double now)
        {
            if (!Time(now) || !HasSeats || Phase == SessionPhase.Migrating) return false;
            Tick(now);
            if (!HasSeats) return false;
            bool result = seats.Disconnect(subject, generation, now);
            Tick(now);
            return result;
        }
        // The SDK integration must validate both provider identity and the session reconnect credential first.
        public bool Reconnect(string subject, long generation, double now)
        {
            if (!Time(now) || !HasSeats || Phase == SessionPhase.Migrating) return false;
            Tick(now);
            if (!HasSeats) return false;
            int previousTeams = seats.ConnectedTeamCount;
            bool result = seats.TryReconnect(subject, generation, now);
            if (result && Phase == SessionPhase.Warmup && previousTeams < 2 && seats.ConnectedTeamCount >= 2)
                phaseDeadline = now + HistoryWarmupSeconds;
            EvaluateOpposition(now);
            return result;
        }

        public SessionCheckpoint CaptureCheckpoint(long tick, double now)
        {
            if (!Time(now) || tick < 0 || (Phase != SessionPhase.Running && Phase != SessionPhase.Warmup))
                throw new InvalidOperationException("Only a live match can publish a session checkpoint.");
            Tick(now);
            if (Phase != SessionPhase.Running && Phase != SessionPhase.Warmup)
                throw new InvalidOperationException("Cannot publish a terminal session checkpoint.");
            return new SessionCheckpoint(matchId, content, AuthorityGeneration, tick, Phase, AbsenceDeadline, seats.Capture(now),
                oppositionStableSince, absenceResolvedAt);
        }

        // Bootstrap only an isolated replacement rules instance, never overwrite the retired runner's live rules.
        public bool TryInstallMigrationBaseline(SessionCheckpoint checkpoint, double now)
        {
            if (Phase != SessionPhase.Closed || AttemptGeneration != 0 || AuthorityGeneration != 0 ||
                !SessionBounds.Time(now) || checkpoint == null || checkpoint.MatchId != matchId || !content.Matches(checkpoint.Content) ||
                !SessionBounds.Generation(checkpoint.AuthorityGeneration) || checkpoint.SimulationTick < 0 ||
                (checkpoint.Phase != SessionPhase.Running && checkpoint.Phase != SessionPhase.Warmup) || checkpoint.Seats == null ||
                !SessionBounds.Time(checkpoint.Seats.CapturedAt) || checkpoint.Seats.CapturedAt > now ||
                !SessionBounds.Time(checkpoint.AbsenceDeadline) || !SessionBounds.Time(checkpoint.AbsenceResolvedAt) ||
                checkpoint.AbsenceResolvedAt > checkpoint.Seats.CapturedAt ||
                (checkpoint.AbsenceDeadline != 0 && (checkpoint.AbsenceDeadline > checkpoint.Seats.CapturedAt + SeatTable.HoldSeconds ||
                    checkpoint.AbsenceDeadline < checkpoint.AbsenceResolvedAt + SeatTable.HoldSeconds)) ||
                (checkpoint.OppositionStableSince != -1 && (!SessionBounds.Time(checkpoint.OppositionStableSince) ||
                    checkpoint.OppositionStableSince > checkpoint.Seats.CapturedAt || checkpoint.AbsenceDeadline == 0 ||
                    checkpoint.OppositionStableSince + SeatTable.HoldSeconds < checkpoint.AbsenceDeadline))) return false;
            var restored = new SeatTable();
            if (!restored.TryRestore(checkpoint.Seats, checkpoint.Seats.CapturedAt)) return false;
            seats = restored;
            AuthorityGeneration = checkpoint.AuthorityGeneration;
            AttemptGeneration = 1;
            AbsenceDeadline = checkpoint.AbsenceDeadline;
            oppositionStableSince = checkpoint.OppositionStableSince;
            absenceResolvedAt = checkpoint.AbsenceResolvedAt;
            Phase = checkpoint.Phase;
            lastTime = now;
            return true;
        }

        public bool BeginMigration(long lastObservedTick, int simulationTickRate, double now)
        {
            if (!Time(now)) return false;
            if (Phase == SessionPhase.Migrating || recovering) { End(SessionPhase.Aborted); return false; }
            Tick(now);
            if ((Phase != SessionPhase.Running && Phase != SessionPhase.Warmup) || lastObservedTick < 0 ||
                simulationTickRate < 1 || simulationTickRate > 1000 || AuthorityGeneration >= long.MaxValue - 1) return false;
            observedTick = lastObservedTick; tickRate = simulationTickRate; migrationStarted = now;
            // Retain known bindings even if their holds expire while the snapshot is in flight.
            migrationSeats = seats.Capture(now);
            oppositionStableSince = -1;
            recovering = true;
            Phase = SessionPhase.Migrating; phaseDeadline = now + RecoveryTimeoutSeconds;
            return true;
        }

        // Session-only restore: the caller must validate the entire gameplay checkpoint before committing
        // either subsystem, then clear old prediction/hitbox histories and rebind provider-verified peers.
        public bool RestoreCheckpoint(SessionCheckpoint checkpoint, double now)
        {
            if (!Time(now) || Phase != SessionPhase.Migrating) return false;
            if (now >= phaseDeadline || checkpoint == null || checkpoint.MatchId != matchId || !content.Matches(checkpoint.Content) ||
                checkpoint.AuthorityGeneration != AuthorityGeneration || checkpoint.SimulationTick < 0 || checkpoint.SimulationTick > observedTick ||
                (observedTick - checkpoint.SimulationTick) / (double)tickRate > MaximumRollbackSeconds ||
                (checkpoint.Phase != SessionPhase.Running && checkpoint.Phase != SessionPhase.Warmup) || checkpoint.Seats == null ||
                checkpoint.Seats.CapturedAt > migrationStarted || !SessionBounds.Time(checkpoint.AbsenceDeadline) ||
                !SessionBounds.Time(checkpoint.AbsenceResolvedAt) || checkpoint.AbsenceResolvedAt > checkpoint.Seats.CapturedAt ||
                (checkpoint.AbsenceDeadline != 0 && (checkpoint.AbsenceDeadline > checkpoint.Seats.CapturedAt + SeatTable.HoldSeconds ||
                    checkpoint.AbsenceDeadline < checkpoint.AbsenceResolvedAt + SeatTable.HoldSeconds)) ||
                (checkpoint.OppositionStableSince != -1 && (!SessionBounds.Time(checkpoint.OppositionStableSince) ||
                    checkpoint.OppositionStableSince > checkpoint.Seats.CapturedAt || checkpoint.AbsenceDeadline == 0 ||
                    checkpoint.OppositionStableSince + SeatTable.HoldSeconds < checkpoint.AbsenceDeadline)))
            { End(SessionPhase.Aborted); return false; }
            var restored = new SeatTable();
            if (!restored.TryRestore(checkpoint.Seats, checkpoint.Seats.CapturedAt)) { End(SessionPhase.Aborted); return false; }
            // A snapshot cannot grant control to SDK peers left over from the retired runner.
            var bindings = restored.Capture(checkpoint.Seats.CapturedAt).Seats;
            for (int i = 0; i < bindings.Length; i++)
            {
                var seat = bindings[i];
                double disconnectedAt = seat.Connected ? migrationStarted : seat.DisconnectedAt;
                foreach (var known in migrationSeats.Seats)
                    if (known.VerifiedSubject == seat.VerifiedSubject)
                    {
                        if (known.ConnectionGeneration > seat.ConnectionGeneration)
                            disconnectedAt = known.Connected ? migrationStarted : known.DisconnectedAt;
                        else if (!known.Connected) disconnectedAt = Math.Min(disconnectedAt, known.DisconnectedAt);
                    }
                bindings[i] = new SeatSnapshot(seat.VerifiedSubject, seat.Team, seat.Slot, seat.ConnectionGeneration,
                    false, false, seat.RequiresRespawn, disconnectedAt, disconnectedAt + SeatTable.HoldSeconds);
            }
            long watermark = Math.Max(migrationSeats.LastConnectionGeneration, checkpoint.Seats.LastConnectionGeneration);
            if (!restored.TryRestore(new SeatTableSnapshot(bindings, watermark, now), now))
            { End(SessionPhase.Aborted); return false; }
            seats = restored;
            AuthorityGeneration++;
            absenceResolvedAt = Math.Max(absenceResolvedAt, checkpoint.AbsenceResolvedAt);
            double savedDeadline = checkpoint.AbsenceDeadline;
            if (savedDeadline < absenceResolvedAt + SeatTable.HoldSeconds) savedDeadline = 0;
            if (AbsenceDeadline < absenceResolvedAt + SeatTable.HoldSeconds) AbsenceDeadline = 0;
            AbsenceDeadline = EarlierDeadline(AbsenceDeadline, savedDeadline);
            if (AbsenceDeadline == 0) AbsenceDeadline = migrationStarted + SeatTable.HoldSeconds;
            oppositionStableSince = -1;
            Phase = SessionPhase.Warmup; phaseDeadline = now + HistoryWarmupSeconds;
            EvaluateOpposition(now);
            return Phase != SessionPhase.Aborted;
        }

        public bool AcceptsAuthority(long generation) => generation == AuthorityGeneration &&
            (Phase == SessionPhase.Running || Phase == SessionPhase.Warmup);
        private bool HasSeats => Phase == SessionPhase.Lobby || Phase == SessionPhase.Loading ||
            Phase == SessionPhase.Warmup || Phase == SessionPhase.Running;
        private bool Time(double now)
        {
            if (!SessionBounds.Time(now) || now < lastTime) return false;
            lastTime = now;
            return true;
        }
        private void EvaluateOpposition(double now)
        {
            if (Phase != SessionPhase.Running && Phase != SessionPhase.Warmup) return;
            if (seats.ConnectedTeamCount >= 2)
            {
                if (AbsenceDeadline == 0) return;
                if (oppositionStableSince < 0) oppositionStableSince = now;
                if (now - oppositionStableSince >= OppositionStabilitySeconds)
                {
                    absenceResolvedAt = oppositionStableSince + OppositionStabilitySeconds;
                    AbsenceDeadline = 0; oppositionStableSince = -1;
                }
                return;
            }
            oppositionStableSince = -1;
            if (AbsenceDeadline == 0) AbsenceDeadline = now + SeatTable.HoldSeconds;
            if (now >= AbsenceDeadline) End(SessionPhase.Aborted);
        }
        private void End(SessionPhase terminal)
        { Phase = terminal; seats = new SeatTable(); recovering = false; }
        private static double EarlierDeadline(double a, double b) => a == 0 ? b : b == 0 ? a : Math.Min(a, b);
    }
}
