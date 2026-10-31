using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wildstone.Game;
using Wildstone.Combat;
using System.Collections.Generic;
using Wildstone.Movement;
using Wildstone.UI;
namespace Wildstone.Tests
{
    public sealed class PracticeInputTests
    {
        InputSettings.BackgroundBehavior background;
        InputSettings.EditorInputBehaviorInPlayMode editorInput;
        [SetUp] public void RouteSyntheticInputToUnfocusedBatchPlayer()
        {
            background=InputSystem.settings.backgroundBehavior;
            editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            // A batch Editor has no focused Game view; exercise player actions, not editor input routing.
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown] public void RestoreInputRouting()
        {
            InputSystem.settings.backgroundBehavior=background;
            InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
        }
        [Test] public void CompleteTapBetweenRenderSamplesSurvivesExactlyOneTick()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                using(var input=new PracticeInput(new LocalSettings()))
                {
                    int performed=0; input.Actions[4].performed+=_=>performed++;
                    TestContext.WriteLine(Describe(input,keyboard,performed));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); input.Sample(true);
                    TestContext.WriteLine(Describe(input,keyboard,performed));
                    var tick=input.ConsumeMovement(1,1,0,0);
                    Assert.That(tick.Pressed,Is.EqualTo(MotorButtons.Jump)); Assert.That(tick.Held,Is.EqualTo(MotorButtons.None));
                    Assert.That(input.ConsumeMovement(2,1,0,0).Pressed,Is.EqualTo(MotorButtons.None));
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [Test] public void FocusClearDiscardsPendingJumpAndHeldMovement()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                using(var input=new PracticeInput(new LocalSettings()))
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space,Key.W)); InputSystem.Update(); input.Sample(true);
                    Assert.That(input.Actions[4].IsPressed(),Is.True,"The fixture must deliver a real action before testing focus clearing.");
                    input.Clear();
                    var tick=input.ConsumeMovement(1,1,0,0);
                    Assert.That(tick.Pressed,Is.EqualTo(MotorButtons.None)); Assert.That(tick.Held,Is.EqualTo(MotorButtons.None)); Assert.That(tick.Move.sqrMagnitude,Is.Zero);
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [Test] public void TapAcrossInputUpdatesSurvivesOnceWithoutRepeatedRenderEdges()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                using(var input=new PracticeInput(new LocalSettings()))
                {
                    int performed=0; input.Actions[4].performed+=_=>performed++;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space,Key.R)); InputSystem.Update();
                    TestContext.WriteLine("After press: "+Describe(input,keyboard,performed));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
                    TestContext.WriteLine("After release: "+Describe(input,keyboard,performed));
                    InputSystem.Update(); input.Sample(true);
                    Assert.That(input.ConsumeMovement(1,1,0,0).Pressed,Is.EqualTo(MotorButtons.Jump));
                    var actions=new System.Collections.Generic.List<Wildstone.Combat.CombatActionKind>();
                    input.ConsumeActions(actions);
                    CollectionAssert.AreEqual(new[]{Wildstone.Combat.CombatActionKind.Reload},actions);
                    input.Sample(true);
                    Assert.That(input.ConsumeMovement(2,1,0,0).Pressed,Is.EqualTo(MotorButtons.None));
                    input.ConsumeActions(actions); Assert.That(actions,Is.Empty);
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [Test] public void HeldStarterRepeatsAtTickCadenceWithoutDoublePressOrCatchupBurst()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var settings=new LocalSettings(); settings.Bindings[7]="<Keyboard>/j";
                using(var input=new PracticeInput(settings))
                {
                    var player=new CombatState {LifeGeneration=1,ConnectionGeneration=1};
                    var actions=new List<CombatActionKind>();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J)); InputSystem.Update(); input.Sample(true);
                    input.ConsumeActions(actions,1,120,player,true);
                    CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    input.ConsumeActions(actions,1,120,player,true); Assert.That(actions,Is.Empty);
                    input.ConsumeActions(actions,18,120,player,true); Assert.That(actions,Is.Empty);
                    input.ConsumeActions(actions,19,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    input.ConsumeActions(actions,1000,120,player,true); Assert.That(actions.Count,Is.EqualTo(1));
                    input.ConsumeActions(actions,1000,120,player,true); Assert.That(actions,Is.Empty);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); input.Sample(true);
                    input.ConsumeActions(actions,1100,120,player,true); Assert.That(actions,Is.Empty);
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        // D036: online the replicated FireReadyTick trails the request clock by the round trip. It must not delay the held
        // schedule, which carries through a late frame; a longer cooldown (a slower weapon before a switch) still holds it.
        [Test] public void HeldRepeatsKeepTheirScheduleAgainstReplicatedCooldown()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var settings=new LocalSettings(); settings.Bindings[7]="<Keyboard>/j";
                using(var input=new PracticeInput(settings))
                {
                    var player=new CombatState {LifeGeneration=1,ConnectionGeneration=1};
                    var actions=new List<CombatActionKind>();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J)); InputSystem.Update(); input.Sample(true);
                    input.ConsumeActions(actions,1,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    // The host stamped that shot five ticks after the request clock (Line Driver 18 ticks, slack 3).
                    player.FireReadyTick=1+5+18;
                    input.ConsumeActions(actions,21,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions,"Replicated ready tick ignored; two ticks late.");
                    input.ConsumeActions(actions,36,120,player,true); Assert.That(actions,Is.Empty);
                    input.ConsumeActions(actions,37,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions,"The late frame did not shift the schedule.");
                    input.ConsumeActions(actions,60,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    input.ConsumeActions(actions,77,120,player,true); Assert.That(actions,Is.Empty,"Beyond the slack the schedule restarts from the late shot.");
                    input.ConsumeActions(actions,78,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    player.FireReadyTick=200;
                    input.ConsumeActions(actions,181,120,player,true); Assert.That(actions,Is.Empty,"A cooldown longer than one interval holds the repeat.");
                    input.ConsumeActions(actions,182,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    player.StarterAmmo=0; player.ReloadUntil=300;
                    input.ConsumeActions(actions,302,120,player,true); Assert.That(actions,Is.Empty,"A finished reload not yet refilled asks for no second reload.");
                    input.ConsumeActions(actions,303,120,player,true);
                    CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions,"A reload ended the fire slack ago fires.");
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [Test] public void StarterRepeatStopsForMenuLifeReconnectSpecialReloadAndControlLoss()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var settings=new LocalSettings(); settings.Bindings[7]="<Keyboard>/j";
                using(var input=new PracticeInput(settings))
                {
                    var player=new CombatState {LifeGeneration=1,ConnectionGeneration=1};
                    var actions=new List<CombatActionKind>();
                    void Press()
                    {
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); input.Sample(true);
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J)); InputSystem.Update(); input.Sample(true);
                    }
                    Press(); input.ConsumeActions(actions,1,120,player,true);
                    player.SelectedSpecial=true;
                    input.ConsumeActions(actions,100,120,player,true); Assert.That(actions,Is.Empty);
                    player.SelectedSpecial=false; player.ReloadUntil=300;
                    input.ConsumeActions(actions,200,120,player,true); Assert.That(actions,Is.Empty);
                    player.ReloadUntil=0; player.StunUntil=400;
                    input.ConsumeActions(actions,300,120,player,true); Assert.That(actions,Is.Empty);
                    player.StunUntil=0; player.StarterAmmo=0; // the held trigger stops shooting and asks for a reload instead
                    input.ConsumeActions(actions,400,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Reload},actions);
                    player.StarterAmmo=WeaponCatalog.StarterAmmo; player.LifeGeneration++;
                    input.ConsumeActions(actions,500,120,player,true); Assert.That(actions,Is.Empty);
                    input.Sample(true); input.ConsumeActions(actions,600,120,player,true); Assert.That(actions,Is.Empty);
                    Press(); input.ConsumeActions(actions,601,120,player,true); Assert.That(actions.Count,Is.EqualTo(1));
                    player.ConnectionGeneration++; input.ConsumeActions(actions,700,120,player,true); Assert.That(actions,Is.Empty);
                    Press(); input.ConsumeActions(actions,701,120,player,true);
                    input.ConsumeActions(actions,702,120,player,false); Assert.That(actions,Is.Empty);
                    input.Sample(true); input.ConsumeActions(actions,800,120,player,true); Assert.That(actions,Is.Empty);
                    Press(); input.ConsumeActions(actions,801,120,player,true);
                    input.ConsumeActions(actions,2,120,player,true); Assert.That(actions,Is.Empty);
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        // An empty trigger reloads the starter (retrying at most every quarter second until the host starts it) or
        // swaps a spent special back to the starter without firing it while still held; the sword never runs dry.
        [Test] public void EmptyTriggerReloadsTheStarterOrDropsASpentSpecial()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var settings=new LocalSettings(); settings.Bindings[7]="<Keyboard>/j";
                using(var input=new PracticeInput(settings))
                {
                    var player=new CombatState {LifeGeneration=1,ConnectionGeneration=1,StarterAmmo=0};
                    var actions=new List<CombatActionKind>();
                    void Press()
                    {
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); input.Sample(true);
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J)); InputSystem.Update(); input.Sample(true);
                    }
                    Press(); input.ConsumeActions(actions,1,120,player,true);
                    CollectionAssert.AreEqual(new[]{CombatActionKind.Reload},actions,"No futile shot, one reload.");
                    input.ConsumeActions(actions,30,120,player,true); Assert.That(actions,Is.Empty,"Throttled while held.");
                    input.ConsumeActions(actions,31,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Reload},actions,"Retried.");
                    player.ReloadUntil=200; input.ConsumeActions(actions,100,120,player,true); Assert.That(actions,Is.Empty,"Reloading.");
                    player.ReloadUntil=0; player.StarterAmmo=WeaponCatalog.StarterAmmo;
                    player.SelectedSpecial=true; player.SpecialWeapon=WeaponKind.LaserSniper; player.SpecialAmmo=0;
                    input.ConsumeActions(actions,200,120,player,true);
                    Press(); input.ConsumeActions(actions,201,120,player,true);
                    CollectionAssert.AreEqual(new[]{CombatActionKind.SelectStarter},actions);
                    player.SelectedSpecial=false;
                    input.ConsumeActions(actions,300,120,player,true); Assert.That(actions,Is.Empty,"The held trigger does not fire the starter.");
                    Press(); input.ConsumeActions(actions,301,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    player.SelectedSpecial=true; player.SpecialWeapon=WeaponKind.LaserSword; player.SpecialAmmo=0;
                    input.ConsumeActions(actions,400,120,player,true);
                    Press(); input.ConsumeActions(actions,401,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [Test] public void EarlySemiAutomaticPressFiresOnceAtReady()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var settings=new LocalSettings(); settings.Bindings[7]="<Keyboard>/j";
                using(var input=new PracticeInput(settings))
                {
                    var player=new CombatState {LifeGeneration=1,ConnectionGeneration=1,SelectedSpecial=true,SpecialWeapon=WeaponKind.LaserSniper,SpecialAmmo=6};
                    var actions=new List<CombatActionKind>();
                    void Press()
                    {
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); input.Sample(true);
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J)); InputSystem.Update(); input.Sample(true);
                    }
                    int interval=WeaponCatalog.Ticks(WeaponCatalog.Get(WeaponKind.LaserSniper).Interval,120), ready=1+interval;
                    Press(); input.ConsumeActions(actions,1,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    Press(); input.ConsumeActions(actions,ready-12,120,player,true); Assert.That(actions,Is.Empty,"0.1 s early waits.");
                    Press(); input.ConsumeActions(actions,ready-6,120,player,true); Assert.That(actions,Is.Empty,"A second early press is the same shot.");
                    input.ConsumeActions(actions,ready-1,120,player,true); Assert.That(actions,Is.Empty);
                    input.ConsumeActions(actions,ready,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions,"Fires once at ready.");
                    input.ConsumeActions(actions,ready+1,120,player,true); Assert.That(actions,Is.Empty);
                    Press(); input.ConsumeActions(actions,ready+interval-12,120,player,true); Assert.That(actions,Is.Empty);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); input.Sample(true);
                    player.SelectedSpecial=false; input.ConsumeActions(actions,ready+interval,120,player,true);
                    Assert.That(actions,Is.Empty,"A weapon switch drops the buffered shot.");
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [Test] public void CompleteFireTapRemainsExactlyOneAction()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var settings=new LocalSettings(); settings.Bindings[7]="<Keyboard>/j";
                using(var input=new PracticeInput(settings))
                {
                    var player=new CombatState {LifeGeneration=1,ConnectionGeneration=1};
                    var actions=new List<CombatActionKind>();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.J)); InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); input.Sample(true);
                    input.ConsumeActions(actions,1,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Fire},actions);
                    input.ConsumeActions(actions,100,120,player,true); Assert.That(actions,Is.Empty);
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        [Test] public void GrenadeThrowsOnReleaseAndHeldAimRidesTheMovementSample()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var settings=new LocalSettings(); settings.Bindings[PracticeInput.AimIndex]="<Keyboard>/k";
                using(var input=new PracticeInput(settings))
                {
                    var player=new CombatState {LifeGeneration=1,ConnectionGeneration=1};
                    var actions=new List<CombatActionKind>();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G,Key.K)); InputSystem.Update(); input.Sample(true);
                    Assert.That(input.GrenadeHeld,Is.True); Assert.That(input.Aiming,Is.True);
                    Assert.That(input.ConsumeMovement(1,1,0,0).Aiming,Is.True);
                    input.ConsumeActions(actions,1,120,player,true); Assert.That(actions,Is.Empty,"Holding the grenade key previews; it does not throw.");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.K)); InputSystem.Update(); input.Sample(true);
                    Assert.That(input.GrenadeHeld,Is.False); int drops=input.AdsDrops;
                    input.ConsumeActions(actions,2,120,player,true); CollectionAssert.AreEqual(new[]{CombatActionKind.Grenade},actions);
                    Assert.That(input.AdsDrops,Is.EqualTo(drops+1),"A throw drops local ADS.");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); input.Sample(true);
                    Assert.That(input.Aiming,Is.False); Assert.That(input.ConsumeMovement(3,1,0,0).Aiming,Is.False);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.G)); InputSystem.Update(); input.Sample(false);
                    Assert.That(input.GrenadeHeld,Is.False,"Losing focus cancels a held grenade.");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
                    input.ConsumeActions(actions,4,120,player,true); Assert.That(actions,Is.Empty,"A cancelled hold never throws on release.");
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
        static string Describe(PracticeInput input,Keyboard keyboard,int performed)
        {
            var action=input.Actions[4];
            return $"Jump enabled={action.enabled} phase={action.phase} controls={action.controls.Count} binding={action.bindings[0].effectivePath} held={action.IsPressed()} edge={action.WasPressedThisFrame()} callbacks={performed}; keyboard enabled={keyboard.enabled} space={keyboard.spaceKey.ReadValue()} current={Keyboard.current?.deviceId} fixture={keyboard.deviceId}; focused={UnityEngine.Application.isFocused} background={UnityEngine.Application.runInBackground} updateMode={InputSystem.settings.updateMode} editorBehavior={InputSystem.settings.editorInputBehaviorInPlayMode} backgroundBehavior={InputSystem.settings.backgroundBehavior}";
        }
    }
}
