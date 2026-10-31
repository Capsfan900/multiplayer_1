using System;
using System.Collections.Generic;

namespace Wildstone.Session
{
    public readonly struct SeatSnapshot
    {
        public readonly string VerifiedSubject;
        public readonly int Team, Slot;
        public readonly long ConnectionGeneration;
        public readonly bool Connected, Ready, RequiresRespawn;
        public readonly double DisconnectedAt, HoldUntil;

        public SeatSnapshot(string verifiedSubject, int team, int slot, long connectionGeneration,
            bool connected, bool ready, bool requiresRespawn, double disconnectedAt, double holdUntil)
        {
            VerifiedSubject = verifiedSubject; Team = team; Slot = slot;
            ConnectionGeneration = connectionGeneration; Connected = connected; Ready = ready;
            RequiresRespawn = requiresRespawn; DisconnectedAt = disconnectedAt; HoldUntil = holdUntil;
        }

        public bool HasVulnerableAvatar(double now) => SessionBounds.Time(now) &&
            !RequiresRespawn && (Connected || now >= DisconnectedAt && now < DisconnectedAt + SeatTable.VulnerableSeconds);
    }

    public sealed class SeatTableSnapshot
    {
        public readonly SeatSnapshot[] Seats;
        public readonly long LastConnectionGeneration;
        public readonly double CapturedAt;
        public SeatTableSnapshot(SeatSnapshot[] seats, long lastConnectionGeneration, double capturedAt)
        { Seats = seats; LastConnectionGeneration = lastConnectionGeneration; CapturedAt = capturedAt; }
    }

    internal static class SessionBounds
    {
        public static bool Time(double value) => !double.IsNaN(value) && value >= 0 && value <= 1e12;
        public static bool Id(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
            Array.TrueForAll(value.ToCharArray(), c => !char.IsControl(c));
        public static bool Generation(long value) => value > 0 && value < long.MaxValue;
    }

    // Host-only rules. The future provider/SDK integration must verify the subject and reconnect
    // credential before calling these methods; an RPC-supplied string is never authentication.
    public sealed class SeatTable
    {
        public const int TeamCount = 4, SlotsPerTeam = 2, Capacity = TeamCount * SlotsPerTeam;
        public const double VulnerableSeconds = 5, HoldSeconds = 30;
        private readonly object gate = new object();
        private readonly List<SeatSnapshot> seats = new List<SeatSnapshot>(Capacity);
        private long lastGeneration;
        private double lastTime;

        public int ConnectedTeamCount
        {
            get
            {
                lock (gate)
                {
                    int mask = 0, count = 0;
                    foreach (var s in seats) if (s.Connected) mask |= 1 << s.Team;
                    for (int team = 0; team < TeamCount; team++) if ((mask & (1 << team)) != 0) count++;
                    return count;
                }
            }
        }

        public bool AllConnectedReady
        {
            get
            {
                lock (gate)
                {
                    bool any = false;
                    foreach (var s in seats) if (s.Connected) { any = true; if (!s.Ready) return false; }
                    return any;
                }
            }
        }

        // requireRespawn: a rematch retires every avatar, so each seat spawns fresh like a new admission.
        public void ClearReadiness(bool requireRespawn = false)
        {
            lock (gate)
                for (int i = 0; i < seats.Count; i++)
                {
                    var s = seats[i];
                    seats[i] = new SeatSnapshot(s.VerifiedSubject, s.Team, s.Slot, s.ConnectionGeneration,
                        s.Connected, false, s.RequiresRespawn || requireRespawn, s.DisconnectedAt, s.HoldUntil);
                }
        }

        public bool TryJoin(string verifiedSubject, int preferredTeam, long connectionGeneration, double now)
        {
            lock (gate)
            {
                if (!ValidTime(now) || !SessionBounds.Id(verifiedSubject) || preferredTeam < 0 || preferredTeam >= TeamCount ||
                    !FreshGeneration(connectionGeneration) || Find(verifiedSubject) >= 0) return false;
                int slot = FreeSlot(preferredTeam);
                if (slot < 0) return false;
                seats.Add(new SeatSnapshot(verifiedSubject, preferredTeam, slot, connectionGeneration, true, false, true, 0, 0));
                lastGeneration = connectionGeneration; lastTime = now;
                return true;
            }
        }

        public bool Disconnect(string verifiedSubject, long connectionGeneration, double now)
        {
            lock (gate)
            {
                int index = Find(verifiedSubject);
                if (!ValidTime(now) || index < 0) return false;
                var seat = seats[index];
                if (!seat.Connected || seat.ConnectionGeneration != connectionGeneration) return false;
                seats[index] = new SeatSnapshot(seat.VerifiedSubject, seat.Team, seat.Slot, seat.ConnectionGeneration,
                    false, false, seat.RequiresRespawn, now, now + HoldSeconds);
                lastTime = now;
                return true;
            }
        }

