using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Wildstone.Editor
{
    public static class ArenaLighting
    {
        [MenuItem("Wildstone/Bake Arena Lighting")]
        public static void BakeAll()
        {
            ContentBuild.ValidatePrepared();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty ||
                    (SceneManager.GetSceneAt(i).path.Length == 0 && SceneManager.GetSceneAt(i).rootCount != 0))
                    throw new InvalidOperationException("Save open scenes before baking arenas.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string id in new[] { "atrium", "split-gallery", "terrace-steps" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Wildstone/Maps/" + id + ".unity");
                    var path = "Assets/Wildstone/Maps/" + id + "-Lighting.asset";
                    var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
                    if (settings == null)
                    {
                        settings = new LightingSettings
                        {
                            bakedGI = true, realtimeGI = false,
                            lightmapper = LightingSettings.Lightmapper.ProgressiveCPU,
                            mixedBakeMode = MixedLightingMode.IndirectOnly,
                            lightmapResolution = 8, lightmapMaxSize = 1024,
                            directSampleCount = 64, indirectSampleCount = 256,
                            environmentSampleCount = 128, maxBounces = 3
                        };
                        AssetDatabase.CreateAsset(settings, path);
                    }
                    Lightmapping.lightingSettings = settings;
                    if (!Lightmapping.Bake() || LightmapSettings.lightmaps.Length == 0)
                        throw new InvalidOperationException("Lighting bake produced no lightmaps for " + id);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save baked arena " + id);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("WILDSTONE LIGHTING BAKED: 3");
            }
            finally
            {
                if (setup.Length != 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
