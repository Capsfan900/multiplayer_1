using System;
using System.IO;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wildstone.Game;
using Wildstone.Movement;

namespace Wildstone.Networking.Editor
{
    public static class FusionContentBuild
    {
        public static void Prepare()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save author scenes before network preparation.");
            Directory.CreateDirectory("Assets/Wildstone/Resources");
            SavePrefab<FusionLobby>("WildstoneFusionLobby");
            SavePrefab<FusionMatch>("WildstoneFusionMatch");
            SavePrefab<FusionPlayer>("WildstoneFusionPlayer");
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            const string path="Assets/Wildstone/Scenes/Bootstrap.unity";
            var previous=SceneManager.GetActiveScene();
            var scene=SceneManager.GetSceneByPath(path); bool opened=!scene.IsValid() || !scene.isLoaded;
            try
            {
                if(opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var practice in root.GetComponentsInChildren<PracticeSession>(true))
                    {
                        if(practice.GetComponent<FusionMenuController>()==null) practice.gameObject.AddComponent<FusionMenuController>();
                        if(practice.GetComponent<FusionGameCoordinator>()==null) practice.gameObject.AddComponent<FusionGameCoordinator>();
                    }
                if(!EditorSceneManager.SaveScene(scene,path)) throw new InvalidOperationException("Network bootstrap save failed.");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if(opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            FusionBuildIdentity.Generate();
            Debug.Log("WILDSTONE NETWORK CONTENT READY");
        }
        static void SavePrefab<T>(string name) where T:NetworkBehaviour
        {
            var go=new GameObject(name);
            try
            {
                go.AddComponent<T>();
                var obj=go.GetComponent<NetworkObject>(); obj.IsSpawnable=true;
                if(typeof(T)==typeof(FusionPlayer))
                {
                    go.layer=MotorTuning.PlayerLayer;
                    var visual=new GameObject("Visual").transform; visual.SetParent(go.transform,false);
                    go.GetComponent<FusionPlayer>().VisualRoot=visual;
                    var root=go.AddComponent<HitboxRoot>(); root.Offset=new Vector3(0,.9f,0); root.BroadRadius=1.2f;
                    var hitbox=go.AddComponent<Hitbox>(); hitbox.Type=HitboxTypes.Box;
                    float top=Wildstone.Combat.CombatRules.BodyTop(1.8f), half=Wildstone.Combat.CombatRules.BodyHalfWidth;
                    hitbox.BoxExtents=new Vector3(half,top*.5f,half); hitbox.Offset=new Vector3(0,top*.5f,0);
                    // Head sphere child; FusionPlayer moves its offset with the authoritative capsule height each tick.
                    var headObject=new GameObject("Head hitbox"); headObject.layer=MotorTuning.PlayerLayer; headObject.transform.SetParent(go.transform,false);
                    var head=headObject.AddComponent<Hitbox>(); head.Type=HitboxTypes.Sphere;
                    head.SphereRadius=Wildstone.Combat.CombatRules.HeadRadius; head.Offset=new Vector3(0,1.8f-Wildstone.Combat.CombatRules.HeadDrop,0);
                    root.Hitboxes=new[]{hitbox,head};
                }
                new NetworkObjectBaker().Bake(go);
                if(PrefabUtility.SaveAsPrefabAsset(go,"Assets/Wildstone/Resources/"+name+".prefab")==null)
                    throw new InvalidOperationException("Network prefab could not be saved.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
