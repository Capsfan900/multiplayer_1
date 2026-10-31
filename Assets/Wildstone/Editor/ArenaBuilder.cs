using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wildstone.Maps;
using Wildstone.Maps.Source;
using Wildstone.Movement;

namespace Wildstone.Editor
{
    public static class ArenaBuilder
    {
        public const string CatalogPath = "Assets/Wildstone/Resources/ArenaCatalog.asset";
        private const string MapsPath = "Assets/Wildstone/Maps";

        [MenuItem("Wildstone/Build Arenas From Catalog")]
        public static void BuildAll()
        {
            ProjectSetup.Configure();
            Directory.CreateDirectory("Assets/Wildstone/Resources");
            Directory.CreateDirectory(MapsPath + "/Geometry");
            var catalog = AssetDatabase.LoadAssetAtPath<ArenaCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArenaCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            // ArenaLayouts is the source of truth: a revision bump (new pickups or geometry) refreshes the shipped snapshot,
            // and the rebuilt scenes then need a fresh bake.
            var authored = ArenaLayouts.CreateAll();
            if (catalog.Maps == null || !catalog.Maps.Select(m => m.Id + "@" + m.Revision).SequenceEqual(authored.Select(m => m.Id + "@" + m.Revision)))
            {
                catalog.Maps = authored;
                EditorUtility.SetDirty(catalog);
            }
            if (catalog.Maps == null || catalog.Maps.Length != 3 || catalog.Maps.Select(m => m.Id).Distinct().Count() != 3)
                throw new InvalidOperationException("Catalog must contain the three distinct MVP arenas.");
            foreach (var definition in catalog.Maps) Build(definition);
            AssetDatabase.SaveAssets();
            Debug.Log("WILDSTONE ARENAS AUTHORED: three saved scenes; lighting bake and traversal QA are separate gates.");
        }

        public static GameObject CreateGeometry(ArenaDefinition definition)
        {
            var errors = definition.Validate();
            if (errors.Count != 0) throw new InvalidOperationException(definition.Id + ": " + string.Join("; ", errors));
            var root = new GameObject(definition.DisplayName);
            root.AddComponent<ArenaMap>().Definition = definition;
            for (int i = 0; i < definition.Pieces.Length; i++)
            {
                var piece = definition.Pieces[i];
                var obj = new GameObject(piece.Name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(MotorSurface));
                obj.transform.SetParent(root.transform, false);
                obj.transform.localPosition = piece.Position;
                obj.transform.localRotation = Quaternion.Euler(piece.Euler);
                obj.transform.localScale = piece.Size;
                obj.GetComponent<MeshFilter>().sharedMesh = LevelMesh.Shared(piece.Size, LevelMesh.BevelFor(piece.Size));
                obj.GetComponent<MotorSurface>().Id = i + 1;
                GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            }
            return root;
        }