        public bool TryReconnect(string verifiedSubject, long connectionGeneration, double now)
        {
            lock (gate)
            {
                int index = Find(verifiedSubject);
                if (!ValidTime(now) || !FreshGeneration(connectionGeneration) || index < 0) return false;
                var seat = seats[index];
                if (seat.Connected || now >= seat.HoldUntil) return false;
                seats[index] = new SeatSnapshot(seat.VerifiedSubject, seat.Team, seat.Slot, connectionGeneration,
                    true, false, seat.RequiresRespawn || now >= seat.DisconnectedAt + VulnerableSeconds, 0, 0);
                lastGeneration = connectionGeneration; lastTime = now;
                return true;
            }
        }

        public bool TrySetReady(string verifiedSubject, long generation, bool ready)
        {
            lock (gate)
            {
                int index = Find(verifiedSubject);
                if (index < 0 || !seats[index].Connected || seats[index].ConnectionGeneration != generation) return false;
                var s = seats[index];
                seats[index] = new SeatSnapshot(s.VerifiedSubject, s.Team, s.Slot, generation, true, ready, s.RequiresRespawn, 0, 0);
                return true;
            }
        }

        public bool TrySelectTeam(string verifiedSubject, long generation, int team)
        {
            lock (gate)
            {
                int index = Find(verifiedSubject);
                if (index < 0 || team < 0 || team >= TeamCount) return false;
                var s = seats[index];
                if (!s.Connected || s.ConnectionGeneration != generation) return false;
                if (s.Team == team) return true;
                int slot = FreeSlot(team);
                if (slot < 0) return false;
                seats[index] = new SeatSnapshot(s.VerifiedSubject, team, slot, generation, true, false, s.RequiresRespawn, 0, 0);
                return true;
            }
        }

        public bool MarkSpawned(string verifiedSubject, long generation)
        {
            lock (gate)
            {
                int index = Find(verifiedSubject);
                if (index < 0 || !seats[index].Connected || seats[index].ConnectionGeneration != generation) return false;
                var s = seats[index];
                seats[index] = new SeatSnapshot(s.VerifiedSubject, s.Team, s.Slot, generation, true, s.Ready, false, 0, 0);
                return true;
            }
        }

        public SeatTableSnapshot Capture(double now)
        {
            lock (gate)
            {
                if (!ValidTime(now)) throw new ArgumentOutOfRangeException(nameof(now));
                lastTime = now;
                return new SeatTableSnapshot(seats.ToArray(), lastGeneration, now);
            }
        }

        public int Expire(double now)
        {
            lock (gate)
            {
                if (!ValidTime(now)) return 0;
                lastTime = now;
                int removed = 0;
                for (int i = seats.Count - 1; i >= 0; i--)
                    if (!seats[i].Connected && now >= seats[i].HoldUntil) { seats.RemoveAt(i); removed++; }
                return removed;
            }
        }

        public bool TryRestore(SeatTableSnapshot snapshot, double now)
        {
            lock (gate)
            {
                if (!ValidTime(now) || snapshot == null || snapshot.Seats == null || snapshot.Seats.Length > Capacity ||
                    !SessionBounds.Time(snapshot.CapturedAt) || snapshot.CapturedAt > now ||
                    snapshot.LastConnectionGeneration < 0 || snapshot.LastConnectionGeneration == long.MaxValue) return false;
                var restored = (SeatSnapshot[])snapshot.Seats.Clone();
                var subjects = new HashSet<string>(StringComparer.Ordinal);
                var slots = new HashSet<int>();
                var generations = new HashSet<long>();
                foreach (var s in restored)
                {
                    if (!SessionBounds.Id(s.VerifiedSubject) || s.Team < 0 || s.Team >= TeamCount || s.Slot < 0 || s.Slot >= SlotsPerTeam ||
                        !SessionBounds.Generation(s.ConnectionGeneration) || s.ConnectionGeneration > snapshot.LastConnectionGeneration ||
                        !subjects.Add(s.VerifiedSubject) || !slots.Add(s.Team * SlotsPerTeam + s.Slot) || !generations.Add(s.ConnectionGeneration)) return false;
                    if (s.Connected)
                    { if (s.DisconnectedAt != 0 || s.HoldUntil != 0) return false; }
                    else if (s.Ready || !SessionBounds.Time(s.DisconnectedAt) || s.DisconnectedAt > snapshot.CapturedAt ||
                        s.HoldUntil != s.DisconnectedAt + HoldSeconds) return false;
                }
                seats.Clear();
                foreach (var s in restored) if (s.Connected || now < s.HoldUntil) seats.Add(s);
                lastGeneration = Math.Max(lastGeneration, snapshot.LastConnectionGeneration);
                lastTime = now;
                return true;
            }
        }

        private bool ValidTime(double now) => SessionBounds.Time(now) && now >= lastTime;
        private bool FreshGeneration(long generation) => SessionBounds.Generation(generation) && generation > lastGeneration;
        private int Find(string subject) => seats.FindIndex(s => string.Equals(s.VerifiedSubject, subject, StringComparison.Ordinal));
        private int FreeSlot(int team)
        {
            for (int slot = 0; slot < SlotsPerTeam; slot++)
                if (!seats.Exists(s => s.Team == team && s.Slot == slot)) return slot;
            return -1;
        }
    }
}
