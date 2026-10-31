// Adapted from the visual-polish branch (25c1afd) ArenaBackdrop: distant, unreachable architecture framing each arena.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wildstone.Maps;
using Wildstone.Maps.Source;

namespace Wildstone.Editor
{
    public static class ArenaBackdrop
    {
        public static GameObject Create(ArenaDefinition map, Material[] palette)
        {
            var batches = new[] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };
            float halfWidth = (map.Maximum.x - map.Minimum.x) * .5f;
            float north = map.Maximum.z + 15;
            float west = map.Minimum.x - 17;
            float east = map.Maximum.x + 18;
            float height = map.Id == "split-gallery" ? 22 : map.Id == "terrace-steps" ? 16 : 19;

            void Box(float x, float y, float z, float width, float tall, float deep, int tone = 0)
            {
                var size = new Vector3(width, tall, deep);
                batches[tone].Add(new CombineInstance {
                    mesh = LevelMesh.Shared(size, .12f),
                    transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.identity, size)
                });
            }

            // The distant gallery continues the arena's language without suggesting reachable cover.
            Box(0, 4, north, halfWidth * 2 + 30, 8, 14, 1);
            Box(0, height, north, halfWidth * 2 + 30, 1.2f, 12);
            Box(0, height + .8f, north + 5, halfWidth * 2 + 30, .4f, 2, 1);
            for (float x = -halfWidth - 10; x <= halfWidth + 10; x += 12)
            {
                Box(x, (height + 8) * .5f, north, 2.2f, height - 8, 9);
                Box(x, 8.5f, north - 6, 4.8f, 1, 1, 1);
            }
            Box(-halfWidth * .65f, 15, north + 14, 16, 30, 10);
            Box(-halfWidth * .65f + 9, 25, north + 14, 2, 20, 10, 1);
            Box(halfWidth * .65f, 12, north + 17, 22, 24, 12, 1);
            Box(halfWidth * .65f, 26, north + 17, 24, 1.2f, 14);

            // Stepped west mass and an open east pavilion make the skyline asymmetric.
            Box(west, 11, -9, 14, 22, 22);
            Box(west - 4, 25, -13, 8, 6, 14, 1);
            Box(west - 1, 28.5f, -13, 16, 1, 16);
            Box(west + 2, 6, 16, 18, 12, 20, 1);
            Box(west + 2, 12.6f, 16, 20, 1.2f, 22);
            for (int i = 0; i < 4; i++)
                Box(west + 7.1f, 15.4f, -17 + i * 5, .3f, 8, 1.8f, 2);
            Box(east, 6, 4, 18, 12, 42, 1);
            Box(east, 17.5f, 4, 19, 1, 44);
            for (int i = 0; i < 5; i++)
                Box(east - 6, 14.5f, -14 + i * 9, 3, 5, 2.2f);
            Box(east + 6, 20, 19, 4, 16, 8);
            Box(east + 6, 28.5f, 23, 18, 1, 16);

            // A second scale of quiet silhouettes provides depth beyond the near gallery.
            for (int i = 0; i < 4; i++)
            {
                float x = -halfWidth - 22 + i * (halfWidth * 2 + 44) / 3;
                float h = 32 + (i % 3) * 8;
                Box(x, h * .5f, north + 42 + (i % 2) * 13, 14 + i * 2, h, 12, i % 2);
                Box(x + 4, h + .5f, north + 42 + (i % 2) * 13, 25, 1, 14);
            }
            float south = map.Minimum.z - 17;
            Box(-halfWidth * .55f, 7, south, 30, 14, 12, 1);
            Box(-halfWidth * .55f, 14.5f, south, 34, 1, 14);
            Box(halfWidth * .55f, 11, south - 10, 16, 22, 16);
            Box(halfWidth * .55f + 5, 25, south - 10, 3, 6, 16, 1);

            Mesh arch = null;
            if (map.Id == "atrium")
            {
                arch = InterruptedArch(new Vector3(2, 20, north + 24));
                batches[0].Add(new CombineInstance { mesh = arch, transform = Matrix4x4.identity });
            }

            var root = new GameObject("Distant architecture");
            for (int tone = 0; tone < batches.Length; tone++)
            {
                var mesh = new Mesh { name = "Backdrop_" + map.Id + "_" + tone };
                mesh.CombineMeshes(batches[tone].ToArray(), true, true);
                var obj = new GameObject("Stone " + tone, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(root.transform, false);
                obj.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = palette[tone];
                // Distant dressing cannot change tactical shadows inside the arena.
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.scaleInLightmap = .15f;
                GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            }
            if (arch != null) Object.DestroyImmediate(arch);
            return root;
        }

        static Mesh InterruptedArch(Vector3 center)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                vertices.AddRange(new[] { center + a, center + b, center + c, center + d });
                indices.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }
            Vector3 Point(float angle, float radius, float z) => new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z);
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(24, 158, i / (float)segments) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(24, 158, (i + 1f) / segments) * Mathf.Deg2Rad;
                Quad(Point(a, 20, -3), Point(b, 20, -3), Point(b, 17, -3), Point(a, 17, -3));
                Quad(Point(a, 17, 3), Point(b, 17, 3), Point(b, 20, 3), Point(a, 20, 3));
                Quad(Point(a, 20, 3), Point(b, 20, 3), Point(b, 20, -3), Point(a, 20, -3));
                Quad(Point(a, 17, -3), Point(b, 17, -3), Point(b, 17, 3), Point(a, 17, 3));
            }
            // Piers meet the angled arch ends exactly, without coplanar overlapping caps.
            foreach(float angle in new[]{24*Mathf.Deg2Rad,158*Mathf.Deg2Rad})
            {
                var inner=Point(angle,17,-3); var outer=Point(angle,20,-3);
                var left=inner.x<outer.x?inner:outer; var right=inner.x<outer.x?outer:inner;
                var leftFoot=new Vector3(left.x,-center.y,-3); var rightFoot=new Vector3(right.x,-center.y,-3);
                var depth=Vector3.forward*6;
                Quad(right,left,leftFoot,rightFoot);
                Quad(left+depth,right+depth,rightFoot+depth,leftFoot+depth);
                Quad(left,left+depth,leftFoot+depth,leftFoot);
                Quad(right+depth,right,rightFoot,rightFoot+depth);
                Quad(leftFoot+depth,rightFoot+depth,rightFoot,leftFoot);
            }
            var mesh = new Mesh { name = "Interrupted skyline arch" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