        private static void Build(ArenaDefinition definition)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var root = CreateGeometry(definition);
                var palette = Palette();
                for (int i = 0; i < definition.Pieces.Length; i++)
                    root.transform.GetChild(i).GetComponent<MeshRenderer>().sharedMaterial = palette[(int)definition.Pieces[i].Tone];
                Physics.SyncTransforms();
                var validation = ArenaValidation.ValidateWorld(root);
                if (validation.Count > 0) throw new InvalidOperationException(definition.Id + ": " + string.Join("; ", validation));
                LevelMesh.PersistUnder(root.transform, PersistMesh);
                // Near skyline architecture frames each arena (adapted from the visual-polish branch's ArenaBackdrop).
                var backdrop = ArenaBackdrop.Create(definition, palette);
                backdrop.transform.SetParent(root.transform, false);
                foreach (var filter in backdrop.GetComponentsInChildren<MeshFilter>()) filter.sharedMesh = PersistMesh(filter.sharedMesh, true);
                AddLighting(root, definition);
                var path = MapsPath + "/" + definition.Id + ".unity";
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save arena " + path);
                var scenes = EditorBuildSettings.scenes.ToList();
                if (!scenes.Any(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        // replace: generated meshes (the backdrop) overwrite their saved asset; shared kit meshes are reused as saved.
        private static Mesh PersistMesh(Mesh mesh) => PersistMesh(mesh, false);
        private static Mesh PersistMesh(Mesh mesh, bool replace)
        {
            var path = MapsPath + "/Geometry/" + mesh.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                Unwrapping.GenerateSecondaryUVSet(mesh);
                AssetDatabase.CreateAsset(mesh, path);
                saved = mesh;
            }
            else if (replace)
            {
                Unwrapping.GenerateSecondaryUVSet(mesh);
                EditorUtility.CopySerialized(mesh, saved);
                UnityEngine.Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(saved);
            }
            else if (saved.uv2.Length != saved.vertexCount)
            {
                Unwrapping.GenerateSecondaryUVSet(saved);
                EditorUtility.SetDirty(saved);
            }
            return saved;
        }

        // D028 stone: the audited vibegame1 architectural shader in its procedural mode (large slabs, hairline joints,
        // real bevels). D034 tunes it to the visual-polish look: clean ivory/cool-grey 4 m panels, almost no grain or
        // wear, and only a faint neutral inlay on charcoal. The generator owns these values on every rebuild.
        private static readonly string[] ToneNames = { "Ivory", "Ash", "Charcoal" };
        private static readonly Color[] DeckTones = { new Color(.82f, .81f, .77f), new Color(.63f, .65f, .64f), new Color(.34f, .36f, .37f) };
        private static readonly Color[] WallTones = { new Color(.82f, .80f, .75f), new Color(.63f, .65f, .64f), new Color(.29f, .31f, .32f) };
        private static readonly Color[] VoidTones = { new Color(.46f, .46f, .45f), new Color(.34f, .35f, .35f), new Color(.15f, .16f, .17f) };
        private static readonly float[] InlayStrength = { 0, 0, .45f };

        private static Material[] Palette()
        {
            var shader = Shader.Find("Wildstone/Architectural Stone");
            if (shader == null) throw new InvalidOperationException("Wildstone/Architectural Stone shader is unavailable.");
            var result = new Material[ToneNames.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var path = "Assets/Wildstone/Settings/Stone-" + ToneNames[i] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader) { name = "Stone " + ToneNames[i] };
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = shader;
                m.SetColor("_DeckColor", DeckTones[i]);
                m.SetColor("_BaseColor", WallTones[i]);
                m.SetColor("_VoidColor", VoidTones[i]);
                m.SetFloat("_Smoothness", .2f);
                m.SetFloat("_DeckSmoothness", .22f);
                m.SetFloat("_PhotoBlend", 0);
                m.SetVector("_DeckBlock", new Vector4(4, 4, 0, 0));
                m.SetVector("_WallBlock", new Vector4(4, 4, 0, 0));
                m.SetFloat("_JointWidth", .01f);
                m.SetFloat("_GrainStrength", .02f);
                m.SetFloat("_EdgeWear", .03f);
                m.SetFloat("_ChipStrength", .02f);
                m.SetFloat("_CavityStrength", .1f);
                m.SetFloat("_EdgeSheen", .16f);
                m.SetColor("_InlayEmission", new Color(1f, .96f, .9f) * InlayStrength[i]);
                foreach (var name in new[] { "_DeckAlbedo", "_DeckMask", "_DeckNormal", "_WallAlbedo", "_WallMask", "_WallNormal" }) m.SetTexture(name, null);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                EditorUtility.SetDirty(m);
                result[i] = m;
            }
            return result;
        }

        private static Material SharedMaterial(string name, string shaderName)
        {
            var path = "Assets/Wildstone/Settings/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException(shaderName + " shader is unavailable.");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T Grade<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing)) return existing;
            var added = profile.Add<T>();
            added.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(added, profile);
            return added;
        }

