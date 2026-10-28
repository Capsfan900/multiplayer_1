using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wildstone.Combat;

namespace Wildstone.Presentation
{
    public sealed class WeaponView : MonoBehaviour
    {
        const int ViewLayer=11;
        Camera overlay;
        GameObject weapon;
        WeaponKind currentWeapon;
        Transform socket;
        bool worldHadViewLayer;
        public void Initialize(Camera world,PresentationCatalog catalog)
        {
            if(world==null || catalog.Hands==null) throw new System.ArgumentException("First-person camera or hands missing.");
            var holder=new GameObject("Viewmodel",typeof(Camera));
            holder.transform.SetParent(world.transform,false);
            overlay=holder.GetComponent<Camera>();
            overlay.CopyFrom(world);
            overlay.cullingMask=1<<ViewLayer;
            overlay.clearFlags=CameraClearFlags.Depth;
            overlay.depth=world.depth+1;
            overlay.nearClipPlane=.01f;
            overlay.farClipPlane=4;
            var worldData=world.GetUniversalAdditionalCameraData();
            var overlayData=overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType=CameraRenderType.Overlay;
            overlayData.renderPostProcessing=false;
            worldHadViewLayer=(world.cullingMask&(1<<ViewLayer))!=0;
            world.cullingMask&=~(1<<ViewLayer);
            worldData.cameraStack.Add(overlay);
            var hands=Instantiate(catalog.Hands,holder.transform);
            hands.transform.localPosition=new Vector3(.22f,-.24f,.48f);
            hands.transform.localRotation=Quaternion.identity;
            SetLayer(hands.transform,ViewLayer);
            socket=GripAttachment.Find(hands.transform,"Grip_R");
            if(socket==null) throw new System.ArgumentException("First-person hands have no Grip_R socket.");
        }
        public void SyncProjection(Camera world)
        {
            if(world==null || overlay==null) return;
            overlay.fieldOfView=world.fieldOfView;
            overlay.aspect=world.aspect;
            overlay.orthographic=world.orthographic;
            overlay.orthographicSize=world.orthographicSize;
        }
        public void Present(CombatState player,PresentationCatalog catalog)
        {
            if(overlay==null || player==null) return;
            overlay.enabled=player.Alive;
            WeaponKind selected=player.SelectedSpecial && player.SpecialWeapon!=WeaponKind.None?player.SpecialWeapon:WeaponKind.LineDriver;
            if(selected==currentWeapon) return;
            if(weapon!=null) Destroy(weapon);
            currentWeapon=selected;
            var prefab=catalog.Weapon(selected);
            if(prefab==null) return;
            weapon=Instantiate(prefab,socket);
            GripAttachment.Align(weapon.transform,socket);
            SetLayer(weapon.transform,ViewLayer);
        }
        public void Teardown(Camera world)
        {
            if(overlay==null) return;
            if(world!=null)
            {
                world.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
                world.cullingMask=(world.cullingMask&~(1<<ViewLayer))|(worldHadViewLayer?(1<<ViewLayer):0);
            }
            Destroy(overlay.gameObject);
            overlay=null; weapon=null; socket=null; currentWeapon=WeaponKind.None;
        }
        static void SetLayer(Transform root,int layer)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=layer;
        }
    }
}
