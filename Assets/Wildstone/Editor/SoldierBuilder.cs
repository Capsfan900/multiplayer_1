using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Wildstone.Presentation;

namespace Wildstone.Editor
{
    // D028 soldier import: Humanoid avatar from Soldier.fbx, clips from SoldierAnimations.fbx (copied avatar),
    // per-team material sets and the generated locomotion/upper-body/additive controller (SoldierRig parameters).
    public static class SoldierBuilder
    {
        const string Art="Assets/Wildstone/Art/";
        const string Output="Assets/Wildstone/Resources/Presentation/";
        const string ModelPath=Art+"Soldier.fbx", AnimationPath=Art+"SoldierAnimations.fbx", ArmsPath=Art+"SoldierArms.fbx";
        const string ClipSidecar=Art+"SoldierAnimations.clips.json";
        static readonly string[] Locomotion=SoldierRig.LocomotionClips, Required=SoldierRig.Clips, Looping=SoldierRig.LoopingClips;

        [Serializable] class ClipInfo { public string name; public bool loop; public float speed; }
        [Serializable] class ClipTable { public ClipInfo[] clips; }

        public static void ConfigureImports()
        {
            var model=Importer(ModelPath);
            bool changed=model.animationType!=ModelImporterAnimationType.Human || model.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel ||
                model.optimizeGameObjects || model.importAnimation;
            model.animationType=ModelImporterAnimationType.Human;
            model.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            model.optimizeGameObjects=false; model.importAnimation=false;
            if(changed) model.SaveAndReimport();
            var avatar=AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if(avatar==null || !avatar.isHuman || !avatar.isValid) throw new InvalidOperationException("Soldier.fbx did not produce a valid Humanoid avatar.");

            var animation=Importer(AnimationPath);
            var clips=animation.clipAnimations.Length>0?animation.clipAnimations:animation.defaultClipAnimations;
            changed=animation.animationType!=ModelImporterAnimationType.Human || animation.avatarSetup!=ModelImporterAvatarSetup.CopyFromOther ||
                animation.sourceAvatar!=avatar || !animation.importAnimation || animation.clipAnimations.Length==0;
            animation.animationType=ModelImporterAnimationType.Human;
            animation.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;
            animation.sourceAvatar=avatar;
            animation.importAnimation=true;
            for(int i=0;i<clips.Length;i++)
            {
                string name=ClipName(clips[i].name);
                bool loop=Looping.Contains(name);
                if(clips[i].name!=name || clips[i].loopTime!=loop || !clips[i].lockRootRotation || !clips[i].lockRootHeightY || !clips[i].lockRootPositionXZ)
                {
                    clips[i].name=name; clips[i].loopTime=loop; clips[i].loopPose=loop;
                    // In place, every root channel baked into the pose from the original: slide/death hip height and
                    // the ~2 cm hip sway survive, and with applyRootMotion off the body stays on the motor.
                    clips[i].lockRootRotation=true; clips[i].lockRootHeightY=true; clips[i].lockRootPositionXZ=true;
                    clips[i].keepOriginalOrientation=true; clips[i].keepOriginalPositionY=true; clips[i].keepOriginalPositionXZ=true;
                    changed=true;
                }
            }
            if(changed) { animation.clipAnimations=clips; animation.SaveAndReimport(); }
            var arms=Importer(ArmsPath);
            if(arms.animationType!=ModelImporterAnimationType.Generic || arms.importAnimation || arms.optimizeGameObjects)
            {
                arms.animationType=ModelImporterAnimationType.Generic; arms.importAnimation=false; arms.optimizeGameObjects=false;
                arms.SaveAndReimport();
            }
        }
        static ModelImporter Importer(string path) =>
            AssetImporter.GetAtPath(path) as ModelImporter ?? throw new InvalidOperationException("Soldier FBX importer missing: "+path);
        static string ClipName(string name)
        {
            int separator=name.LastIndexOf('|');
            return separator>=0?name.Substring(separator+1):name;
        }

