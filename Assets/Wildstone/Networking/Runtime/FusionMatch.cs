using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class FusionMatch : NetworkBehaviour
    {
        [Networked] public FusionMatchState Header { get; set; }
        [Networked] public NetworkBool Initialized { get; set; }
        [Networked] public NetworkBool RecoveryWarmup { get; set; }
        [Networked] public int FirstRunnerTick { get; set; }
        [Networked, Capacity(FusionMatchState.MaximumPlayers)] public NetworkArray<FusionCombatState> Players => default;
        [Networked, Capacity(FusionMatchState.MaximumPickups)] public NetworkArray<FusionPickupState> Pickups => default;
        [Networked, Capacity(FusionMatchState.MaximumProjectiles)] public NetworkArray<FusionProjectileState> Projectiles => default;
        [Networked, Capacity(FusionMatchState.TeamCount)] public NetworkArray<int> Scores => default;

        [Networked] public int EventSequence { get; set; }
        [Networked, Capacity(FusionCombatEvent.Capacity)] public NetworkArray<FusionCombatEvent> EventRing => default;

        readonly FusionPlayer[] avatars = new FusionPlayer[FusionMatchState.MaximumPlayers];
        readonly List<CombatAction> actions = new List<CombatAction>(128);
        ArenaDefinition definition;
        FusionCombatWorld world;
        Action simulateMovement;
        int processedEvents;
        public ArenaMatch Model { get; private set; }
        public override void Spawned() { DontDestroyOnLoad(gameObject); RegisterOrbWorld(); }

        // A rematch after host migration starts at the session's current authority generation so checkpoints stay coherent.
        public void Initialize(ArenaDefinition map, bool practice = false, int authorityGeneration = 1)
        {
            RequireHost();
            if (Initialized) throw new InvalidOperationException("Match is already initialized.");
            if (authorityGeneration < 1) throw new ArgumentOutOfRangeException(nameof(authorityGeneration));
            if (map == null || map.Validate().Count != 0 || map.Pickups.Length > FusionMatchState.MaximumPickups)
                throw new ArgumentException("Invalid or oversized match map.", nameof(map));
            FusionMatchState.ValidateText(map.Id, nameof(map.Id));
            foreach (var pickup in map.Pickups) FusionMatchState.ValidateText(pickup.Item, nameof(pickup.Item));
            if (Runner.DeltaTime != MotorTuning.TickDelta || Runner.GetPhysicsScene() != Physics.defaultPhysicsScene)
                throw new InvalidOperationException("Match requires 120 Hz and the default physics scene.");
            definition = map;
            InitializeOrbState();
            Model = new ArenaMatch(map, WeaponCatalog.DefaultTickRate, practice);
            if (authorityGeneration > 1 && !Model.AdvanceAuthorityGeneration(authorityGeneration)) throw new InvalidOperationException("Match authority generation rejected.");
            world = new FusionCombatWorld(Runner, map);
            simulateMovement = SimulateMovement;
            FirstRunnerTick = Runner.Tick.Raw + 1;
            Initialized = true;
            Publish();
        }

        public bool BindPlayer(int participant, FusionPlayer avatar, int connectionGeneration, int team, bool forceFreshLife=false)
        {
            RequireHost();
            if (!Initialized || participant < 1 || participant > avatars.Length || avatar == null ||
                avatar.Runner != Runner || !avatar.HasStateAuthority || !avatar.Initialized || connectionGeneration < 1)
                return false;
            for (int i = 0; i < avatars.Length; i++) if (i != participant - 1 && avatars[i] == avatar) return false;
            var existing = Model.Find(participant);
            bool newInputStream=existing!=null && existing.ConnectionGeneration!=connectionGeneration;
            if (existing == null)
            {
                var motor = avatar.State;
                if (!Model.AddPlayer(participant, team, connectionGeneration, motor.Generation, motor.Position)) return false;
                existing = Model.Find(participant);
                existing.Aim = Quaternion.Euler(motor.Pitch, motor.Yaw, 0) * Vector3.forward;
            }
            else
            {
                if (existing.Team != team || connectionGeneration < existing.ConnectionGeneration) return false;
                if (!Model.SetConnection(participant, connectionGeneration, true,forceFreshLife)) return false;
            }
            avatars[participant - 1] = avatar;
            avatar.BindMatch(this, participant);
            avatar.ApplyCombatState(existing);
            if(newInputStream) avatar.ResetConnectionInput();
            world.Bind(participant, avatar.Object.InputAuthority, avatar.GetComponent<ArenaMotor>(), avatar.GetComponent<HitboxRoot>(), () => existing.Alive, () => avatar.PoseEpoch);
            Publish();
            return true;
        }

        public void RestoreCheckpoint(ArenaDefinition map,MatchSnapshot saved,int newAuthorityGeneration)
        {
            RequireHost();
            if(Model!=null || saved==null || newAuthorityGeneration<=saved.AuthorityGeneration)
                throw new InvalidOperationException("Recovery requires a new host model and newer authority generation");
            var restored=new ArenaMatch(map,saved.TickRate,saved.Practice);
            if(!restored.TryRestore(saved) || !restored.AdvanceAuthorityGeneration(newAuthorityGeneration))
                throw new ArgumentException("Invalid complete match checkpoint");
            RestoreOrbState();
            definition=map; Model=restored; world=new FusionCombatWorld(Runner,map);
            simulateMovement=SimulateMovement; FirstRunnerTick=Runner.Tick.Raw+1; RecoveryWarmup=true; Initialized=true;
            Publish();
        }

        public void BindRecoveredPlayer(int participant,FusionPlayer avatar)
        {
            RequireHost();
            var existing=Model?.Find(participant);
            if(existing==null || avatar==null || avatar.Runner!=Runner || !avatar.Initialized ||
                avatar.State.Generation!=existing.LifeGeneration || participant<1 || participant>avatars.Length)
                throw new ArgumentException("Recovered avatar does not match the complete checkpoint");
            avatars[participant-1]=avatar; avatar.BindMatch(this,participant);
            world.Bind(participant,PlayerRef.None,avatar.GetComponent<ArenaMotor>(),avatar.GetComponent<HitboxRoot>(),()=>existing.Alive,()=>avatar.PoseEpoch,true);
        }

        public bool UnbindAvatar(int participant, FusionPlayer expected)
        {
            RequireHost();
            if (participant < 1 || participant > avatars.Length || ReferenceEquals(expected, null) ||
                !ReferenceEquals(avatars[participant - 1], expected)) return false;
            world.Unbind(participant);
            avatars[participant - 1] = null;
            return true;
        }

        public bool SetConnection(int participant, int connectionGeneration, bool connected)
        {
            RequireHost();
            if (!Initialized || !Model.SetConnection(participant, connectionGeneration, connected)) return false;
            if (participant >= 1 && participant <= avatars.Length && avatars[participant - 1] != null)
            {
                var avatar = avatars[participant - 1];
                avatar.ApplyCombatState(Model.Find(participant));
                avatar.SetSuppressed(Model.Find(participant).Suppressed(Model.Tick));
            }
            Publish();
            return true;
        }

        public bool Begin()
        {
            RequireHost();
            if (!Initialized || !Model.Begin()) return false;
            Publish();
            return true;
        }

        // Match tick a combat request sent now is stamped with. A client maps Fusion's predicted tick (advanced every
        // simulated tick and timed to reach the host just in time) through the replicated tick pair; its 20 Hz model copy
        // moves in six-tick steps and would quantize a held trigger (D036). It holds small drops: during RecoveryWarmup the
        // host keeps Header.Tick but advances OrbSnapshotRunnerTick, and PracticeInput reads a drop as a reset.
        public int NextActionTick
        {
            get
            {
                if (Runner.IsServer) return Model.Tick + 1;
                int tick = Runner.Tick.Raw + Header.Tick - OrbSnapshotRunnerTick + 1;
                if (tick < clientActionTick && tick > clientActionTick - WeaponCatalog.DefaultTickRate) tick = clientActionTick;
                return clientActionTick = tick;
            }
        }
        int clientActionTick;

        // Only the bound avatar's input-authority RPC may call this admission boundary.
        public bool SubmitAction(FusionPlayer sender, FusionCombatAction request)
        {
            RequireHost();
            if (FusionSession.OwnerOf(Runner)?.SimulationFrozen == true || !Initialized || Model.Phase != ArenaMatchPhase.Running || sender == null || actions.Count == actions.Capacity)
                return false;
            int participant = sender.Participant;
            if (participant < 1 || participant > avatars.Length || avatars[participant - 1] != sender) return false;
            var player = Model.Find(participant);
            if (player == null || !player.Connected || request.ConnectionGeneration != player.ConnectionGeneration ||
                request.AuthorityGeneration != Model.AuthorityGeneration || request.LifeGeneration != player.LifeGeneration) return false;
            var action = request.ToCombat();
            action.Participant = participant;
            action.ConnectionGeneration = player.ConnectionGeneration;
            action.AuthorityGeneration = Model.AuthorityGeneration;
            action.Tick = Model.Tick + 1;
            actions.Add(action);
            return true;
        }

        public override void FixedUpdateNetwork()
        {
            if (FusionSession.OwnerOf(Runner)?.SimulationFrozen == true || !Initialized || !HasStateAuthority || !Runner.IsServer || Runner.Tick.Raw < FirstRunnerTick) return;
            if (Model == null) throw new InvalidOperationException("Match authority requires an installed recovery checkpoint.");
            if (Model.Phase == ArenaMatchPhase.Running && !RecoveryWarmup)
            {
                world.ObservePoseEpochs();
                processedEvents = 0;
                if (!Model.Step(Model.Tick + 1, actions, world, true, simulateMovement))
                    throw new InvalidOperationException("Authoritative match step was rejected.");
                ApplyEvents();
                ApplySuppression();
                PublishPresentationEvents();
            }
            else SimulateMovement();
            actions.Clear();
            OrbSnapshotRunnerTick = Runner.Tick.Raw;
            Publish();
        }

        void SimulateMovement()
        {
            if (Model.Phase == ArenaMatchPhase.Running) ApplyEvents();
            for (int i = 0; i < avatars.Length; i++)
            {
                var avatar = avatars[i];
                var player = Model.Find(i + 1);
                if (avatar == null || player == null) continue;
                avatar.ApplyCombatState(player);
                bool stepping = Model.Phase == ArenaMatchPhase.Running && !RecoveryWarmup;
                avatar.SetSuppressed(RecoveryWarmup || player.Suppressed(Model.Tick) || Model.Phase == ArenaMatchPhase.Finished || Model.Phase == ArenaMatchPhase.Aborted,
                    stepping ? StunLift(player, avatar.State.Tick + 1) : 0);
                avatar.SimulateHostTick();
                var motor = avatar.State;
                var aim = Quaternion.Euler(motor.Pitch, motor.Yaw, 0) * Vector3.forward;
                if (!Model.SetPose(player.Participant, player.LifeGeneration, motor.Position, aim, motor.CapsuleHeight, avatar.AimHeld))
                    throw new InvalidOperationException("Host motor produced an invalid combat pose.");
            }
            Physics.SyncTransforms();
        }

        void ApplyEvents()
        {
            while (processedEvents < Model.Events.Count)
            {
                var entry = Model.Events[processedEvents++];
                if (entry.Target < 1 || entry.Target > avatars.Length) continue;
                var avatar = avatars[entry.Target - 1];
                if (avatar == null) continue;
                if (entry.Kind == CombatEventKind.Blink) avatar.Teleport(entry.Position, avatar.State.Yaw);
                else if (entry.Kind == CombatEventKind.Respawn) avatar.ApplyCombatState(Model.Find(entry.Target));
                else if (entry.Kind == CombatEventKind.Impulse)
                {
                    if (avatar.State.LastImpulseId == int.MaxValue) throw new InvalidOperationException("Combat impulse sequence exhausted.");
                    avatar.ApplyImpulse(entry.Vector, avatar.State.LastImpulseId + 1, avatar.State.Tick);
                }
            }
        }

        void ApplySuppression()
        {
            for (int i = 0; i < avatars.Length; i++)
            {
                if (avatars[i] == null) continue;
                var player = Model.Find(i + 1);
                if (player != null) avatars[i].SetSuppressed(player.Suppressed(Model.Tick) || Model.Phase != ArenaMatchPhase.Running,
                    Model.Phase == ArenaMatchPhase.Running ? StunLift(player, avatars[i].State.Tick) : 0);
            }
        }

        // Motor tick a stun-only suppression lifts at; motorTick is the motor tick simulated at Model.Tick.
        int StunLift(CombatState player, int motorTick) =>
            player.Alive && player.Connected && Model.Tick < player.StunUntil ? player.StunUntil - Model.Tick + motorTick : 0;

        void PublishPresentationEvents()
        {
            for (int i = 0; i < Model.Events.Count; i++)
            {
                var entry = Model.Events[i];
                if (!FusionCombatEvent.IsPresentationEvent(entry.Kind)) continue;
                int sequence = checked(EventSequence + 1);
                EventRing.Set((sequence - 1) % FusionCombatEvent.Capacity,
                    FusionCombatEvent.FromCombat(entry, sequence, Model.AuthorityGeneration));
                EventSequence = sequence;
            }
        }

        // Keep one cursor per match object and preallocate destination.Capacity to the bounded ring size.
        public void CopyPresentationEvents(List<CombatEvent> destination, ref int cursor)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destination.Capacity < FusionCombatEvent.Capacity)
                throw new ArgumentException("Presentation destination must be preallocated to the ring capacity.", nameof(destination));
            destination.Clear();
            if (!Initialized) return;
            int latest = EventSequence;
            int first = FusionCombatEvent.FirstAvailableSequence(cursor, latest);
            if (first == 0) return;
            int count = latest - first + 1;
            for (int i = 0; i < count; i++)
            {
                int sequence = first + i;
                var entry = EventRing[(sequence - 1) % FusionCombatEvent.Capacity];
                if (entry.Sequence == sequence && entry.AuthorityGeneration > 0 && entry.AuthorityGeneration <= Header.AuthorityGeneration)
                    destination.Add(entry.ToCombat());
            }
            cursor = latest;
        }

        void Publish()
        {
            for (int i = 0; i < Model.Players.Count; i++) Players.Set(i, FusionCombatState.FromCombat(Model.Players[i]));
            for (int i = 0; i < Model.Pickups.Count; i++) Pickups.Set(i, FusionPickupState.FromPickup(Model.Pickups[i]));
            for (int i = 0; i < Model.Projectiles.Count; i++) Projectiles.Set(i, FusionProjectileState.FromProjectile(Model.Projectiles[i]));
            for (int i = 0; i < FusionMatchState.TeamCount; i++) Scores.Set(i, Model.ScoreTicks[i]);
            Header = new FusionMatchState
            {
                MapId = definition.Id, MapRevision = definition.Revision, CatalogRevision = WeaponCatalog.Revision,
                TickRate = Model.TickRate, Tick = Model.Tick, StartTick = Model.StartTick,
                AuthorityGeneration = Model.AuthorityGeneration, AbsenceSince = Model.AbsenceSince,
                OppositionStableSince = Model.OppositionStableSince, Winner = Model.Winner,
                Practice = Model.Practice, Phase = Model.Phase, Captor = Model.Captor, Contested = Model.Contested,
                PlayerCount = Model.Players.Count, PickupCount = Model.Pickups.Count, ProjectileCount = Model.Projectiles.Count
            };
        }

        // Allocates only when explicitly requested for recovery or infrequent presentation, never per simulation tick.
        public MatchSnapshot CaptureSnapshot()
        {
            if (!Initialized) throw new InvalidOperationException("Match is not initialized.");
            var header = Header;
            if (header.PlayerCount < 0 || header.PlayerCount > Players.Length || header.PickupCount < 0 || header.PickupCount > Pickups.Length ||
                header.ProjectileCount < 0 || header.ProjectileCount > Projectiles.Length)
                throw new InvalidOperationException("Match checkpoint counts exceed network capacities.");
            var players = new CombatState[header.PlayerCount];
            var pickups = new PickupState[header.PickupCount];
            var projectiles = new ArenaProjectile[header.ProjectileCount];
            var scores = new int[FusionMatchState.TeamCount];
            for (int i = 0; i < players.Length; i++) players[i] = Players[i].ToCombat();
            for (int i = 0; i < pickups.Length; i++) pickups[i] = Pickups[i].ToPickup();
            for (int i = 0; i < projectiles.Length; i++) projectiles[i] = Projectiles[i].ToProjectile();
            for (int i = 0; i < scores.Length; i++) scores[i] = Scores[i];
            return header.ToSnapshot(players, pickups, projectiles, scores);
        }

        void RequireHost()
        {
            if (Object == null || !HasStateAuthority || Runner == null || !Runner.IsServer)
                throw new InvalidOperationException("Only the match host may mutate authoritative state.");
        }
#if WILDSTONE_SMOKE
        // Smoke-only stand-in for the 480 s clock: the winner banks a second, the first player is wounded and a pickup taken,
        // so rematch smoke can prove the next match starts fresh. Live combat closures are moot once the match is finished.
        internal bool SmokeFinish(int winner)
        {
            RequireHost();
            if (!Initialized || Model.Phase != ArenaMatchPhase.Running || winner < 0 || winner >= FusionMatchState.TeamCount) return false;
            var saved = Model.Capture();
            saved.Phase = ArenaMatchPhase.Finished; saved.Winner = winner; saved.ScoreTicks[winner] = Math.Min(120 * saved.TickRate, saved.ScoreTicks[winner] + saved.TickRate);
            if (saved.Players.Length > 0 && saved.Players[0].Alive) saved.Players[0].Health = 50;
            if (saved.Pickups.Length > 0) { saved.Pickups[0].Generation++; saved.Pickups[0].AvailableTick = saved.Tick + 1; }
            if (!Model.TryRestore(saved)) return false;
            Publish();
            return true;
        }
#endif
    }
}
