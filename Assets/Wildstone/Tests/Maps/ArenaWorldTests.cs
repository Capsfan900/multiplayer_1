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
