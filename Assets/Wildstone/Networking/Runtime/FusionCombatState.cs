using System;
using Fusion;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Match;

namespace Wildstone.Networking
{
    public struct FusionCombatAction : INetworkStruct
    {
        public int Participant;
        public int ConnectionGeneration;
        public int LifeGeneration;
        public int AuthorityGeneration;
        public int Tick;
        public int Sequence;
        public CombatActionKind Kind;
        public Vector3 Direction;
        public int PickupId;
        public int PickupGeneration;

        public static FusionCombatAction FromCombat(CombatAction state)
        {
            return new FusionCombatAction
            {
                Participant = state.Participant,
                ConnectionGeneration = state.ConnectionGeneration,
                LifeGeneration = state.LifeGeneration,
                AuthorityGeneration = state.AuthorityGeneration,
                Tick = state.Tick,
                Sequence = state.Sequence,
                Kind = state.Kind,
                Direction = state.Direction,
                PickupId = state.PickupId,
                PickupGeneration = state.PickupGeneration,
            };
        }

        public CombatAction ToCombat() => new CombatAction
        {
            Participant = Participant,
            ConnectionGeneration = ConnectionGeneration,
            LifeGeneration = LifeGeneration,
            AuthorityGeneration = AuthorityGeneration,
            Tick = Tick,
            Sequence = Sequence,
            Kind = Kind,
            Direction = Direction,
            PickupId = PickupId,
            PickupGeneration = PickupGeneration,
        };
    }

    public struct FusionPendingAttack : INetworkStruct
    {
        public FusionCombatAction Action;
        public WeaponKind Weapon;
        public int ResolveTick;
        public int EndTick;
        public int HitMask;
        public int LastSampleTick;
        public Vector3 Direction;
        public Vector3 PreviousTip;

        public static FusionPendingAttack FromCombat(PendingAttack state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new FusionPendingAttack
            {
                Action = FusionCombatAction.FromCombat(state.Action),
                Weapon = state.Weapon,
                ResolveTick = state.ResolveTick,
                EndTick = state.EndTick,
                HitMask = state.HitMask,
                LastSampleTick = state.LastSampleTick,
                Direction = state.Direction,
                PreviousTip = state.PreviousTip,
            };
        }

        public PendingAttack ToCombat() => new PendingAttack
        {
            Action = Action.ToCombat(),
            Weapon = Weapon,
            ResolveTick = ResolveTick,
            EndTick = EndTick,
            HitMask = HitMask,
            LastSampleTick = LastSampleTick,
            Direction = Direction,
            PreviousTip = PreviousTip,
        };
    }

    public struct FusionCombatState : INetworkStruct
    {
        public int Participant;
        public int Team;
        public int ConnectionGeneration;
        public int LifeGeneration;
        public NetworkBool Connected;
        public NetworkBool Alive;
        public NetworkBool SelectedSpecial;
        public float Health;
        public float Shield;
        public float CapsuleHeight;
        public int StarterAmmo;
        public int SpecialAmmo;
        public int GrenadeAmmo;
        public int BlinkCharges;
        public int ShieldCharges;
        public WeaponKind SpecialWeapon;
        public int LastActionSequence;
        public int FireReadyTick;
        public int GrenadeReadyTick;
        public int ReloadUntil;
        public int ShieldUntil;
        public int StunUntil;
        public int ImmunityUntil;
        public int RespawnTick;
        public int DisconnectUntil;
        public Vector3 Feet;
        public Vector3 Aim;
        public Vector3 SupportPoint;
        public Vector3 SupportNormal;
        public NetworkBool Supported;
        public int DiscontinuityTick;
        public FusionPendingAttack Pending;
        public NetworkBool HasPending;
        public NetworkBool AimHeld;
        public float AdsProgress;
        public float Bloom;
        public int BloomRecoverTick;

