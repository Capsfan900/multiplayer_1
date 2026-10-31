using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wildstone.Maps;
using Wildstone.Movement;

namespace Wildstone.Editor
{
    public static class ArenaValidation
    {
        public static List<string> ValidateWorld(GameObject root)
        {
            var errors = new List<string>();
            var map = root.GetComponent<ArenaMap>().Definition;
            var colliders = root.GetComponentsInChildren<Collider>();
            var ids = new HashSet<int>();
            foreach (var collider in colliders)
            {
                var surface = collider.GetComponent<MotorSurface>();
                if (surface == null || surface.Id <= 0 || !ids.Add(surface.Id)) errors.Add("Missing/duplicate replay surface ID: " + collider.name);
            }
            Physics.SyncTransforms();
            foreach (var spawn in map.Spawns)
                if (!SupportedAndClear(spawn.Position, colliders, root.transform, .2f)) errors.Add("Spawn is unsupported or blocked for team " + spawn.Team);
            foreach (var pickup in map.Pickups)
                if (!SupportedAndClear(pickup.Position - Vector3.up * .45f, colliders, root.transform, .2f)) errors.Add("Pickup is unsupported or blocked: " + pickup.Item);
            for (int i = 0; i < map.Spawns.Length; i++) for (int j = i + 1; j < map.Spawns.Length; j++)
            {
                if (map.Spawns[i].Team == map.Spawns[j].Team) continue;
                var start = map.Spawns[i].Position + Vector3.up * 1.4f;
                var delta = map.Spawns[j].Position + Vector3.up * 1.4f - start;
                if (!Hit(new Ray(start, delta.normalized), delta.magnitude, colliders, out _))
                    errors.Add("Opening enemy spawn sightline: teams " + map.Spawns[i].Team + "/" + map.Spawns[j].Team);
            }
            for (int i = 0; i < map.ZoneRoute.Length; i++)
            {
                var start = map.ZoneRoute[i];
                var end = map.ZoneRoute[(i + 1) % map.ZoneRoute.Length];
                int steps = Mathf.CeilToInt(Vector3.Distance(start, end) / .5f);
                for (int step = 0; step <= steps; step++)
                {
                    var center = Vector3.Lerp(start, end, (float)step / steps);
                    if (!Footprint(center, colliders, root.transform))
                    {
                        errors.Add("Zone footprint lacks support or clearance on segment " + i + " at " + center);
                        break;
                    }
                }
            }
            errors.AddRange(ValidateTraversal(root));
            return errors;
        }

