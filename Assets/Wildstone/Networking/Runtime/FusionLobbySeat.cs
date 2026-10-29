using System;
using System.Collections.Generic;
using Fusion;
using Wildstone.Session;

namespace Wildstone.Networking
{
    public struct FusionLobbySeat : INetworkStruct
    {
        public PlayerRef Player;
        public NetworkBool Occupied, Connected, Ready;
        public int Team, Slot;
        public long ConnectionGeneration;
    }

    // Host-only binding: the legacy VerifiedSubject field stores Runner.GetPlayerUserId, an anonymous peer ID.
    // IDs can be chosen by clients; only the independent secret credential protects ownership of a held seat.
    public sealed class FusionLobbyAdmission
    {
        readonly SessionState rules;
        readonly ContentVersion content;
        readonly Dictionary<PlayerRef, SeatSnapshot> peers = new Dictionary<PlayerRef, SeatSnapshot>();
        readonly Dictionary<string, FusionCredentialHash> credentials = new Dictionary<string, FusionCredentialHash>(StringComparer.Ordinal);
        readonly HashSet<PlayerRef> freshAccounts = new HashSet<PlayerRef>();
        readonly Dictionary<string,double> vulnerableUntil = new Dictionary<string,double>(StringComparer.Ordinal);
        long generation;
        public FusionLobbyAdmission(SessionState rules, ContentVersion content) { this.rules = rules; this.content = content; }
        public bool Admit(PlayerRef player, string verifiedSubject, byte[] credential, double now)
        {
            if (player == PlayerRef.None || string.IsNullOrWhiteSpace(verifiedSubject) || verifiedSubject.Length > 128 ||
                Array.Exists(verifiedSubject.ToCharArray(), char.IsControl) || credential == null || credential.Length != 32) return false;
            rules.Tick(now);
            var snapshot = rules.CaptureSeats(now);
            if (peers.TryGetValue(player, out var current))
                return string.Equals(current.VerifiedSubject, verifiedSubject, StringComparison.Ordinal) &&
                    credentials.TryGetValue(verifiedSubject, out var currentHash) && currentHash.MatchesCredential(credential) &&
                    Array.Exists(snapshot.Seats, seat => seat.Connected && seat.VerifiedSubject == verifiedSubject &&
                        seat.ConnectionGeneration == current.ConnectionGeneration);
            foreach (var subject in new List<string>(credentials.Keys))
                if (!Array.Exists(snapshot.Seats, seat => seat.VerifiedSubject == subject))
                { credentials.Remove(subject); vulnerableUntil.Remove(subject); }
            generation = Math.Max(generation, snapshot.LastConnectionGeneration);
            if (generation >= long.MaxValue - 1) return false;
            long next = ++generation;
            foreach (var seat in snapshot.Seats)
                if (seat.VerifiedSubject == verifiedSubject)
                {
                    if (seat.Connected || !credentials.TryGetValue(verifiedSubject, out var expected) || !expected.MatchesCredential(credential) ||
                        !rules.Reconnect(verifiedSubject, next, now)) return false;
                    double deadline = seat.DisconnectedAt + SeatTable.VulnerableSeconds;
                    if (vulnerableUntil.TryGetValue(verifiedSubject, out double original)) deadline = Math.Min(deadline, original);
                    vulnerableUntil[verifiedSubject] = deadline;
                    return Remember(player, verifiedSubject, now);
                }
            if (rules.Phase != SessionPhase.Lobby && rules.Phase != SessionPhase.Running) return false;
            int team = -1, minimum = int.MaxValue;
            for (int candidate = 0; candidate < SeatTable.TeamCount; candidate++)
            {
                int count = 0;
                foreach (var seat in snapshot.Seats) if (seat.Team == candidate) count++;
                if (count < SeatTable.SlotsPerTeam && count < minimum) { team = candidate; minimum = count; }
            }
            if (team < 0 || !rules.TryAdmit(verifiedSubject, team, next, content, now)) return false;
            credentials[verifiedSubject] = FusionCredentialHash.FromCredential(credential);
            freshAccounts.Add(player);
            return Remember(player, verifiedSubject, now);
        }
        bool Remember(PlayerRef player, string subject, double now)
        {
            foreach (var seat in rules.CaptureSeats(now).Seats)
                if (seat.VerifiedSubject == subject) { peers.Add(player, seat); return true; }
            return false;
        }
        public bool IsFreshAdmission(PlayerRef player) => freshAccounts.Contains(player);
        public bool RequiresFreshLife(PlayerRef player, double now) => IsFreshAdmission(player) ||
            peers.TryGetValue(player, out var seat) && vulnerableUntil.TryGetValue(seat.VerifiedSubject, out double deadline) && now >= deadline;
        public void MarkControlRestored(PlayerRef player)
        {
            if (peers.TryGetValue(player, out var seat)) vulnerableUntil.Remove(seat.VerifiedSubject);
        }
        public bool TryGetBinding(PlayerRef player, out SeatSnapshot seat) => peers.TryGetValue(player, out seat);
        public PlayerRef PlayerFor(string subject)
        {
            foreach (var peer in peers) if (peer.Value.VerifiedSubject == subject) return peer.Key;
            return PlayerRef.None;
        }
        public bool Disconnect(PlayerRef player, double now)
        {
            if (!peers.TryGetValue(player, out var seat)) return false;
            peers.Remove(player); freshAccounts.Remove(player);
            return rules.Disconnect(seat.VerifiedSubject, seat.ConnectionGeneration, now);
        }
        public double GetVulnerableUntil(string subject) => vulnerableUntil.TryGetValue(subject, out double deadline) ? deadline : 0;
        public bool TryGetCredentialHash(string subject, out FusionCredentialHash hash) => credentials.TryGetValue(subject, out hash);
        public bool RestoreCredentials(FusionRecoverySeat[] recovery, double now)
        {
            if (recovery == null || recovery.Length > SeatTable.Capacity || peers.Count != 0) return false;
            var supplied = new Dictionary<string,FusionCredentialHash>(StringComparer.Ordinal);
            foreach (var seat in recovery)
            {
                string subject = seat.VerifiedSubject.ToString();
                if (string.IsNullOrWhiteSpace(subject) || supplied.ContainsKey(subject) || double.IsNaN(seat.VulnerableUntil) ||
                    double.IsInfinity(seat.VulnerableUntil) || seat.VulnerableUntil < 0 || seat.VulnerableUntil > now + SeatTable.VulnerableSeconds) return false;
                supplied.Add(subject, seat.CredentialHash);
            }
            var snapshot = rules.CaptureSeats(now);
            foreach (var seat in snapshot.Seats) if (!supplied.ContainsKey(seat.VerifiedSubject)) return false;
            credentials.Clear(); freshAccounts.Clear(); vulnerableUntil.Clear();
            foreach (var seat in snapshot.Seats) credentials.Add(seat.VerifiedSubject, supplied[seat.VerifiedSubject]);
            foreach (var recovered in recovery)
            {
                string subject = recovered.VerifiedSubject.ToString();
                if (recovered.VulnerableUntil != 0 && Array.Exists(snapshot.Seats, seat => seat.VerifiedSubject == subject))
                    vulnerableUntil.Add(subject, recovered.VulnerableUntil);
            }
            generation = Math.Max(generation, snapshot.LastConnectionGeneration);
            return true;
        }
    }
}
