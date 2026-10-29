using System;
using System.IO;
using System.Linq;
using Fusion;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Wildstone.Maps;
using Wildstone.Presentation;

namespace Wildstone.Networking.Editor
{
    // Builds an isolated diagnostic executable; the smoke driver is excluded from ordinary player builds.
    public static class FusionSmokeBuild
    {
        [Serializable] sealed class BuildRecord
        {
            public string createdUtc, executable, result, sourceHead, unityVersion, unityLog, contentIdentity;
            public string[] scenes, defines;
        }
        public static void Build()
        {
            var scenes=EditorBuildSettings.scenes.Where(scene=>scene.enabled).Select(scene=>scene.path).ToArray();
            string[] required={"Assets/Wildstone/Scenes/Bootstrap.unity","Assets/Wildstone/Maps/atrium.unity",
                "Assets/Wildstone/Maps/split-gallery.unity","Assets/Wildstone/Maps/terrace-steps.unity"};
            if(scenes.Length==0 || scenes[0]!=required[0] || required.Any(path=>!scenes.Contains(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path)==null))
                throw new BuildFailedException("Prepare saved game scenes before building the smoke executable.");
            if(Resources.Load<ArenaCatalog>("ArenaCatalog")==null || Resources.Load<PresentationCatalog>("WildstonePresentation")==null)
                throw new BuildFailedException("Prepare arena and presentation resources before building the smoke executable.");
            var config=NetworkProjectConfig.Global;
            FusionSession.ValidateProjectConfig(config);
            foreach(string name in new[]{"WildstoneFusionLobby","WildstoneFusionMatch","WildstoneFusionPlayer"})
            {
                string path="Assets/Wildstone/Resources/"+name+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
                if(prefab==null || !prefab.IsSpawnable || !config.PrefabTable.GetId(NetworkObjectGuid.Parse(AssetDatabase.AssetPathToGUID(path))).IsValid)
                    throw new BuildFailedException("Prepare and register the network prefab: "+name);
            }
            string root=Path.GetDirectoryName(Application.dataPath);
            string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string output=Path.Combine(root,"Artifacts","Unity","Smoke-"+stamp,"WildstoneArena.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var record=new BuildRecord {createdUtc=DateTime.UtcNow.ToString("O"),executable=output,result="Building",
                sourceHead=ReadHead(root),unityVersion=Application.unityVersion,unityLog=Application.consoleLogPath,
                scenes=scenes,defines=new[]{"WILDSTONE_SMOKE"}};
            WriteRecord(root,record);
            Debug.Log("WILDSTONE SMOKE TEST BUILD ONLY: "+output);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=scenes,locationPathName=output,target=BuildTarget.StandaloneWindows64,
                options=BuildOptions.Development,extraScriptingDefines=record.defines
            });
            record.result=report.summary.result.ToString();
            record.contentIdentity=File.Exists(FusionBuildIdentity.Output)?File.ReadAllText(FusionBuildIdentity.Output).Trim():"missing";
            WriteRecord(root,record);
            if(report.summary.result!=BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException("Smoke build failed: "+record.result);
            Debug.Log("WILDSTONE SMOKE BUILD OK: "+output);
        }
        static void WriteRecord(string root,BuildRecord record)
        {
            string directory=Path.Combine(root,"Artifacts","Smoke"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"latest-build.json"),JsonUtility.ToJson(record,true));
        }
        static string ReadHead(string root)
        {
            try
            {
                using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName="git",Arguments="rev-parse HEAD",WorkingDirectory=root,UseShellExecute=false,
                    RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true
                }))
                {
                    if(!process.WaitForExit(5000)) { process.Kill(); return "unknown"; }
                    string head=process.StandardOutput.ReadToEnd().Trim();
                    return process.ExitCode==0 && head.Length==40 && head.All(Uri.IsHexDigit)?head:"unknown";
                }
            }
            catch(Exception) { return "unknown"; }
        }
    }
}
