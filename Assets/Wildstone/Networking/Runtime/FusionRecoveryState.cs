using System;
using System.Security.Cryptography;
using Fusion;
using Wildstone.Session;

namespace Wildstone.Networking
{
    public struct FusionCredentialHash : INetworkStruct
    {
        public ulong A, B, C, D;
        public static FusionCredentialHash FromCredential(byte[] credential)
        {
            if (credential == null || credential.Length != 32) throw new ArgumentException("A 32-byte reconnect credential is required.", nameof(credential));
            using (var sha = SHA256.Create())
            {
                var digest = sha.ComputeHash(credential);
                return new FusionCredentialHash
                {
                    A = Read(digest, 0), B = Read(digest, 8), C = Read(digest, 16), D = Read(digest, 24)
                };
            }
        }
        public bool MatchesCredential(byte[] credential)
        {
            if (credential == null || credential.Length != 32) return false;
            var candidate = FromCredential(credential);
            return ((A ^ candidate.A) | (B ^ candidate.B) | (C ^ candidate.C) | (D ^ candidate.D)) == 0;
        }
        static ulong Read(byte[] bytes, int offset)
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++) value |= (ulong)bytes[offset + i] << (8 * i);
            return value;
        }
    }

    public struct FusionRecoverySeat : INetworkStruct
    {
        public NetworkString<_128> VerifiedSubject;
        public int Team, Slot;
        public long ConnectionGeneration;
        public NetworkBool Connected, Ready, RequiresRespawn;
        public double DisconnectedAt, HoldUntil, LoadDeadline, VulnerableUntil;
        public FusionCredentialHash CredentialHash;

        public static FusionRecoverySeat FromSeat(SeatSnapshot seat, FusionCredentialHash credentialHash, double loadDeadline = 0, double vulnerableUntil = 0)
        {
            FusionRecoveryState.ValidateId(seat.VerifiedSubject, nameof(seat.VerifiedSubject));
            if (double.IsNaN(loadDeadline) || double.IsInfinity(loadDeadline) || loadDeadline < 0 || loadDeadline > 1e12)
                throw new ArgumentOutOfRangeException(nameof(loadDeadline));
            if (double.IsNaN(vulnerableUntil) || double.IsInfinity(vulnerableUntil) || vulnerableUntil < 0 || vulnerableUntil > 1e12)
                throw new ArgumentOutOfRangeException(nameof(vulnerableUntil));
            return new FusionRecoverySeat
            {
                VerifiedSubject = seat.VerifiedSubject, Team = seat.Team, Slot = seat.Slot,
                ConnectionGeneration = seat.ConnectionGeneration, Connected = seat.Connected,
                Ready = seat.Ready, RequiresRespawn = seat.RequiresRespawn,
                DisconnectedAt = seat.DisconnectedAt, HoldUntil = seat.HoldUntil, LoadDeadline = loadDeadline, VulnerableUntil = vulnerableUntil, CredentialHash = credentialHash
            };
        }
        public SeatSnapshot ToSeat() => new SeatSnapshot(VerifiedSubject.ToString(), Team, Slot,
            ConnectionGeneration, Connected, Ready, RequiresRespawn, DisconnectedAt, HoldUntil);
    }

    public struct FusionRecoveryState : INetworkStruct
    {
        public NetworkString<_128> MatchId, Protocol, Build, Map, Ruleset;
        public long AuthorityGeneration, SimulationTick, LastConnectionGeneration;
        public SessionPhase Phase;
        public double CapturedAt, AbsenceDeadline, OppositionStableSince, AbsenceResolvedAt;
        public int SeatCount, MatchTick;

        public static FusionRecoveryState FromCheckpoint(SessionCheckpoint checkpoint, int matchTick)
        {
            if (checkpoint == null || checkpoint.Seats == null || checkpoint.Seats.Seats == null ||
                checkpoint.Seats.Seats.Length > SeatTable.Capacity || matchTick < 0)
                throw new ArgumentException("A complete bounded recovery checkpoint is required.", nameof(checkpoint));
            ValidateId(checkpoint.MatchId, nameof(checkpoint.MatchId));
            ValidateId(checkpoint.Content.Protocol, nameof(checkpoint.Content.Protocol));
            ValidateId(checkpoint.Content.Build, nameof(checkpoint.Content.Build));
            ValidateId(checkpoint.Content.Map, nameof(checkpoint.Content.Map));
            ValidateId(checkpoint.Content.Ruleset, nameof(checkpoint.Content.Ruleset));
            return new FusionRecoveryState
            {
                MatchId = checkpoint.MatchId, Protocol = checkpoint.Content.Protocol, Build = checkpoint.Content.Build,
                Map = checkpoint.Content.Map, Ruleset = checkpoint.Content.Ruleset, AuthorityGeneration = checkpoint.AuthorityGeneration,
                SimulationTick = checkpoint.SimulationTick, LastConnectionGeneration = checkpoint.Seats.LastConnectionGeneration,
                Phase = checkpoint.Phase, CapturedAt = checkpoint.Seats.CapturedAt, AbsenceDeadline = checkpoint.AbsenceDeadline,
                OppositionStableSince = checkpoint.OppositionStableSince, AbsenceResolvedAt = checkpoint.AbsenceResolvedAt,
                SeatCount = checkpoint.Seats.Seats.Length, MatchTick = matchTick
            };
        }

        public SessionCheckpoint ToCheckpoint(FusionRecoverySeat[] seats)
        {
            if (seats == null || SeatCount < 0 || SeatCount > SeatTable.Capacity || seats.Length != SeatCount)
                throw new ArgumentException("Recovery seat count differs from the header.", nameof(seats));
            var restored = new SeatSnapshot[SeatCount];
            for (int i = 0; i < restored.Length; i++) restored[i] = seats[i].ToSeat();
            return new SessionCheckpoint(MatchId.ToString(), new ContentVersion(Protocol.ToString(), Build.ToString(), Map.ToString(), Ruleset.ToString()),
                AuthorityGeneration, SimulationTick, Phase, AbsenceDeadline,
                new SeatTableSnapshot(restored, LastConnectionGeneration, CapturedAt), OppositionStableSince, AbsenceResolvedAt);
        }

        internal static void ValidateId(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) throw new ArgumentException("Recovery IDs must fit 128 UTF-16 code units.", name);
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsControl(value[i])) throw new ArgumentException("Recovery IDs cannot contain control characters.", name);
                if (!char.IsSurrogate(value[i])) continue;
                if (!char.IsHighSurrogate(value[i]) || i + 1 == value.Length || !char.IsLowSurrogate(value[++i]))
                    throw new ArgumentException("Recovery IDs cannot contain unpaired surrogates.", name);
            }
        }
    }

    public static class FusionRecoveryValidation
    {
        public static SessionCheckpoint ValidateSession(FusionRecoveryState header, FusionRecoverySeat[] seats,
            ContentVersion expectedContent, string expectedMatchId, long lastObservedRunnerTick)
        {
            if (header.MatchTick < 0 || header.AuthorityGeneration >= int.MaxValue)
                throw new ArgumentException("Recovery match clock or authority generation is invalid.");
            FusionRecoveryClock.ValidateRollback(header.SimulationTick, lastObservedRunnerTick, 120);
            var checkpoint = header.ToCheckpoint(seats);
            var isolated = new SessionState(expectedContent, expectedMatchId);
            if (!isolated.TryInstallMigrationBaseline(checkpoint, checkpoint.Seats.CapturedAt))
                throw new ArgumentException("Recovery session metadata or identity bindings are invalid.");
            foreach (var seat in seats)
            {
                if (double.IsNaN(seat.LoadDeadline) || double.IsInfinity(seat.LoadDeadline) || seat.LoadDeadline < 0 ||
                    seat.LoadDeadline > header.CapturedAt + SessionState.OperationTimeoutSeconds)
                    throw new ArgumentException("Recovery load deadlines cannot exceed their original bounded loading window.");
                if (double.IsNaN(seat.VulnerableUntil) || double.IsInfinity(seat.VulnerableUntil) || seat.VulnerableUntil < 0 ||
                    seat.VulnerableUntil > header.CapturedAt + SeatTable.VulnerableSeconds)
                    throw new ArgumentException("Recovery vulnerability deadlines cannot exceed their original five-second window.");
                if ((seat.CredentialHash.A | seat.CredentialHash.B | seat.CredentialHash.C | seat.CredentialHash.D) == 0)
                    throw new ArgumentException("Every recovery seat requires its reconnect credential hash.");
            }
            return checkpoint;
        }
    }

    // Keep original host-clock deadlines unchanged; only convert this peer's monotonic clock into that timeline.
    public sealed class FusionRecoveryClock
    {
        readonly double hostAnchor, localAnchor;
        double lastLocal;
        public FusionRecoveryClock(double capturedAt, long snapshotRunnerTick, long lastObservedRunnerTick, int tickRate, double localAnchorTime)
        {
            ValidateRollback(snapshotRunnerTick, lastObservedRunnerTick, tickRate);
            if (!ValidTime(capturedAt) || !ValidTime(localAnchorTime)) throw new ArgumentOutOfRangeException("Recovery clock anchors must be finite and non-negative.");
            hostAnchor = capturedAt + (lastObservedRunnerTick - snapshotRunnerTick) / (double)tickRate;
            if (!ValidTime(hostAnchor)) throw new ArgumentOutOfRangeException(nameof(capturedAt));
            localAnchor = lastLocal = localAnchorTime;
        }
        // localAnchorTime is when the authoritative sample was first observed, so loss detection and recovery consume deadlines.
        public double Now(double localRealtime)
        {
            if (!ValidTime(localRealtime) || localRealtime < lastLocal) throw new ArgumentOutOfRangeException(nameof(localRealtime));
            double now = hostAnchor + localRealtime - localAnchor;
            if (!ValidTime(now)) throw new ArgumentOutOfRangeException(nameof(localRealtime));
            lastLocal = localRealtime;
            return now;
        }
        public static void ValidateRollback(long snapshotRunnerTick, long lastObservedRunnerTick, int tickRate)
        {
            if (snapshotRunnerTick < 0 || lastObservedRunnerTick < snapshotRunnerTick || tickRate < 1 || tickRate > 1000 ||
                (lastObservedRunnerTick - snapshotRunnerTick) / (double)tickRate > SessionState.MaximumRollbackSeconds)
                throw new ArgumentException("Recovery snapshot is missing, future-dated or older than the allowed rollback.");
        }
        static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 1e12;
    }
}
