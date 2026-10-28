using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wildstone.Editor;

namespace Wildstone.Tests
{
    public sealed class ProjectSetupTests
    {
        [TestCase(false),TestCase(true)]
        public void MissingBootstrapAcceptsOnlyDisposableUntitledStartup(bool hasAuthorWork)
        {
            using var scope=new NativeSceneTestScope();
            const string path="Assets/Wildstone/Scenes/Bootstrap.unity";
            string backup="Assets/Wildstone/Scenes/Bootstrap-preservation-"+Guid.NewGuid().ToString("N")+".unity";
            bool existed=AssetDatabase.LoadAssetAtPath<SceneAsset>(path)!=null;
            bool moved=false;
            var buildScenes=EditorBuildSettings.scenes;
            var fixture=scope.Scene;
            if(hasAuthorWork)
            {
                new GameObject("Unsaved author marker");
                EditorSceneManager.MarkSceneDirty(fixture);
            }
            var roots=fixture.GetRootGameObjects();
            try
            {
                if(existed) { Assert.That(AssetDatabase.MoveAsset(path,backup),Is.Empty); moved=true; }
                if(hasAuthorWork)
                {
                    Assert.Throws<InvalidOperationException>(()=>ProjectSetup.Configure());
                    Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),Is.Null);
                    Assert.That(SceneManager.GetActiveScene(),Is.EqualTo(fixture));
                    Assert.That(fixture.path,Is.Empty);
                    Assert.That(fixture.isDirty,Is.True);
                    CollectionAssert.AreEquivalent(roots,fixture.GetRootGameObjects());
                }
                else
                {
                    ProjectSetup.Configure();
                    Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),Is.Not.Null);
                    Assert.That(SceneManager.GetActiveScene().path,Is.EqualTo(path));
                    Assert.That(SceneManager.GetActiveScene().isDirty,Is.False);
                }
            }
            finally
            {
                if(!existed||moved) AssetDatabase.DeleteAsset(path);
                if(moved) Assert.That(AssetDatabase.MoveAsset(backup,path),Is.Empty);
                EditorBuildSettings.scenes=buildScenes;
            }
        }
    }
}

