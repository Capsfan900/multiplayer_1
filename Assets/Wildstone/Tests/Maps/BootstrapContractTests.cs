using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Wildstone.Editor;
using Wildstone.Game;
using Wildstone.Maps;
namespace Wildstone.Tests
{
    public sealed class BootstrapContractTests
    {
        [Test] public void SavedAuthorLightChildAndCameraSurviveBootstrapConfiguration()
        {
            var scope=new NativeSceneTestScope();
            var fixture=scope.Scene;
            string path="Assets/Wildstone/Tests/BootstrapPreservation-"+Guid.NewGuid().ToString("N")+".unity";
            var panel=ScriptableObject.CreateInstance<PanelSettings>();
            try
            {
                SceneManager.SetActiveScene(fixture);
                var authored=new GameObject("Author camera and light",typeof(Camera),typeof(Light));
                var child=new GameObject("Author child prop"); child.transform.SetParent(authored.transform);
                Assert.That(EditorSceneManager.SaveScene(fixture,path),Is.True);
                typeof(BootstrapBuilder).GetMethod("ConfigureScene",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{fixture,panel});
                Assert.That(authored,Is.Not.Null); Assert.That(authored.GetComponent<Light>(),Is.Not.Null);
                Assert.That(authored.GetComponent<Camera>(),Is.Not.Null); Assert.That(child.transform.parent,Is.EqualTo(authored.transform));
                fixture=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var saved=fixture.GetRootGameObjects().First(go=>go.name=="Author camera and light");
                Assert.That(saved.GetComponent<Light>(),Is.Not.Null); Assert.That(saved.transform.Find("Author child prop"),Is.Not.Null);
                var cameraData=saved.GetComponent<UniversalAdditionalCameraData>();
                Assert.That(cameraData,Is.Not.Null,"Persist the world camera's URP configuration.");
                Assert.That(cameraData.renderType,Is.EqualTo(CameraRenderType.Base));
                Assert.That(cameraData.renderPostProcessing,Is.True,"The saved map grades run on the world camera.");
            }
            finally
            {
                if(fixture.IsValid()&&fixture.isLoaded) EditorSceneManager.CloseScene(fixture,true);
                AssetDatabase.DeleteAsset(path); UnityEngine.Object.DestroyImmediate(panel);
                scope.Dispose();
            }
        }
        [Test] public void PracticePathsResolveToPreparedMapBuilderScenes()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<ArenaCatalog>(ArenaBuilder.CatalogPath);
            Assert.That(catalog,Is.Not.Null,"Prepare content before native content tests.");
            foreach(var map in catalog.Maps)
            {
                string path=PracticeSession.MapScenePath(map.Id);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),Is.Not.Null,path);
                Assert.That(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==path),Is.True,path);
            }
        }
        [Test] public void BootstrapRefusesToDiscardUnsavedSceneChanges()
        {
            var scope=new NativeSceneTestScope();
            var fixture=scope.Scene;
            try
            {
                var marker=new GameObject("Unsaved scene fixture"); SceneManager.MoveGameObjectToScene(marker,fixture);
                EditorSceneManager.MarkSceneDirty(fixture);
                Assert.Throws<InvalidOperationException>(()=>BootstrapBuilder.Configure());
                Assert.That(fixture.isLoaded,Is.True); Assert.That(fixture.isDirty,Is.True); Assert.That(marker,Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(fixture,true);
                scope.Dispose();
            }
        }
    }
}
