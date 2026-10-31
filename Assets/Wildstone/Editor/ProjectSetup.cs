using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Wildstone.Editor
{
    public static class ProjectSetup
    {
        private const string Root = "Assets/Wildstone";
        private const string Settings = Root + "/Settings";
        private const string ScenePath = Root + "/Scenes/Bootstrap.unity";

        public static void Configure()
        {
            bool disposableStartup = UnityEngine.SceneManagement.SceneManager.sceneCount == 0;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var open = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (open.path.Length != 0) continue;
                if (open.isDirty || open.rootCount != 0 || UnityEngine.SceneManagement.SceneManager.sceneCount != 1)
                    throw new InvalidOperationException("Save untitled author scenes before configuring Wildstone.");
                disposableStartup = true;
            }
            Directory.CreateDirectory(Settings);
            Directory.CreateDirectory(Root + "/Scenes");

            const string rendererPath = Settings + "/WildstoneRenderer.asset";
            const string pipelinePath = Settings + "/WildstonePipeline.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            if (renderer.postProcessData == null)
            {
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset");
                if (renderer.postProcessData == null)
                    throw new Exception("URP default PostProcessData asset is missing");
                EditorUtility.SetDirty(renderer);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            RenderingSetup.Configure(renderer, pipeline);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "Wildstone";
            PlayerSettings.productName = "Better Bean Battles"; // D038; the WildstoneArena.exe build name stays for QA/release scripts
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.wildstone.arena");
            ConfigureInput();

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            string[] sourceLayers = { "Player", "Enemy", "Interactable", "Sky", "Atmosphere", "Viewmodel", "Vista" };
            for (int i = 0; i < sourceLayers.Length; i++)
                layers.GetArrayElementAtIndex(i + 6).stringValue = sourceLayers[i];
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    disposableStartup ? NewSceneMode.Single : NewSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                try
                {
                    var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                    camera.tag = "MainCamera";
                    camera.transform.SetPositionAndRotation(new Vector3(0, 2, -5), Quaternion.Euler(12, 0, 0));
                    if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save bootstrap scene");
                }
                finally
                {
                    if (!disposableStartup) EditorSceneManager.CloseScene(scene, true);
                    if (previous.IsValid() && previous.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                }
            }
            else if (disposableStartup)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(scene => string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase)))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("WILDSTONE CONFIGURED");
        }

        private static void ConfigureInput()
        {
            // Unity 6 exposes this setting through serialized PlayerSettings, as the installed Input System package does.
            var field = typeof(BuildProfile).GetField("s_GlobalPlayerSettings", BindingFlags.Static | BindingFlags.NonPublic);
            var settings = field?.GetValue(null) as PlayerSettings;
            if (settings == null)
                throw new Exception("Global PlayerSettings not found for active input handling");
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null)
            {
                var overrideField = typeof(BuildProfile).GetField("m_PlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                settings = overrideField?.GetValue(profile) as PlayerSettings ?? settings;
            }
            var serialized = new SerializedObject(settings);
            var handler = serialized.FindProperty("activeInputHandler");
            if (handler == null)
                throw new Exception("activeInputHandler not found in PlayerSettings");
            handler.intValue = 1; // Input System package only.
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void BuildWindows()
        {
            ContentBuild.ValidatePrepared();
            var buildScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (buildScenes.Length == 0)
                throw new Exception("No enabled scenes in EditorBuildSettings");
            var output = Path.GetFullPath("Artifacts/Unity/Windows/WildstoneArena.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = buildScenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + report.summary.result);
            Debug.Log("WILDSTONE BUILD OK: " + output);
        }
    }
}
