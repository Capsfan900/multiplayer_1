using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wildstone.Maps
{
    public enum StoneTone { Ivory, Ash, Charcoal }

    [Serializable]
    public struct ArenaPiece
    {
        public string Name;
        public Vector3 Position, Size, Euler;
        public StoneTone Tone;
    }

    [Serializable]
    public struct ArenaSpawn
    {
        public int Team;
        public Vector3 Position;
        public float Yaw;
    }

    [Serializable]
    public struct ArenaPickup
    {
        public string Item;
        public Vector3 Position;
    }

    [Serializable]
    public sealed class ArenaDefinition
    {
        public string Id, DisplayName;
        public int Revision = 1;
        public Vector3 Minimum, Maximum;
        public float KillHeight = -12;
        public ArenaPiece[] Pieces = Array.Empty<ArenaPiece>();
        public ArenaSpawn[] Spawns = Array.Empty<ArenaSpawn>();
        public ArenaPickup[] Pickups = Array.Empty<ArenaPickup>();
        public Vector3[] ZoneRoute = Array.Empty<Vector3>();

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (Id != "atrium" && Id != "split-gallery" && Id != "terrace-steps") errors.Add("Unknown map ID.");
            if (string.IsNullOrWhiteSpace(DisplayName) || Revision < 1) errors.Add("Missing map name or revision.");
            if (!Finite(Minimum) || !Finite(Maximum) || Minimum.x >= Maximum.x || Minimum.y >= Maximum.y || Minimum.z >= Maximum.z)
                errors.Add("Invalid playable bounds.");
            if (!Finite(KillHeight) || KillHeight >= Minimum.y) errors.Add("Death boundary must be below the arena.");
            if (Pieces == null || Pieces.Length == 0) errors.Add("Missing geometry.");
            else foreach (var piece in Pieces)
                if (string.IsNullOrWhiteSpace(piece.Name) || !Finite(piece.Position) || !Finite(piece.Size) || !Finite(piece.Euler) ||
                    piece.Size.x <= 0 || piece.Size.y <= 0 || piece.Size.z <= 0 || !Enum.IsDefined(typeof(StoneTone), piece.Tone))
                    errors.Add("Invalid geometry piece: " + piece.Name);
            var seats = new int[4];
            if (Spawns != null) for (int i = 0; i < Spawns.Length; i++)
            {
                var spawn = Spawns[i];
                if (spawn.Team < 0 || spawn.Team >= seats.Length) errors.Add("Invalid spawn team.");
                else seats[spawn.Team]++;
                if (!Contains(spawn.Position) || !Finite(spawn.Yaw)) errors.Add("Invalid spawn position/aim.");
                for (int j = 0; j < i; j++)
                    if ((spawn.Position - Spawns[j].Position).sqrMagnitude < .81f) errors.Add("Spawn capsules overlap.");
            }
            for (int team = 0; team < seats.Length; team++)
                if (seats[team] != 2) errors.Add("Team " + team + " needs two spawn positions.");
            if (Pickups == null || Pickups.Length == 0) errors.Add("Missing pickups.");
            else foreach (var pickup in Pickups)
                if (!KnownItem(pickup.Item) || !Contains(pickup.Position)) errors.Add("Invalid pickup: " + pickup.Item);
            if (ZoneRoute == null || ZoneRoute.Length < 3) errors.Add("Zone needs a closed route with at least three points.");
            else for (int i = 0; i < ZoneRoute.Length; i++)
            {
                if (!Contains(ZoneRoute[i])) errors.Add("Zone point is outside playable bounds.");
                var delta = ZoneRoute[(i + 1) % ZoneRoute.Length] - ZoneRoute[i];
                if (delta.sqrMagnitude < .01f || delta.y * delta.y > delta.x * delta.x + delta.z * delta.z)
                    errors.Add("Zone route has a zero-length or unwalkable segment.");
            }
            return errors;
        }

        public bool Contains(Vector3 point) => Finite(point) && point.x >= Minimum.x && point.x <= Maximum.x &&
            point.y >= Minimum.y && point.y <= Maximum.y && point.z >= Minimum.z && point.z <= Maximum.z;

        public static bool KnownItem(string id) => id == "line-driver" || id == "pulse-lobber" || id == "rebound-disc" ||
            id == "push-lens" || id == "grenade" || id == "stun-gun" || id == "laser-sword" || id == "blink" || id == "shield";
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
