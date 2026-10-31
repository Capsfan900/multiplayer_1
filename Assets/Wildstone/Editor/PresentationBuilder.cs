using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wildstone.Presentation;

namespace Wildstone.Editor
{
    public static class PresentationBuilder
    {
        const string Art="Assets/Wildstone/Art/";
        const string Output="Assets/Wildstone/Resources/Presentation/";
        const string CatalogPath="Assets/Wildstone/Resources/WildstonePresentation.asset";
        static readonly string[] WeaponNames={"LineDriver","PulseLobber","ReboundDisc","PushLens","Grenade","StunGun","LaserSword","LaserPistol","LaserShotgun","LaserSniper","LaserSMG"};
        static readonly string[] MaterialNames={"MatteIvory","Graphite","SoftGrey","SignalCyan","SignalAmber","SignalCoral","SignalViolet","MatteCharcoalSuit","DeepCharcoal","HeadValue","TeamMark","OpticGlass","SignalMint"};
        static readonly Color[] Colors={new Color(.76f,.73f,.66f),new Color(.105f,.115f,.125f),new Color(.32f,.34f,.35f),new Color(.13f,.68f,.74f),new Color(.95f,.43f,.12f),new Color(.84f,.30f,.24f),new Color(.55f,.39f,.80f),new Color(.19f,.21f,.22f),new Color(.105f,.12f,.13f),new Color(.28f,.30f,.31f),new Color(.70f,.70f,.66f),new Color(.05f,.09f,.10f),new Color(.30f,.86f,.50f)};
        static Material[] palette;
        public const float SignalGlow=1.2f;
        static readonly string[] SocketNames={"Grip","Muzzle","Sight","LeftHand"};

        // D030 loops are minutes long: stream them in the background instead of decoding into memory on load.
        static void ConfigureMusic()
        {
            foreach(var guid in AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Wildstone/Resources/Audio/Music"}))
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var settings=importer.defaultSampleSettings;
                if(settings.loadType==AudioClipLoadType.Streaming && !settings.preloadAudioData && importer.loadInBackground) continue;
                settings.loadType=AudioClipLoadType.Streaming; settings.preloadAudioData=false;
                settings.compressionFormat=AudioCompressionFormat.Vorbis; settings.quality=.7f;
                importer.defaultSampleSettings=settings; importer.loadInBackground=true;
                importer.SaveAndReimport();
            }
        }

