using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wildstone.Editor;

namespace Wildstone.Tests
{
    public sealed class ContentPreparationTests
    {
        [Test,Explicit("Rebuilds the generated human controller; requires imported human animations.")]
        public void HumanControllerRepairsInterruptedEmptyLayerState()
        {
            const string path="Assets/Wildstone/Resources/Presentation/WildstoneHuman.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            Assert.That(controller,Is.Not.Null,"Prepare presentation assets first.");
            var original=controller.layers;
            try
            {
                controller.layers=Array.Empty<AnimatorControllerLayer>();
                typeof(PresentationBuilder).GetMethod("HumanController",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
                Assert.That(controller.layers.Length,Is.EqualTo(1));
                Assert.That(controller.layers[0].stateMachine.defaultState.name,Is.EqualTo("Idle"));
                Assert.That(controller.layers[0].stateMachine.states.Length,Is.EqualTo(7));
            }
            finally
            {
                controller.layers=original;
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }
        }

        [Test,Explicit("Regenerates authored content. Run deliberately after saving all open scenes; does not bake lighting.")]
        public void PreparePreservesCleanOpenScenesAndLeavesNoTemporaryObjects()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
                Assert.That(SceneManager.GetSceneAt(i).isDirty,Is.False,"Save open scenes before this explicit preparation check.");
            var scope=new NativeSceneTestScope();
            var fixture=scope.Scene;
            string path="Assets/Wildstone/Tests/PreparePreservation-"+Guid.NewGuid().ToString("N")+".unity";
            try
            {
                SceneManager.SetActiveScene(fixture);
                new GameObject("Saved author marker");
                Assert.That(EditorSceneManager.SaveScene(fixture,path),Is.True);
                var scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
                var roots=scenes.Select(s=>s.GetRootGameObjects()).ToArray();
                try { ContentBuild.Prepare(); }
                finally
                {
                    Assert.That(SceneManager.GetActiveScene(),Is.EqualTo(fixture));
                    Assert.That(SceneManager.sceneCount,Is.EqualTo(scenes.Length));
                    for(int i=0;i<scenes.Length;i++)
                    {
                        Assert.That(scenes[i].isLoaded,Is.True);
                        Assert.That(scenes[i].isDirty,Is.False,"Preparation must retain the saved state, including on failure.");
                        CollectionAssert.AreEquivalent(roots[i],scenes[i].GetRootGameObjects(),"Temporary authoring objects must not leak.");
                    }
                }
            }
            finally
            {
                if(fixture.IsValid()&&fixture.isLoaded) EditorSceneManager.CloseScene(fixture,true);
                AssetDatabase.DeleteAsset(path);
                scope.Dispose();
            }
        }
    }
}
