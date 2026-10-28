using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Wildstone.Editor;
using Wildstone.Maps;

namespace Wildstone.Tests
{
    public sealed class ArenaGradePersistenceTests
    {
        [Test] public void GradeOverridesSurviveReloadAndASecondBuilderRun()
        {
            var map = ArenaLayouts.CreateAll()[0];
            map.Id = "grade-test-" + Guid.NewGuid().ToString("N");
            string path = "Assets/Wildstone/Maps/" + map.Id + "-Grade.asset";
            const string folder = "Assets/Wildstone/Maps";
            bool createdFolder = !Directory.Exists(folder);
            var addLighting = typeof(ArenaBuilder).GetMethod("AddLighting", BindingFlags.Static | BindingFlags.NonPublic);
            var scope = new NativeSceneTestScope();
            var fixture = scope.Scene;
            GameObject root = null;
            try
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.True, "Import the fixture output folder before creating assets.");
                SceneManager.SetActiveScene(fixture);
                for (int run = 0; run < 2; run++)
                {
                    root = new GameObject("Isolated grade fixture");
                    addLighting.Invoke(null, new object[] { root, map });
                    var profile = root.GetComponentInChildren<Volume>().sharedProfile;
                    Assert.That(profile.TryGet<Tonemapping>(out var tone), Is.True);
                    Assert.That(profile.TryGet<Bloom>(out var bloom), Is.True);
                    if (run == 1) Assert.That(bloom.intensity.value, Is.EqualTo(.23f), "Preserve authored profiles on rebuild.");
                    AssetDatabase.SaveAssets();
                    UnityEngine.Object.DestroyImmediate(root); root = null;
                    Resources.UnloadAsset(tone); Resources.UnloadAsset(bloom); Resources.UnloadAsset(profile);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                    Assert.That(profile.TryGet<Tonemapping>(out tone), Is.True);
                    Assert.That(profile.TryGet<Bloom>(out bloom), Is.True);
                    var persisted = AssetDatabase.LoadAllAssetsAtPath(path);
                    Assert.That(persisted.OfType<Tonemapping>().Count(), Is.EqualTo(1));
                    Assert.That(persisted.OfType<Bloom>().Count(), Is.EqualTo(1));
                    Assert.That(tone.mode.overrideState, Is.True);
                    Assert.That(tone.mode.value, Is.EqualTo(TonemappingMode.ACES));
                    Assert.That(bloom.intensity.overrideState, Is.True);
                    Assert.That(bloom.intensity.value, Is.EqualTo(run == 0 ? .12f : .23f));
                    Assert.That(bloom.threshold.overrideState, Is.True);
                    Assert.That(bloom.threshold.value, Is.EqualTo(1.2f));
                    bloom.intensity.Override(.23f);
                    EditorUtility.SetDirty(bloom);
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                AssetDatabase.DeleteAsset(path);
                if (createdFolder && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    AssetDatabase.DeleteAsset(folder);
                EditorSceneManager.CloseScene(fixture, true);
                scope.Dispose();
            }
        }
    }
}
