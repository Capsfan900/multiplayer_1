using System;
using Fusion;
using UnityEngine;
using Wildstone.Movement;
using Wildstone.Combat;

namespace Wildstone.Networking
{
    [RequireComponent(typeof(ArenaMotor), typeof(NetworkObject))]
    public sealed class FusionPlayer : NetworkBehaviour, ILaunchOrbWorld, IInputAuthorityGained, IInputAuthorityLost
    {
        [Networked] public FusionMotorState State { get; set; }
        [Networked] public int FirstRunnerTick { get; set; }
        [Networked] public int LastSampleSequence { get; set; }
        [Networked] public NetworkBool Initialized { get; set; }
        [Networked] public int PoseEpoch { get; set; }
        [Networked] public int Participant { get; set; }
        [Networked] public NetworkBool MatchControlled { get; set; }
        public Transform VisualRoot;
        ArenaMotor motor;
        MotorState freshState;
        FusionMatch match;
        FusionMatch orbWorld;
        bool predictionStarted;
        Hitbox hitbox, head;
        bool aimHeld;
        // Host-side ADS intent from the latest accepted input; held like movement for at most 100 ms of missing input.
        public bool AimHeld => aimHeld;

        public void BindMatch(FusionMatch owner, int participant)
        {
            if (!HasStateAuthority || !Runner.IsServer || owner == null || participant < 1 || participant > 8)
                throw new InvalidOperationException("Only the host can bind an admitted avatar");
            match = owner; Participant = participant; MatchControlled = true;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
        public void RPC_Combat(FusionCombatAction action, RpcInfo info = default)
        {
            if (match != null && info.Source == Object.InputAuthority && AcceptsCombatTick(Runner.Tick.Raw,info.Tick.Raw,action.Kind))
                match.SubmitAction(this, action);
#if WILDSTONE_SMOKE
            else if (match != null && info.Source == Object.InputAuthority)
                Debug.Log($"WILDSTONE COMBAT RPC EXPIRED participant={Participant} kind={action.Kind} sequence={action.Sequence} age={Runner.Tick.Raw-info.Tick.Raw}");
#endif
        }

        // Shots, grenades, Blink and shield answer timing, so they keep a 250 ms window (30 ticks at 120 Hz, D040
        // proposal) matching the hitscan rewind cap. Pickups, reloads and weapon selections are judged on current host
        // state, so one resent after packet loss still lands up to 500 ms late instead of silently vanishing.
        internal const int AttackWindowTicks=30, InventoryWindowTicks=60;
        internal static bool AcceptsCombatTick(int now,int sent,CombatActionKind kind=CombatActionKind.Fire)
        {
            long age=(long)now-sent;
            bool inventory=kind==CombatActionKind.Pickup || kind==CombatActionKind.Reload ||
                kind==CombatActionKind.SelectStarter || kind==CombatActionKind.SelectSpecial;
            return sent>=0 && age>=0 && age<=(inventory?InventoryWindowTicks:AttackWindowTicks);
        }

        public override void Spawned()
        {
            DontDestroyOnLoad(gameObject);
            motor = GetComponent<ArenaMotor>();
            motor.LaunchOrbWorld = this;
            freshState = ArenaMotor.InitialState(transform.position,transform.eulerAngles.y);
            hitbox = GetComponent<Hitbox>();
            foreach (var candidate in GetComponentsInChildren<Hitbox>(true))
                if (candidate != hitbox && candidate.Type == HitboxTypes.Sphere) head = candidate;
            RequireSupportedWorld();
            if (VisualRoot == transform) throw new InvalidOperationException("VisualRoot must be a child separate from the authoritative collider");
        }

        public void InputAuthorityGained() { predictionStarted=false; }
        public void InputAuthorityLost() { predictionStarted=false; }

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
            if (HasStateAuthority && MatchControlled) return;
            SimulateTick();
        }

        public void SimulateHostTick()
        {
            if (!HasStateAuthority || !Runner.IsServer || !MatchControlled)
                throw new InvalidOperationException("Only the host match can schedule this avatar");
            SimulateTick();
        }

