using System;
using UnityEngine;
namespace Wildstone.Combat
{
    [Serializable] public sealed class ArenaProjectile
    {
        public CombatAction Action;
        public WeaponKind Weapon;
        public int OwnerTeam, ExpireTick, Bounces, HitMask;
        public Vector3 Position, Velocity;
        public float Travel;
        public ArenaProjectile Copy() => (ArenaProjectile)MemberwiseClone();
        internal void CopyFrom(ArenaProjectile other)
        { Action=other.Action; Weapon=other.Weapon; OwnerTeam=other.OwnerTeam; ExpireTick=other.ExpireTick; Bounces=other.Bounces; HitMask=other.HitMask; Position=other.Position; Velocity=other.Velocity; Travel=other.Travel; }
        public bool Valid(int tick) => Action.Participant>=1 && Action.Participant<=8 && Action.Sequence>=0 && Action.LifeGeneration>=1 &&
            Action.ConnectionGeneration>=1 && Action.AuthorityGeneration>=1 && Action.Tick>=0 && Action.Tick<=tick && OwnerTeam>=0 && OwnerTeam<4 &&
            (Weapon==WeaponKind.PulseLobber || Weapon==WeaponKind.ReboundDisc || Weapon==WeaponKind.Grenade || Weapon==WeaponKind.StunGun) &&
            ExpireTick>tick && ExpireTick<=Action.Tick+WeaponCatalog.DefaultTickRate*10 && Bounces>=0 && Bounces<=2 && (HitMask&~510)==0 &&
            CombatRules.Finite(Position) && CombatRules.Finite(Velocity) && Velocity.sqrMagnitude<=10000 && CombatRules.Finite(Travel) && Travel>=0;
    }
}