        public static FusionCombatState FromCombat(CombatState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new FusionCombatState
            {
                Participant = state.Participant,
                Team = state.Team,
                ConnectionGeneration = state.ConnectionGeneration,
                LifeGeneration = state.LifeGeneration,
                Connected = state.Connected,
                Alive = state.Alive,
                SelectedSpecial = state.SelectedSpecial,
                Health = state.Health,
                Shield = state.Shield,
                CapsuleHeight = state.CapsuleHeight,
                StarterAmmo = state.StarterAmmo,
                SpecialAmmo = state.SpecialAmmo,
                GrenadeAmmo = state.GrenadeAmmo,
                BlinkCharges = state.BlinkCharges,
                ShieldCharges = state.ShieldCharges,
                SpecialWeapon = state.SpecialWeapon,
                LastActionSequence = state.LastActionSequence,
                FireReadyTick = state.FireReadyTick,
                GrenadeReadyTick = state.GrenadeReadyTick,
                ReloadUntil = state.ReloadUntil,
                ShieldUntil = state.ShieldUntil,
                StunUntil = state.StunUntil,
                ImmunityUntil = state.ImmunityUntil,
                RespawnTick = state.RespawnTick,
                DisconnectUntil = state.DisconnectUntil,
                Feet = state.Feet,
                Aim = state.Aim,
                SupportPoint = state.SupportPoint,
                SupportNormal = state.SupportNormal,
                Supported = state.Supported,
                DiscontinuityTick = state.DiscontinuityTick,
                HasPending = state.Pending != null,
                Pending = state.Pending == null ? default : FusionPendingAttack.FromCombat(state.Pending),
                AimHeld = state.AimHeld,
                AdsProgress = state.AdsProgress,
                Bloom = state.Bloom,
                BloomRecoverTick = state.BloomRecoverTick,
            };
        }

        public CombatState ToCombat() => new CombatState
        {
            Participant = Participant,
            Team = Team,
            ConnectionGeneration = ConnectionGeneration,
            LifeGeneration = LifeGeneration,
            Connected = Connected,
            Alive = Alive,
            SelectedSpecial = SelectedSpecial,
            Health = Health,
            Shield = Shield,
            CapsuleHeight = CapsuleHeight,
            StarterAmmo = StarterAmmo,
            SpecialAmmo = SpecialAmmo,
            GrenadeAmmo = GrenadeAmmo,
            BlinkCharges = BlinkCharges,
            ShieldCharges = ShieldCharges,
            SpecialWeapon = SpecialWeapon,
            LastActionSequence = LastActionSequence,
            FireReadyTick = FireReadyTick,
            GrenadeReadyTick = GrenadeReadyTick,
            ReloadUntil = ReloadUntil,
            ShieldUntil = ShieldUntil,
            StunUntil = StunUntil,
            ImmunityUntil = ImmunityUntil,
            RespawnTick = RespawnTick,
            DisconnectUntil = DisconnectUntil,
            Feet = Feet,
            Aim = Aim,
            SupportPoint = SupportPoint,
            SupportNormal = SupportNormal,
            Supported = Supported,
            DiscontinuityTick = DiscontinuityTick,
            Pending = HasPending ? Pending.ToCombat() : null,
            AimHeld = AimHeld,
            AdsProgress = AdsProgress,
            Bloom = Bloom,
            BloomRecoverTick = BloomRecoverTick,
        };
    }

    public struct FusionProjectileState : INetworkStruct
    {
        public FusionCombatAction Action;
        public WeaponKind Weapon;
        public int OwnerTeam;
        public int ExpireTick;
        public int Bounces;
        public int HitMask;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Travel;
        public NetworkBool Deflected;

        public static FusionProjectileState FromProjectile(ArenaProjectile state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new FusionProjectileState
            {
                Action = FusionCombatAction.FromCombat(state.Action),
                Weapon = state.Weapon,
                OwnerTeam = state.OwnerTeam,
                ExpireTick = state.ExpireTick,
                Bounces = state.Bounces,
                HitMask = state.HitMask,
                Position = state.Position,
                Velocity = state.Velocity,
                Travel = state.Travel,
                Deflected = state.Deflected,
            };
        }

        public ArenaProjectile ToProjectile() => new ArenaProjectile
        {
            Action = Action.ToCombat(),
            Weapon = Weapon,
            OwnerTeam = OwnerTeam,
            ExpireTick = ExpireTick,
            Bounces = Bounces,
            HitMask = HitMask,
            Position = Position,
            Velocity = Velocity,
            Travel = Travel,
            Deflected = Deflected,
        };
    }

    public struct FusionPickupState : INetworkStruct
    {
        public int Id;
        public int Generation;
        public int AvailableTick;
        public NetworkString<_32> Item;
        public Vector3 Position;

        public static FusionPickupState FromPickup(PickupState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            FusionMatchState.ValidateText(state.Item, nameof(state.Item));
            return new FusionPickupState
            {
                Id = state.Id,
                Generation = state.Generation,
                AvailableTick = state.AvailableTick,
                Item = state.Item,
                Position = state.Position,
            };
        }

        public PickupState ToPickup() => new PickupState
        {
            Id = Id,
            Generation = Generation,
            AvailableTick = AvailableTick,
            Item = Item.ToString(),
            Position = Position,
        };
    }

}
