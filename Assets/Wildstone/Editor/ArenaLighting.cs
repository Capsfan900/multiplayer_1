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
                        settings = new LightingSettings();
                        AssetDatabase.CreateAsset(settings, path);
                    }
                    // D028: denser lightmaps with baked contact AO; GPU falls back to CPU where unsupported.
                    settings.bakedGI = true; settings.realtimeGI = false;
                    settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
                    settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
                    settings.lightmapResolution = 16; settings.lightmapMaxSize = 2048;
                    settings.directSampleCount = 64; settings.indirectSampleCount = 256;
                    settings.environmentSampleCount = 128; settings.maxBounces = 3;
                    settings.ao = true; settings.aoMaxDistance = 1.2f;
                    settings.aoExponentIndirect = 1; settings.aoExponentDirect = 0;
                    EditorUtility.SetDirty(settings);
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
