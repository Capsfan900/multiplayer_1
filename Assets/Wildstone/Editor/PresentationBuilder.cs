using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Wildstone.Presentation;

namespace Wildstone.Editor
{
    public static class PresentationBuilder
    {
        const string Art="Assets/Wildstone/Art/";
        const string Output="Assets/Wildstone/Resources/Presentation/";
        const string CatalogPath="Assets/Wildstone/Resources/WildstonePresentation.asset";
        static readonly string[] HumanNames={"WildstoneHuman","WildstoneHumanTeam02","WildstoneHumanTeam03","WildstoneHumanTeam04"};
        static readonly string[] WeaponNames={"LineDriver","PulseLobber","ReboundDisc","PushLens","Grenade","StunGun","LaserSword"};
        static readonly string[] MaterialNames={"MatteIvory","Graphite","SoftGrey","SignalCyan","SignalAmber","SignalCoral","SignalViolet","MatteCharcoalSuit","DeepCharcoal","HeadValue","TeamMark"};
        static readonly Color[] Colors={new Color(.76f,.73f,.66f),new Color(.105f,.115f,.125f),new Color(.32f,.34f,.35f),new Color(.13f,.68f,.74f),new Color(.95f,.43f,.12f),new Color(.84f,.30f,.24f),new Color(.55f,.39f,.80f),new Color(.19f,.21f,.22f),new Color(.105f,.12f,.13f),new Color(.28f,.30f,.31f),new Color(.70f,.70f,.66f)};
        static Material[] palette;

