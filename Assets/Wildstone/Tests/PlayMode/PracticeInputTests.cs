using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wildstone.Game;
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
        static string Describe(PracticeInput input,Keyboard keyboard,int performed)
        {
            var action=input.Actions[4];
            return $"Jump enabled={action.enabled} phase={action.phase} controls={action.controls.Count} binding={action.bindings[0].effectivePath} held={action.IsPressed()} edge={action.WasPressedThisFrame()} callbacks={performed}; keyboard enabled={keyboard.enabled} space={keyboard.spaceKey.ReadValue()} current={Keyboard.current?.deviceId} fixture={keyboard.deviceId}; focused={UnityEngine.Application.isFocused} background={UnityEngine.Application.runInBackground} updateMode={InputSystem.settings.updateMode} editorBehavior={InputSystem.settings.editorInputBehaviorInPlayMode} backgroundBehavior={InputSystem.settings.backgroundBehavior}";
        }
    }
}
