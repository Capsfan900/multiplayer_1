using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Movement;
using Wildstone.Presentation;
using Wildstone.UI;

namespace Wildstone.Editor
{
    // Dev tool: renders the first-person viewmodel at hip and full ADS for every held weapon over the baked Atrium,
    // so framing is judged in minutes instead of a player build (Tools/Unity/Invoke-Unity.ps1 -Action CaptureViewmodel).
    public static class ViewmodelCapture
    {
        static readonly WeaponKind[] Kinds={WeaponKind.LineDriver,WeaponKind.LaserSMG,WeaponKind.LaserPistol,WeaponKind.LaserSniper,WeaponKind.LaserShotgun,
            WeaponKind.PulseLobber,WeaponKind.ReboundDisc,WeaponKind.PushLens,WeaponKind.StunGun,WeaponKind.LaserSword};

        public static void Run()
        {
            string output=Path.Combine("Artifacts/Visual","Viewmodel-"+System.DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(output);
            CaptureMaps(output);
            EditorSceneManager.OpenScene("Assets/Wildstone/Maps/atrium.unity");
            var catalog=AssetDatabase.LoadAssetAtPath<PresentationCatalog>("Assets/Wildstone/Resources/WildstonePresentation.asset");
            var map=AssetDatabase.LoadAssetAtPath<ArenaCatalog>(ArenaBuilder.CatalogPath).Find("atrium");
            var spawn=map.Spawns[0].Position; var toward=(map.Minimum+map.Maximum)*.5f-spawn; toward.y=0;
            var texture=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var player=new CombatState{Participant=1,LifeGeneration=1};
            var motor=new MotorState{Grounded=true};
            foreach(var kind in Kinds)
                foreach(float ads in new[]{0f,1f})
                {
                    if(ads>0 && !WeaponCatalog.CanAim(kind)) continue;
                    // A fresh camera and view per shot: WeaponView swaps weapons with Destroy, which edit mode ignores.
                    var camera=new GameObject("Capture camera",typeof(Camera)).GetComponent<Camera>();
                    var view=new GameObject("Capture view",typeof(WeaponView)).GetComponent<WeaponView>();
                    try
                    {
                        camera.transform.SetPositionAndRotation(spawn+Vector3.up*1.65f,Quaternion.LookRotation(toward));
                        var data=camera.GetUniversalAdditionalCameraData();
                        data.renderPostProcessing=true; data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        camera.targetTexture=texture; camera.aspect=16f/9;
                        camera.fieldOfView=LocalSettings.VerticalFov(LocalSettings.DefaultFov,camera.aspect);
                        view.Initialize(camera,catalog);
                        player.SelectedSpecial=kind!=WeaponKind.LineDriver; player.SpecialWeapon=kind; player.AdsProgress=ads;
                        for(int i=0;i<120;i++) view.Present(player,catalog,in motor,10,1f/60,false); // equip raise and ADS settle
                        view.SyncProjection(camera);
                        if(ads==0) Measure(kind,camera.transform);
                        camera.Render(); // warm-up: the first render of a batch session comes out washed out
                        camera.Render();
                        RenderTexture.active=texture; image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply(); RenderTexture.active=null;
                        File.WriteAllBytes(Path.Combine(output,kind+(ads>0?"-ads":"-hip")+".png"),image.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(camera.gameObject); }
                }
            Debug.Log("WILDSTONE VIEWMODEL CAPTURE: "+Path.GetFullPath(output));
        }
        // Baked map views (no HUD or viewmodel): a high corner overview and an eye-height look from the centre.
        static void CaptureMaps(string output)
        {
            foreach(var map in AssetDatabase.LoadAssetAtPath<ArenaCatalog>(ArenaBuilder.CatalogPath).Maps)
            {
                EditorSceneManager.OpenScene("Assets/Wildstone/Maps/"+map.Id+".unity");
                // Created after the scene opens: opening a scene unloads unreferenced textures.
                var texture=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);
                var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
                var spawn=map.Spawns[0].Position;
                var centre=(map.Minimum+map.Maximum)*.5f; centre.y=spawn.y;
                var corner=new Vector3(map.Minimum.x+3,spawn.y+9,map.Minimum.z+3);
                var views=new[]{("overview",corner,centre-corner),("ground",centre+Vector3.up*1.65f,new Vector3(spawn.x-centre.x,0,spawn.z-centre.z))};
                foreach(var (name,position,forward) in views)
                {
                    var camera=new GameObject("Map capture camera",typeof(Camera)).GetComponent<Camera>();
                    try
                    {
                        camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward));
                        var data=camera.GetUniversalAdditionalCameraData();
                        data.renderPostProcessing=true; data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        camera.targetTexture=texture; camera.aspect=16f/9; camera.farClipPlane=1500;
                        camera.fieldOfView=LocalSettings.VerticalFov(LocalSettings.DefaultFov,camera.aspect);
                        camera.Render(); camera.Render();
                        RenderTexture.active=texture; image.ReadPixels(new Rect(0,0,1920,1080),0,0); image.Apply(); RenderTexture.active=null;
                        File.WriteAllBytes(Path.Combine(output,"map-"+map.Id+"-"+name+".png"),image.EncodeToPNG());
                    }
                    finally { Object.DestroyImmediate(camera.gameObject); }
                }
            }
        }
        // Camera-space grip, sight, muzzle and weapon bounds, so hip poses are set by numbers rather than eyeballed.
        static void Measure(WeaponKind kind,Transform eye)
        {
            var pivot=GripAttachment.Find(eye,"Weapon pivot");
            Transform weapon=null;
            foreach(Transform child in pivot) if(child.name.EndsWith("(Clone)")) weapon=child;
            if(weapon==null) return;
            string Point(string name) { var point=GripAttachment.Find(weapon,name); return point==null?"-":eye.InverseTransformPoint(point.position).ToString("F3"); }
            Vector3 min=Vector3.positiveInfinity, max=Vector3.negativeInfinity;
            foreach(var renderer in weapon.GetComponentsInChildren<Renderer>())
            {
                var bounds=renderer.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    var local=eye.InverseTransformPoint(new Vector3(corner%2==0?bounds.min.x:bounds.max.x,corner/2%2==0?bounds.min.y:bounds.max.y,corner<4?bounds.min.z:bounds.max.z));
                    min=Vector3.Min(min,local); max=Vector3.Max(max,local);
                }
            }
            Debug.Log("WILDSTONE VIEWMODEL MEASURE "+kind+" grip="+Point("Grip")+" sight="+Point("Sight")+" muzzle="+Point("Muzzle")+" left="+Point("LeftHand")+" min="+min.ToString("F3")+" max="+max.ToString("F3"));
        }
    }
}
