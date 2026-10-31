using NUnit.Framework;
using UnityEngine;
using Wildstone.Editor;
using Wildstone.Maps;
using Wildstone.Maps.Source;

namespace Wildstone.Tests
{
    public sealed class ArenaWorldTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DefaultArenaSupportsTheFullZoneAndStarts(int index)
        {
            var map = ArenaLayouts.CreateAll()[index];
            var root = ArenaBuilder.CreateGeometry(map);
            try { Assert.That(ArenaValidation.ValidateWorld(root), Is.Empty, map.Id); }
            finally { Object.DestroyImmediate(root); }
        }

        // Backlog 4: pickups and the zone are reachable from every start (and back) with walking, steps and plain jumps.
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EveryStartReachesEveryPickupAndZonePointByPlainMovement(int index)
        {
            var map = ArenaLayouts.CreateAll()[index];
            var root = ArenaBuilder.CreateGeometry(map);
            try
            {
                var report = new System.Collections.Generic.List<string>();
                Assert.That(ArenaValidation.ValidateTraversal(root, report), Is.Empty, map.Id);
                Debug.Log("WILDSTONE TRAVERSAL " + map.Id + " (team 0 / 1 / 2 / 3 nearest start)\n" + string.Join("\n", report));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void RemovingTheGalleryRampsStrandsItsPickups()
        {
            var map = ArenaLayouts.CreateAll()[0];
            map.Pieces = System.Array.FindAll(map.Pieces, piece => !piece.Name.EndsWith("ascent"));
            var root = ArenaBuilder.CreateGeometry(map);
            try { Assert.That(ArenaValidation.ValidateTraversal(root), Has.Some.Contains("cannot reach laser-sniper")); }
            finally { Object.DestroyImmediate(root); }
        }

        // The crate's faces sit 0.1 m past a grid cell, so no floor cell next to it is standable: only a longer jump reaches the top.
        [TestCase(1.6f, true)] [TestCase(2.6f, false)]
        public void APickupOnACrateNeedsAJumpWithinReach(float height, bool reachable)
        {
            var map = ArenaLayouts.CreateAll()[0];
            var crate = new ArenaPiece { Name = "Test crate", Position = new Vector3(-6.25f, height / 2, -8.25f), Size = new Vector3(3.8f, height, 3.8f) };
            map.Pieces = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Append(map.Pieces, crate));
            int pistol = System.Array.FindIndex(map.Pickups, pickup => pickup.Item == "laser-pistol");
            map.Pickups[pistol].Position = new Vector3(-6.25f, height + .45f, -8.25f);
            var root = ArenaBuilder.CreateGeometry(map);
            try
            {
                var errors = ArenaValidation.ValidateTraversal(root);
                if (reachable) Assert.That(errors, Is.Empty);
                else Assert.That(errors, Has.Some.Contains("cannot reach laser-pistol"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        // Revision 3's 6 m outer walls stood 2 m above the +4 m floors: flush (Atrium's gallery, Terrace Steps' upper court)
        // or 3 m short of them (Split Gallery's crossover).
        [TestCase(0, "East boundary")] [TestCase(1, "North boundary")] [TestCase(2, "East boundary")]
        public void SixMetreOuterWallsAreAnEscape(int index, string wall)
        {
            var map = ArenaLayouts.CreateAll()[index];
            for (int i = 0; i < map.Pieces.Length; i++)
                if (map.Pieces[i].Name.EndsWith("boundary")) { map.Pieces[i].Size.y = 6; map.Pieces[i].Position.y = 3; }
            var root = ArenaBuilder.CreateGeometry(map);
            try { Assert.That(ArenaValidation.ValidateTraversal(root), Has.Some.Contains(wall + " top is within jump reach")); }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void RemovingAllFloorSupportIsDetected()
        {
            var map = ArenaLayouts.CreateAll()[0];
            var root = ArenaBuilder.CreateGeometry(map);
            try
            {
                foreach (var collider in root.GetComponentsInChildren<Collider>()) collider.enabled = false;
                Assert.That(ArenaValidation.ValidateWorld(root), Has.Some.Contains("unsupported"));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SavedCatalogContainsSupportedLaserPickups(int index)
        {
            var catalog=Resources.Load<ArenaCatalog>("ArenaCatalog");
            var map=catalog.Maps[index];
            foreach(string item in new[]{"laser-pistol","laser-shotgun","laser-sniper"})
                Assert.That(System.Array.FindAll(map.Pickups,p=>p.Item==item).Length,Is.EqualTo(1),map.Id+" "+item);
            var root=ArenaBuilder.CreateGeometry(map);
            try { Assert.That(ArenaValidation.ValidateWorld(root),Is.Empty,map.Id); }
            finally { Object.DestroyImmediate(root); }
        }

        // The first view of a match: each seat looks out through its shelter opening, not into its own arrival screens.
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SpawnsFaceTheirShelterOpening(int index)
        {
            var map = ArenaLayouts.CreateAll()[index];
            var root = ArenaBuilder.CreateGeometry(map);
            try
            {
                Physics.SyncTransforms();
                var colliders = root.GetComponentsInChildren<Collider>();
                foreach (var spawn in map.Spawns)
                {
                    var ray = new Ray(spawn.Position + Vector3.up * 1.65f, Quaternion.Euler(0, spawn.Yaw, 0) * Vector3.forward);
                    Collider first = null; float nearest = 40;
                    foreach (var collider in colliders)
                        if (collider.Raycast(ray, out var hit, nearest)) { nearest = hit.distance; first = collider; }
                    string where = map.Id + " team " + spawn.Team + " " + spawn.Position;
                    Assert.That(first == null || !first.name.StartsWith("Arrival"), Is.True, where + " faces " + first?.name);
                    Assert.That(nearest, Is.GreaterThan(4.5f), where + " faces " + first?.name);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0, "Gallery fin east", 27f)]
        [TestCase(2, "Cantilever tower", 28f)]
        public void LandmarkBlockingOnlyTheZonePerimeterIsDetected(int index, string landmark, float oldZ)
        {
            var map = ArenaLayouts.CreateAll()[index];
            for (int i = 0; i < map.Pieces.Length; i++)
                if (map.Pieces[i].Name == landmark) map.Pieces[i].Position.z = oldZ;
            var root = ArenaBuilder.CreateGeometry(map);
            try { Assert.That(ArenaValidation.ValidateWorld(root), Has.Some.Contains("Zone footprint")); }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void SourceChamferRetainsBoundsAndTriangleBudget()
        {
            var mesh = LevelMesh.ChamferedBox(new Vector3(8, 2, 4), .06f);
            try
            {
                Assert.That(mesh.triangles.Length / 3, Is.EqualTo(44));
                Assert.That(Vector3.Distance(mesh.bounds.size, Vector3.one), Is.LessThan(.0001f));
                foreach (var normal in mesh.normals) Assert.That(normal.sqrMagnitude, Is.EqualTo(1).Within(.0001f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
