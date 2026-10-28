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
        [Test] public void SettingsRoundTripAndMalformedDataRecovery()
        {
            bool existed=PlayerPrefs.HasKey(LocalSettings.StorageKey); string previous=PlayerPrefs.GetString(LocalSettings.StorageKey);
            try
            {
                var settings=new LocalSettings { Fov=102,ReducedMotion=true,MasterVolume=.25f }; settings.Bindings[0]="<Keyboard>/upArrow"; settings.Save();
                var restored=LocalSettings.Load(); Assert.That(restored.Fov,Is.EqualTo(102)); Assert.That(restored.ReducedMotion,Is.True);
                Assert.That(restored.MasterVolume,Is.EqualTo(.25f)); Assert.That(restored.Bindings[0],Is.EqualTo("<Keyboard>/upArrow"));
                PlayerPrefs.SetString(LocalSettings.StorageKey,"not json"); Assert.That(LocalSettings.Load().Fov,Is.EqualTo(90));
            }
            finally { if(existed) PlayerPrefs.SetString(LocalSettings.StorageKey,previous); else PlayerPrefs.DeleteKey(LocalSettings.StorageKey); PlayerPrefs.Save(); }
        }
    }
}
