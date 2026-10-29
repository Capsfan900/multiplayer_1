using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
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
                typeof(ArenaMenu).GetField("root",Private).SetValue(menu,new VisualElement());
                typeof(PracticeSession).GetField("<Settings>k__BackingField",Private).SetValue(session,new LocalSettings());
                typeof(PracticeSession).GetField("<Match>k__BackingField",Private).SetValue(session,new ArenaMatch(ArenaLayouts.CreateAll()[0],120,true));
                void Render(MenuScreen screen) { session.Flow.Show(screen); typeof(ArenaMenu).GetMethod("Render",Private).Invoke(menu,null); }
                Render(MenuScreen.Playing); var first=typeof(ArenaMenu).GetField("hud",Private).GetValue(menu);
                Render(MenuScreen.Pause); Assert.That(typeof(ArenaMenu).GetField("hud",Private).GetValue(menu),Is.SameAs(first));
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
    }
}