        // Plain traversal: a 0.5 m grid of standable floors (walkable normal, capsule clearance) linked by walking (rise up
        // to WalkRise: steps and ramps), a jump onto a ledge up to JumpRise (the motor's apex is 2.4 m) or a drop. Jumps and
        // drops may land up to Leap away: the capsule keeps grid cells next to a ledge face unstandable, so adjacent cells
        // alone would miss most ledges. Wall-runs, dashes and launches only add routes, so a failure here means a place needs
        // advanced movement. Every start must reach every pickup and zone point and get back. The report gives each team's
        // walking distance per destination.
        public const float Cell = .5f, WalkRise = .45f, JumpRise = 2.2f, Leap = 1.15f, EscapeClearance = 2.8f, EscapeReach = 8f;
        public static List<string> ValidateTraversal(GameObject root, List<string> report = null)
        {
            var errors = new List<string>();
            var map = root.GetComponent<ArenaMap>().Definition;
            Physics.SyncTransforms();
            int nx = Mathf.FloorToInt((map.Maximum.x - map.Minimum.x) / Cell), nz = Mathf.FloorToInt((map.Maximum.z - map.Minimum.z) / Cell);
            var nodes = new List<Vector3>();
            var surfaces = new List<string>();
            var columns = new List<(float y, int node)>[nx * nz];
            float depth = map.Maximum.y - map.Minimum.y + 2;
            for (int iz = 0; iz < nz; iz++) for (int ix = 0; ix < nx; ix++)
            {
                var hits = new List<(float y, int node)>();
                var top = new Vector3(map.Minimum.x + (ix + .5f) * Cell, map.Maximum.y + 1, map.Minimum.z + (iz + .5f) * Cell);
                foreach (var hit in Physics.RaycastAll(top, Vector3.down, depth, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider.transform.IsChildOf(root.transform)) continue;
                    int node = -1;
                    if (hit.normal.y >= .7f && Clear(hit.point, root.transform)) { node = nodes.Count; nodes.Add(hit.point); surfaces.Add(hit.collider.name); }
                    hits.Add((hit.point.y, node));
                }
                hits.Sort((a, b) => b.y.CompareTo(a.y));
                columns[iz * nx + ix] = hits;
            }
            var forward = new List<(int to, float cost)>[nodes.Count];
            var backward = new List<(int to, float cost)>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) { forward[i] = new List<(int, float)>(); backward[i] = new List<(int, float)>(); }
            for (int iz = 0; iz < nz; iz++) for (int ix = 0; ix < nx; ix++)
                foreach (var (_, from) in columns[iz * nx + ix])
                {
                    if (from < 0) continue;
                    var a = nodes[from];
                    for (int dz = -2; dz <= 2; dz++) for (int dx = -2; dx <= 2; dx++)
                    {
                        int jx = ix + dx, jz = iz + dz;
                        float cost = Cell * Mathf.Sqrt(dx * dx + dz * dz);
                        if ((dx == 0 && dz == 0) || cost > Leap || jx < 0 || jz < 0 || jx >= nx || jz >= nz) continue;
                        bool adjacent = Mathf.Abs(dx) <= 1 && Mathf.Abs(dz) <= 1;
                        var column = columns[jz * nx + jx];
                        foreach (var (y, to) in column)
                        {
                            if (to < 0) continue;
                            var b = nodes[to];
                            float rise = y - a.y;
                            bool linked;
                            if (Mathf.Abs(rise) <= WalkRise) linked = adjacent;
                            else if (rise > 0)
                            {
                                var apex = a + Vector3.up * rise;
                                linked = rise <= JumpRise && Sweep(a, apex, root.transform) && Sweep(apex, b, root.transform);
                            }
                            else
                            {
                                // A drop lands on the first surface below the ledge in that column, if that is a floor.
                                int landing = column.FindIndex(h => h.y < a.y + .05f);
                                linked = column[landing].node == to && Sweep(a, new Vector3(b.x, a.y, b.z), root.transform);
                            }
                            if (!linked) continue;
                            forward[from].Add((to, cost)); backward[to].Add((from, cost));
                        }
                    }
                }
            int Find(Vector3 floor)
            {
                int best = -1; float nearest = float.MaxValue;
                int cx = Mathf.FloorToInt((floor.x - map.Minimum.x) / Cell), cz = Mathf.FloorToInt((floor.z - map.Minimum.z) / Cell);
                for (int jz = cz - 2; jz <= cz + 2; jz++) for (int jx = cx - 2; jx <= cx + 2; jx++)
                {
                    if (jx < 0 || jz < 0 || jx >= nx || jz >= nz) continue;
                    foreach (var (y, node) in columns[jz * nx + jx])
                    {
                        if (node < 0 || Mathf.Abs(y - floor.y) > .3f) continue;
                        float distance = new Vector2(nodes[node].x - floor.x, nodes[node].z - floor.z).sqrMagnitude;
                        if (distance < nearest) { nearest = distance; best = node; }
                    }
                }
                return best;
            }
            var targets = new List<(string name, int node)>();
            foreach (var pickup in map.Pickups) targets.Add((pickup.Item + " " + pickup.Position, Find(pickup.Position - Vector3.up * .45f)));
            for (int i = 0; i < map.ZoneRoute.Length; i++) targets.Add(("zone point " + i, Find(map.ZoneRoute[i])));
            foreach (var target in targets) if (target.node < 0) errors.Add("Traversal: no standable floor at " + target.name);
            var nearestByTeam = new Dictionary<int, float[]>();
            var reached = new bool[nodes.Count];
            foreach (var spawn in map.Spawns)
            {
                int start = Find(spawn.Position);
                if (start < 0) { errors.Add("Traversal: no standable floor at team " + spawn.Team + " start"); continue; }
                var there = Distances(start, forward);
                var back = Distances(start, backward);
                for (int i = 0; i < reached.Length; i++) reached[i] |= there[i] < float.MaxValue;
                if (!nearestByTeam.TryGetValue(spawn.Team, out var best)) nearestByTeam[spawn.Team] = best = Enumerable.Repeat(float.MaxValue, targets.Count).ToArray();
                for (int t = 0; t < targets.Count; t++)
                {
                    if (targets[t].node < 0) continue;
                    if (there[targets[t].node] == float.MaxValue) errors.Add("Traversal: team " + spawn.Team + " cannot reach " + targets[t].name);
                    else if (back[targets[t].node] == float.MaxValue) errors.Add("Traversal: no way back from " + targets[t].name + " to team " + spawn.Team);
                    best[t] = Mathf.Min(best[t], there[targets[t].node]);
                }
            }
            // Escape: a running jump lifts the feet 2.4 m and carries them several metres, so each outer wall must stand
            // EscapeClearance above every reachable floor within EscapeReach of it. Advanced movement is not covered.
            foreach (var wall in root.GetComponentsInChildren<Collider>())
            {
                if (!wall.name.EndsWith("boundary")) continue;
                float top = wall.bounds.max.y;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (!reached[i] || surfaces[i].EndsWith("boundary") || top - nodes[i].y >= EscapeClearance) continue;
                    var near = wall.ClosestPoint(new Vector3(nodes[i].x, top, nodes[i].z));
                    if (new Vector2(near.x - nodes[i].x, near.z - nodes[i].z).magnitude >= EscapeReach) continue;
                    errors.Add("Traversal: " + wall.name + " top is within jump reach of " + surfaces[i] + " at " + nodes[i]);
                    break;
                }
            }
            if (report != null)
            {
                for (int t = 0; t < targets.Count; t++)
                {
                    var teams = nearestByTeam.OrderBy(pair => pair.Key).Select(pair => pair.Value[t]).ToArray();
                    report.Add(targets[t].name + ": " + string.Join(" / ", teams.Select(d => d == float.MaxValue ? "-" : d.ToString("F0"))) +
                        " m, spread " + (teams.Max() - teams.Min()).ToString("F0") + " m");
                }
                float highest = Enumerable.Range(0, nodes.Count).Where(i => reached[i]).Max(i => nodes[i].y);
                report.Add("Highest plain-reachable floor " + highest.ToString("F1") + " m");
            }
            return errors;
        }

        private static float[] Distances(int start, List<(int to, float cost)>[] edges)
        {
            var distance = Enumerable.Repeat(float.MaxValue, edges.Length).ToArray();
            var open = new SortedSet<(float, int)>();
            distance[start] = 0; open.Add((0, start));
            while (open.Count > 0)
            {
                var (d, node) = open.Min; open.Remove(open.Min);
                foreach (var (to, cost) in edges[node])
                    if (d + cost < distance[to])
                    {
                        if (distance[to] < float.MaxValue) open.Remove((distance[to], to));
                        distance[to] = d + cost; open.Add((distance[to], to));
                    }
            }
            return distance;
        }

        private static bool Clear(Vector3 floor, Transform root)
        {
            foreach (var collider in Physics.OverlapCapsule(floor + Vector3.up * .43f, floor + Vector3.up * 1.43f, .38f, ~0, QueryTriggerInteraction.Ignore))
                if (collider.transform.IsChildOf(root)) return false;
            return true;
        }

        // The same capsule moved in a straight line from a clear start.
        private static bool Sweep(Vector3 from, Vector3 to, Transform root)
        {
            var path = to - from;
            foreach (var hit in Physics.CapsuleCastAll(from + Vector3.up * .43f, from + Vector3.up * 1.43f, .38f, path.normalized, path.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.transform.IsChildOf(root)) return false;
            return true;
        }

        private static bool Footprint(Vector3 center, Collider[] colliders, Transform root)
        {
            if (!SupportedAndClear(center, colliders, root, .2f)) return false;
            foreach (float radius in new[] { 2f, 4f })
                for (int point = 0; point < 24; point++)
                {
                    float angle = point * Mathf.PI / 12;
                    var expected = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    if (!SupportedAndClear(expected, colliders, root, 1.25f)) return false;
                }
            return true;
        }

        private static bool SupportedAndClear(Vector3 expected, Collider[] colliders, Transform root, float tolerance)
        {
            if (!Hit(new Ray(expected + Vector3.up * 1.4f, Vector3.down), 2.8f, colliders, out var floor) ||
                floor.normal.y < .7f || Mathf.Abs(floor.point.y - expected.y) > tolerance) return false;
            var overlaps = Physics.OverlapCapsule(floor.point + Vector3.up * .43f, floor.point + Vector3.up * 1.43f,
                .38f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var collider in overlaps)
                if (collider.transform.IsChildOf(root)) return false;
            return true;
        }

        private static bool Hit(Ray ray, float length, Collider[] colliders, out RaycastHit nearest)
        {
            nearest = default;
            bool found = false;
            foreach (var collider in colliders)
                if (!collider.isTrigger && collider.Raycast(ray, out var hit, length) && (!found || hit.distance < nearest.distance))
                { nearest = hit; found = true; }
            return found;
        }
    }
}