        public void RestoreCheckpoint(FusionMotorState saved,int lastSample,int poseEpoch,int participant,int newFirstRunnerTick)
        {
            if (!HasStateAuthority || !Runner.IsServer || participant<1 || participant>8 || lastSample< -1 || poseEpoch<0)
                throw new InvalidOperationException("Only a validated host recovery may restore movement");
            var restored=saved.ToMotor();
            if(!ArenaMotor.ValidSnapshot(restored) || newFirstRunnerTick!=Runner.Tick.Raw-restored.Tick)
                throw new ArgumentException("Recovery motor state or tick origin is invalid");
            RequireSupportedWorld(); motor=GetComponent<ArenaMotor>(); motor.RestoreState(restored);
            State=saved; FirstRunnerTick=newFirstRunnerTick; LastSampleSequence=lastSample;
            PoseEpoch=checked(poseEpoch+1); Participant=participant; MatchControlled=true; Initialized=true;
        }

        public void ApplyCombatState(CombatState player)
        {
            if (!HasStateAuthority || !Runner.IsServer || player == null || player.Participant != Participant)
                throw new InvalidOperationException("Combat state must belong to the host-bound avatar");
            var state = State.ToMotor();
            if (state.Generation != player.LifeGeneration)
            {
                int tick = state.Tick;
                state = freshState;
                state.Tick = tick;
                state.Generation = player.LifeGeneration;
                state.Position = player.Feet;
                state.Yaw = Mathf.Atan2(player.Aim.x, player.Aim.z) * Mathf.Rad2Deg;
                LastSampleSequence = -1;
                PoseEpoch++;
            }
            motor.RestoreState(state);
            State = FusionMotorState.FromMotor(motor.CaptureState());
        }

        public void SetSuppressed(bool suppressed, int liftTick = 0)
        {
            if (!HasStateAuthority || !Runner.IsServer) throw new InvalidOperationException("Only the host sets combat suppression");
            motor.RestoreState(State.ToMotor());
            motor.SetSuppressed(suppressed, liftTick);
            State = FusionMotorState.FromMotor(motor.CaptureState());
        }

        public void ResetConnectionInput()
        {
            if(!HasStateAuthority || !Runner.IsServer) throw new InvalidOperationException("Only the host can retire an input stream");
            var state=State.ToMotor(); state.Held=MotorButtons.None; state.LastMove=Vector2.zero;
            state.LastFreshInputTick=state.Tick-13;
            motor.RestoreState(state); State=FusionMotorState.FromMotor(state); LastSampleSequence=-1; aimHeld=false;
        }

        public void ApplyImpulse(Vector3 impulse, int sequence, int tick)
        {
            if (!HasStateAuthority || !Runner.IsServer) throw new InvalidOperationException("Only the host applies combat impulses");
            motor.RestoreState(State.ToMotor());
            motor.ApplyImpulse(impulse, sequence, tick);
            State = FusionMotorState.FromMotor(motor.CaptureState());
        }