        [MenuItem("Wildstone/Build Presentation")]
        public static void Build()
        {
            AssetImportValidation.ValidateFixture();
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            palette=new Material[MaterialNames.Length];
            for(int i=0;i<palette.Length;i++) palette[i]=Material(MaterialNames[i],Colors[i],"Universal Render Pipeline/Lit");
            var catalog=AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
            if(catalog==null)
            {
                catalog=ScriptableObject.CreateInstance<PresentationCatalog>();
                AssetDatabase.CreateAsset(catalog,CatalogPath);
            }
            ConfigureHumanImports();
            var controller=HumanController();
            for(int i=0;i<HumanNames.Length;i++) catalog.Humans[i]=ModelPrefab(HumanNames[i],controller);
            catalog.Hands=ModelPrefab("FirstPersonHands",null);
            for(int i=0;i<WeaponNames.Length;i++) catalog.Weapons[i]=ModelPrefab(WeaponNames[i],null);
            catalog.BlinkPickup=ModelPrefab("BlinkPickup",null);
            catalog.ShieldPickup=ModelPrefab("ShieldPickup",null);
            catalog.Beam=Effect("Beam",new Color(.65f,.84f,.88f));
            catalog.Pulse=Effect("Pulse",new Color(.95f,.49f,.18f));
            catalog.Disc=Effect("Disc",new Color(.92f,.36f,.30f));
            catalog.Push=Effect("Push",new Color(.60f,.45f,.86f));
            catalog.Stun=Effect("Stun",new Color(.36f,.85f,.89f));
            catalog.Blink=Effect("Blink",new Color(.65f,.52f,.94f));
            catalog.Shield=Effect("Shield",new Color(.56f,.84f,.93f));
            catalog.Danger=Effect("Danger",new Color(.98f,.57f,.20f));
            catalog.ZoneIdle=Effect("ZoneIdle",new Color(.65f,.66f,.67f));
            catalog.ZoneOwned=Effect("ZoneOwned",new Color(.74f,.83f,.49f));
            catalog.ZoneContested=Effect("ZoneContested",new Color(.95f,.62f,.32f));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("WILDSTONE PRESENTATION BUILT: 4 humans, hands, 7 weapons, 2 items, bounded effects/audio catalog.");
        }
        static Material Material(string name,Color color,string shaderName)
        {
            string path=Output+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(shaderName);
            if(shader==null) throw new InvalidOperationException("Missing URP shader: "+shaderName);
            if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader=shader;
            material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",.12f);
            EditorUtility.SetDirty(material);
            return material;
        }
        static Material Effect(string name,Color color) => Material(name,color,"Universal Render Pipeline/Unlit");
        static bool Loop(string name) => name=="Idle" || name=="Run" || name=="Strafe" || name=="Aim";
        static string ClipName(string name)
        {
            int separator=name.LastIndexOf('|');
            return separator>=0?name.Substring(separator+1):name;
        }
        static void ConfigureHumanImports()
        {
            foreach(var name in HumanNames)
            {
                string path=Art+name+".fbx";
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(importer==null) throw new InvalidOperationException("Human FBX importer missing: "+path);
                var clips=importer.clipAnimations.Length>0?importer.clipAnimations:importer.defaultClipAnimations;
                if(clips.Length<7) throw new InvalidOperationException(name+" is missing exported animation takes.");
                bool changed=importer.animationType!=ModelImporterAnimationType.Generic || !importer.importAnimation;
                importer.animationType=ModelImporterAnimationType.Generic;
                importer.importAnimation=true;
                for(int i=0;i<clips.Length;i++)
                {
                    bool loop=Loop(ClipName(clips[i].name));
                    if(clips[i].loopTime!=loop) { clips[i].loopTime=loop; changed=true; }
                }
                if(changed || importer.clipAnimations.Length==0)
                {
                    importer.clipAnimations=clips;
                    importer.SaveAndReimport();
                }
            }
        }
        static GameObject ModelPrefab(string name,RuntimeAnimatorController controller)
        {
            string sourcePath=Art+name+".fbx";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if(source==null) throw new InvalidOperationException("Missing FBX: "+sourcePath);
            var root=new GameObject(name);
            try
            {
                var model=PrefabUtility.InstantiatePrefab(source) as GameObject;
                if(model==null) throw new InvalidOperationException("FBX could not instantiate: "+name);
                model.transform.SetParent(root.transform,false);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var assigned=renderer.sharedMaterials;
                    for(int i=0;i<assigned.Length;i++)
                    {
                        int index=Array.FindIndex(MaterialNames,n=>assigned[i]!=null && assigned[i].name.StartsWith(n,StringComparison.OrdinalIgnoreCase));
                        if(index<0) throw new InvalidOperationException(name+" has unmapped FBX material: "+(assigned[i]!=null?assigned[i].name:"null"));
                        assigned[i]=palette[index];
                    }
                    renderer.sharedMaterials=assigned;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows=true;
                }
                if(controller!=null)
                {
                    var animator=model.GetComponent<Animator>();
                    if(animator==null) animator=model.AddComponent<Animator>();
                    animator.runtimeAnimatorController=controller;
                    animator.applyRootMotion=false;
                    if(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0) throw new InvalidOperationException(name+" has no skinned body.");
                    if(!model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="WeaponHand_R")) throw new InvalidOperationException(name+" has no hand socket.");
                }
                if(WeaponNames.Contains(name) && !model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Muzzle"))
                    throw new InvalidOperationException(name+" has no muzzle socket.");
                if(WeaponNames.Contains(name) && !model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Grip"))
                    throw new InvalidOperationException(name+" has no grip socket.");
                if(name=="FirstPersonHands" && !model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Grip_R"))
                    throw new InvalidOperationException("First-person hands have no Grip_R socket.");
                string path=Output+name+".prefab";
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                if(prefab==null) throw new InvalidOperationException("Could not save prefab: "+path);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        static AnimatorController HumanController()
        {
            const string path=Output+"WildstoneHuman.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            if(controller.layers.Length==0) controller.AddLayer("Base Layer");
            var clips=AssetDatabase.LoadAllAssetsAtPath(Art+"WildstoneHuman.fbx").OfType<AnimationClip>().ToArray();
            string[] names={"Idle","Run","Strafe","Jump","Slide","Aim","Death"};
            var machine=controller.layers[0].stateMachine;
            foreach(var name in names)
            {
                var clip=clips.FirstOrDefault(c=>c.name==name || c.name.EndsWith("|"+name,StringComparison.Ordinal));
                if(clip==null) throw new InvalidOperationException("Human FBX clip missing: "+name+" (found "+string.Join(", ",clips.Select(c=>c.name))+")");
                var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==name)??machine.AddState(name);
                state.motion=clip;
                if(name=="Idle") machine.defaultState=state;
            }
            EditorUtility.SetDirty(controller); return controller;
        }
    }
}