        [MenuItem("Wildstone/Build Presentation")]
        public static void Build()
        {
            AssetImportValidation.ValidateFixture();
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh();
            palette=new Material[MaterialNames.Length];
            for(int i=0;i<palette.Length;i++)
                palette[i]=MaterialNames[i]=="OpticGlass"?Material(MaterialNames[i],Colors[i],"Wildstone/Reticle"):Material(MaterialNames[i],Colors[i],"Universal Render Pipeline/Lit");
            var catalog=AssetDatabase.LoadAssetAtPath<PresentationCatalog>(CatalogPath);
            if(catalog==null)
            {
                catalog=ScriptableObject.CreateInstance<PresentationCatalog>();
                AssetDatabase.CreateAsset(catalog,CatalogPath);
            }
            ConfigureMusic();
            // D028 soldier: one Humanoid mesh, four team material sets, generated controller, first-person arms.
            SoldierBuilder.ConfigureImports();
            var controller=SoldierBuilder.Controller();
            for(int i=0;i<catalog.Humans.Length;i++) catalog.Humans[i]=SoldierBuilder.TeamPrefab(i,controller,Material);
            catalog.Hands=SoldierBuilder.ArmsPrefab(Material);
            if(catalog.Weapons.Length!=WeaponNames.Length) Array.Resize(ref catalog.Weapons,WeaponNames.Length);
            for(int i=0;i<WeaponNames.Length;i++) catalog.Weapons[i]=ModelPrefab(WeaponNames[i]);
            catalog.BlinkPickup=ModelPrefab("BlinkPickup");
            catalog.ShieldPickup=ModelPrefab("ShieldPickup");
            catalog.HealthPickup=ModelPrefab("HealthPickup");
            // D028 effects: the ported vibegame1 Vfx shader with in-repo generated or credited textures (Art/VFX/CREDITS.md).
            catalog.Beam=Effect("Beam",new Color(.035f,.50f,1f),2);
            catalog.Pulse=Effect("Pulse",new Color(1f,.22f,.025f),2);
            catalog.Disc=Effect("Disc",new Color(.95f,.06f,.12f),2);
            catalog.Push=Effect("Push",new Color(.42f,.045f,.95f),2);
            catalog.Stun=Effect("Stun",new Color(.025f,.85f,.42f),2);
            catalog.Blink=Effect("Blink",new Color(.65f,.52f,.94f),1.4f);
            catalog.Shield=Effect("Shield",new Color(.56f,.84f,.93f),1.4f);
            catalog.Health=Effect("Health",new Color(.30f,.86f,.50f),1.4f);
            catalog.Danger=Effect("Danger",new Color(.98f,.57f,.20f),1.6f);
            // Zone: idle is a saturated magenta in the gap between the violet and crimson teams (ArenaHud.TeamTints), apart from
            // the danger orange; an owned zone is painted in the captor's team tint at runtime (ArenaEffects.ZoneColor), so
            // ZoneOwned is no longer read; contested stays a warm orange, more saturated so it holds over ivory.
            catalog.ZoneIdle=Effect("ZoneIdle",new Color(.95f,.14f,.80f));
            catalog.ZoneOwned=Effect("ZoneOwned",new Color(.74f,.83f,.49f));
            catalog.ZoneContested=Effect("ZoneContested",new Color(1f,.50f,.12f));
            catalog.VfxBlade=Vfx("VfxBlade",Color.white,"T_VfxBlade.png");
            catalog.VfxSpark=Vfx("VfxSpark",Color.white,"T_VfxSpark.png");
            catalog.VfxGlow=Vfx("VfxGlow",Color.white,"T_VfxFlare.png");
            catalog.VfxFire=Vfx("VfxFire",Color.white,"T_VfxFireBurst_8x8_PM.png",tiles:new Vector4(8,8,0,0),mask:new Vector4(1,0,.5f,0));
            catalog.VfxSmoke=Vfx("VfxSmoke",Color.white,"T_VfxSmokeBillow_8x8_6A.png",occluding:true,tiles:new Vector4(8,8,0,0),mask:new Vector4(0,0,0,1),topLight:1);
            catalog.VfxDust=Vfx("VfxDust",Color.white,"T_VfxMist_16x4.png",occluding:true,tiles:new Vector4(16,4,1,0));
            catalog.VfxDebris=Vfx("VfxDebris",Color.white,"T_VfxDebris_4x4.png",occluding:true,tiles:new Vector4(4,4,0,0),blend:false);
            catalog.VfxShockwave=Vfx("VfxShockwave",Color.white,"T_VfxShockwave.png",mask:new Vector4(0,0,1,0),blend:false);
            catalog.Scorch=Decal("Scorch",new Color(.04f,.035f,.03f,1),1,"T_VfxScorch.png");
            catalog.ZoneDecal=Decal("ZoneDecal",Color.white,0,null);
            // Painted, not additive (premultiplied over the scene, like the pickup rings): additive light cannot show over ivory
            // stone and sky. Soft fade stays tight so the base is not eaten, and the camera-near fade keeps the beacon from
            // tinting the screen when a player stands inside it.
            catalog.ZoneCurtain=Vfx("ZoneCurtain",Color.white,"T_VfxStrip.png",occluding:true,blend:false);
            catalog.ZoneCurtain.SetFloat("_SoftFade",.15f);
            catalog.ZoneCurtain.SetTexture("_NoiseMap",LoadVfx("T_VfxEnergy.png"));
            catalog.ZoneCurtain.EnableKeyword("_FADING_ON");
            catalog.ZoneCurtain.SetVector("_CameraFadeParams",new Vector4(1,1,0,0)); // clear within 1 m, solid from 2 m
            // Derez elimination: solid additive voxels and the dissolve body shader.
            catalog.VfxPixel=Vfx("VfxPixel",Color.white,null,blend:false);
            catalog.Derez=Material("Derez",new Color(.7f,.7f,.7f),"Wildstone/Derez");
            // Active shield: an additive fresnel shell in the Shield cue's hue, above the bloom threshold at its rim.
            catalog.ShieldShell=Material("ShieldShell",new Color(.78f,1.17f,1.3f),"Wildstone/ShieldShell");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("WILDSTONE PRESENTATION BUILT: 4 soldiers, arms, "+WeaponNames.Length+" weapons, 2 items, bounded effects/audio catalog.");
        }
        static Material Material(string name,Color color,string shaderName)
        {
            string path=Output+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(shaderName);
            if(shader==null) throw new InvalidOperationException("Missing URP shader: "+shaderName);
            if(material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
            material.shader=shader;
            // Signal accents are the weapons' energy parts: a dark albedo lets the emission carry the hue (lit albedo plus
            // emission washed amber out to yellow), and they emit above the bloom threshold (WeaponView pulses them).
            bool signal=name.StartsWith("Signal",StringComparison.Ordinal);
            material.SetColor("_BaseColor",signal?new Color(color.r*.35f,color.g*.35f,color.b*.35f,1):color);
            if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",.12f);
            if(material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor",signal?new Color(color.r*SignalGlow,color.g*SignalGlow,color.b*SignalGlow,1):Color.black);
            if(signal) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags=signal?MaterialGlobalIlluminationFlags.RealtimeEmissive:MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            return material;
        }
        // Line and ring effects: a soft cross-width strip (LineRenderer Stretch: v runs across the line).
        static Material Effect(string name,Color color,float intensity=1) =>
            Vfx(name,new Color(color.r*intensity,color.g*intensity,color.b*intensity,1),"T_VfxStrip.png",blend:false);
        static Texture2D LoadVfx(string file)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"VFX/"+file);
            if(texture==null) throw new InvalidOperationException("Missing VFX texture: "+file);
            return texture;
        }
        static Material Vfx(string name,Color color,string texture,bool occluding=false,Vector4 tiles=default,Vector4 mask=default,float topLight=0,bool blend=true)
        {
            var material=Material(name,color,"Wildstone/Vfx");
            material.SetTexture("_BaseMap",texture!=null?LoadVfx(texture):null);
            material.SetFloat("_DstBlend",(float)(occluding?UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha:UnityEngine.Rendering.BlendMode.One));
            material.SetVector("_FlipbookTiles",tiles==default?new Vector4(1,1,0,0):tiles);
            material.SetFloat("_FlipbookBlend",blend && tiles.x*tiles.y>1?1:0);
            material.SetVector("_MaskChannel",mask==default?new Vector4(1,0,0,0):mask);
            material.SetFloat("_TopLight",topLight);
            material.SetFloat("_SoftFade",occluding?.4f:.15f);
            material.renderQueue=occluding?2990:3000;
            EditorUtility.SetDirty(material);
            return material;
        }
        static Material Decal(string name,Color color,float mode,string texture)
        {
            var material=Material(name,color,"Wildstone/Projected Decal");
            material.SetFloat("_Mode",mode);
            if(texture!=null) material.SetTexture("_BaseMap",LoadVfx(texture));
            EditorUtility.SetDirty(material);
            return material;
        }
        static GameObject ModelPrefab(string name)
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
                // The mirrored legacy import leaves Blender-identity empties facing the weapon's top. Sockets take the
                // weapon frame (+Z barrel, +Y up) that the hand sockets and the ADS sight solve expect.
                foreach(var socket in model.GetComponentsInChildren<Transform>(true))
                    if(SocketNames.Contains(socket.name)) socket.rotation=root.transform.rotation;
                if(WeaponNames.Contains(name) && !model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Muzzle"))
                    throw new InvalidOperationException(name+" has no muzzle socket.");
                if(WeaponNames.Contains(name) && !model.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Grip"))
                    throw new InvalidOperationException(name+" has no grip socket.");
                string path=Output+name+".prefab";
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);
                if(prefab==null) throw new InvalidOperationException("Could not save prefab: "+path);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