        static Dictionary<string,AnimationClip> Clips()
        {
            var clips=AssetDatabase.LoadAllAssetsAtPath(AnimationPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal))
                .GroupBy(c=>ClipName(c.name)).ToDictionary(g=>g.Key,g=>g.First());
            var missing=Required.Where(n=>!clips.ContainsKey(n)).ToArray();
            if(missing.Length>0) throw new InvalidOperationException("Soldier clips missing: "+string.Join(", ",missing)+" (found "+string.Join(", ",clips.Keys)+")");
            return clips;
        }
        // Authored in-place speeds (m/s) from the clip sidecar; the blend positions match them so feet do not skate.
        static Dictionary<string,float> Speeds()
        {
            var speeds=new Dictionary<string,float>();
            if(!File.Exists(ClipSidecar)) throw new InvalidOperationException("Missing "+ClipSidecar);
            var table=JsonUtility.FromJson<ClipTable>(File.ReadAllText(ClipSidecar));
            foreach(var clip in table?.clips??Array.Empty<ClipInfo>()) speeds[clip.name]=clip.speed;
            foreach(var name in Locomotion.Skip(1))
                if(!speeds.TryGetValue(name,out float speed) || !(speed>.1f)) throw new InvalidOperationException("Clip sidecar has no travel speed for "+name);
            return speeds;
        }

