using System;
using System.Collections.Generic;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    // Engine-free menu motion laws, ported from vibegame1 2cb6e576 MenuCameraDrift/CloudDive/UiSprites.
    public static class MenuMotion
    {
        // Composition: vertical lens, boom length/height above the squad, look height, and the turn that puts the squad in the right third.
        public const float Fov=50, Radius=8.5f, Height=4.2f, LookHeight=1.2f, ComposeYaw=16;
        // Slow pendulum around the squad plus the source's incommensurate Lissajous breathing.
        public const float ArcDegrees=30, ArcPeriod=96, DriftPeriod=70, DriftYaw=1.5f, DriftPitch=.8f;
        public static readonly Vector3 DriftAmplitude=new Vector3(.6f,.25f,.6f);
        // Dive on entering an arena: camera push toward the squad and a tan-space lens widen.
        public const float DiveSeconds=.7f, StillSeconds=.25f, DivePush=.5f, WidenGain=.3f;
        // Menu scrim: opaque under the column, then the source ramp's eased fall to clear.
        public const float RampFlat=.58f;

        // Reduced motion freezes the pose at t=0: a composed still, never a moving camera.
        public static void Drift(float t,bool reducedMotion,out float arcYaw,out Vector3 offset,out float yaw,out float pitch)
        {
            if(reducedMotion || float.IsNaN(t) || float.IsInfinity(t)) t=0;
            float w=2*Mathf.PI/DriftPeriod;
            float a=Mathf.Sin(w*t), b=Mathf.Sin(w*1.37f*t+1.1f), c=Mathf.Sin(w*.71f*t+2.3f);
            arcYaw=ArcDegrees*Mathf.Sin(2*Mathf.PI*t/ArcPeriod);
            offset=new Vector3(DriftAmplitude.x*a,DriftAmplitude.y*b,DriftAmplitude.z*c);
            yaw=DriftYaw*a; pitch=DriftPitch*b;
        }
        // CloudDive time law: leaves from rest and is still accelerating at the hand-over.
        public static float DiveEase(float k) { k=Mathf.Clamp01(k); return k*k*(1.3f-.3f*k); }
        public static float Widen(float fov,float gain) => 2*Mathf.Atan(Mathf.Tan(fov*.5f*Mathf.Deg2Rad)*(1+gain))*Mathf.Rad2Deg;
        public static float RampAlpha(float u)
        {
            u=Mathf.Clamp01(u); if(u<=RampFlat) return 1;
            float x=Mathf.Pow((u-RampFlat)/(1-RampFlat),.8f);
            return 1-x*x*(3-2*x);
        }
    }

    // Menu-only scene dressing: a slow camera drift around a posed four-team squad in the loaded arena.
    // Runs only while no match is attached; never touches simulation, input or network state.
    [DefaultExecutionOrder(150),RequireComponent(typeof(PracticeSession))]
    public sealed class MenuBackdrop : MonoBehaviour
    {
        static readonly WeaponKind[] Loadout={WeaponKind.LineDriver,WeaponKind.LaserPistol,WeaponKind.LaserSniper,WeaponKind.LaserSMG};
        // Per team: metres across the view, metres toward the camera, degrees turned from facing the camera, raise-aim phase.
        static readonly Vector4[] Slots={new Vector4(-1.5f,.2f,18,-1),new Vector4(-.5f,-.5f,6,.15f),new Vector4(.6f,.3f,-10,.55f),new Vector4(1.6f,-.4f,-24,-1)};
        PracticeSession session;
        PresentationCatalog catalog;
        Transform squadRoot;
        readonly List<PlayerVisual> visuals=new List<PlayerVisual>();
        readonly List<CombatState> states=new List<CombatState>();
        string builtFor;
        Vector3 focus;
        float baseYaw, boom=-1, diveStart=-1;
        MenuScreen diveScreen;
        public bool Active => session!=null && session.Settings!=null && session.Match==null && session.Camera!=null && session.Flow.Screen!=MenuScreen.Playing;
        public int SquadCount => visuals.Count;
        void Awake()
        {
            session=GetComponent<PracticeSession>();
            catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
        }
        // Starts the push-in; it ends when the menu leaves the starting screen for anything but Loading.
        public void Dive() { if(Active) { diveStart=Time.unscaledTime; diveScreen=session.Flow.Screen; } }
        void Update()
        {
            bool active=Active, loading=active && session.Flow.Screen==MenuScreen.Loading;
            if(active && !loading && builtFor!=session.MapId) Build();
            if(squadRoot!=null && squadRoot.gameObject.activeSelf!=(active && !loading)) squadRoot.gameObject.SetActive(active && !loading);
            if(!active || loading) return;
            float t=Time.unscaledTime, dt=Time.unscaledDeltaTime; bool still=session.Settings.ReducedMotion;
            for(int i=visuals.Count-1;i>=0;i--)
            {
                try
                {
                    float phase=Slots[states[i].Team].w;
                    states[i].AdsProgress=phase<0 || still?0:Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Sin(2*Mathf.PI*(t/11+phase))*2));
                    var motor=new MotorState { Position=states[i].Feet,Grounded=true,CapsuleHeight=1.8f };
                    visuals[i].Present(states[i],motor,catalog);
                    visuals[i].PresentHandling(states[i],0,dt,still);
                }
                catch(Exception ex) { Debug.LogException(ex,this); if(visuals[i]!=null) Destroy(visuals[i].gameObject); visuals.RemoveAt(i); states.RemoveAt(i); }
            }
        }
        void LateUpdate()
        {
            var screen=session!=null?session.Flow.Screen:MenuScreen.Title;
            if(diveStart>=0 && screen!=diveScreen && screen!=MenuScreen.Loading) diveStart=-1;
            if(!Active || builtFor==null) return;
            var camera=session.Camera; bool still=session.Settings.ReducedMotion;
            MenuMotion.Drift(Time.unscaledTime,still,out float arc,out var offset,out float yawJitter,out float pitchJitter);
            var look=focus+Vector3.up*MenuMotion.LookHeight;
            var orbit=Quaternion.Euler(0,baseYaw+arc,0);
            var arm=orbit*(new Vector3(0,MenuMotion.Height-MenuMotion.LookHeight,-MenuMotion.Radius)+offset);
            float length=arm.magnitude, clear=length; arm/=length;
            // Boom: pull in at once when architecture blocks the squad, ease back out.
            if(Physics.SphereCast(look,.3f,arm,out var hit,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) clear=Mathf.Max(1.5f,hit.distance);
            boom=boom<0 || clear<boom?clear:Mathf.MoveTowards(boom,clear,Time.unscaledDeltaTime*1.5f);
            var position=look+arm*boom;
            float fov=MenuMotion.Fov;
            if(diveStart>=0 && !still)
            {
                float e=MenuMotion.DiveEase((Time.unscaledTime-diveStart)/MenuMotion.DiveSeconds);
                position=Vector3.Lerp(position,look,MenuMotion.DivePush*e);
                fov=Mathf.Lerp(fov,MenuMotion.Widen(fov,MenuMotion.WidenGain),e);
            }
            var toward=look-position;
            float yaw=Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg, pitch=-Mathf.Asin(Mathf.Clamp(toward.y/toward.magnitude,-1,1))*Mathf.Rad2Deg;
            camera.transform.SetPositionAndRotation(position,Quaternion.Euler(pitch+pitchJitter,yaw-MenuMotion.ComposeYaw+yawJitter,0));
            camera.fieldOfView=fov;
        }
        void Build()
        {
            Clear(); builtFor=session.MapId; boom=-1;
            ArenaDefinition map=null;
            try { map=session.Catalog!=null?session.Catalog.Find(session.MapId):null; } catch(ArgumentException) { }
            if(map==null) { builtFor=null; return; }
            var center=(map.Minimum+map.Maximum)*.5f;
            focus=map.ZoneRoute!=null && map.ZoneRoute.Length>0?map.ZoneRoute[0]:new Vector3(center.x,0,center.z);
            Ground(ref focus);
            // Look across the arena from its outer side; fall back to north when the focus is the centre.
            var inward=new Vector3(center.x-focus.x,0,center.z-focus.z);
            baseYaw=inward.sqrMagnitude>.01f?Mathf.Atan2(inward.x,inward.z)*Mathf.Rad2Deg:0;
            if(catalog==null) return;
            squadRoot=new GameObject("Menu backdrop squad").transform; squadRoot.SetParent(transform,false);
            var frame=Quaternion.Euler(0,baseYaw,0);
            for(int team=0;team<Slots.Length && team<catalog.Humans.Length;team++)
            {
                var slot=Slots[team];
                var feet=focus+frame*new Vector3(slot.x,0,-slot.y);
                if(!Ground(ref feet) || Physics.CheckCapsule(feet+Vector3.up*.45f,feet+Vector3.up*1.45f,.3f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) continue;
                var go=new GameObject("Backdrop soldier "+team,typeof(PlayerVisual)); go.SetActive(false); go.transform.SetParent(squadRoot,false);
                try { go.GetComponent<PlayerVisual>().Initialize(catalog,team); }
                catch(ArgumentException) { Destroy(go); continue; } // missing or incomplete team prefab: show the arena without it
                var kind=Loadout[team];
                states.Add(new CombatState { Participant=100+team,Team=team,Feet=feet,SelectedSpecial=kind!=WeaponKind.LineDriver,SpecialWeapon=kind,
                    Aim=Quaternion.Euler(12,baseYaw+180+slot.z,0)*Vector3.forward });
                visuals.Add(go.GetComponent<PlayerVisual>());
            }
        }
        static bool Ground(ref Vector3 point)
        {
            if(!Physics.Raycast(point+Vector3.up*3,Vector3.down,out var hit,8,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) return false;
            point=hit.point; return true;
        }
        void Clear()
        {
            if(squadRoot!=null) Destroy(squadRoot.gameObject);
            squadRoot=null; visuals.Clear(); states.Clear();
        }
        void OnDestroy() => Clear();
    }
}
