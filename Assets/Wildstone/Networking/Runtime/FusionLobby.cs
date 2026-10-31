using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Wildstone.Session;

namespace Wildstone.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FusionLobby : NetworkBehaviour
    {
        [Networked] public SessionPhase Phase { get; set; }
        [Networked] public PlayerRef HostPlayer { get; set; }
        [Networked] public int RosterRevision { get; set; }
        [Networked] public NetworkBool FinalFrozen { get; set; }
        [Networked] public FusionRecoveryState RecoveryHeader { get; set; }
        [Networked, Capacity(SeatTable.Capacity)] public NetworkArray<FusionRecoverySeat> RecoverySeats => default;
        [Networked, Capacity(SeatTable.Capacity)] public NetworkArray<FusionLobbySeat> Seats => default;
        public SessionState HostRules => HasStateAuthority ? owner?.Rules : null;
        public FusionLobbyAdmission Admission { get; private set; }
        public event Action Changed;
        public event Action<PlayerRef> PlayerDisconnected;
        FusionSession owner;
        SessionPhase observedPhase;
        int observedRosterRevision = -1;
        long observedRecoveryTick = -1;
        double nextHoldExpiry = double.PositiveInfinity;
        readonly Dictionary<string,double> loadDeadlines = new Dictionary<string,double>(StringComparer.Ordinal);
        readonly List<PlayerRef> expiredLoads = new List<PlayerRef>(SeatTable.Capacity);
        // The SDK can raise PlayerJoined before it knows the peer's user ID; that peer is retried briefly instead of removed.
        public const string IdentityPending = "identity-pending";
        internal const double IdentityWaitSeconds = 5;
        readonly Dictionary<PlayerRef,double> identityWait = new Dictionary<PlayerRef,double>();
        readonly List<PlayerRef> identityRetry = new List<PlayerRef>(SeatTable.Capacity);
        internal static bool AwaitIdentity(string subject, bool local, double now, double deadline) =>
            string.IsNullOrEmpty(subject) && !local && now < deadline;
        public override void Spawned()
        {
            DontDestroyOnLoad(gameObject);
            owner = FusionSession.OwnerOf(Runner);
            if (owner == null) throw new InvalidOperationException("Lobby runner has no session owner.");
            if (HasStateAuthority && Admission == null)
            {
                HostPlayer = Runner.LocalPlayer;
                Admission = new FusionLobbyAdmission(owner.Rules, owner.Content);
            }
            owner.AttachLobby(this);
        }
        // Reason-only code for the last refused Admit (see FusionLobbyAdmission.Rejection).
        public string Rejection { get; private set; }
        public bool Admit(PlayerRef player)
        {
            Rejection = null;
            if (!HasStateAuthority || Admission == null) { Rejection = "lobby-not-ready"; return false; }
            double now = owner.Now;
            string subject = Runner.GetPlayerUserId(player);
            if (!identityWait.TryGetValue(player, out double identityDeadline)) identityDeadline = now + IdentityWaitSeconds;
            if (AwaitIdentity(subject, player == Runner.LocalPlayer, now, identityDeadline))
            { identityWait[player] = identityDeadline; Rejection = IdentityPending; return false; }
            identityWait.Remove(player);
            if (!string.IsNullOrEmpty(subject) && loadDeadlines.TryGetValue(subject, out double originalDeadline) && now >= originalDeadline)
            { Rejection = "load-timeout"; return false; }
            bool alreadyBound = Admission.TryGetBinding(player, out _);
            var token = player == Runner.LocalPlayer ? owner.ConnectionToken : Runner.GetPlayerConnectionToken(player);
            if (!FusionSession.TryReadCredential(token, owner.Content, out var credential)) { Rejection = "credential-unreadable"; return false; }
            if (!Admission.Admit(player, subject, credential, now)) { Rejection = Admission.Rejection; return false; }
            if (alreadyBound) return true;
            if ((HostRules.Phase == SessionPhase.Running || HostRules.Phase == SessionPhase.Warmup) && !loadDeadlines.ContainsKey(subject))
                loadDeadlines[subject] = now + SessionState.OperationTimeoutSeconds;
            Publish();
            return true;
        }
        public void Disconnect(PlayerRef player)
        {
            if (!HasStateAuthority || Admission == null) return;
            if (Admission.Disconnect(player, owner.Now)) PlayerDisconnected?.Invoke(player);
            Publish();
        }
        public bool PublishRecovery(long simulationTick, int matchTick)
        {
            if (!HasStateAuthority || !Runner.IsServer || Admission == null || simulationTick != Runner.Tick.Raw ||
                (HostRules.Phase != SessionPhase.Running && HostRules.Phase != SessionPhase.Warmup)) return false;
            var checkpoint = HostRules.CaptureCheckpoint(simulationTick, owner.Now);
            var header = FusionRecoveryState.FromCheckpoint(checkpoint, matchTick);
            var seats = checkpoint.Seats.Seats;
            var wire = new FusionRecoverySeat[seats.Length];
            for (int i = 0; i < seats.Length; i++)
            {
                if (!Admission.TryGetCredentialHash(seats[i].VerifiedSubject, out var hash)) return false;
                wire[i] = FusionRecoverySeat.FromSeat(seats[i], hash, vulnerableUntil: Admission.GetVulnerableUntil(seats[i].VerifiedSubject));
                if (loadDeadlines.TryGetValue(seats[i].VerifiedSubject, out double deadline)) wire[i].LoadDeadline = deadline;
            }
            for (int i = 0; i < RecoverySeats.Length; i++) RecoverySeats.Set(i, i < wire.Length ? wire[i] : default);
            RecoveryHeader = header;
            return true;
        }
        public bool TryCaptureRecovery(out FusionRecoveryState header, out FusionRecoverySeat[] seats)
        {
            header = RecoveryHeader; seats = null;
            if (header.AuthorityGeneration < 1 || header.SimulationTick < 0 || header.MatchTick < 0 || header.SeatCount < 1 ||
                header.SeatCount > SeatTable.Capacity || (header.Phase != SessionPhase.Running && header.Phase != SessionPhase.Warmup)) return false;
            var wire = new FusionRecoverySeat[header.SeatCount];
            for (int i = 0; i < wire.Length; i++) wire[i] = RecoverySeats[i];
            foreach (var seat in wire)
                if (!ValidLoadDeadline(seat.LoadDeadline, header.CapturedAt) || !ValidVulnerabilityDeadline(seat.VulnerableUntil, header.CapturedAt) || (seat.Ready && seat.LoadDeadline != 0)) return false;
            var checkpoint = header.ToCheckpoint(wire);
            if (!checkpoint.Content.IsValid || !new SeatTable().TryRestore(checkpoint.Seats, header.CapturedAt)) return false;
            seats = wire; return true;
        }
        public void RestoreRecovery(FusionRecoveryState header, FusionRecoverySeat[] seats)
        {
            if (!HasStateAuthority || !Runner.IsServer) throw new InvalidOperationException("Only the resumed host can restore lobby bindings.");
            owner = FusionSession.OwnerOf(Runner);
            if (owner == null || seats == null || seats.Length != header.SeatCount ||
                !owner.Content.Matches(header.ToCheckpoint(seats).Content)) throw new InvalidOperationException("Recovery lobby owner or content is invalid.");
            var restored = new FusionLobbyAdmission(owner.Rules, owner.Content);
            if (!restored.RestoreCredentials(seats, owner.Now)) throw new InvalidOperationException("Recovery credentials do not cover the retained seats.");
            Admission = restored; HostPlayer = Runner.LocalPlayer; FinalFrozen = false;
            loadDeadlines.Clear(); expiredLoads.Clear();
            foreach (var seat in seats)
            {
                if (!ValidLoadDeadline(seat.LoadDeadline, header.CapturedAt) || !ValidVulnerabilityDeadline(seat.VulnerableUntil, header.CapturedAt) || (seat.Ready && seat.LoadDeadline != 0))
                    throw new InvalidOperationException("Recovery load deadline is invalid.");
                if (seat.LoadDeadline != 0) loadDeadlines.Add(seat.VerifiedSubject.ToString(), seat.LoadDeadline);
            }
            for (int i = 0; i < RecoverySeats.Length; i++) RecoverySeats.Set(i, i < seats.Length ? seats[i] : default);
            RecoveryHeader = header;
            Publish();
        }
        public void RequestTeam(int team) => RPC_Team(team);
        public void RequestReady(bool ready) => RPC_Ready(ready);
        public void RequestStart() => RPC_Start();
        public void RequestLoaded() => RPC_Loaded();
        // Host-only rematch. Deliberately not an RPC: no client message can return the session to its lobby.
        public bool ReturnToLobby()
        {
            if (!HasStateAuthority || !Runner.IsServer || Admission == null || owner.SimulationFrozen || !HostRules.ReturnToLobby(owner.Now)) return false;
            loadDeadlines.Clear(); Publish();
            return true;
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        void RPC_Team(int team, RpcInfo info = default)
        {
            if (Binding(info.Source, out var seat)) HostRules.SelectTeam(seat.VerifiedSubject, seat.ConnectionGeneration, team);
            Publish();
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        void RPC_Ready(bool ready, RpcInfo info = default)
        {
            if (Binding(info.Source, out var seat)) HostRules.SetReady(seat.VerifiedSubject, seat.ConnectionGeneration, ready);
            Publish();
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        void RPC_Start(RpcInfo info = default)
        {
            if (info.Source == HostPlayer && Binding(info.Source, out _)) HostRules.StartLoading(owner.Now);
            Publish();
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        void RPC_Loaded(RpcInfo info = default)
        {
            if (!Binding(info.Source, out var seat)) return;
            if (loadDeadlines.TryGetValue(seat.VerifiedSubject, out double deadline) && owner.Now >= deadline)
            { FusionSession.LogRemoval("load-timeout", HostRules.Phase); Disconnect(info.Source); Runner.Disconnect(info.Source); return; }
            if (HostRules.SetLoaded(seat.VerifiedSubject, seat.ConnectionGeneration)) loadDeadlines.Remove(seat.VerifiedSubject);
            Publish();
        }
        static bool ValidVulnerabilityDeadline(double deadline, double capturedAt) => !double.IsNaN(deadline) && !double.IsInfinity(deadline) &&
            deadline >= 0 && deadline <= capturedAt + SeatTable.VulnerableSeconds;
        static bool ValidLoadDeadline(double deadline, double capturedAt) => !double.IsNaN(deadline) && !double.IsInfinity(deadline) &&
            deadline >= 0 && (deadline == 0 || deadline <= capturedAt + SessionState.OperationTimeoutSeconds);
        bool Binding(PlayerRef source, out SeatSnapshot seat)
        {
            seat = default;
            return HasStateAuthority && Runner.IsServer && Admission != null && !owner.SimulationFrozen && Admission.TryGetBinding(source, out seat) &&
                string.Equals(Runner.GetPlayerUserId(source), seat.VerifiedSubject, StringComparison.Ordinal);
        }
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            double now = owner.Now;
            HostRules.Tick(now);
            expiredLoads.Clear();
            foreach (var pending in loadDeadlines)
                if (now >= pending.Value)
                {
                    var peer = Admission.PlayerFor(pending.Key);
                    if (peer != PlayerRef.None) expiredLoads.Add(peer);
                }
            foreach (var player in expiredLoads) { FusionSession.LogRemoval("load-timeout", HostRules.Phase); Disconnect(player); Runner.Disconnect(player); }
            identityRetry.Clear(); identityRetry.AddRange(identityWait.Keys);
            foreach (var player in identityRetry)
            {
                if (!Runner.IsPlayerValid(player)) { identityWait.Remove(player); continue; }
                if (owner.SimulationFrozen || !Admit(player) && Rejection != IdentityPending)
                {
                    identityWait.Remove(player);
                    FusionSession.LogRemoval(owner.SimulationFrozen ? "simulation-frozen" : Rejection, HostRules.Phase); Runner.Disconnect(player);
                }
            }
            if (HostRules.Phase == SessionPhase.Loading) HostRules.FinishLoading(now);
            if (Phase != HostRules.Phase || now >= nextHoldExpiry) Publish();
        }
        void Publish()
        {
            if (!HasStateAuthority || Admission == null || owner.SimulationFrozen) return;
            Phase = HostRules.Phase;
            RosterRevision++;
            var snapshot = HostRules.CaptureSeats(owner.Now);
            nextHoldExpiry = double.PositiveInfinity;
            foreach (var subject in new List<string>(loadDeadlines.Keys))
                if (!Array.Exists(snapshot.Seats, seat => seat.VerifiedSubject == subject)) loadDeadlines.Remove(subject);
            for (int i = 0; i < SeatTable.Capacity; i++) Seats.Set(i, default);
            foreach (var seat in snapshot.Seats)
            {
                if (!seat.Connected) nextHoldExpiry = Math.Min(nextHoldExpiry, seat.HoldUntil);
                Seats.Set(seat.Team * SeatTable.SlotsPerTeam + seat.Slot, new FusionLobbySeat
                {
                    Occupied = true, Player = Admission.PlayerFor(seat.VerifiedSubject), Connected = seat.Connected,
                    Ready = seat.Ready, Team = seat.Team, Slot = seat.Slot, ConnectionGeneration = seat.ConnectionGeneration
                });
            }
        }
        public override void Render()
        {
            if (observedPhase == Phase && observedRosterRevision == RosterRevision && observedRecoveryTick == RecoveryHeader.SimulationTick) return;
            observedPhase = Phase; observedRosterRevision = RosterRevision; observedRecoveryTick = RecoveryHeader.SimulationTick;
            Changed?.Invoke(); owner.NotifyChanged();
        }
    }
}