        public static AnimatorController Controller()
        {
            const string path=Output+"Soldier.controller";
            AssetDatabase.DeleteAsset(path);
            var controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var clips=Clips(); var speeds=Speeds();
            foreach(var name in new[]{"MoveX","MoveZ","WallSide","VerticalSpeed","Aim","LocomotionRate"})
                controller.AddParameter(name,AnimatorControllerParameterType.Float);
            foreach(var name in new[]{"Grounded","Sliding","WallRun","Dashing","Dead","DeathBack","Reload"})
                controller.AddParameter(name,AnimatorControllerParameterType.Bool);
            controller.AddParameter("Stance",AnimatorControllerParameterType.Int);
            foreach(var name in new[]{"Throw","Swing","Equip","Fire","HitFront","HitBack"})
                controller.AddParameter(name,AnimatorControllerParameterType.Trigger);
            controller.parameters=controller.parameters.Select(p=>{ if(p.name=="Grounded" || p.name=="LocomotionRate") { if(p.type==AnimatorControllerParameterType.Bool) p.defaultBool=true; else p.defaultFloat=1; } return p; }).ToArray();

            var layer=controller.layers[0]; var machine=layer.stateMachine;
            var locomotion=machine.AddState("Locomotion");
            var tree=new BlendTree { name="Locomotion", blendType=BlendTreeType.FreeformCartesian2D, blendParameter="MoveX", blendParameterY="MoveZ", hideFlags=HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree,controller);
            tree.AddChild(clips["Idle"],Vector2.zero);
            foreach(var gait in new[]{"Walk","Run"})
            {
                tree.AddChild(clips[gait+"F"],new Vector2(0,speeds[gait+"F"]));
                tree.AddChild(clips[gait+"B"],new Vector2(0,-speeds[gait+"B"]));
                tree.AddChild(clips[gait+"L"],new Vector2(-speeds[gait+"L"],0));
                tree.AddChild(clips[gait+"R"],new Vector2(speeds[gait+"R"],0));
            }
            locomotion.motion=tree;
            // Beyond the authored run speed the cycle plays faster instead of skating (SoldierRig sets LocomotionRate).
            locomotion.speedParameter="LocomotionRate"; locomotion.speedParameterActive=true;
            machine.defaultState=locomotion;
            var jump=State(machine,"JumpStart",clips); var fall=State(machine,"Fall",clips); var land=State(machine,"Land",clips);
            var slideEnter=State(machine,"SlideEnter",clips); var slide=State(machine,"Slide",clips);
            var dash=State(machine,"Dash",clips);
            var wall=machine.AddState("WallRun");
            var wallTree=new BlendTree { name="WallRun", blendType=BlendTreeType.Simple1D, blendParameter="WallSide", useAutomaticThresholds=false, hideFlags=HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(wallTree,controller);
            wallTree.AddChild(clips["WallRunL"],-1); wallTree.AddChild(clips["WallRunR"],1);
            wall.motion=wallTree;
            // Wall runs reach 13.75 m/s against the 7.94 m/s authored stride; scale the cycle like the ground run.
            wall.speedParameter="LocomotionRate"; wall.speedParameterActive=true;
            var deathFront=State(machine,"DeathF",clips); var deathBack=State(machine,"DeathB",clips);

            Link(locomotion,jump,.08f,If("Grounded",false),Greater("VerticalSpeed",1));
            Link(locomotion,fall,.18f,If("Grounded",false),Less("VerticalSpeed",1));
            Link(jump,fall,.15f,null,exit:.85f);
            Link(jump,land,.08f,If("Grounded",true));
            Link(fall,land,.06f,If("Grounded",true),If("Sliding",false));
            Link(land,locomotion,.15f,null,exit:.7f);
            Link(land,slideEnter,.08f,If("Sliding",true));
            Link(fall,slideEnter,.08f,If("Sliding",true));
            Link(locomotion,slideEnter,.08f,If("Sliding",true));
            Link(slideEnter,slide,.12f,null,exit:.9f);
            Link(slideEnter,locomotion,.15f,If("Sliding",false));
            Link(slide,locomotion,.18f,If("Sliding",false),If("Grounded",true));
            Link(slide,fall,.18f,If("Sliding",false),If("Grounded",false));
            foreach(var from in new[]{locomotion,jump,fall,land,slide})
            {
                Link(from,wall,.12f,If("WallRun",true));
                Link(from,dash,.06f,If("Dashing",true),If("Sliding",false),If("WallRun",false));
            }
            Link(wall,fall,.15f,If("WallRun",false),If("Grounded",false));
            Link(wall,locomotion,.15f,If("WallRun",false),If("Grounded",true));
            Link(dash,fall,.12f,If("Dashing",false),If("Grounded",false));
            Link(dash,locomotion,.12f,If("Dashing",false),If("Grounded",true));
            AnyLink(machine,deathFront,.1f,If("Dead",true),If("DeathBack",false));
            AnyLink(machine,deathBack,.1f,If("Dead",true),If("DeathBack",true));

            // Upper body: weapon stance holds blended toward the ADS raise, reload/throw/swing/equip actions.
            var upper=new AnimatorControllerLayer { name="Upper body", defaultWeight=1, blendingMode=AnimatorLayerBlendingMode.Override, avatarMask=UpperMask(), stateMachine=new AnimatorStateMachine { name="Upper body", hideFlags=HideFlags.HideInHierarchy } };
            AssetDatabase.AddObjectToAsset(upper.stateMachine,controller);
            controller.AddLayer(upper);
            var upperMachine=upper.stateMachine;
            var rifle=upperMachine.AddState("HoldRifle"); rifle.motion=AimTree(controller,"Rifle",clips["HoldRifle"],clips["AimRifle"]);
            var pistol=upperMachine.AddState("HoldPistol"); pistol.motion=AimTree(controller,"Pistol",clips["HoldPistol"],clips["AimPistol"]);
            var sword=State(upperMachine,"HoldSword",clips);
            upperMachine.defaultState=rifle;
            var holds=new[]{rifle,pistol,sword};
            for(int i=0;i<holds.Length;i++)
                for(int j=0;j<holds.Length;j++) if(i!=j) Link(holds[i],holds[j],.15f,Equals("Stance",j));
            var reload=State(upperMachine,"Reload",clips);
            Link(rifle,reload,.1f,If("Reload",true));
            Link(reload,rifle,.15f,If("Reload",false));
            foreach(var action in new[]{("Throw","Throw"),("SwordSwing","Swing"),("Equip","Equip")})
            {
                var state=State(upperMachine,action.Item1,clips);
                var any=upperMachine.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If,0,action.Item2); any.duration=.06f; any.canTransitionToSelf=true;
                for(int j=0;j<holds.Length;j++) Link(state,holds[j],.12f,Equals("Stance",j),exit:.85f);
            }
            // Additive cues: recoil kick and hit flinches over whatever the body is doing.
            var additive=new AnimatorControllerLayer { name="Additive", defaultWeight=1, blendingMode=AnimatorLayerBlendingMode.Additive, stateMachine=new AnimatorStateMachine { name="Additive", hideFlags=HideFlags.HideInHierarchy } };
            AssetDatabase.AddObjectToAsset(additive.stateMachine,controller);
            controller.AddLayer(additive);
            var none=additive.stateMachine.AddState("None");
            additive.stateMachine.defaultState=none;
            foreach(var cue in new[]{("Recoil","Fire"),("HitF","HitFront"),("HitB","HitBack")})
            {
                var state=State(additive.stateMachine,cue.Item1,clips);
                var any=additive.stateMachine.AddAnyStateTransition(state);
                any.AddCondition(AnimatorConditionMode.If,0,cue.Item2); any.duration=.02f; any.canTransitionToSelf=true;
                Link(state,none,.08f,null,exit:.9f);
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }
        static Motion AimTree(AnimatorController controller,string name,AnimationClip hold,AnimationClip aim)
        {
            var tree=new BlendTree { name=name+" aim", blendType=BlendTreeType.Simple1D, blendParameter="Aim", useAutomaticThresholds=false, hideFlags=HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree,controller);
            tree.AddChild(hold,0); tree.AddChild(aim,1);
            return tree;
        }
        static AvatarMask UpperMask()
        {
            const string path=Output+"SoldierUpperBody.mask";
            var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if(mask==null) { mask=new AvatarMask(); AssetDatabase.CreateAsset(mask,path); }
            for(var part=AvatarMaskBodyPart.Root;part<AvatarMaskBodyPart.LastBodyPart;part++)
                mask.SetHumanoidBodyPartActive(part,part!=AvatarMaskBodyPart.Root && part!=AvatarMaskBodyPart.LeftLeg && part!=AvatarMaskBodyPart.RightLeg &&
                    part!=AvatarMaskBodyPart.LeftFootIK && part!=AvatarMaskBodyPart.RightFootIK);
            EditorUtility.SetDirty(mask);
            return mask;
        }
        static AnimatorState State(AnimatorStateMachine machine,string name,Dictionary<string,AnimationClip> clips)
        {
            var state=machine.AddState(name); state.motion=clips[name]; return state;
        }
        static (AnimatorConditionMode,float,string) If(string parameter,bool value) => (value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,parameter);
        static (AnimatorConditionMode,float,string) Greater(string parameter,float value) => (AnimatorConditionMode.Greater,value,parameter);
        static (AnimatorConditionMode,float,string) Less(string parameter,float value) => (AnimatorConditionMode.Less,value,parameter);
        static (AnimatorConditionMode,float,string) Equals(string parameter,int value) => (AnimatorConditionMode.Equals,value,parameter);
        static void Link(AnimatorState from,AnimatorState to,float duration,(AnimatorConditionMode,float,string)? first,(AnimatorConditionMode,float,string)? second=null,(AnimatorConditionMode,float,string)? third=null,float exit=-1)
        {
            var transition=from.AddTransition(to);
            transition.duration=duration; transition.hasFixedDuration=true;
            transition.hasExitTime=exit>=0; if(exit>=0) transition.exitTime=exit;
            foreach(var condition in new[]{first,second,third}) if(condition.HasValue) transition.AddCondition(condition.Value.Item1,condition.Value.Item2,condition.Value.Item3);
        }
        static void AnyLink(AnimatorStateMachine machine,AnimatorState to,float duration,params (AnimatorConditionMode,float,string)[] conditions)
        {
            var transition=machine.AddAnyStateTransition(to);
            transition.duration=duration; transition.canTransitionToSelf=false;
            foreach(var condition in conditions) transition.AddCondition(condition.Item1,condition.Item2,condition.Item3);
        }

        // One prefab per team from the single soldier mesh: team accent materials and that team's symbol only.
        public static GameObject TeamPrefab(int team,AnimatorController controller,Func<string,Color,string,Material> material)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) ?? throw new InvalidOperationException("Missing FBX: "+ModelPath);
            var accent=SoldierRig.TeamAccent(team);
            var shared=new Dictionary<string,Material>
            {
                {"SoldierArmor",Lit(material("SoldierArmor",new Color(.78f,.77f,.73f),"Universal Render Pipeline/Lit"),.45f,0)},
                {"SoldierSuit",Lit(material("SoldierSuit",new Color(.12f,.13f,.14f),"Universal Render Pipeline/Lit"),.22f,0)},
                {"SoldierTrim",Lit(material("SoldierTrim",new Color(.30f,.31f,.33f),"Universal Render Pipeline/Lit"),.55f,.7f)},
                {"SoldierVisor",Lit(material("SoldierVisor",new Color(.03f,.05f,.06f),"Universal Render Pipeline/Lit"),.92f,0)},
                {"SoldierAccent",Lit(material("SoldierAccentTeam"+(team+1),accent,"Universal Render Pipeline/Lit"),.35f,0)},
                // Symbols are neutral ivory on the dark plates: the shape, not the hue, carries the team in grey/CVD views.
                {"SoldierMark",Lit(material("SoldierMark",new Color(.95f,.95f,.92f),"Universal Render Pipeline/Lit"),.4f,0)},
                {"SoldierGlow",Glow(material("SoldierGlowTeam"+(team+1),accent,"Universal Render Pipeline/Lit"),accent*2.2f)},
            };
            var root=new GameObject("SoldierTeam"+(team+1));
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform,false);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var assigned=renderer.sharedMaterials;
                    for(int i=0;i<assigned.Length;i++)
                    {
                        string key=shared.Keys.FirstOrDefault(n=>assigned[i]!=null && assigned[i].name.StartsWith(n,StringComparison.OrdinalIgnoreCase));
                        if(key==null) throw new InvalidOperationException("Soldier has unmapped FBX material: "+(assigned[i]!=null?assigned[i].name:"null"));
                        assigned[i]=shared[key];
                    }
                    renderer.sharedMaterials=assigned;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On; renderer.receiveShadows=true;
                    if(renderer.name.StartsWith("TeamMark",StringComparison.Ordinal)) renderer.gameObject.SetActive(renderer.name=="TeamMark"+(team+1));
                }
                var animator=model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController=controller; animator.applyRootMotion=false;
                animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                if(animator.avatar==null || !animator.avatar.isHuman) throw new InvalidOperationException("Soldier prefab has no Humanoid avatar.");
                foreach(var socket in new[]{"WeaponHand_R","WeaponHand_L","HeadSocket"})
                    if(GripAttachment.Find(model.transform,socket)==null) throw new InvalidOperationException("Soldier has no "+socket+" socket.");
                if(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==0) throw new InvalidOperationException("Soldier has no skinned body.");
                string path=Output+"SoldierTeam"+(team+1)+".prefab";
                return PrefabUtility.SaveAsPrefabAsset(root,path) ?? throw new InvalidOperationException("Could not save prefab: "+path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        public static GameObject ArmsPrefab(Func<string,Color,string,Material> material)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPath) ?? throw new InvalidOperationException("Missing FBX: "+ArmsPath);
            var root=new GameObject("SoldierArms");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(source);
                model.transform.SetParent(root.transform,false);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var assigned=renderer.sharedMaterials;
                    for(int i=0;i<assigned.Length;i++)
                    {
                        string name=assigned[i]!=null?assigned[i].name:"";
                        string key=new[]{"SoldierArmor","SoldierSuit","SoldierTrim","SoldierAccent","SoldierGlow"}.FirstOrDefault(n=>name.StartsWith(n,StringComparison.OrdinalIgnoreCase));
                        if(key==null) throw new InvalidOperationException("Soldier arms have unmapped FBX material: "+name);
                        assigned[i]=AssetDatabase.LoadAssetAtPath<Material>(Output+(key=="SoldierAccent"?"SoldierAccentTeam1":key=="SoldierGlow"?"SoldierGlowTeam1":key)+".mat");
                    }
                    renderer.sharedMaterials=assigned;
                    renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=true;
                }
                foreach(var socket in new[]{"Grip_R","Grip_L"})
                    if(GripAttachment.Find(model.transform,socket)==null) throw new InvalidOperationException("Soldier arms have no "+socket+" socket.");
                return PrefabUtility.SaveAsPrefabAsset(root,Output+"SoldierArms.prefab") ?? throw new InvalidOperationException("Could not save arms prefab.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        // Emission is on so PlayerVisual's hit flash can light the armour through a property block. Editor material
        // validation (seen after CaptureViewmodel) turns _EMISSION off unless the material is flagged realtime
        // emissive with a non-black colour, so the base emission sits a hair above black.
        static Material Lit(Material material,float smoothness,float metallic)
        {
            material.SetFloat("_Smoothness",smoothness); material.SetFloat("_Metallic",metallic);
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",new Color(.004f,.004f,.004f));
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material); return material;
        }
        static Material Glow(Material material,Color emission)
        {
            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",emission);
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetFloat("_Smoothness",.6f);
            EditorUtility.SetDirty(material); return material;
        }
    }
}
