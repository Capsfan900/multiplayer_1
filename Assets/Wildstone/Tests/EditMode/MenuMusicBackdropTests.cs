using NUnit.Framework;
using UnityEngine;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Presentation;
using Wildstone.UI;

namespace Wildstone.Tests
{
    [Category("Managed")]
    public sealed class MenuMusicBackdropTests
    {
        static void Run(MusicMix mix,float seconds,bool ducked=false) { for(float t=0;t<seconds-1e-4f;t+=.05f) mix.Step(.05f,ducked); }

        [Test] public void MenusShareTheThemeAndMatchesPlayTheirMapLoopUntilResults()
        {
            Assert.That(MusicMix.TrackFor(MenuScreen.Title,false,"atrium"),Is.EqualTo("MenuTheme"));
            Assert.That(MusicMix.TrackFor(MenuScreen.Loading,false,"atrium"),Is.EqualTo("MenuTheme"));
            Assert.That(MusicMix.TrackFor(MenuScreen.Online,false,"split-gallery"),Is.EqualTo("MenuTheme"));
            Assert.That(MusicMix.TrackFor(MenuScreen.Results,true,"atrium"),Is.EqualTo("MenuTheme"));
            Assert.That(MusicMix.TrackFor(MenuScreen.Playing,true,""),Is.EqualTo("MenuTheme"));
            // Resource names are the music lane's contract; one per shipped map id.
            var expected=new[]{"Level_atrium","Level_split-gallery","Level_terrace-steps"};
            var maps=ArenaLayouts.CreateAll();
            Assert.That(maps.Length,Is.EqualTo(expected.Length));
            for(int i=0;i<maps.Length;i++)
            {
                Assert.That(MusicMix.TrackFor(MenuScreen.Playing,true,maps[i].Id),Is.EqualTo(expected[i]));
                // Pause and settings over the match keep the map loop, only ducked.
                Assert.That(MusicMix.TrackFor(MenuScreen.Pause,true,maps[i].Id),Is.EqualTo(expected[i]));
                Assert.That(MusicMix.TrackFor(MenuScreen.Settings,true,maps[i].Id),Is.EqualTo(expected[i]));
            }
            Assert.That(MusicMix.Ducked(MenuScreen.Pause,true),Is.True); Assert.That(MusicMix.Ducked(MenuScreen.Settings,true),Is.True);
            Assert.That(MusicMix.Ducked(MenuScreen.Playing,true),Is.False); Assert.That(MusicMix.Ducked(MenuScreen.Results,true),Is.False);
            Assert.That(MusicMix.Ducked(MenuScreen.Settings,false),Is.False);
        }
        [Test] public void TrackChangeCrossfadesOverOneAndAHalfSecondsAtConstantPower()
        {
            var mix=new MusicMix();
            int menu=mix.Select("MenuTheme"); Assert.That(menu,Is.GreaterThanOrEqualTo(0));
            Assert.That(mix.Volume(menu,1),Is.Zero,"A new track starts from silence.");
            Run(mix,MusicMix.FadeSeconds); Assert.That(mix.Gains[menu],Is.EqualTo(1).Within(1e-4f));
            Assert.That(mix.Select("MenuTheme"),Is.EqualTo(-1),"Same track never restarts across menu screens.");
            int level=mix.Select("Level_atrium"); Assert.That(level,Is.EqualTo(1-menu));
            Run(mix,MusicMix.FadeSeconds*.5f);
            float a=mix.Volume(menu,1),b=mix.Volume(level,1);
            Assert.That(a,Is.EqualTo(b).Within(1e-3f)); Assert.That(a*a+b*b,Is.EqualTo(1).Within(1e-3f),"Equal-power midpoint.");
            Run(mix,MusicMix.FadeSeconds*.5f);
            Assert.That(mix.Gains[menu],Is.Zero.Within(1e-4f)); Assert.That(mix.Gains[level],Is.EqualTo(1).Within(1e-4f));
            Assert.That(mix.Volume(level,.4f),Is.EqualTo(.4f).Within(1e-4f),"Settled level equals the music setting.");
        }
        [Test] public void ReversingMidFadeResumesTheFadingVoiceWithoutRestart()
        {
            var mix=new MusicMix(); int menu=mix.Select("MenuTheme"); Run(mix,2);
            mix.Select("Level_atrium"); Run(mix,.5f);
            float partial=mix.Gains[menu];
            Assert.That(partial,Is.InRange(.6f,.75f));
            Assert.That(mix.Select("MenuTheme"),Is.EqualTo(-1)); Assert.That(mix.Active,Is.EqualTo(menu));
            mix.Step(.05f,false); Assert.That(mix.Gains[menu],Is.GreaterThan(partial),"The theme rises from where it was.");
        }
        [Test] public void PauseDucksAndResumeRestoresWithoutTouchingTheCrossfade()
        {
            var mix=new MusicMix(); int voice=mix.Select("Level_atrium"); Run(mix,2);
            Run(mix,MusicMix.DuckSeconds,true);
            Assert.That(mix.Duck,Is.EqualTo(MusicMix.DuckGain).Within(1e-4f)); Assert.That(mix.Gains[voice],Is.EqualTo(1));
            Assert.That(mix.Volume(voice,.8f),Is.EqualTo(.8f*MusicMix.DuckGain).Within(1e-4f));
            Run(mix,MusicMix.DuckSeconds); Assert.That(mix.Duck,Is.EqualTo(1).Within(1e-4f));
            mix.Step(float.NaN,true); mix.Step(-3,true); Assert.That(mix.Duck,Is.EqualTo(1),"Invalid frame deltas change nothing.");
        }
        [Test] public void ReducedMotionBackdropIsAStillAndNormalDriftIsSlowAndBounded()
        {
            MenuMotion.Drift(0,true,out float arc0,out var offset0,out float yaw0,out float pitch0);
            foreach(float t in new[]{3.5f,47f,612f,float.NaN})
            {
                MenuMotion.Drift(t,true,out float arc,out var offset,out float yaw,out float pitch);
                Assert.That(arc,Is.EqualTo(arc0)); Assert.That(offset,Is.EqualTo(offset0)); Assert.That(yaw,Is.EqualTo(yaw0)); Assert.That(pitch,Is.EqualTo(pitch0));
            }
            const float step=.1f;
            MenuMotion.Drift(0,false,out float lastArc,out var lastOffset,out _,out _);
            bool moved=false;
            for(float t=step;t<400;t+=step)
            {
                MenuMotion.Drift(t,false,out float arc,out var offset,out float yaw,out float pitch);
                Assert.That(Mathf.Abs(arc),Is.LessThanOrEqualTo(MenuMotion.ArcDegrees+1e-3f));
                Assert.That(Mathf.Abs(offset.x),Is.LessThanOrEqualTo(MenuMotion.DriftAmplitude.x+1e-4f));
                Assert.That(Mathf.Abs(offset.y),Is.LessThanOrEqualTo(MenuMotion.DriftAmplitude.y+1e-4f));
                Assert.That(Mathf.Abs(yaw),Is.LessThanOrEqualTo(MenuMotion.DriftYaw+1e-4f)); Assert.That(Mathf.Abs(pitch),Is.LessThanOrEqualTo(MenuMotion.DriftPitch+1e-4f));
                // Cinematic, not travel: under 2.5 deg/s of orbit and 0.15 m/s of breathing.
                Assert.That(Mathf.Abs(arc-lastArc)/step,Is.LessThan(2.5f),"t="+t);
                Assert.That((offset-lastOffset).magnitude/step,Is.LessThan(.15f),"t="+t);
                moved|=Mathf.Abs(arc-arc0)>1;
                lastArc=arc; lastOffset=offset;
            }
            Assert.That(moved,Is.True,"Normal motion actually drifts.");
        }
        [Test] public void DiveLeavesFromRestWidensTheLensAndScrimHoldsUnderTheColumn()
        {
            Assert.That(MenuMotion.DiveEase(0),Is.Zero); Assert.That(MenuMotion.DiveEase(1),Is.EqualTo(1).Within(1e-5f));
            Assert.That(MenuMotion.DiveEase(.02f),Is.LessThan(.001f),"Starts from rest like the drift left it.");
            Assert.That(MenuMotion.DiveEase(2),Is.EqualTo(1).Within(1e-5f)); Assert.That(MenuMotion.DiveEase(-1),Is.Zero);
            for(float k=0;k<1;k+=.05f) Assert.That(MenuMotion.DiveEase(k+.05f),Is.GreaterThanOrEqualTo(MenuMotion.DiveEase(k)));
            Assert.That(MenuMotion.Widen(MenuMotion.Fov,0),Is.EqualTo(MenuMotion.Fov).Within(1e-3f));
            Assert.That(MenuMotion.Widen(MenuMotion.Fov,MenuMotion.WidenGain),Is.InRange(MenuMotion.Fov+5,MenuMotion.Fov+20));
            for(float u=0;u<=MenuMotion.RampFlat;u+=.02f) Assert.That(MenuMotion.RampAlpha(u),Is.EqualTo(1),"Column text keeps its full scrim.");
            for(float u=0;u<1;u+=.01f) Assert.That(MenuMotion.RampAlpha(u+.01f),Is.LessThanOrEqualTo(MenuMotion.RampAlpha(u)+1e-6f));
            Assert.That(MenuMotion.RampAlpha(1),Is.Zero.Within(1e-6f));
        }
        [Test] public void MusicVolumeNeedsNoSchemaChangeAndSurvivesMigration()
        {
            var current=new LocalSettings { Version=LocalSettings.CurrentVersion,MusicVolume=.15f }; current.Migrate(); current.Normalize();
            Assert.That(current.MusicVolume,Is.EqualTo(.15f));
            var legacy=new LocalSettings { Version=0,MusicVolume=.7f }; legacy.Migrate(); legacy.Normalize();
            Assert.That(legacy.MusicVolume,Is.EqualTo(.7f),"Version-1 payloads already stored MusicVolume.");
            Assert.That(new LocalSettings().Copy().MusicVolume,Is.EqualTo(.4f));
        }
    }
}
