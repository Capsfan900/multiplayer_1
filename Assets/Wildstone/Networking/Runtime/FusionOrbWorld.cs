using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    public struct FusionOrbState : INetworkStruct
    {
        public int Id, ReadyTick, ActionSequence;
        public bool Valid => Id > 0 && ReadyTick >= 0 && ActionSequence >= 0;
        public bool TryConsume(int runnerTick, out int action)
        {
            action = 0;
            if (!Valid || runnerTick < 0 || runnerTick < ReadyTick || runnerTick > int.MaxValue - MotorLaunchOrb.RespawnTicks || ActionSequence == int.MaxValue) return false;
            ReadyTick = runnerTick + MotorLaunchOrb.RespawnTicks;
            action = ++ActionSequence;
            return true;
        }
        public void Rebase(int oldRunnerTick, int newRunnerTick)
        {
            if (!Valid || oldRunnerTick < 0 || newRunnerTick < 0) throw new ArgumentException("Invalid orb checkpoint clock.");
            ReadyTick = checked(newRunnerTick + Math.Max(0, ReadyTick - oldRunnerTick));
        }
    }

    public sealed partial class FusionMatch : ILaunchOrbWorld
    {
        public const int MaximumOrbs = 16;
        [Networked] public int OrbCount { get; set; }
        [Networked] public int OrbSnapshotRunnerTick { get; set; }
        [Networked, Capacity(MaximumOrbs)] public NetworkArray<FusionOrbState> Orbs => default;
        static readonly Dictionary<NetworkRunner, FusionMatch> orbWorlds = new Dictionary<NetworkRunner, FusionMatch>();

        public static bool TryGetOrbWorld(NetworkRunner runner, out FusionMatch match) => orbWorlds.TryGetValue(runner, out match) && match != null && match.Object != null && match.Object.IsValid;

        void RegisterOrbWorld()
        {
            if (OrbCount > 0 && Runner.Config.ClientsOnlyRollbackUpdatedObjects)
                throw new InvalidOperationException("Shared orb prediction requires rollback of every simulated object.");
            orbWorlds[Runner] = this;
            Runner.SetIsSimulated(Object, true);
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (orbWorlds.TryGetValue(runner, out var current) && ReferenceEquals(current, this)) orbWorlds.Remove(runner);
        }
        void InitializeOrbState()
        {
            var contacts = SceneOrbs();
            if (contacts.Length > MaximumOrbs) throw new InvalidOperationException("Launch orb capacity exceeded.");
            OrbCount = contacts.Length;
            for (int i = 0; i < contacts.Length; i++)
            {
                if (contacts[i].Id <= 0 || (i > 0 && contacts[i - 1].Id == contacts[i].Id)) throw new InvalidOperationException("Launch orb IDs must be positive and unique.");
                Orbs.Set(i, new FusionOrbState { Id = contacts[i].Id });
            }
            OrbSnapshotRunnerTick = Runner.Tick.Raw;
        }
        MotorLaunchOrb[] SceneOrbs()
        {
            var contacts = FindObjectsByType<MotorLaunchOrb>();
            foreach (var contact in contacts)
                if (contact.gameObject.scene.GetPhysicsScene() != Runner.GetPhysicsScene())
                    throw new InvalidOperationException("Launch orbs must belong to the active runner physics world.");
            Array.Sort(contacts, (a, b) => a.Id.CompareTo(b.Id));
            return contacts;
        }
        public void ValidateOrbScene()
        {
            if (OrbCount < 0 || OrbCount > MaximumOrbs) throw new InvalidOperationException("Invalid launch orb count.");
            var contacts = SceneOrbs();
            if (contacts.Length != OrbCount) throw new InvalidOperationException("Launch orb authoring differs from the replicated world.");
            for (int i = 0; i < contacts.Length; i++)
                if (!Orbs[i].Valid || contacts[i].Id != Orbs[i].Id || (i > 0 && Orbs[i - 1].Id >= Orbs[i].Id))
                    throw new InvalidOperationException("Launch orb identity/state differs from the replicated world.");
        }
        public void ValidateOrbCheckpoint(long snapshotRunnerTick)
        {
            ValidateOrbScene();
            if (OrbSnapshotRunnerTick < 0 || OrbSnapshotRunnerTick != snapshotRunnerTick) throw new InvalidOperationException("Launch orb checkpoint is from a different simulation tick.");
            for (int i = 0; i < OrbCount; i++)
                if (Orbs[i].ReadyTick > snapshotRunnerTick + MotorLaunchOrb.RespawnTicks)
                    throw new InvalidOperationException("Launch orb cooldown exceeds its bounded checkpoint lifetime.");
        }
        void RestoreOrbState()
        {
            ValidateOrbCheckpoint(OrbSnapshotRunnerTick);
            for (int i = 0; i < OrbCount; i++)
            {
                var saved = Orbs[i]; saved.Rebase(OrbSnapshotRunnerTick, Runner.Tick.Raw); Orbs.Set(i, saved);
            }
            OrbSnapshotRunnerTick = Runner.Tick.Raw;
        }
        public bool TryConsume(MotorLaunchOrb contact, out int action) => TryConsumeAtTick(contact,Runner.Tick.Raw,out action);
        internal bool TryConsumeAtTick(MotorLaunchOrb contact,int runnerTick,out int action)
        {
            action = 0;
            if (!Initialized || RecoveryWarmup || Header.Phase == Wildstone.Match.ArenaMatchPhase.Finished || Header.Phase == Wildstone.Match.ArenaMatchPhase.Aborted) return false;
            if(runnerTick<0 || runnerTick>Runner.Tick.Raw || (long)Runner.Tick.Raw-runnerTick>Wildstone.Session.SessionState.MaximumRollbackSeconds*120)
                throw new InvalidOperationException("Launch orb prediction tick is outside the bounded runner history.");
            if (contact == null || contact.gameObject.scene.GetPhysicsScene() != Runner.GetPhysicsScene())
                throw new InvalidOperationException("Unbound launch orb contact.");
            for (int i = 0; i < OrbCount; i++)
            {
                var state = Orbs[i];
                if (state.Id != contact.Id) continue;
                if (!state.TryConsume(runnerTick, out action)) return false;
                Orbs.Set(i, state);
                return true;
            }
            throw new InvalidOperationException("Launch orb contact is absent from the replicated world.");
        }
    }
}
