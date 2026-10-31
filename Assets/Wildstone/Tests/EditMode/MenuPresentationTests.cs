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
        [Test] public void PelletHitsInOneFrameSoundOnce()
        {
            var hud=new ArenaHud(); var voice=new GameObject("HUD feedback test",typeof(AudioSource));
            try
            {
                hud.Feedback=voice.GetComponent<AudioSource>();
                var mark=typeof(ArenaHud).GetMethod("Mark",Private);
                for(int pellet=0;pellet<8;pellet++) mark.Invoke(hud,new object[]{ArenaHud.Marker.Body,5f});
                Assert.That(hud.CuesSounded,Is.EqualTo(1));
                mark.Invoke(hud,new object[]{ArenaHud.Marker.Kill,5f});
                Assert.That(hud.CuesSounded,Is.EqualTo(2),"A kill in the same frame still confirms.");
                mark.Invoke(hud,new object[]{ArenaHud.Marker.Body,5.5f});
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
        // The last quarter of any magazine warns; the sword and an empty hand never do.
        [Test] public void LastQuarterOfAMagazineWarnsWithADryClickUnderTheHitTick()
        {
            Assert.That(ArenaHud.LowAmmo(WeaponKind.LineDriver,6),Is.True); Assert.That(ArenaHud.LowAmmo(WeaponKind.LineDriver,7),Is.False);
            Assert.That(ArenaHud.LowAmmo(WeaponKind.LaserSniper,1),Is.True); Assert.That(ArenaHud.LowAmmo(WeaponKind.LaserSniper,2),Is.False);
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
