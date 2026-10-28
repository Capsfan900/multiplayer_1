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
                catalog.Maps = ArenaLayouts.CreateAll();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
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

        private static Mesh PersistMesh(Mesh mesh)
        {
            var path = MapsPath + "/Geometry/" + mesh.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                Unwrapping.GenerateSecondaryUVSet(mesh);
                AssetDatabase.CreateAsset(mesh, path);
                saved = mesh;
            }
            else if (saved.uv2.Length != saved.vertexCount)
            {
                Unwrapping.GenerateSecondaryUVSet(saved);
                EditorUtility.SetDirty(saved);
            }
            return saved;
        }

        private static Material[] Palette()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var colors = new[] { new Color(.79f, .77f, .71f), new Color(.52f, .55f, .55f), new Color(.16f, .19f, .20f) };
            var names = new[] { "Ivory", "Ash", "Charcoal" };
            var result = new Material[3];
            for (int i = 0; i < result.Length; i++)
            {
                var path = "Assets/Wildstone/Settings/Stone-" + names[i] + ".mat";
                result[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (result[i] != null) continue;
                result[i] = new Material(shader) { name = "Stone " + names[i], color = colors[i] };
                result[i].SetFloat("_Smoothness", .18f);
                AssetDatabase.CreateAsset(result[i], path);
            }
            return result;
        }

        private static void AddLighting(GameObject root, ArenaDefinition definition)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.66f, .73f, .79f);
            RenderSettings.ambientEquatorColor = new Color(.48f, .49f, .47f);
            RenderSettings.ambientGroundColor = new Color(.28f, .29f, .28f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.69f, .73f, .75f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55;
            RenderSettings.fogEndDistance = 180;
            var sun = new GameObject("Soft key", typeof(Light));
            sun.transform.SetParent(root.transform, false);
            sun.transform.rotation = Quaternion.Euler(44, -32, 0);
            var key = sun.GetComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1, .92f, .80f);
            key.intensity = 1.3f;
            key.shadows = LightShadows.Soft;
            key.lightmapBakeType = LightmapBakeType.Mixed;
            RenderSettings.sun = key;
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
            var volume = new GameObject("Soft grade", typeof(Volume));
            volume.transform.SetParent(root.transform, false);
            var path = MapsPath + "/" + definition.Id + "-Grade.asset";
            var saved = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (saved == null)
            {
                saved = ScriptableObject.CreateInstance<VolumeProfile>();
                var tone = saved.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
                var bloom = saved.Add<Bloom>(); bloom.intensity.Override(.12f); bloom.threshold.Override(1.2f);
                AssetDatabase.CreateAsset(saved, path);
                AssetDatabase.AddObjectToAsset(tone, saved);
                AssetDatabase.AddObjectToAsset(bloom, saved);
                EditorUtility.SetDirty(saved);
            }
            volume.GetComponent<Volume>().isGlobal = true;
            volume.GetComponent<Volume>().sharedProfile = saved;
        }
    }
}
