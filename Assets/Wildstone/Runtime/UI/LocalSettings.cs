using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Wildstone.Combat;

namespace Wildstone.UI
{
    [Serializable] public sealed class LocalSettings
    {
        public const string StorageKey="Wildstone.Arena.Settings.v1";
        // Version 2: Fov became horizontal degrees and the Aim binding was appended.
        // Version 3: D028 split the pipelines (levels 0-2 Low, 3-5 High), so the old Medium default moves to High.
        public const int CurrentVersion=3, DefaultQuality=3;
        public const float DefaultFov=100, MinimumFov=80, MaximumFov=120;
        public int Version;
        public float Sensitivity=.12f, Fov=DefaultFov, MasterVolume=.8f, MusicVolume=.4f, EffectsVolume=.8f, UiScale=1, AdsSensitivity=1;
        public int Width=1920, Height=1080, FpsCap=144, Quality=DefaultQuality;
        public bool Fullscreen=true, VSync, ReducedMotion, AdsToggle;
        public string[] Bindings=(string[])DefaultBindings.Clone();
        public static readonly string[] ActionNames={"Forward","Back","Left","Right","Jump","Dash","Slide","Fire","Reload","Starter","Special","Grenade","Blink","Shield","Pickup","Aim"};
        public static readonly string[] DefaultBindings={"<Keyboard>/w","<Keyboard>/s","<Keyboard>/a","<Keyboard>/d","<Keyboard>/space","<Keyboard>/leftShift","<Keyboard>/leftCtrl","<Mouse>/leftButton","<Keyboard>/r","<Keyboard>/1","<Keyboard>/2","<Keyboard>/g","<Keyboard>/q","<Keyboard>/e","<Keyboard>/f","<Mouse>/rightButton"};
        static float Clamp(float value,float low,float high,float fallback) => float.IsNaN(value)||float.IsInfinity(value)?fallback:Math.Max(low,Math.Min(high,value));
        public void Normalize(int qualityCount=int.MaxValue)
        {
            Sensitivity=Clamp(Sensitivity,.01f,1,.12f); Fov=Clamp(Fov,MinimumFov,MaximumFov,DefaultFov); AdsSensitivity=Clamp(AdsSensitivity,.2f,2,1);
            MasterVolume=Clamp(MasterVolume,0,1,.8f); MusicVolume=Clamp(MusicVolume,0,1,.4f); EffectsVolume=Clamp(EffectsVolume,0,1,.8f);
            UiScale=Clamp(UiScale,.8f,1.4f,1); Width=Math.Max(1280,Math.Min(7680,Width)); Height=Math.Max(720,Math.Min(4320,Height));
            FpsCap=Math.Max(30,Math.Min(360,FpsCap)); Quality=Math.Max(0,Math.Min(Math.Max(0,qualityCount-1),Quality));
            if(Bindings==null || Bindings.Length!=DefaultBindings.Length) Bindings=(string[])DefaultBindings.Clone();
            for(int i=0;i<Bindings.Length;i++) if(!ValidBinding(Bindings[i])) Bindings[i]=DefaultBindings[i];
            Version=CurrentVersion;
        }
        // Upgrades a deserialized older payload before Normalize. Version 1 stored a vertical FOV, so it resets to the new default;
        // a saved Medium (the pre-D028 default) becomes High.
        public void Migrate()
        {
            if(Version>=CurrentVersion) return;
            if(Version<2)
            {
                Fov=DefaultFov;
                if(Bindings!=null && Bindings.Length==DefaultBindings.Length-1)
                { var upgraded=(string[])DefaultBindings.Clone(); Array.Copy(Bindings,upgraded,Bindings.Length); Bindings=upgraded; }
            }
            if(Quality==2) Quality=DefaultQuality;
            Version=CurrentVersion;
        }
        public static bool ValidBinding(string value)
        {
            if(string.IsNullOrEmpty(value) || value.Length>80) return false;
            if(value.StartsWith("<Mouse>/",StringComparison.Ordinal))
            { string control=value.Substring(8); return control=="leftButton" || control=="rightButton" || control=="middleButton" || control=="forwardButton" || control=="backButton"; }
            if(!value.StartsWith("<Keyboard>/",StringComparison.Ordinal)) return false;
            string key=value.Substring(11);
            if(key.Length==1 && key[0]>='0' && key[0]<='9') return true;
            return Enum.TryParse(key,true,out Key parsed) && Enum.IsDefined(typeof(Key),parsed) && parsed!=Key.None && parsed!=Key.Escape;
        }
        public LocalSettings Copy() { var copy=(LocalSettings)MemberwiseClone(); copy.Bindings=(string[])Bindings.Clone(); return copy; }
        public static LocalSettings Load()
        {
            LocalSettings result=null;
            try { result=JsonUtility.FromJson<LocalSettings>(PlayerPrefs.GetString(StorageKey,"")); }
            catch(ArgumentException) { }
            if(result!=null) result.Migrate();
            result=result??new LocalSettings(); result.Normalize(QualitySettings.names.Length); return result;
        }
        public void Save() { Normalize(QualitySettings.names.Length); PlayerPrefs.SetString(StorageKey,JsonUtility.ToJson(this)); PlayerPrefs.Save(); }
        public void Apply(Camera camera)
        {
            int qualityCount=QualitySettings.names.Length; Normalize(qualityCount); if(camera!=null) camera.fieldOfView=VerticalFov(Fov,camera.aspect);
            if(qualityCount>0) QualitySettings.SetQualityLevel(Quality,true);
            AudioListener.volume=Application.isBatchMode?0:MasterVolume; // headless test players stay silent
            QualitySettings.vSyncCount=VSync?1:0; Application.targetFrameRate=FpsCap;
            if(Screen.width!=Width || Screen.height!=Height || Screen.fullScreen!=Fullscreen) Screen.SetResolution(Width,Height,Fullscreen);
        }
        // Same result as Camera.HorizontalToVerticalFieldOfView (native parity test), usable outside the engine.
        public static float VerticalFov(float horizontal,float aspect)
        {
            if(float.IsNaN(aspect) || float.IsInfinity(aspect) || aspect<=0) aspect=16f/9;
            return 2*Mathf.Atan(Mathf.Tan(horizontal*.5f*Mathf.Deg2Rad)/aspect)*Mathf.Rad2Deg;
        }
        // Look multiplier while aiming: tan(adsVfov/2)/tan(hipVfov/2), then the ADS preference, blended by progress.
        public float LookScale(WeaponKind kind,float ads,float hipVertical)
        {
            ads=float.IsNaN(ads)?0:Math.Max(0,Math.Min(1,ads));
            float ratio=Mathf.Tan(WeaponCatalog.AdsVerticalFov(hipVertical,kind,ads)*.5f*Mathf.Deg2Rad)/Mathf.Tan(hipVertical*.5f*Mathf.Deg2Rad);
            return ratio*(1+(AdsSensitivity-1)*ads);
        }
        public static string BindingLabel(string value) => value?.Replace("<Keyboard>/","").Replace("<Mouse>/","Mouse ")??"Unbound";
    }
}
