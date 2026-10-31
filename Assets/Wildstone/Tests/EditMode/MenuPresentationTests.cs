using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.UI;

namespace Wildstone.Tests
{
    public sealed class MenuPresentationTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [Test] public void PauseSettingsAndResumeReuseHudUntilTheActualMatchChanges()
        {
            var owner=new GameObject("HUD lifecycle"); owner.SetActive(false);
            try
            {
                var menu=owner.AddComponent<ArenaMenu>(); var session=owner.GetComponent<PracticeSession>();
                typeof(ArenaMenu).GetField("session",Private).SetValue(menu,session);
                var root=new VisualElement(); typeof(ArenaMenu).GetField("root",Private).SetValue(menu,root);
                typeof(PracticeSession).GetField("<Settings>k__BackingField",Private).SetValue(session,new LocalSettings());
                typeof(PracticeSession).GetField("<Match>k__BackingField",Private).SetValue(session,new ArenaMatch(ArenaLayouts.CreateAll()[0],120,true));
                void Render(MenuScreen screen) { session.Flow.Show(screen); typeof(ArenaMenu).GetMethod("Render",Private).Invoke(menu,null); }
                Render(MenuScreen.Playing); var first=typeof(ArenaMenu).GetField("hud",Private).GetValue(menu);
                Render(MenuScreen.Pause); Assert.That(typeof(ArenaMenu).GetField("hud",Private).GetValue(menu),Is.SameAs(first));
                // The menu column never shows a horizontal scroller (it drew a pale bar along the bottom).
                Assert.That(root.Q<ScrollView>(className:"menu-scroll").horizontalScrollerVisibility,Is.EqualTo(ScrollerVisibility.Hidden));
                Render(MenuScreen.Settings); Assert.That(typeof(ArenaMenu).GetField("hud",Private).GetValue(menu),Is.SameAs(first));
                Render(MenuScreen.Playing); Assert.That(typeof(ArenaMenu).GetField("hud",Private).GetValue(menu),Is.SameAs(first));
                typeof(PracticeSession).GetField("<Match>k__BackingField",Private).SetValue(session,new ArenaMatch(ArenaLayouts.CreateAll()[0],120,true));
                Render(MenuScreen.Playing); Assert.That(typeof(ArenaMenu).GetField("hud",Private).GetValue(menu),Is.Not.SameAs(first));
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void SameScreenFocusRestoresTheLogicalFieldInsteadOfItsInternalTextInput()
        {
            var card=new VisualElement(); var field=new TextField {name="menu-Join code"}; card.Add(field);
            var inner=new VisualElement(); field.Add(inner);
            var key=(string)typeof(ArenaMenu).GetMethod("FocusKey",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{inner,card});
            Assert.That(key,Is.EqualTo(field.name));
            var first=new Button {name="menu-Host"}; card.Insert(0,first);
            var target=typeof(ArenaMenu).GetMethod("FocusTarget",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{card,MenuScreen.Online,key});
            Assert.That(target,Is.SameAs(field));
        }
        [Test] public void SettingsStartAtSensitivityAndHudHasNoInitialHitFlash()
        {
            var card=new VisualElement(); var sensitivity=new Slider(); card.Add(sensitivity); card.Add(new Button());
            var target=typeof(ArenaMenu).GetMethod("FocusTarget",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{card,MenuScreen.Settings,null});
            Assert.That(target,Is.SameAs(sensitivity));
            var hud=new ArenaHud();
            Assert.That(hud.Root.Q<Label>("hud-hit-cue").style.display.value,Is.EqualTo(DisplayStyle.None));
        }
        // Ivory reticle parts keep a dark rim (and the hitmarker a dark outline) so they read over ivory stone.
        [Test] public void ReticleAndHitmarkerKeepADarkEdge()
        {
            var hud=new ArenaHud();
            var parts=hud.Root.Q("hud-crosshair").Children();
            Assert.That(parts,Is.Not.Empty);
            foreach(var part in parts)
            {
                Assert.That(part.style.borderLeftWidth.value,Is.EqualTo(1));
                var rim=part.style.borderLeftColor.value; var core=part.style.backgroundColor.value;
                Assert.That(rim.grayscale,Is.LessThan(.1f)); Assert.That(rim.a,Is.GreaterThan(.5f));
                Assert.That(core.grayscale,Is.GreaterThan(.85f));
                Assert.That(Mathf.Min(part.style.width.value.value,part.style.height.value.value),Is.EqualTo(4),"A 2 px core inside the rim.");
            }
            var cue=hud.Root.Q<Label>("hud-hit-cue");
            Assert.That(cue.style.unityTextOutlineWidth.value,Is.GreaterThan(0));
            Assert.That(cue.style.unityTextOutlineColor.value.grayscale,Is.LessThan(.1f));
        }
        // Taking damage draws an arc far outside the reticle and the 48 px hitmarker, never a mark beside them.
        [Test] public void DamageDirectionIsAFarArcNotAReticleMark()
        {
            var hud=new ArenaHud();
            Assert.That(hud.Root.Q("hud-damage-direction").childCount,Is.Zero);
            Assert.That(ArenaHud.DamageArcRadius(1080),Is.EqualTo(378).Within(.01f));
            Assert.That(ArenaHud.DamageArcRadius(float.NaN),Is.EqualTo(60),"An unlaid-out panel still draws clear of the hitmarker.");
            Assert.That(ArenaHud.DamageArcRadius(240),Is.EqualTo(84).Within(.01f));
        }
        // Blink names its key and effect at the pickup; other items keep the plain prompt.
        [Test] public void BlinkPickupPromptSaysWhatItDoes()
        {
            Assert.That(ArenaHud.PickupPrompt("blink","<Keyboard>/e","<Keyboard>/q"),Is.EqualTo("E   PICK UP BLINK · Q TO TELEPORT 6 m"));
            Assert.That(ArenaHud.PickupPrompt("laser-sniper","<Keyboard>/e","<Keyboard>/q"),Is.EqualTo("E   PICK UP LASER SNIPER"));
        }
        // The zone waypoint sits over the zone on screen and slides to the edge toward it when off screen or behind.
        [Test] public void ZoneWaypointStaysOnScreenAndPointsTheWayToTurn()
        {
            Assert.That(ArenaHud.ZoneMarkerPosition(new Vector3(.25f,.75f,10),1920,1080,28),Is.EqualTo(new Vector2(480,270)));
            Assert.That(ArenaHud.ZoneMarkerPosition(new Vector3(1.5f,.5f,10),1920,1080,28),Is.EqualTo(new Vector2(1892,540)),"Off to the right.");
            Assert.That(ArenaHud.ZoneMarkerPosition(new Vector3(.5f,.5f,-5),1920,1080,28),Is.EqualTo(new Vector2(960,1052)),"Straight behind: bottom edge.");
            Assert.That(ArenaHud.ZoneMarkerPosition(new Vector3(.8f,.5f,-5),1920,1080,28),Is.EqualTo(new Vector2(28,540)),"Behind on the left: left edge.");
            Assert.That(ArenaHud.ZoneMarkerPosition(new Vector3(.8f,.3f,-5),1920,1080,28),Is.EqualTo(new Vector2(28,540)),"A pitched view does not lift a point behind to the top.");
            Assert.That(ArenaHud.ZoneMarkerPosition(new Vector3(.1f,.5f,-5),1920,1080,28),Is.EqualTo(new Vector2(1892,540)),"Behind on the right: right edge.");
            var chip=new ArenaHud().Root.Q<Label>("hud-zone-marker").style.backgroundColor.value;
            Assert.That(chip.grayscale,Is.LessThan(.15f)); Assert.That(chip.a,Is.GreaterThan(.5f),"Coloured text alone is muddy on ivory stone.");
            Assert.That(ArenaHud.ZoneIdle.grayscale,Is.LessThan(.5f)); Assert.That(ArenaHud.ZoneIdle.maxColorComponent-Mathf.Min(ArenaHud.ZoneIdle.r,Mathf.Min(ArenaHud.ZoneIdle.g,ArenaHud.ZoneIdle.b)),Is.GreaterThan(.6f),"Saturated, not stone grey.");
        }
        // Body and shield hits tick: shorter and quieter than the head ding, so a head hit still stands out.
        [Test] public void BodyHitTickIsShorterAndQuieterThanTheHeadConfirm()
        {
            var tick=Wildstone.Presentation.ArenaAudio.MakeTick(); var head=Wildstone.Presentation.ArenaAudio.MakeConfirm(false);
            try
            {
                Assert.That(tick.length,Is.LessThanOrEqualTo(.06f)); Assert.That(tick.length,Is.LessThan(head.length/2));
                var data=new float[tick.samples]; tick.GetData(data,0); float peak=0; foreach(var v in data) peak=Mathf.Max(peak,Mathf.Abs(v));
                Assert.That(peak,Is.EqualTo(.85f).Within(.001f),"Normalised like the confirmations.");
                Assert.That(Mathf.Abs(data[data.Length-1]),Is.LessThan(.02f),"It decays without a click.");
                Assert.That(ArenaHud.TickScale,Is.LessThan(.6f));
            }
            finally { Object.DestroyImmediate(tick); Object.DestroyImmediate(head); }
        }
        // A shotgun reports a hit per pellet: one sound per frame, a stronger marker in that frame still sounds, and the
        // next shot sounds again.
        [Test] public void BodyMarkerSizeFollowsDamageAndTheBloomRingWaitsForAds()
        {
            Assert.That(ArenaHud.MarkerSize(5),Is.EqualTo(24)); Assert.That(ArenaHud.MarkerSize(10),Is.EqualTo(24));
            Assert.That(ArenaHud.MarkerSize(21),Is.EqualTo(28).Within(.01f)); Assert.That(ArenaHud.MarkerSize(32),Is.EqualTo(32)); Assert.That(ArenaHud.MarkerSize(80),Is.EqualTo(32));
            var hud=new ArenaHud(); var mark=typeof(ArenaHud).GetMethod("Mark",Private); var cue=UnityEngine.UIElements.UQueryExtensions.Q<Label>(hud.Root,"hud-hit-cue");
            mark.Invoke(hud,new object[]{ArenaHud.Marker.Body,5f,13f}); Assert.That(cue.style.fontSize.value.value,Is.EqualTo(ArenaHud.MarkerSize(13)).Within(.01f));
            mark.Invoke(hud,new object[]{ArenaHud.Marker.Head,6f,13f}); Assert.That(cue.style.fontSize.value.value,Is.EqualTo(34),"Head and kill sizes are unchanged.");
            mark.Invoke(hud,new object[]{ArenaHud.Marker.Kill,7f,80f}); Assert.That(cue.style.fontSize.value.value,Is.EqualTo(40));
            foreach(WeaponKind kind in System.Enum.GetValues(typeof(WeaponKind)))
            {
                Assert.That(ArenaHud.BloomRingVisible(kind,1,.5f,true),Is.EqualTo(!WeaponCatalog.HipCrosshairVisible(kind,1)),kind+" rings only while ADS hides the hip crosshair.");
                Assert.That(ArenaHud.BloomRingVisible(kind,0,.5f,true),Is.False,kind+" hip fire keeps the ticks.");
            }
            Assert.That(ArenaHud.BloomRingVisible(WeaponKind.LineDriver,1,.04f,true),Is.False,"A closed bloom shows no ring.");
            Assert.That(ArenaHud.BloomRingVisible(WeaponKind.LineDriver,1,.5f,false),Is.False);
        }
        [Test] public void PelletHitsInOneFrameSoundOnce()
        {
            var hud=new ArenaHud(); var voice=new GameObject("HUD feedback test",typeof(AudioSource));
            try
            {
                hud.Feedback=voice.GetComponent<AudioSource>();
                var mark=typeof(ArenaHud).GetMethod("Mark",Private);
                for(int pellet=0;pellet<8;pellet++) mark.Invoke(hud,new object[]{ArenaHud.Marker.Body,5f,9f});
                Assert.That(hud.CuesSounded,Is.EqualTo(1));
                mark.Invoke(hud,new object[]{ArenaHud.Marker.Kill,5f,0f});
                Assert.That(hud.CuesSounded,Is.EqualTo(2),"A kill in the same frame still confirms.");
                mark.Invoke(hud,new object[]{ArenaHud.Marker.Body,5.5f,9f});
                Assert.That(hud.CuesSounded,Is.EqualTo(3),"The next shot sounds again.");
            }
            finally { Object.DestroyImmediate(voice); }
        }
        // A target finished this tick still takes 0-damage hits until the tick ends: the lethal hit names the killer and
        // its headshot, not a later pellet or another shooter.
        [Test] public void TheLethalHitNamesTheKillerNotALaterZeroDamageHit()
        {
            var hud=new ArenaHud(); var record=typeof(ArenaHud).GetMethod("RecordHit",Private);
            void Hit(int shooter,float amount,bool head) => record.Invoke(hud,new object[]{new CombatEvent(CombatEventKind.Hit,new CombatAction { Participant=shooter },50,target:5,amount:amount,headshot:head)});
            Hit(2,30,true); Hit(2,0,false); Hit(3,0,false);
            Assert.That(hud.Killer(5,50,out bool head),Is.EqualTo(2)); Assert.That(head,Is.True,"The lethal pellet was the headshot.");
            Hit(4,20,false); Hit(2,0,true);
            Assert.That(hud.Killer(5,50,out head),Is.EqualTo(4)); Assert.That(head,Is.False,"A 0-damage head pellet is no headshot kill.");
            Assert.That(hud.Killer(5,51,out _),Is.EqualTo(0),"Only a same-tick hit is credited.");
            Assert.That(hud.Killer(9,50,out _),Is.EqualTo(0));
        }
        // Your kill names the victim under the crosshair; quick follow-ups count up, and a pause starts over.
        [Test] public void QuickKillsCountUpUnderTheCrosshair()
        {
            var hud=new ArenaHud();
            void Kill(string victim,bool head,float now) => hud.Confirm(victim,head,now);
            Kill("◆ P3",false,10f);
            Assert.That(hud.KillStreak,Is.EqualTo(1)); Assert.That(hud.BannerText,Is.EqualTo("ELIMINATED  ◆ P3"));
            Kill("● P4",true,10f+ArenaHud.StreakSeconds);
            Assert.That(hud.BannerText,Is.EqualTo("<color=#EB5440>DOUBLE</color>  ·  ELIMINATED  ● P4  ·  HEADSHOT"));
            Kill("● P5",false,15f);
            Assert.That(hud.BannerText,Does.Contain(">TRIPLE<"));
            Kill("● P6",false,16f);
            Assert.That(hud.BannerText,Does.Contain(">×4<"));
            Kill("■ P2",false,16f+ArenaHud.StreakSeconds+.01f);
            Assert.That(hud.KillStreak,Is.EqualTo(1),"A pause past the window starts over.");
            Assert.That(hud.BannerText,Is.EqualTo("ELIMINATED  ■ P2"));
            Assert.That(ArenaHud.BannerSeconds,Is.LessThan(ArenaHud.StreakSeconds));
        }
        // Each round opens on the map's name for three seconds, fading over the last half second unless motion is reduced.
        [Test] public void EachRoundOpensOnTheMapNameThenClears()
        {
            Assert.That(ArenaHud.MapIntroOpacity(0,120,false),Is.EqualTo(1));
            Assert.That(ArenaHud.MapIntroOpacity(300,120,false),Is.EqualTo(1));
            Assert.That(ArenaHud.MapIntroOpacity(330,120,false),Is.EqualTo(.5f).Within(.001f));
            Assert.That(ArenaHud.MapIntroOpacity(330,120,true),Is.EqualTo(1),"Reduced motion skips the fade.");
            Assert.That(ArenaHud.MapIntroOpacity(360,120,false),Is.EqualTo(0));
            Assert.That(ArenaHud.MapIntroOpacity(360,120,true),Is.EqualTo(0));
            Assert.That(ArenaHud.MapIntroOpacity(0,0,false),Is.EqualTo(0),"No tick rate shows nothing.");
        }
        // The last quarter of any magazine warns; the sword and an empty hand never do.
        [Test] public void LastQuarterOfAMagazineWarnsWithADryClickUnderTheHitTick()
        {
            Assert.That(ArenaHud.LowAmmo(WeaponKind.LineDriver,6),Is.True); Assert.That(ArenaHud.LowAmmo(WeaponKind.LineDriver,7),Is.False);
            Assert.That(ArenaHud.LowAmmo(WeaponKind.LaserSniper,3),Is.True); Assert.That(ArenaHud.LowAmmo(WeaponKind.LaserSniper,4),Is.False,"A quarter of the two-magazine pickup.");
            Assert.That(ArenaHud.LowAmmo(WeaponKind.LaserSMG,0),Is.True);
            Assert.That(ArenaHud.LowAmmo(WeaponKind.LaserSword,0),Is.False); Assert.That(ArenaHud.LowAmmo(WeaponKind.None,0),Is.False);
            var dry=Wildstone.Presentation.ArenaAudio.MakeDry(); var tick=Wildstone.Presentation.ArenaAudio.MakeTick();
            try
            {
                Assert.That(dry.length,Is.LessThanOrEqualTo(.06f));
                int Crossings(AudioClip clip) { var d=new float[clip.samples]; clip.GetData(d,0); int n=0; for(int i=1;i<d.Length;i++) if(d[i-1]<0!=d[i]<0) n++; return n; }
                var data=new float[dry.samples]; dry.GetData(data,0); float peak=0; foreach(var v in data) peak=Mathf.Max(peak,Mathf.Abs(v));
                Assert.That(peak,Is.EqualTo(.85f).Within(.001f));
                Assert.That(Mathf.Abs(data[data.Length-1]),Is.LessThan(.02f),"It decays without a click.");
                Assert.That(Crossings(dry)/dry.length,Is.LessThan(Crossings(tick)/tick.length*.7f),"Pitched well under the hit tick.");
                Assert.That(ArenaHud.DryScale,Is.LessThan(ArenaHud.TickScale));
            }
            finally { Object.DestroyImmediate(dry); Object.DestroyImmediate(tick); }
        }
        [Test] public void RecoveringMenuHidesStaleTeamAndStartActionsButAllowsLeaving()
        {
            var owner=new GameObject("recovery presentation"); owner.SetActive(false);
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>();
                bridge.Available=true; bridge.ActiveSession=true; bridge.Recovering=true; bridge.InLobby=true; bridge.Host=true;
                var menu=owner.AddComponent<ArenaMenu>(); var session=owner.GetComponent<PracticeSession>(); var root=new VisualElement();
                typeof(ArenaMenu).GetField("session",Private).SetValue(menu,session);
                typeof(ArenaMenu).GetField("online",Private).SetValue(menu,bridge);
                typeof(ArenaMenu).GetField("root",Private).SetValue(menu,root);
                session.Flow.Show(MenuScreen.Online); typeof(ArenaMenu).GetMethod("Render",Private).Invoke(menu,null);
                var buttons=root.Query<Button>().ToList();
                Assert.That(buttons.Exists(button=>button.text=="Leave match"),Is.True);
                Assert.That(buttons.Exists(button=>button.text=="Start match" || button.text=="Ready" || button.text.StartsWith("Join ")),Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void RecoveryTakesForegroundInsteadOfLeavingAFrozenPlayingHud()
        {
            var owner=new GameObject("foreground recovery"); owner.SetActive(false);
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>(); bridge.Recovering=true;
                var menu=owner.AddComponent<ArenaMenu>(); var session=owner.GetComponent<PracticeSession>();
                typeof(ArenaMenu).GetField("session",Private).SetValue(menu,session);
                typeof(ArenaMenu).GetField("online",Private).SetValue(menu,bridge);
                session.Flow.Show(MenuScreen.Playing);
                typeof(ArenaMenu).GetMethod("OnlineChanged",Private).Invoke(menu,null);
                Assert.That(session.Flow.Screen,Is.EqualTo(MenuScreen.Online));
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test] public void CancellingRecoveryCannotResumeTheRetiredGameplayPresentation()
        {
            var owner=new GameObject("cancelled recovery presentation"); owner.SetActive(false);
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>(); bridge.ActiveSession=false;
                var menu=owner.AddComponent<ArenaMenu>(); var session=owner.GetComponent<PracticeSession>();
                typeof(ArenaMenu).GetField("session",Private).SetValue(menu,session);
                typeof(ArenaMenu).GetField("online",Private).SetValue(menu,bridge);
                typeof(ArenaMenu).GetField("showingRecovery",Private).SetValue(menu,true);
                typeof(PracticeSession).GetField("<ExternalSimulation>k__BackingField",Private).SetValue(session,true);
                typeof(PracticeSession).GetField("<Match>k__BackingField",Private).SetValue(session,new ArenaMatch(ArenaLayouts.CreateAll()[0],120,true));
                session.Flow.Show(MenuScreen.Title);
                typeof(ArenaMenu).GetMethod("OnlineChanged",Private).Invoke(menu,null);
                Assert.That(session.Flow.Screen,Is.EqualTo(MenuScreen.Title));
            }
            finally { Object.DestroyImmediate(owner); }
        }
        // Host of a live finished match: Rematch. Its clients: a wait note. Aborted (session closed): neither. Online never offers Play again.
        [TestCase(true,true,ArenaMatchPhase.Finished,"Rematch",null,"Leave match")]
        [TestCase(false,true,ArenaMatchPhase.Finished,null,"Waiting for the host to start a rematch.","Leave match")]
        [TestCase(true,false,ArenaMatchPhase.Aborted,null,null,"Back to title")]
        [TestCase(true,true,ArenaMatchPhase.Aborted,null,null,"Leave match")]
        public void OnlineResultsOfferRematchOnlyToTheHostOfALiveFinishedMatch(bool host,bool active,ArenaMatchPhase phase,string rematch,string note,string leave)
        {
            var owner=new GameObject("online results"); owner.SetActive(false);
            try
            {
                var bridge=owner.AddComponent<OnlineMenuBridge>(); bridge.Available=true; bridge.ActiveSession=active; bridge.Host=host;
                var menu=owner.AddComponent<ArenaMenu>(); var session=owner.GetComponent<PracticeSession>(); var root=new VisualElement();
                typeof(ArenaMenu).GetField("session",Private).SetValue(menu,session);
                typeof(ArenaMenu).GetField("online",Private).SetValue(menu,bridge);
                typeof(ArenaMenu).GetField("root",Private).SetValue(menu,root);
                var match=new ArenaMatch(ArenaLayouts.CreateAll()[0],120); var saved=match.Capture(); saved.Phase=phase; saved.Winner=1;
                Assert.That(match.TryRestore(saved),Is.True);
                typeof(PracticeSession).GetField("<ExternalSimulation>k__BackingField",Private).SetValue(session,true);
                typeof(PracticeSession).GetField("<Match>k__BackingField",Private).SetValue(session,match);
                session.Flow.Show(MenuScreen.Results); typeof(ArenaMenu).GetMethod("Render",Private).Invoke(menu,null);
                var buttons=root.Query<Button>().ToList().ConvertAll(button=>button.text);
                var notes=root.Query<Label>().ToList().ConvertAll(label=>label.text);
                Assert.That(buttons.Contains("Rematch"),Is.EqualTo(rematch!=null));
                Assert.That(notes.Exists(text=>text.StartsWith("Waiting for the host",System.StringComparison.Ordinal)),Is.EqualTo(note!=null));
                if(note!=null) Assert.That(notes,Does.Contain(note));
                Assert.That(buttons,Does.Contain(leave));
                Assert.That(buttons.Exists(text=>text=="Play again" || text=="Choose arena"),Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
