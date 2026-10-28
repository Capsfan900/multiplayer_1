// Adapted from vibegame1 commit 2cb6e5760bccc0733f34b367be2f18c90b802766; namespace only. Source project unchanged.
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Wildstone.Maps.Source
{
    /// <summary>
    /// The chamfered box every structural level piece is drawn with (VFX/graphics pass 2026-09-22, item 1;
    /// docs/research/VFX-GRAPHICS-PASS-2026-09-22.md section 5). Before this, every platform, ramp, gate and
    /// realm slab was Unity's 24-vertex cube scaled non-uniformly: a razor-edged slab against the sky whose
    /// shared edges vanished against the next slab. A one-segment chamfer (6 faces, 12 strips, 8 corner
    /// triangles = 96 verts / 44 tris) reads as CUT STONE and is what lets a flat-shaded block catch a
    /// highlight at all; rounded bevels read as plastic.
    ///
    /// <para><b>Built in normalised space, so <c>localScale</c> stays the size.</b> Positions are divided by
    /// the size per axis and normals are pre-multiplied by it, so Unity's inverse-transpose scale restores
    /// the exact world normals under <c>localScale = size</c> and the bevel is a uniform 0.06 m in the world.
    /// Nothing that reads <c>localScale</c> (the exporter, <c>RampFrom</c>, <c>Trim</c>, Level Studio) can
    /// tell the difference, and the <c>BoxCollider</c> of the primitive is left exactly as it was.</para>
    ///
    /// <para><b>What the mesh tells the shader.</b> Flat faces carry uv 0..1 across their flat rectangle (the
    /// Architectural Stone shader recovers face size from derivatives, unchanged); bevel strips and corners
    /// carry <c>u &lt;= -1</c>, the band flag (never 0: every flat face has a u = 0 edge, which would
    /// false-flag a sliver of it). COLOR.r is baked ambient occlusion with G = 1, B = 0 as the marker a
    /// colourless legacy mesh can never produce, whichever default the platform feeds a missing stream.</para>
    ///
    /// <para><b>Caps.</b> A capped face keeps its full extent and its four edges stay sharp: <see cref="Caps.Top"/>
    /// lets a trimmed platform's 0.15 m neon bar sit flush on an unbevelled top edge; <see cref="Caps.EndsX"/> /
    /// <see cref="Caps.EndsZ"/> make a trim bar an octagonal tube whose mesh does not depend on its length, so
    /// every bar in the level shares two meshes.</para>
    /// </summary>
    public static class LevelMesh
    {
        /// <summary>World bevel on a structural piece.</summary>
        public const float Bevel = 0.06f;
        /// <summary>World bevel when the piece's smallest dimension is under <see cref="SmallBelow"/>.</summary>
        public const float SmallBevel = 0.04f;
        public const float SmallBelow = 0.5f;
        /// <summary>World bevel on a 0.15 m trim bar.</summary>
        public const float TrimBevel = 0.02f;
        /// <summary>A bevel never exceeds this fraction of the smallest dimension, or a face would invert.</summary>
        public const float MaxBevelFraction = 0.45f;

        /// <summary>Baked ambient occlusion (COLOR.r): up-facing facets 1.0, side faces 1.0 at the top to
        /// 0.85 at the foot, down-facing bevel strips 0.7, the underside 0.55.</summary>
        public const float AoTop = 1f, AoSideFoot = 0.85f, AoBottomStrip = 0.7f, AoVoid = 0.55f;

        /// <summary>Strips carry u in [-2, -1]; flat faces [0, 1]. The shader flags u &lt; -0.5.</summary>
        public const float StripU = -1f;

        public const int FullVertices = 96, FullTriangles = 44;
        public const int TopCappedVertices = 72, TopCappedTriangles = 36;
        public const int TubeVertices = 48, TubeTriangles = 28;

        /// <summary>Every generated mesh's name starts with this (and nothing else in the project's does).</summary>
        public const string Prefix = "Box_";

        const float MinSize = 0.001f;

        /// <summary>Which faces keep their full extent and sharp edges. Only mutually opposite faces may be
        /// capped, which is all any caller needs.</summary>
        public enum Caps { None, Top, EndsX, EndsZ }

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>0.06 m, or 0.04 m on a piece thinner than 0.5 m.</summary>
        public static float BevelFor(Vector3 size)
        {
            return MinComponent(size) < SmallBelow ? SmallBevel : Bevel;
        }

        /// <summary>The bevel actually built: never more than <see cref="MaxBevelFraction"/> of the smallest
        /// dimension that hosts a bevel.</summary>
        public static float ClampBevel(Vector3 size, float bevel, Caps caps = Caps.None)
        {
            return Mathf.Clamp(bevel, 0f, MaxBevelFraction * MinComponent(Canonical(size, caps)));
        }

        /// <summary>
        /// The shared mesh for a box of this size, built once per (size to the millimetre, bevel, caps) and
        /// reused by every piece that matches. In memory until <see cref="PersistUnder"/> hands it to the
        /// editor's persist hook.
        /// </summary>
        public static Mesh Shared(Vector3 size, float bevel, Caps caps = Caps.None)
        {
            size = Canonical(Quantise(size), caps);
            string name = NameFor(size, bevel, caps);
            Mesh mesh;
            if (cache.TryGetValue(name, out mesh) && mesh != null) return mesh;
            mesh = ChamferedBox(size, bevel, caps);
            cache[name] = mesh;
            return mesh;
        }

        /// <summary>Deterministic, so a persisted asset keeps its name (and GUID) across rebuilds:
        /// <c>Box_12x2x8_b0.06</c>, <c>Box_12x2x8_b0.06_top</c>, <c>Box_Lx0.15x0.15_b0.02_tube</c>.</summary>
        public static string NameFor(Vector3 size, float bevel, Caps caps = Caps.None)
        {
            size = Canonical(Quantise(size), caps);
            float b = ClampBevel(size, bevel, caps);
            string x = caps == Caps.EndsX ? "L" : F(size.x);
            string z = caps == Caps.EndsZ ? "L" : F(size.z);
            string suffix = caps == Caps.Top ? "_top" : caps == Caps.None ? "" : "_tube";
            return Prefix + x + "x" + F(size.y) + "x" + z + "_b" + F(b) + suffix;
        }

        public static bool IsGenerated(Mesh mesh)
        {
            return mesh != null && mesh.name.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Hands every generated mesh under <paramref name="root"/> to <paramref name="persist"/> (the editor
        /// makes it an asset) and points the filters and the cache at what comes back. Called once at the end
        /// of a campaign build, so only meshes a saved scene actually references become assets — a Level
        /// Studio preview or a test never litters the project. Returns the number of distinct meshes.
        /// </summary>
        public static int PersistUnder(Transform root, Func<Mesh, Mesh> persist)
        {
            if (root == null || persist == null) return 0;
            var done = new Dictionary<Mesh, Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!IsGenerated(mesh)) continue;
                Mesh kept;
                if (!done.TryGetValue(mesh, out kept))
                {
                    kept = persist(mesh) ?? mesh;
                    done[mesh] = kept;
                    cache[mesh.name] = kept;
                }
                if (kept != mesh) filter.sharedMesh = kept;
            }
            return done.Count;
        }

        // ------------------------------------------------------------------ the geometry

        /// <summary>
        /// Pure: the chamfered box of <paramref name="size"/> (metres) with a world bevel of
        /// <paramref name="bevel"/>, in normalised space (bounds exactly -0.5..0.5 on every axis).
        /// </summary>
        public static Mesh ChamferedBox(Vector3 size, float bevel, Caps caps = Caps.None)
        {
            size = Canonical(Vector3.Max(size, Vector3.one * MinSize), caps);
            float b = ClampBevel(size, bevel, caps);
            var g = new Builder(size, b, caps);

            for (int a = 0; a < 3; a++)
                for (int s = -1; s <= 1; s += 2)
                    g.Face(a, s);

            for (int a1 = 0; a1 < 3; a1++)
                for (int a2 = a1 + 1; a2 < 3; a2++)
                    for (int s1 = -1; s1 <= 1; s1 += 2)
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                            g.Strip(a1, s1, a2, s2);

            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        g.Corner(sx, sy, sz);

            var mesh = new Mesh { name = NameFor(size, bevel, caps) };
            mesh.SetVertices(g.v);
            mesh.SetNormals(g.n);
            mesh.SetTangents(g.t);
            mesh.SetUVs(0, g.uv);
            mesh.SetColors(g.c);
            mesh.SetTriangles(g.tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        sealed class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>(FullVertices);
            public readonly List<Vector3> n = new List<Vector3>(FullVertices);
            public readonly List<Vector4> t = new List<Vector4>(FullVertices);
            public readonly List<Vector2> uv = new List<Vector2>(FullVertices);
            public readonly List<Color32> c = new List<Color32>(FullVertices);
            public readonly List<int> tri = new List<int>(FullTriangles * 3);

            readonly Vector3 size, h;
            readonly float b;
            readonly Caps caps;

            public Builder(Vector3 size, float bevel, Caps caps)
            {
                this.size = size;
                h = size * 0.5f;
                b = bevel;
                this.caps = caps;
            }

            bool Capped(int axis, int sign)
            {
                if (caps == Caps.Top) return axis == 1 && sign > 0;
                if (caps == Caps.EndsX) return axis == 0;
                if (caps == Caps.EndsZ) return axis == 2;
                return false;
            }

            /// <summary>The edge between two faces is bevelled unless either face is capped.</summary>
            bool Bevelled(int a1, int s1, int a2, int s2) { return !Capped(a1, s1) && !Capped(a2, s2); }

            // Which world axes a face's u and v run along: sides keep v UP (the shader's wall height reads
            // uv.y), decks run u along x and v along z.
            static int UAxis(int a) { return a == 0 ? 2 : 0; }
            static int VAxis(int a) { return a == 1 ? 2 : 1; }

            static Vector3 Axis(int a, float value)
            {
                var p = Vector3.zero;
                p[a] = value;
                return p;
            }

            public void Face(int a, int s)
            {
                int ua = UAxis(a), va = VAxis(a);
                var pts = new List<Vector3>(8);
                if (!Capped(a, s))
                {
                    float u0 = -h[ua] + (Bevelled(a, s, ua, -1) ? b : 0f), u1 = h[ua] - (Bevelled(a, s, ua, 1) ? b : 0f);
                    float v0 = -h[va] + (Bevelled(a, s, va, -1) ? b : 0f), v1 = h[va] - (Bevelled(a, s, va, 1) ? b : 0f);
                    pts.Add(OnFace(a, s, ua, u0, va, v0));
                    pts.Add(OnFace(a, s, ua, u1, va, v0));
                    pts.Add(OnFace(a, s, ua, u1, va, v1));
                    pts.Add(OnFace(a, s, ua, u0, va, v1));
                }
                else
                {
                    // The cap keeps the full rectangle, cut at each corner where the two neighbouring faces
                    // meet on a bevelled edge (the octagon). Sorted by angle: convex, so that is its outline.
                    for (int cu = -1; cu <= 1; cu += 2)
                        for (int cv = -1; cv <= 1; cv += 2)
                        {
                            if (Bevelled(ua, cu, va, cv))
                            {
                                pts.Add(OnFace(a, s, ua, cu * (h[ua] - b), va, cv * h[va]));
                                pts.Add(OnFace(a, s, ua, cu * h[ua], va, cv * (h[va] - b)));
                            }
                            else pts.Add(OnFace(a, s, ua, cu * h[ua], va, cv * h[va]));
                        }
                    pts.Sort((p, q) => Mathf.Atan2(p[va], p[ua]).CompareTo(Mathf.Atan2(q[va], q[ua])));
                }

                // uv 0..1 across the flat polygon's own rectangle.
                float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
                foreach (var p in pts)
                {
                    minU = Mathf.Min(minU, p[ua]); maxU = Mathf.Max(maxU, p[ua]);
                    minV = Mathf.Min(minV, p[va]); maxV = Mathf.Max(maxV, p[va]);
                }
                var uvs = new Vector2[pts.Count];
                for (int i = 0; i < pts.Count; i++)
                    uvs[i] = new Vector2((pts[i][ua] - minU) / Mathf.Max(maxU - minU, 1e-6f),
                                         (pts[i][va] - minV) / Mathf.Max(maxV - minV, 1e-6f));
                Polygon(Axis(a, s), pts.ToArray(), uvs);
            }

            /// <summary>A point on face (a, s), at <paramref name="u"/> / <paramref name="vv"/> metres along its
            /// two in-plane axes.</summary>
            Vector3 OnFace(int a, int s, int ua, float u, int va, float vv)
            {
                var p = Vector3.zero;
                p[a] = s * h[a];
                p[ua] = u;
                p[va] = vv;
                return p;
            }

            public void Strip(int a1, int s1, int a2, int s2)
            {
                if (!Bevelled(a1, s1, a2, s2)) return;
                int a3 = 3 - a1 - a2;
                float lo = -h[a3] + (Capped(a3, -1) ? 0f : b);
                float hi = h[a3] - (Capped(a3, 1) ? 0f : b);
                Vector3 onA = Axis(a1, s1 * h[a1]) + Axis(a2, s2 * (h[a2] - b));
                Vector3 onB = Axis(a1, s1 * (h[a1] - b)) + Axis(a2, s2 * h[a2]);
                var pts = new[] { onA + Axis(a3, lo), onA + Axis(a3, hi), onB + Axis(a3, hi), onB + Axis(a3, lo) };
                var uvs = new[] { new Vector2(StripU, 0f), new Vector2(StripU, 1f), new Vector2(StripU - 1f, 1f), new Vector2(StripU - 1f, 0f) };
                Polygon((Axis(a1, s1) + Axis(a2, s2)).normalized, pts, uvs);
            }

            public void Corner(int sx, int sy, int sz)
            {
                if (Capped(0, sx) || Capped(1, sy) || Capped(2, sz)) return;
                var pts = new[]
                {
                    new Vector3(sx * h.x, sy * (h.y - b), sz * (h.z - b)),
                    new Vector3(sx * (h.x - b), sy * h.y, sz * (h.z - b)),
                    new Vector3(sx * (h.x - b), sy * (h.y - b), sz * h.z),
                };
                var uvs = new[] { new Vector2(StripU, 0f), new Vector2(StripU - 1f, 0f), new Vector2(StripU - 0.5f, 1f) };
                Polygon(new Vector3(sx, sy, sz).normalized, pts, uvs);
            }

            /// <summary>One flat facet: a convex polygon in metres with its WORLD normal. Written in normalised
            /// space with a scale-compensated normal, a tangent from its uv, baked AO, and a fan whose winding
            /// is fixed here against the normal (so no caller can get a face inside out).</summary>
            void Polygon(Vector3 worldNormal, Vector3[] metres, Vector2[] uvs)
            {
                int start = v.Count;
                Vector3 objNormal = Vector3.Scale(worldNormal, size).normalized;
                var p = new Vector3[metres.Length];
                for (int i = 0; i < metres.Length; i++)
                    p[i] = new Vector3(metres[i].x / size.x, metres[i].y / size.y, metres[i].z / size.z);

                Vector4 tangent = Tangent(p[0], p[1], p[2], uvs[0], uvs[1], uvs[2], objNormal);
                for (int i = 0; i < p.Length; i++)
                {
                    v.Add(p[i]);
                    n.Add(objNormal);
                    t.Add(tangent);
                    uv.Add(uvs[i]);
                    c.Add(new Color32((byte)Mathf.RoundToInt(Ao(worldNormal, metres[i].y) * 255f), 255, 0, 255));
                }

                bool flip = Vector3.Dot(Vector3.Cross(metres[1] - metres[0], metres[2] - metres[0]), worldNormal) < 0f;
                for (int i = 1; i + 1 < p.Length; i++)
                {
                    tri.Add(start);
                    tri.Add(start + (flip ? i + 1 : i));
                    tri.Add(start + (flip ? i : i + 1));
                }
            }

            float Ao(Vector3 worldNormal, float y)
            {
                if (worldNormal.y > 0.1f) return AoTop;
                if (worldNormal.y < -0.9f) return AoVoid;
                if (worldNormal.y < -0.1f) return AoBottomStrip;
                return Mathf.Lerp(AoSideFoot, AoTop, Mathf.Clamp01((y + h.y) / Mathf.Max(size.y, 1e-6f)));
            }

            static Vector4 Tangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 w0, Vector2 w1, Vector2 w2, Vector3 normal)
            {
                Vector3 e1 = p1 - p0, e2 = p2 - p0;
                Vector2 d1 = w1 - w0, d2 = w2 - w0;
                float r = d1.x * d2.y - d2.x * d1.y;
                Vector3 tan, bit;
                if (Mathf.Abs(r) < 1e-12f)
                {
                    tan = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right);
                    bit = Vector3.Cross(normal, tan);
                }
                else
                {
                    tan = (e1 * d2.y - e2 * d1.y) / r;
                    bit = (e2 * d1.x - e1 * d2.x) / r;
                }
                tan = (tan - normal * Vector3.Dot(normal, tan)).normalized;
                float w = Vector3.Dot(Vector3.Cross(normal, tan), bit) < 0f ? -1f : 1f;
                return new Vector4(tan.x, tan.y, tan.z, w);
            }
        }

        // ------------------------------------------------------------------ small helpers

        static float MinComponent(Vector3 v) { return Mathf.Min(v.x, Mathf.Min(v.y, v.z)); }

        /// <summary>Millimetres: a ramp's irrational slope length still shares a mesh with its twin.</summary>
        static Vector3 Quantise(Vector3 v)
        {
            return new Vector3(Mathf.Round(v.x * 1000f) / 1000f, Mathf.Round(v.y * 1000f) / 1000f, Mathf.Round(v.z * 1000f) / 1000f);
        }

        /// <summary>A tube's mesh does not depend on its length: build it at length 1.</summary>
        static Vector3 Canonical(Vector3 size, Caps caps)
        {
            if (caps == Caps.EndsX) size.x = 1f;
            if (caps == Caps.EndsZ) size.z = 1f;
            return size;
        }

        static string F(float value)
        {
            return (Mathf.Round(value * 1000f) / 1000f).ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
