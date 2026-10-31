using NUnit.Framework;
using UnityEngine;
using Wildstone.UI;
namespace Wildstone.Tests
{
    public sealed class LocalSettingsPersistenceTests
    {
        [TestCase(false)] [TestCase(true)]
        public void ApplyingEveryActualQualityPresetPreservesIndependentVSync(bool enabled)
        {
            int quality=QualitySettings.GetQualityLevel(),vsync=QualitySettings.vSyncCount,fps=Application.targetFrameRate;
            int width=Screen.width,height=Screen.height; bool fullscreen=Screen.fullScreen; float volume=AudioListener.volume;
            try
            {
                var names=QualitySettings.names;
                for(int i=0;i<names.Length;i++)
                {
                    var settings=new LocalSettings { Quality=i,VSync=enabled,Width=width,Height=height,Fullscreen=fullscreen };
                    settings.Apply(null);
                    Assert.That(QualitySettings.GetQualityLevel(),Is.EqualTo(i),names[i]);
                    Assert.That(QualitySettings.vSyncCount,Is.EqualTo(enabled?1:0),names[i]);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality,true); QualitySettings.vSyncCount=vsync; Application.targetFrameRate=fps; AudioListener.volume=volume;
                if(Screen.width!=width||Screen.height!=height||Screen.fullScreen!=fullscreen) Screen.SetResolution(width,height,fullscreen);
            }
        }
        [Test] public void ManagedFovConversionMatchesUnityCamera()
        {
            foreach(float aspect in new[]{16f/9,64f/27,4f/3,1f})
                foreach(float horizontal in new[]{LocalSettings.MinimumFov,LocalSettings.DefaultFov,LocalSettings.MaximumFov})
                    Assert.That(LocalSettings.VerticalFov(horizontal,aspect),Is.EqualTo(Camera.HorizontalToVerticalFieldOfView(horizontal,aspect)).Within(.001f),horizontal+" @ "+aspect);
        }
        [Test] public void SettingsRoundTripAndMalformedDataRecovery()
        {
            bool existed=PlayerPrefs.HasKey(LocalSettings.StorageKey); string previous=PlayerPrefs.GetString(LocalSettings.StorageKey);
            try
            {
                var settings=new LocalSettings { Fov=102,ReducedMotion=true,MasterVolume=.25f }; settings.Bindings[0]="<Keyboard>/upArrow"; settings.Save();
                var restored=LocalSettings.Load(); Assert.That(restored.Fov,Is.EqualTo(102)); Assert.That(restored.ReducedMotion,Is.True);
                Assert.That(restored.MasterVolume,Is.EqualTo(.25f)); Assert.That(restored.Bindings[0],Is.EqualTo("<Keyboard>/upArrow"));
                PlayerPrefs.SetString(LocalSettings.StorageKey,"not json"); Assert.That(LocalSettings.Load().Fov,Is.EqualTo(LocalSettings.DefaultFov));
                // A version-1 payload (vertical FOV, 15 bindings, no Version field) migrates on load.
                var legacy=new LocalSettings { Version=LocalSettings.CurrentVersion }; var json=JsonUtility.ToJson(legacy).Replace("\"Version\":"+LocalSettings.CurrentVersion+",","")
                    .Replace(",\"<Mouse>/rightButton\"]","]").Replace("\"Fov\":"+LocalSettings.DefaultFov,"\"Fov\":70");
                Assert.That(json,Does.Not.Contain("rightButton")); Assert.That(json,Does.Not.Contain("\"Version\""));
                PlayerPrefs.SetString(LocalSettings.StorageKey,json); var migrated=LocalSettings.Load();
                Assert.That(migrated.Version,Is.EqualTo(LocalSettings.CurrentVersion)); Assert.That(migrated.Fov,Is.EqualTo(LocalSettings.DefaultFov));
                Assert.That(migrated.Bindings[LocalSettings.ActionNames.Length-1],Is.EqualTo("<Mouse>/rightButton"));
            }
            finally { if(existed) PlayerPrefs.SetString(LocalSettings.StorageKey,previous); else PlayerPrefs.DeleteKey(LocalSettings.StorageKey); PlayerPrefs.Save(); }
        }
    }
}