        private static void AddLighting(GameObject root, ArenaDefinition definition)
        {
            // D034: the visual-polish daylight. A pale neutral sky ambient and haze, a near-white key.
            var horizon = new Color(.79f, .81f, .80f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.62f, .68f, .74f);
            RenderSettings.ambientEquatorColor = new Color(.74f, .76f, .75f);
            RenderSettings.ambientGroundColor = new Color(.49f, .49f, .46f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = horizon;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 65;
            RenderSettings.fogEndDistance = 230;
            var sun = new GameObject("Soft key", typeof(Light));
            sun.transform.SetParent(root.transform, false);
            sun.transform.rotation = Quaternion.Euler(44, -32, 0);
            var key = sun.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1, .97f, .92f);
            key.intensity = 1.35f;
            key.shadows = LightShadows.Soft;
            key.lightmapBakeType = LightmapBakeType.Mixed;
            RenderSettings.sun = key;
            Vector4 toSun = -sun.transform.forward;
            var strip = SharedMaterial("CeilingStrip", "Universal Render Pipeline/Unlit");
            strip.SetColor("_BaseColor", new Color(1.75f, 1.8f, 1.85f));
            strip.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(strip);
            // Broad baked ceiling bounce keeps shaded players readable under overhangs without runtime lights.
            foreach (var piece in definition.Pieces)
            {
                float underside = piece.Position.y - piece.Size.y * .5f;
                if (piece.Euler.sqrMagnitude > .001f || piece.Size.y > 1.2f || underside < 2.4f || Mathf.Min(piece.Size.x, piece.Size.z) < 6) continue;
                var fill = new GameObject(piece.Name + " baked bounce", typeof(Light));
                fill.transform.SetParent(root.transform, false);
                fill.transform.SetPositionAndRotation(new Vector3(piece.Position.x, underside - .08f, piece.Position.z), Quaternion.Euler(90, 0, 0));
                var light = fill.GetComponent<Light>();
                light.type = LightType.Rectangle;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.areaSize = new Vector2(piece.Size.x * .9f, piece.Size.z * .9f);
                light.color = new Color(.87f, .92f, 1f);
                light.intensity = .85f;
                light.range = 16;
                light.shadows = LightShadows.Soft;
                // D034: the bounce gets a visible source, glowing strips across the underside (no collider, no GI).
                bool alongX = piece.Size.x >= piece.Size.z;
                float length = Mathf.Max(piece.Size.x, piece.Size.z) * .85f, span = Mathf.Min(piece.Size.x, piece.Size.z) * .7f;
                int count = Mathf.Max(2, Mathf.RoundToInt(length / 3.5f));
                for (int s = 0; s < count; s++)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
                    bar.name = "Light strip";
                    bar.transform.SetParent(fill.transform, false);
                    float offset = (s + .5f) / count * length - length * .5f;
                    bar.transform.SetPositionAndRotation(new Vector3(piece.Position.x + (alongX ? offset : 0), underside - .01f, piece.Position.z + (alongX ? 0 : offset)), Quaternion.Euler(-90, 0, 0));
                    bar.transform.localScale = alongX ? new Vector3(.22f, span, 1) : new Vector3(span, .22f, 1);
                    var renderer = bar.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = strip;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    GameObjectUtility.SetStaticEditorFlags(bar, StaticEditorFlags.BatchingStatic);
                }
            }

            var sky = SharedMaterial("DaySky", "Wildstone/Day Sky");
            sky.SetColor("_HorizonColor", horizon);
            sky.SetColor("_ZenithColor", new Color(.40f, .53f, .66f));
            sky.SetColor("_GroundColor", new Color(.49f, .49f, .46f));
            sky.SetFloat("_CloudCover", .3f);
            sky.SetVector("_SunDirection", toSun);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            AddSkyline(definition, horizon, toSun);

            var probes = new GameObject("Diffuse probes", typeof(LightProbeGroup));
            probes.transform.SetParent(root.transform, false);
            var positions = new List<Vector3>();
            var solids = root.GetComponentsInChildren<Collider>();
            for (float x = definition.Minimum.x + 4; x < definition.Maximum.x; x += 8)
                for (float z = definition.Minimum.z + 4; z < definition.Maximum.z; z += 8)
                    foreach (float y in new[] { .7f, 2.2f, 4.7f, 6.2f })
                    {
                        var point = new Vector3(x, y, z);
                        if (!solids.Any(c => (c.ClosestPoint(point) - point).sqrMagnitude < .0225f)) positions.Add(point);
                    }
            if (positions.Count == 0) throw new InvalidOperationException("Arena has no clear probe positions.");
            probes.GetComponent<LightProbeGroup>().probePositions = positions.ToArray();

