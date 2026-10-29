using System;
using Fusion;
using Wildstone.Combat;
using Wildstone.Match;

namespace Wildstone.Networking
{
    // Collections live in bounded NetworkArrays on FusionMatch; this is their coherent checkpoint header.
    public struct FusionMatchState : INetworkStruct
    {
        public const int MaximumPlayers = 8, TeamCount = 4, MaximumPickups = 32;
        public const int MaximumProjectiles = WeaponCatalog.MaximumProjectiles;
        public NetworkString<_32> MapId;
        public int MapRevision, CatalogRevision, TickRate, Tick, StartTick, AuthorityGeneration, AbsenceSince, OppositionStableSince, Winner;
        public NetworkBool Practice;
        public ArenaMatchPhase Phase;
        public int Captor;
        public NetworkBool Contested;
        public int PlayerCount, PickupCount, ProjectileCount;

        public static FusionMatchState FromSnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            ValidateText(snapshot.MapId, nameof(snapshot.MapId));
            ValidateCollections(snapshot.Players, snapshot.Pickups, snapshot.Projectiles, snapshot.ScoreTicks);
            foreach (var player in snapshot.Players) if (player == null) throw new ArgumentException("Null player.");
            foreach (var pickup in snapshot.Pickups)
            {
                if (pickup == null) throw new ArgumentException("Null pickup.");
                ValidateText(pickup.Item, nameof(pickup.Item));
            }
            foreach (var projectile in snapshot.Projectiles) if (projectile == null) throw new ArgumentException("Null projectile.");
            return new FusionMatchState
            {
                MapId = snapshot.MapId, MapRevision = snapshot.MapRevision, CatalogRevision = snapshot.CatalogRevision,
                TickRate = snapshot.TickRate, Tick = snapshot.Tick, StartTick = snapshot.StartTick,
                AuthorityGeneration = snapshot.AuthorityGeneration, AbsenceSince = snapshot.AbsenceSince,
                OppositionStableSince = snapshot.OppositionStableSince, Winner = snapshot.Winner,
                Practice = snapshot.Practice, Phase = snapshot.Phase,
                PlayerCount = snapshot.Players.Length, PickupCount = snapshot.Pickups.Length, ProjectileCount = snapshot.Projectiles.Length
            };
        }

        public MatchSnapshot ToSnapshot(CombatState[] players, PickupState[] pickups, ArenaProjectile[] projectiles, int[] scoreTicks)
        {
            ValidateCollections(players, pickups, projectiles, scoreTicks);
            if (players.Length != PlayerCount || pickups.Length != PickupCount || projectiles.Length != ProjectileCount)
                throw new ArgumentException("Checkpoint collection counts differ from the header.");
            return new MatchSnapshot
            {
                MapId = MapId.ToString(), MapRevision = MapRevision, CatalogRevision = CatalogRevision,
                TickRate = TickRate, Tick = Tick, StartTick = StartTick, AuthorityGeneration = AuthorityGeneration,
                AbsenceSince = AbsenceSince, OppositionStableSince = OppositionStableSince, Winner = Winner,
                Practice = Practice, Phase = Phase, Players = players, Pickups = pickups, Projectiles = projectiles, ScoreTicks = scoreTicks
            };
        }

        internal static void ValidateText(string value, string name)
        {
            if (value == null || value.Length > 32) throw new ArgumentException("Wire text must be non-null and fit 32 UTF-16 code units.", name);
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsSurrogate(value[i])) continue;
                if (!char.IsHighSurrogate(value[i]) || i + 1 == value.Length || !char.IsLowSurrogate(value[++i]))
                    throw new ArgumentException("Wire text contains an unpaired surrogate.", name);
            }
        }

        static void ValidateCollections(CombatState[] players, PickupState[] pickups, ArenaProjectile[] projectiles, int[] scores)
        {
            if (players == null || players.Length > MaximumPlayers || pickups == null || pickups.Length > MaximumPickups ||
                projectiles == null || projectiles.Length > MaximumProjectiles || scores == null || scores.Length != TeamCount)
                throw new ArgumentException("Checkpoint collections exceed the match wire contract.");
        }
    }
}
