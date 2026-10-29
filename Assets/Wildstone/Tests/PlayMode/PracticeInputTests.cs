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
                    player.StunUntil=0; player.StarterAmmo=0;
                    input.ConsumeActions(actions,400,120,player,true); Assert.That(actions,Is.Empty);
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
        static string Describe(PracticeInput input,Keyboard keyboard,int performed)
        {
            var action=input.Actions[4];
            return $"Jump enabled={action.enabled} phase={action.phase} controls={action.controls.Count} binding={action.bindings[0].effectivePath} held={action.IsPressed()} edge={action.WasPressedThisFrame()} callbacks={performed}; keyboard enabled={keyboard.enabled} space={keyboard.spaceKey.ReadValue()} current={Keyboard.current?.deviceId} fixture={keyboard.deviceId}; focused={UnityEngine.Application.isFocused} background={UnityEngine.Application.runInBackground} updateMode={InputSystem.settings.updateMode} editorBehavior={InputSystem.settings.editorInputBehaviorInPlayMode} backgroundBehavior={InputSystem.settings.backgroundBehavior}";
        }
    }
}