        void SimulateTick()
        {
            if (FusionSession.OwnerOf(Runner)?.SimulationFrozen == true) return;
            if (!Initialized || (!HasStateAuthority && !HasInputAuthority)) return;
            if (Runner.DeltaTime != MotorTuning.TickDelta) throw new InvalidOperationException("Wildstone movement requires 120 Hz on host and client");
            int tick = Runner.Tick.Raw - FirstRunnerTick;
            if (tick < 0) return;
            var restored = State.ToMotor();
            bool initialPrediction=!Runner.IsServer && HasInputAuthority && !Runner.IsResimulation && !predictionStarted;
            int missing=InitialPredictionBootstrapTicks(restored.Tick,tick,FirstRunnerTick,Runner.LatestServerTick.Raw,initialPrediction);
            if(missing>0)
            {
                PrepareOrbWorld();
                restored=BootstrapInitialPrediction(motor,restored,tick,FirstRunnerTick,Runner.LatestServerTick.Raw);
            }
            if (restored.Tick != tick - 1) throw new InvalidOperationException($"Fusion movement tick origin or restored snapshot is not sequential: motor={restored.Tick}, expected={tick-1}, runner={Runner.Tick.Raw}, origin={FirstRunnerTick}, server={Runner.IsServer}, resimulation={Runner.IsResimulation}, first={Runner.IsFirstTick}, skipped={IsResimulationSkipped}");

            // Fusion rewinds networked properties; the CharacterController must follow that snapshot every tick.
            motor.RestoreState(restored);
            bool available = GetInput(out FusionInput sample);
            var input = FusionMotorInput.Schedule(available, sample, restored, LastSampleSequence, out int consumed);
            PrepareOrbWorld();
            if (!motor.Simulate(input, MotorTuning.TickDelta, tick))
                throw new InvalidOperationException("Scheduled Fusion movement input was rejected");
            State = FusionMotorState.FromMotor(motor.CaptureState());
            LastSampleSequence = consumed;
            if (input.Present) aimHeld = (sample.Held & MotorButtons.Aim) != 0;
            else if ((State.Tick - State.LastFreshInputTick) * (double)MotorTuning.TickDelta > .1) aimHeld = false;
            if(HasInputAuthority && !Runner.IsServer) predictionStarted=true;
            if (hitbox != null && HasStateAuthority)
            {
                float top = CombatRules.BodyTop(State.CapsuleHeight);
                hitbox.BoxExtents = new Vector3(CombatRules.BodyHalfWidth, top * .5f, CombatRules.BodyHalfWidth);
                hitbox.Offset = Vector3.up * (top * .5f);
            }
            if (head != null && HasStateAuthority)
            {
                // Same sphere as CombatRules.HeadCenter; lag compensation records the per-tick offset.
                head.SphereRadius = CombatRules.HeadRadius;
                head.Offset = Vector3.up * (State.CapsuleHeight - CombatRules.HeadDrop);
            }
        }

        internal static int InitialPredictionBootstrapTicks(int savedTick,int targetTick,int origin,int confirmedTick,bool initialPrediction)
        {
            long missing=(long)targetTick-savedTick-1;
            if(!initialPrediction || missing<=0) return 0;
            long snapshotTick=(long)origin+savedTick, currentTick=(long)origin+targetTick;
            int maximum=(int)(Wildstone.Session.SessionState.MaximumRollbackSeconds*WeaponCatalog.DefaultTickRate);
            if(savedTick< -1 || snapshotTick<0 || snapshotTick>confirmedTick || currentTick<0 || currentTick>int.MaxValue || missing>maximum)
                throw new InvalidOperationException("Initial movement prediction requires a confirmed checkpoint within the recovery tick budget.");
            return (int)missing;
        }

        internal static MotorState BootstrapInitialPrediction(ArenaMotor target,in MotorState checkpoint,int targetTick,int origin,int confirmedTick)
        {
            int missing=InitialPredictionBootstrapTicks(checkpoint.Tick,targetTick,origin,confirmedTick,true);
            target.RestoreState(checkpoint);
            // The SDK exposes only current-tick input; cold-start gaps use the existing missing-input policy.
            for(int i=1;i<=missing;i++)
                if(!target.Simulate(default,MotorTuning.TickDelta,checkpoint.Tick+i))
                    throw new InvalidOperationException("Initial movement prediction rejected a missing-input tick.");
            return target.CaptureState();
        }

        void PrepareOrbWorld()
        {
            motor.LaunchOrbWorld = this;
            if (FusionMatch.TryGetOrbWorld(Runner, out var current))
            {
                if (!ReferenceEquals(orbWorld, current)) current.ValidateOrbScene();
                orbWorld = current;
            }
            else
            {
                orbWorld = null;
                // Initial object arrival can precede the shared world; never mutate the offline fallback.
            }
        }
        public bool TryConsume(MotorLaunchOrb contact, out int action)
        {
            if (orbWorld == null) { action = 0; return false; }
            return orbWorld.TryConsumeAtTick(contact,checked(FirstRunnerTick+motor.CaptureState().Tick),out action);
        }

        void RequireSupportedWorld()
        {
            if (Runner.DeltaTime != MotorTuning.TickDelta) throw new InvalidOperationException("Wildstone movement requires 120 Hz on host and client");
            // shortcut: global ArenaMotor queries need the default physics scene; port queries before multi-peer scene isolation.
            if (Runner.GetPhysicsScene() != Physics.defaultPhysicsScene)
                throw new InvalidOperationException("ArenaMotor does not yet support runner-local physics scenes");

        }
    }
}
