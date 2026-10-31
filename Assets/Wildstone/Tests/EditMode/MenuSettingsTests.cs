using NUnit.Framework;
using Wildstone.Game;
using Wildstone.UI;

namespace Wildstone.Tests
{
    [Category("Managed")]
    public sealed class MenuSettingsTests
    {
        [TestCase(false)] [TestCase(true)]
        public void CancelledLoadBackdropCannotDismissNewSettings(bool failure)
        {
            var flow=new MenuFlow(); flow.BeginLoad(); flow.CancelLoad(); int background=flow.Generation;
            flow.Show(MenuScreen.Settings);
            if(failure) flow.FailLoad(background); else flow.CompleteLoad(background,false);
            Assert.That(flow.Screen,Is.EqualTo(MenuScreen.Settings)); Assert.That(flow.GameplayInput,Is.False);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void ActualProjectQualityIndexRemainsReachable(int selected)
        {
            var settings=new LocalSettings { Quality=selected }; settings.Normalize(); Assert.That(settings.Quality,Is.EqualTo(selected));
        }
        [TestCase(-1,6,0)] [TestCase(6,6,5)] [TestCase(5,3,2)] [TestCase(0,0,0)]
        public void QualityIndexClampsAgainstActualPresetCount(int selected,int count,int expected)
        {
            var settings=new LocalSettings { Quality=selected }; settings.Normalize(count); Assert.That(settings.Quality,Is.EqualTo(expected));
        }
        [TestCase(null,false)] [TestCase("",false)] [TestCase("    ",false)] [TestCase("abc",false)]
        [TestCase("ROOM-12",false)] [TestCase("four\nletters",false)] [TestCase("ABCDEFGHIJKLM",false)]
        [TestCase("aBc123",true)] [TestCase("  ROOM12  ",true)]
        public void JoinCodeRequiresBoundedLettersAndDigits(string value,bool expected) => Assert.That(MenuFlow.ValidJoinCode(value),Is.EqualTo(expected));

        [Test] public void CancelledLoadCannotCaptureInputOrOverwriteNewLoad()
        {
            var flow=new MenuFlow(); int old=flow.BeginLoad(); flow.CancelLoad(); int next=flow.BeginLoad();
            Assert.That(flow.CompleteLoad(old,true),Is.False); Assert.That(flow.FailLoad(old),Is.False);
            Assert.That(flow.Screen,Is.EqualTo(MenuScreen.Loading)); Assert.That(flow.CompleteLoad(next,true),Is.True);
            Assert.That(flow.GameplayInput,Is.True);
        }
        [Test] public void FocusPauseResultsAndLeaveReleaseGameplayInput()
        {
            var flow=new MenuFlow(); flow.CompleteLoad(flow.BeginLoad(),true); flow.LoseFocus();
            Assert.That(flow.Screen,Is.EqualTo(MenuScreen.Pause)); Assert.That(flow.GameplayInput,Is.False);
            flow.Show(MenuScreen.Settings); Assert.That(flow.GameplayInput,Is.False);
            flow.Show(MenuScreen.Playing); Assert.That(flow.GameplayInput,Is.True);
            flow.Show(MenuScreen.Results); Assert.That(flow.GameplayInput,Is.False);
            int retry=flow.BeginLoad(); Assert.That(flow.CompleteLoad(retry,true),Is.True);
            flow.Show(MenuScreen.Title); Assert.That(flow.GameplayInput,Is.False);
        }
        [Test] public void SettingsClampNonfiniteAndBoundedValues()
        {
            var settings=new LocalSettings { Sensitivity=float.NaN,Fov=float.PositiveInfinity,MasterVolume=-1,MusicVolume=2,EffectsVolume=float.NegativeInfinity,UiScale=100,Width=-50,Height=99999,FpsCap=0,Quality=9,Bindings=null };
            settings.Normalize(6);
            Assert.That(settings.Sensitivity,Is.EqualTo(.12f)); Assert.That(settings.Fov,Is.EqualTo(LocalSettings.DefaultFov));
            Assert.That(settings.MasterVolume,Is.Zero); Assert.That(settings.MusicVolume,Is.EqualTo(1)); Assert.That(settings.EffectsVolume,Is.EqualTo(.8f));
            Assert.That(settings.UiScale,Is.EqualTo(1.4f)); Assert.That(settings.Width,Is.EqualTo(1280)); Assert.That(settings.Height,Is.EqualTo(4320));
            Assert.That(settings.FpsCap,Is.EqualTo(30)); Assert.That(settings.Quality,Is.EqualTo(5));
            Assert.That(settings.Bindings,Is.EqualTo(LocalSettings.DefaultBindings));
        }
        [Test] public void SettingsDraftDoesNotMutateAppliedBindings()
        {
            var original=new LocalSettings(); var draft=original.Copy(); draft.Bindings[0]="<Keyboard>/upArrow"; draft.Fov=110;
            Assert.That(original.Bindings[0],Is.EqualTo("<Keyboard>/w")); Assert.That(original.Fov,Is.EqualTo(LocalSettings.DefaultFov));
        }
        [TestCase("<Keyboard>/w",true)] [TestCase("<Keyboard>/1",true)] [TestCase("<Keyboard>/leftCtrl",true)]
        [TestCase("<Mouse>/leftButton",true)] [TestCase("<Mouse>/rightButton",true)] [TestCase("<Keyboard>/escape",false)] [TestCase("<Keyboard>/*",false)]
        [TestCase("<Keyboard>/notAKey",false)] [TestCase("<Mouse>/delta",false)] [TestCase(null,false)]
        public void BindingValidationKeepsPauseAvailable(string binding,bool expected) => Assert.That(LocalSettings.ValidBinding(binding),Is.EqualTo(expected));
        [Test] public void LegacySettingsMigrateToHorizontalFovAndGainAimBinding()
        {
            var legacy=new LocalSettings { Version=0,Fov=70,AdsSensitivity=1,Bindings=new string[LocalSettings.DefaultBindings.Length-1] };
            for(int i=0;i<legacy.Bindings.Length;i++) legacy.Bindings[i]=LocalSettings.DefaultBindings[i];
            legacy.Bindings[0]="<Keyboard>/upArrow";
            legacy.Migrate(); legacy.Normalize();
            Assert.That(legacy.Version,Is.EqualTo(LocalSettings.CurrentVersion));
            Assert.That(legacy.Fov,Is.EqualTo(LocalSettings.DefaultFov),"A version-1 vertical FOV is not reinterpreted as horizontal.");
            Assert.That(legacy.Bindings.Length,Is.EqualTo(LocalSettings.ActionNames.Length));
            Assert.That(legacy.Bindings[0],Is.EqualTo("<Keyboard>/upArrow"),"Custom bindings survive migration.");
            Assert.That(legacy.Bindings[LocalSettings.ActionNames.Length-1],Is.EqualTo("<Mouse>/rightButton"));
            Assert.That(LocalSettings.ActionNames[LocalSettings.ActionNames.Length-1],Is.EqualTo("Aim"));
            var current=new LocalSettings { Version=LocalSettings.CurrentVersion,Fov=112 }; current.Migrate(); current.Normalize();
            Assert.That(current.Fov,Is.EqualTo(112),"Current payloads keep their horizontal FOV.");
            var medium=new LocalSettings { Version=2,Fov=112,Quality=2 }; medium.Migrate(); medium.Normalize();
            Assert.That(medium.Quality,Is.EqualTo(LocalSettings.DefaultQuality),"The pre-D028 Medium default moves to the High pipeline.");
            Assert.That(medium.Fov,Is.EqualTo(112),"A version-2 payload keeps its horizontal FOV.");
            var chosen=new LocalSettings { Version=2,Quality=0 }; chosen.Migrate(); chosen.Normalize();
            Assert.That(chosen.Quality,Is.Zero,"A deliberately chosen tier survives.");
            Assert.That(UnityEngine.QualitySettings.names[LocalSettings.DefaultQuality],Is.EqualTo("High"));
            var low=new LocalSettings { Fov=40,AdsSensitivity=9 }; low.Normalize();
            Assert.That(low.Fov,Is.EqualTo(LocalSettings.MinimumFov)); Assert.That(low.AdsSensitivity,Is.EqualTo(2));
        }
        [TestCase(90,16f/9,58.7155f)] [TestCase(90,4f/3,73.7398f)] [TestCase(90,64f/27,45.7499f)] [TestCase(100,16f/9,67.6727f)]
        public void HorizontalFovConvertsPerAspect(float horizontal,float aspect,float vertical)
        {
            Assert.That(LocalSettings.VerticalFov(horizontal,aspect),Is.EqualTo(vertical).Within(.01f));
            Assert.That(LocalSettings.VerticalFov(horizontal,float.NaN),Is.EqualTo(LocalSettings.VerticalFov(horizontal,16f/9)));
        }
        [Test] public void AdsLookScaleMatchesZoomThenAppliesAdsPreference()
        {
            var settings=new LocalSettings(); float hip=LocalSettings.VerticalFov(100,16f/9);
            Assert.That(settings.LookScale(Wildstone.Combat.WeaponKind.LaserSniper,0,hip),Is.EqualTo(1).Within(1e-5f));
            Assert.That(settings.LookScale(Wildstone.Combat.WeaponKind.LaserSniper,1,hip),Is.EqualTo(1/Wildstone.Combat.WeaponCatalog.AdsZoom(Wildstone.Combat.WeaponKind.LaserSniper)).Within(1e-4f));
            settings.AdsSensitivity=.5f;
            Assert.That(settings.LookScale(Wildstone.Combat.WeaponKind.LaserSniper,1,hip),Is.EqualTo(.5f/4).Within(1e-4f));
            Assert.That(settings.LookScale(Wildstone.Combat.WeaponKind.LaserSniper,float.NaN,hip),Is.EqualTo(1).Within(1e-5f));
        }
        [Test] public void InvalidBindingsRestoreOnlyAffectedDefault()
        {
            var settings=new LocalSettings(); settings.Bindings[0]="<Keyboard>/upArrow";settings.Bindings[1]="<Keyboard>/escape"; settings.Normalize();
            Assert.That(settings.Bindings[0],Is.EqualTo("<Keyboard>/upArrow")); Assert.That(settings.Bindings[1],Is.EqualTo(LocalSettings.DefaultBindings[1]));
        }
    }
}
