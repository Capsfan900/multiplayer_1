using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wildstone.Editor;
using Wildstone.Maps;

namespace Wildstone.Tests
{
    public sealed class ContentPreparationTests
    {
        // A layout revision that never reaches the shipped catalog leaves players on stale maps (e.g. no SMG pickups).
        [Test]
        public void ShippedArenaCatalogMatchesTheAuthoredLayouts()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<ArenaCatalog>(ArenaBuilder.CatalogPath);
            Assert.That(catalog,Is.Not.Null,"Run Prepare first.");
            var authored=ArenaLayouts.CreateAll();
            Assert.That(catalog.Maps.Select(m=>m.Id+"@"+m.Revision),Is.EqualTo(authored.Select(m=>m.Id+"@"+m.Revision)));
            for(int i=0;i<authored.Length;i++)
            {
                Assert.That(catalog.Maps[i].Pickups.Select(p=>p.Item+p.Position),Is.EqualTo(authored[i].Pickups.Select(p=>p.Item+p.Position)),authored[i].Id);
                Assert.That(catalog.Maps[i].Spawns.Length,Is.EqualTo(authored[i].Spawns.Length),authored[i].Id);
                for(int s=0;s<authored[i].Spawns.Length;s++)
                {
                    Assert.That(catalog.Maps[i].Spawns[s].Position,Is.EqualTo(authored[i].Spawns[s].Position),authored[i].Id);
                    Assert.That(catalog.Maps[i].Spawns[s].Yaw,Is.EqualTo(authored[i].Spawns[s].Yaw).Within(.001f),authored[i].Id+" spawn "+s);
                }
            }
        }
        [Test,Explicit("Regenerates the soldier controller; requires imported soldier animations.")]
        public void SoldierControllerRegeneratesEveryLayerFromTheClipContract()
        {
            SoldierBuilder.ConfigureImports();
            var controller=SoldierBuilder.Controller();
            AssetDatabase.SaveAssets();
            Assert.That(controller.layers.Select(l=>l.name),Is.EqualTo(new[]{"Base Layer","Upper body","Additive"}));
            Assert.That(controller.layers[0].stateMachine.defaultState.name,Is.EqualTo("Locomotion"));
            Assert.That(controller.layers[1].stateMachine.defaultState.name,Is.EqualTo("HoldRifle"));
            Assert.That(controller.layers[2].stateMachine.defaultState.name,Is.EqualTo("None"));
            foreach(var name in new[]{"MoveX","MoveZ","LocomotionRate","Grounded","Sliding","WallRun","Dashing","Dead","Stance","Aim","Fire"})
                Assert.That(controller.parameters.Any(p=>p.name==name),Is.True,name);
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
