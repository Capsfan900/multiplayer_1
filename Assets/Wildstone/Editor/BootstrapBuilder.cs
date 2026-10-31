using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Wildstone.Game;
using Wildstone.Presentation;
using Wildstone.UI;

namespace Wildstone.Editor
{
    public static class BootstrapBuilder
    {
        const string ScenePath="Assets/Wildstone/Scenes/Bootstrap.unity";
        public static void Configure()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene changes before configuring Bootstrap.");
            var previousScene=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(ScenePath);
            bool openedHere=!scene.IsValid()||!scene.isLoaded;
            try
            {
            const string panelPath="Assets/Wildstone/UI/ArenaPanel.asset";
            Directory.CreateDirectory("Assets/Wildstone/UI"); AssetDatabase.Refresh();
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if(panel==null) { panel=ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(panel,panelPath); }
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution=new Vector2Int(1280,720);
            panel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight; panel.match=.5f;
            panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Wildstone/UI/ArenaTheme.tss");
            if(panel.themeStyleSheet==null) throw new InvalidOperationException("UI theme failed to import.");
            EditorUtility.SetDirty(panel);
            if(openedHere) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            ConfigureScene(scene,panel);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            }
            finally
            {
                if(openedHere&&scene.IsValid()&&scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
                if(previousScene.IsValid()&&previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            }
        }
        static void ConfigureScene(Scene scene,PanelSettings panel)
        {
            var camera=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if(camera==null) camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.tag="MainCamera"; camera.nearClipPlane=.04f; camera.farClipPlane=1500; camera.fieldOfView=90;
            var cameraData=camera.GetUniversalAdditionalCameraData();
            cameraData.renderType=CameraRenderType.Base; cameraData.renderPostProcessing=true;
            cameraData.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing; cameraData.antialiasingQuality=AntialiasingQuality.High;
            // Existing saved lights and their children belong to the author, not this builder.
            var existing=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<PracticeSession>(true)).FirstOrDefault();
            var root=existing!=null?existing.gameObject:new GameObject("Wildstone");
            var session=existing!=null?existing:root.AddComponent<PracticeSession>(); session.Camera=camera;
            var document=root.GetComponent<UIDocument>();
            if(document==null) document=root.AddComponent<UIDocument>();
            document.panelSettings=panel;
            if(root.GetComponent<ArenaMenu>()==null) root.AddComponent<ArenaMenu>();
            if(root.GetComponent<ArenaPresentation>()==null) root.AddComponent<ArenaPresentation>();
            if(!EditorSceneManager.SaveScene(scene,scene.path)) throw new InvalidOperationException("Bootstrap scene could not be saved.");
        }
    }
}
