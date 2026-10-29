using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Wildstone.Networking.Editor
{
    public sealed class FusionBuildIdentity : IPreprocessBuildWithReport
    {
        public const string Output="Assets/Wildstone/Resources/WildstoneBuild.txt";
        public int callbackOrder => 1000;
        public void OnPreprocessBuild(BuildReport report) => Generate();
        public static void Generate()
        {
            string root=Path.GetDirectoryName(UnityEngine.Application.dataPath);
            var paths=new List<string>();
            foreach(string directory in new[]{"Assets/Wildstone","Assets/Photon","ProjectSettings","Packages"})
                if(Directory.Exists(Path.Combine(root,directory)))
                    foreach(string file in Directory.EnumerateFiles(Path.Combine(root,directory),"*",SearchOption.AllDirectories))
                    { string relative=file.Substring(root.Length+1).Replace('\\','/'); if(IncludePath(relative)) paths.Add(relative); }
            if(paths.Count==0) throw new BuildFailedException("Cannot fingerprint an empty game content tree.");
            string digest=Compute(root,paths);
            string output=Path.Combine(root,Output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            if(!File.Exists(output) || File.ReadAllText(output)!=digest+"\n") File.WriteAllText(output,digest+"\n",new UTF8Encoding(false));
            AssetDatabase.ImportAsset(Output,ImportAssetOptions.ForceSynchronousImport);
        }
        public static bool IncludePath(string path)
        {
            path=path.Replace('\\','/');
            if(path.Equals(Output,StringComparison.OrdinalIgnoreCase) || path.Equals(Output+".meta",StringComparison.OrdinalIgnoreCase)) return false;
            string name=Path.GetFileName(path);
            foreach(string excluded in new[]{"PhotonAppSettings.asset","WildstoneOnline.json","LocalServiceConfig.json"})
                if(name.Equals(excluded,StringComparison.OrdinalIgnoreCase) || name.Equals(excluded+".meta",StringComparison.OrdinalIgnoreCase)) return false;
            if(path.StartsWith("Assets/Wildstone/Tests/",StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets/Wildstone/Networking/Tests/",StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets/Wildstone/Editor/",StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Assets/Wildstone/Networking/Editor/",StringComparison.OrdinalIgnoreCase)) return false;
            if(path.StartsWith("Packages/",StringComparison.OrdinalIgnoreCase)) return path=="Packages/manifest.json" || path=="Packages/packages-lock.json";
            return true;
        }
        public static string Compute(string root,IEnumerable<string> paths)
        {
            using(var sha=SHA256.Create())
            using(var stream=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write))
            using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                foreach(string relative in paths.Select(path=>path.Replace('\\','/')).OrderBy(path=>path,StringComparer.Ordinal))
                {
                    writer.Write(relative);
                    using(var file=File.OpenRead(Path.Combine(root,relative)))
                    { writer.Write(file.Length); writer.Flush(); file.CopyTo(stream); }
                }
                writer.Flush(); stream.FlushFinalBlock();
                return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
            }
        }
    }
}
