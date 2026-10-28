using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wildstone.Maps
{
    // Defaults seed editable catalog data once. Rebuilding a scene uses the saved catalog.
    public static class ArenaLayouts
    {
        public static ArenaDefinition[] CreateAll() => new[] { Atrium(), SplitGallery(), TerraceSteps() };

        private static ArenaDefinition Atrium()
        {
            var kit = new Kit("atrium", "Atrium", 64, 64);
            kit.Slab("North gallery", 0, 4, 20, 62, 16);
            kit.Ramp("East ascent", new Vector3(20, 0, -4), new Vector3(20, 4, 12), 12);
            kit.Ramp("West ascent", new Vector3(-20, 0, -4), new Vector3(-20, 4, 12), 12);
            kit.Box("Arch west pier", -6, 4, 1, 2, 8, 3);
            kit.Box("Arch east pier", 6, 5, 1, 2, 10, 3);
            kit.Box("Interrupted lintel", -1.5f, 8.5f, 1, 11, 1, 3);
            kit.Box("Gallery fin west", -10, 8, 29, 2, 8, 4, StoneTone.Ash);
            kit.Box("Gallery fin east", 10, 10, 29, 2, 12, 4);
            kit.Box("Court cover west", -9, 1, -17, 5, 2, 1.4f, StoneTone.Ash);
            kit.Box("Court cover east", 9, 1, -17, 5, 2, 1.4f, StoneTone.Ash);
            kit.Box("Gallery parapet west", -8, 4.7f, 12, 10, 1.4f, 1);
            kit.Box("Gallery parapet east", 8, 4.7f, 12, 10, 1.4f, 1);
            kit.CornerStarts(26, 27);
            kit.Pick("pulse-lobber", 0, 0, -2);
            kit.Pick("rebound-disc", -10, 0, -10);
            kit.Pick("push-lens", 10, 0, -10);
            kit.Pick("laser-sword", 0, 4, 18);
            kit.Pick("stun-gun", 0, 0, 8);
            kit.Utilities(14, 18);
            return kit.Finish(new[] { V(0, 0, -8), V(20, 0, -8), V(20, 0, -4), V(20, 4, 12),
                V(20, 4, 22), V(-20, 4, 22), V(-20, 4, 12), V(-20, 0, -4), V(-20, 0, -8) });
        }

        private static ArenaDefinition SplitGallery()
        {
            var kit = new Kit("split-gallery", "Split Gallery", 84, 48);
            kit.Slab("Elevated crossover", 0, 4, 0, 12, 40);
            foreach (int side in new[] { -1, 1 }) foreach (int hall in new[] { -1, 1 })
                kit.Ramp("Crossover ramp " + side + " " + hall, V(side * 20, 0, hall * 14), V(side * 6, 4, hall * 14), 12);
            kit.Box("West divided spine", -14, 3.5f, 0, 16, 7, 3, StoneTone.Ash);
            kit.Box("East divided spine", 14, 3.5f, 0, 16, 7, 3, StoneTone.Ash);
            kit.Box("West skyline fin", -8, 8, 0, 2, 16, 7);
            kit.Box("East skyline fin", 8, 6, 0, 2, 12, 7);
            kit.Box("South hall baffle", 0, 1.2f, -6, 5, 2.4f, 1.5f, StoneTone.Charcoal);
            kit.Box("North hall baffle", 0, 1.2f, 6, 5, 2.4f, 1.5f, StoneTone.Charcoal);
            kit.CornerStarts(37, 19);
            kit.Pick("pulse-lobber", 0, 4, 0);
            kit.Pick("rebound-disc", -28, 0, 0);
            kit.Pick("push-lens", 28, 0, 0);
            kit.Pick("laser-sword", -12, 0, -5);
            kit.Pick("stun-gun", 12, 0, 5);
            kit.Utilities(26, 7);
            return kit.Finish(new[] { V(-28, 0, -14), V(-20, 0, -14), V(-6, 4, -14), V(0, 4, -14),
                V(0, 4, 14), V(6, 4, 14), V(20, 0, 14), V(28, 0, 14), V(28, 0, -14),
                V(20, 0, -14), V(6, 4, -14), V(0, 4, -14), V(0, 4, 14), V(-6, 4, 14),
                V(-20, 0, 14), V(-28, 0, 14) });
        }

        private static ArenaDefinition TerraceSteps()
        {
            var kit = new Kit("terrace-steps", "Terrace Steps", 66, 72);
            kit.Slab("Offset upper court", 6, 4, 16, 50, 32);
            kit.Slab("West return terrace", -25, 4, 22, 12, 20);
            kit.Ramp("West return ascent", V(-25, 0, -4), V(-25, 4, 12), 12);
            kit.Ramp("East broad ascent", V(20, 0, -16), V(20, 4, 0), 12);
            kit.Ramp("Middle ascent", V(-4, 0, -16), V(-4, 4, 0), 12);
            kit.Box("Interrupted wall west", -12, 3.5f, -10, 2, 7, 10, StoneTone.Ash);
            kit.Box("Interrupted wall east", 8, 4, -7, 2, 8, 11);
            kit.Box("Cantilever tower", 9, 10, 29, 4, 12, 4);
            kit.Box("Tower wing", 15, 14.5f, 29, 16, 1, 4);
            kit.Box("Upper court cover", 4, 4.9f, 12, 6, 1.8f, 2, StoneTone.Ash);
            kit.Box("Lower court cover", 5, 1, -28, 7, 2, 2, StoneTone.Ash);
            kit.CornerStarts(27, 30);
            kit.Pick("pulse-lobber", 3, 0, -8);
            kit.Pick("rebound-disc", -14, 4, 7);
            kit.Pick("push-lens", 14, 4, 7);
            kit.Pick("laser-sword", -19, 0, -15);
            kit.Pick("stun-gun", 0, 4, 26);
            kit.Utilities(14, 22);
            return kit.Finish(new[] { V(-24, 0, -21), V(20, 0, -21), V(20, 0, -16), V(20, 4, 0),
                V(20, 4, 22), V(-25, 4, 22), V(-25, 4, 12), V(-25, 0, -4), V(-25, 0, -21) });
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private sealed class Kit
        {
            private readonly ArenaDefinition map;
            private readonly List<ArenaPiece> pieces = new List<ArenaPiece>();
            private readonly List<ArenaSpawn> spawns = new List<ArenaSpawn>();
            private readonly List<ArenaPickup> pickups = new List<ArenaPickup>();
            public Kit(string id, string name, float width, float depth)
            {
                map = new ArenaDefinition { Id = id, DisplayName = name, Minimum = V(-width / 2, -2, -depth / 2), Maximum = V(width / 2, 24, depth / 2) };
                Slab("Foundation", 0, 0, 0, width, depth, 2, StoneTone.Ash);
                Box("South boundary", 0, 3, -depth / 2 + .5f, width, 6, 1);
                Box("North boundary", 0, 3, depth / 2 - .5f, width, 6, 1);
                Box("West boundary", -width / 2 + .5f, 3, 0, 1, 6, depth);
                Box("East boundary", width / 2 - .5f, 3, 0, 1, 6, depth);
            }
            public void Box(string name, float x, float y, float z, float sx, float sy, float sz, StoneTone tone = StoneTone.Ivory) =>
                pieces.Add(new ArenaPiece { Name = name, Position = V(x, y, z), Size = V(sx, sy, sz), Tone = tone });
            public void Slab(string name, float x, float top, float z, float width, float depth, float thickness = 1, StoneTone tone = StoneTone.Ivory) =>
                Box(name, x, top - thickness / 2, z, width, thickness, depth, tone);
            public void Ramp(string name, Vector3 low, Vector3 high, float width)
            {
                var delta = high - low;
                double run = Math.Sqrt(delta.x * delta.x + delta.z * delta.z);
                float length = (float)Math.Sqrt(run * run + delta.y * delta.y);
                var normal = V((float)(-delta.x / run * delta.y / length), (float)run / length, (float)(-delta.z / run * delta.y / length));
                pieces.Add(new ArenaPiece { Name = name, Position = (low + high) / 2 - normal * .4f, Size = V(width, .8f, length),
                    Euler = V((float)(-Math.Atan2(delta.y, run) * 180 / Math.PI), (float)(Math.Atan2(delta.x, delta.z) * 180 / Math.PI), 0) });
            }
            public void CornerStarts(float x, float z)
            {
                int team = 0;
                foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 })
                {
                    for (int seat = 0; seat < 2; seat++) spawns.Add(new ArenaSpawn { Team = team,
                        Position = V(sx * x, .02f, sz * (z - seat * 2)), Yaw = (float)(Math.Atan2(-sx * x, -sz * z) * 180 / Math.PI) });
                    Box("Arrival side screen " + team, sx * (x - 4), 1.6f, sz * (z - 1), 1, 3.2f, 6, StoneTone.Charcoal);
                    Box("Arrival front screen " + team, sx * (x - 1), 1.6f, sz * (z - 4), 6, 3.2f, 1, StoneTone.Charcoal);
                    team++;
                }
            }
            public void Pick(string item, float x, float floor, float z) => pickups.Add(new ArenaPickup { Item = item, Position = V(x, floor + .45f, z) });
            public void Utilities(float x, float z)
            {
                Pick("blink", -x, 0, -z); Pick("shield", x, 0, -z);
                Pick("grenade", -x, 0, z); Pick("grenade", x, 0, z);
            }
            public ArenaDefinition Finish(Vector3[] route)
            {
                map.Pieces = pieces.ToArray(); map.Spawns = spawns.ToArray(); map.Pickups = pickups.ToArray(); map.ZoneRoute = route;
                return map;
            }
        }
    }
}