            // Four blended, box-projected quadrant probes give the stone and armour local reflections.
            var min = definition.Minimum; var max = definition.Maximum;
            for (int ix = 0; ix < 2; ix++)
                for (int iz = 0; iz < 2; iz++)
                {
                    var holder = new GameObject("Reflection " + ix + iz, typeof(ReflectionProbe));
                    holder.transform.SetParent(root.transform, false);
                    holder.transform.position = new Vector3(Mathf.Lerp(min.x, max.x, (ix + .5f) / 2), 3.5f, Mathf.Lerp(min.z, max.z, (iz + .5f) / 2));
                    var probe = holder.GetComponent<ReflectionProbe>();
                    probe.mode = ReflectionProbeMode.Baked;
                    probe.boxProjection = true;
                    probe.resolution = 128;
                    probe.blendDistance = 4;
                    probe.size = new Vector3((max.x - min.x) / 2 + 8, max.y - min.y, (max.z - min.z) / 2 + 8);
                    probe.center = new Vector3(0, (min.y + max.y) / 2 - 3.5f, 0);
                }

            var volume = new GameObject("Soft grade", typeof(Volume));
            volume.transform.SetParent(root.transform, false);
            var path = MapsPath + "/" + definition.Id + "-Grade.asset";
            var saved = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            bool created = saved == null;
            if (created)
            {
                saved = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(saved, path);
            }
            // D028 grade, adapted from vibegame1's PC profile; D034 makes it neutral and brighter for the visual-polish
            // look. Written once on creation; authored edits survive rebuilds (delete the -Grade asset to regenerate it).
            if (created)
            {
                Grade<Tonemapping>(saved).mode.Override(TonemappingMode.Neutral);
                var color = Grade<ColorAdjustments>(saved);
                color.postExposure.Override(.6f); color.contrast.Override(8); color.saturation.Override(0);
                var lgg = Grade<LiftGammaGain>(saved);
                lgg.gamma.Override(new Vector4(1, 1, 1, 0)); lgg.gain.Override(new Vector4(1, 1, 1, .05f));
                var smh = Grade<ShadowsMidtonesHighlights>(saved);
                smh.shadows.Override(new Vector4(.97f, .99f, 1.04f, 0)); smh.highlights.Override(new Vector4(1, 1, 1, 0));
                Grade<WhiteBalance>(saved).temperature.Override(0);
                var bloom = Grade<Bloom>(saved);
                bloom.threshold.Override(1.05f); bloom.intensity.Override(.6f); bloom.scatter.Override(.6f);
                var vignette = Grade<Vignette>(saved);
                vignette.intensity.Override(.15f); vignette.smoothness.Override(.4f);
                var grain = Grade<FilmGrain>(saved);
                grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(.04f); grain.response.Override(.8f);
                foreach (var component in saved.components) EditorUtility.SetDirty(component);
                EditorUtility.SetDirty(saved);
            }
            volume.GetComponent<Volume>().isGlobal = true;
            volume.GetComponent<Volume>().sharedProfile = saved;
        }

        // Distant tower silhouettes on the Vista layer: unlit by the bake, no shadows, no colliders.
        private static void AddSkyline(ArenaDefinition definition, Color horizon, Vector4 toSun)
        {
            var material = SharedMaterial("Vista", "Wildstone/Vista");
            material.SetColor("_HazeColor", horizon);
            material.SetVector("_SunDirection", toSun);
            EditorUtility.SetDirty(material);
            var skyline = new GameObject("Skyline");
            int layer = LayerMask.NameToLayer("Vista");
            var random = new System.Random(definition.Id.Aggregate(17, (h, c) => h * 31 + c));
            const int towers = 22;
            for (int i = 0; i < towers; i++)
            {
                float angle = (i + (float)random.NextDouble() * .6f) * Mathf.PI * 2 / towers;
                float radius = 240 + (float)random.NextDouble() * 420;
                float height = 40 + (float)random.NextDouble() * (radius > 450 ? 170 : 90);
                float width = 14 + (float)random.NextDouble() * 26;
                var basePosition = new Vector3(Mathf.Cos(angle) * radius, -30, Mathf.Sin(angle) * radius);
                Block(skyline.transform, basePosition + Vector3.up * height / 2, new Vector3(width, height, width * (.7f + (float)random.NextDouble() * .6f)), angle, material, layer);
                if (random.NextDouble() < .55)
                    Block(skyline.transform, basePosition + Vector3.up * (height + 6), new Vector3(width * .55f, 12, width * .55f), angle, material, layer);
            }
        }

        private static void Block(Transform parent, Vector3 position, Vector3 size, float angle, Material material, int layer)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());
            block.name = "Tower";
            block.layer = layer;
            block.transform.SetParent(parent, false);
            block.transform.SetPositionAndRotation(position, Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0));
            block.transform.localScale = size;
            var renderer = block.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
