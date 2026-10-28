using System.Collections.Generic;
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
            return errors;
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
