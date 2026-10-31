using System;
using Fusion;
using UnityEngine;
using Wildstone.Combat;

namespace Wildstone.Networking
{
    // Presentation only: command acknowledgements/rejections and their diagnostic Reason are deliberately excluded.
    public struct FusionCombatEvent : INetworkStruct
    {
        public const int Capacity = 128;
        public int Sequence, AuthorityGeneration;
        public CombatEventKind Kind;
        public FusionCombatAction Action;
        public WeaponKind Weapon;
        public int Tick, Target, TargetLife, SubIndex;
        public Vector3 Position, Vector;
        public float Amount;
        public NetworkBool Headshot;

        public static bool IsPresentationEvent(CombatEventKind kind) => kind >= CombatEventKind.Shot && kind <= CombatEventKind.Deflected;

        public static FusionCombatEvent FromCombat(CombatEvent value, int sequence, int authorityGeneration)
        {
            if (!IsPresentationEvent(value.Kind) || sequence < 1 || authorityGeneration < 1)
                throw new ArgumentException("Presentation entries require a gameplay event and positive sequence/authority epoch.");
            return new FusionCombatEvent
            {
                Sequence = sequence, AuthorityGeneration = authorityGeneration,
                Kind = value.Kind, Action = FusionCombatAction.FromCombat(value.Action), Weapon = value.Weapon,
                Tick = value.Tick, Target = value.Target, TargetLife = value.TargetLife, SubIndex = value.SubIndex,
                Position = value.Position, Vector = value.Vector, Amount = value.Amount, Headshot = value.Headshot
            };
        }

        public CombatEvent ToCombat() => new CombatEvent(Kind, Action.ToCombat(), Tick, Target, TargetLife,
            Position, Vector, Amount, reason: null, subIndex: SubIndex, weapon: Weapon, headshot: Headshot);

        // Old entries may be evicted between snapshots; effects can be missed but never drive gameplay state.
        public static int FirstAvailableSequence(int cursor, int latest)
        {
            if (cursor < 0 || latest < 0) throw new ArgumentOutOfRangeException("Presentation cursors cannot be negative.");
            if (latest <= cursor) return 0;
            return Math.Max(cursor + 1, Math.Max(1, latest - Capacity + 1));
        }
    }
}
