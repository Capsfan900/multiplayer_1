using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wildstone.Editor
{
    public static class ContentBuild
    {
        [MenuItem("Wildstone/Prepare All Content")]
        public static void Prepare()
        {
            // Validate the asymmetric imported fixture before trusting any production FBX dimensions.
            AssetImportValidation.ValidateFixture();
            ArenaBuilder.BuildAll();
            PresentationBuilder.Build();
            BootstrapBuilder.Configure();
            AssetDatabase.SaveAssets();
            Debug.Log("WILDSTONE CONTENT READY");
        }

        public static void ValidatePrepared()
        {
            // Packaging saved scenes must not regenerate them and discard their lighting bake.
            string[] required = { "Assets/Wildstone/Scenes/Bootstrap.unity",
                "Assets/Wildstone/Maps/atrium.unity", "Assets/Wildstone/Maps/split-gallery.unity",
                "Assets/Wildstone/Maps/terrace-steps.unity" };
            var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (enabled.Length == 0 || enabled[0] != required[0] || required.Any(path =>
                !enabled.Contains(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null))
                throw new InvalidOperationException("Prepare all content before building; Bootstrap must be the first enabled scene.");
        }
    }
}
