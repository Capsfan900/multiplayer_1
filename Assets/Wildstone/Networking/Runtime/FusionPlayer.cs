using System;
using Fusion;
using UnityEngine;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    [RequireComponent(typeof(ArenaMotor), typeof(NetworkObject))]
    public sealed class FusionPlayer : NetworkBehaviour
    {
        [Networked] public FusionMotorState State { get; set; }
        [Networked] public int FirstRunnerTick { get; set; }
        [Networked] public int LastSampleSequence { get; set; }
        [Networked] public NetworkBool Initialized { get; set; }
        [Networked] public int PoseEpoch { get; set; }
        public Transform VisualRoot;
        ArenaMotor motor;

        public override void Spawned()
        {
            motor = GetComponent<ArenaMotor>();
            RequireSupportedWorld();
            if (VisualRoot == transform) throw new InvalidOperationException("VisualRoot must be a child separate from the authoritative collider");
        }

        public void Teleport(Vector3 position, float yaw)
        {
            if (!Initialized || !HasStateAuthority || !Runner.IsServer) throw new InvalidOperationException("Only the player host can teleport initialized movement");
            motor.RestoreState(State.ToMotor());
            motor.Teleport(position, yaw);
            State = FusionMotorState.FromMotor(motor.CaptureState());
            PoseEpoch++;
        }

        public override void Render()
        {
            if (!Initialized || HasStateAuthority || HasInputAuthority) return;
            var latest = State.ToMotor();
            if (TryGetSnapshotsBuffers(out var from, out var to, out float alpha))
            {
                var stateReader = GetPropertyReader<FusionMotorState>(nameof(State));
                var epochReader = GetPropertyReader<int>(nameof(PoseEpoch));
                FusionProxyPose.Apply(motor, VisualRoot, latest, stateReader.Read(from).ToMotor(), stateReader.Read(to).ToMotor(),
                    alpha, epochReader.Read(from) != epochReader.Read(to) || epochReader.Read(to) != PoseEpoch);
            }
            else FusionProxyPose.Apply(motor, VisualRoot, latest, latest, latest, 1, true);
        }

        // Admission/spawn owner supplies the authoritative life and first tick; never copy them from input.
        public void Initialize(in MotorState initial, int firstRunnerTick)
        {
            if (!HasStateAuthority || !Runner.IsServer) throw new InvalidOperationException("Only the admitted player host can initialize movement");
            if (Initialized) throw new InvalidOperationException("Movement is already initialized; respawn requires a composed authoritative life transition");
            if (initial.Tick != -1 || initial.Generation < 1 || firstRunnerTick < Runner.Tick.Raw)
                throw new ArgumentException("Initial movement requires Tick=-1, an admitted generation and a current/future first runner tick");
            RequireSupportedWorld();
            motor = GetComponent<ArenaMotor>();
            motor.RestoreState(initial);
            State = FusionMotorState.FromMotor(initial);
            FirstRunnerTick = firstRunnerTick;
            LastSampleSequence = -1;
            Initialized = true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Initialized || (!HasStateAuthority && !HasInputAuthority)) return;
            if (Runner.DeltaTime != MotorTuning.TickDelta) throw new InvalidOperationException("Wildstone movement requires 120 Hz on host and client");
            int tick = Runner.Tick.Raw - FirstRunnerTick;
            if (tick < 0) return;
            var restored = State.ToMotor();
            if (restored.Tick != tick - 1) throw new InvalidOperationException("Fusion movement tick origin or restored snapshot is not sequential");

            // Fusion rewinds networked properties; the CharacterController must follow that snapshot every tick.
            motor.RestoreState(restored);
            bool available = GetInput(out FusionInput sample);
            var input = FusionMotorInput.Schedule(available, sample, restored, LastSampleSequence, out int consumed);
            if (!motor.Simulate(input, MotorTuning.TickDelta, tick))
                throw new InvalidOperationException("Scheduled Fusion movement input was rejected");
            State = FusionMotorState.FromMotor(motor.CaptureState());
            LastSampleSequence = consumed;
        }

        void RequireSupportedWorld()
        {
            if (Runner.DeltaTime != MotorTuning.TickDelta) throw new InvalidOperationException("Wildstone movement requires 120 Hz on host and client");
            // shortcut: global ArenaMotor queries need the default physics scene; port queries before multi-peer scene isolation.
            if (Runner.GetPhysicsScene() != Physics.defaultPhysicsScene)
                throw new InvalidOperationException("ArenaMotor does not yet support runner-local physics scenes");
            // Shared availability is outside MotorState; fail closed until it joins the rollback checkpoint.
            if (FindAnyObjectByType<MotorLaunchOrb>() != null)
                throw new InvalidOperationException("Launch-orb world rollback is not implemented for Fusion movement");
        }
    }
}
