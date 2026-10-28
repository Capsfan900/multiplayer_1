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
            Assert.That(settings.Sensitivity,Is.EqualTo(.12f)); Assert.That(settings.Fov,Is.EqualTo(90));
            Assert.That(settings.MasterVolume,Is.Zero); Assert.That(settings.MusicVolume,Is.EqualTo(1)); Assert.That(settings.EffectsVolume,Is.EqualTo(.8f));
            Assert.That(settings.UiScale,Is.EqualTo(1.4f)); Assert.That(settings.Width,Is.EqualTo(1280)); Assert.That(settings.Height,Is.EqualTo(4320));
            Assert.That(settings.FpsCap,Is.EqualTo(30)); Assert.That(settings.Quality,Is.EqualTo(5));
            Assert.That(settings.Bindings,Is.EqualTo(LocalSettings.DefaultBindings));
        }
        [Test] public void SettingsDraftDoesNotMutateAppliedBindings()
        {
            var original=new LocalSettings(); var draft=original.Copy(); draft.Bindings[0]="<Keyboard>/upArrow"; draft.Fov=110;
            Assert.That(original.Bindings[0],Is.EqualTo("<Keyboard>/w")); Assert.That(original.Fov,Is.EqualTo(90));
        }
        [TestCase("<Keyboard>/w",true)] [TestCase("<Keyboard>/1",true)] [TestCase("<Keyboard>/leftCtrl",true)]
        [TestCase("<Mouse>/leftButton",true)] [TestCase("<Keyboard>/escape",false)] [TestCase("<Keyboard>/*",false)]
        [TestCase("<Keyboard>/notAKey",false)] [TestCase("<Mouse>/delta",false)] [TestCase(null,false)]
        public void BindingValidationKeepsPauseAvailable(string binding,bool expected) => Assert.That(LocalSettings.ValidBinding(binding),Is.EqualTo(expected));
        [Test] public void InvalidBindingsRestoreOnlyAffectedDefault()
        {
            var settings=new LocalSettings(); settings.Bindings[0]="<Keyboard>/upArrow";settings.Bindings[1]="<Keyboard>/escape"; settings.Normalize();
            Assert.That(settings.Bindings[0],Is.EqualTo("<Keyboard>/upArrow")); Assert.That(settings.Bindings[1],Is.EqualTo(LocalSettings.DefaultBindings[1]));
        }
    }
}
