using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;
using Wildstone.Networking;

namespace Wildstone.Tests
{
    [Category("Native")]
    public sealed class FusionPredictionBootstrapMotorTests
    {
        [TestCase(319,328),TestCase(-9,0)]
        public void MissingInitialTicksReplayWholeStateAndExpireHeldInputBeforeCurrentSample(int origin,int confirmed)
        {
            var first=new GameObject("Bootstrap motor fixture"); var second=new GameObject("Reference motor fixture");
            try
            {
                first.transform.position=second.transform.position=Vector3.up*20;
                var predicted=first.AddComponent<ArenaMotor>(); var reference=second.AddComponent<ArenaMotor>();
                var checkpoint=predicted.CaptureState(); checkpoint.Tick=9;
                checkpoint.LastInputSequence=73; checkpoint.LastFreshInputTick=3;
                checkpoint.LastMove=Vector2.right; checkpoint.Held=MotorButtons.Jump;
                checkpoint.DashReadyAt=1.25; checkpoint.StaminaDelay=.2f;
                reference.RestoreState(checkpoint);
                for(int tick=10;tick<16;tick++) Assert.That(reference.Simulate(default,MotorTuning.TickDelta,tick),Is.True);
                var restored=FusionPlayer.BootstrapInitialPrediction(predicted,checkpoint,16,origin,confirmed);
                var expected=reference.CaptureState();
                foreach(var field in typeof(MotorState).GetFields())
                {
                    if(field.FieldType==typeof(Vector3)) Assert.That(Vector3.Distance((Vector3)field.GetValue(restored),(Vector3)field.GetValue(expected)),Is.LessThan(.00001f),field.Name);
                    else Assert.That(field.GetValue(restored),Is.EqualTo(field.GetValue(expected)),field.Name);
                }
                Assert.That(restored.Tick,Is.EqualTo(15));
                Assert.That(restored.LastInputSequence,Is.EqualTo(73));
                Assert.That(restored.LastFreshInputTick,Is.EqualTo(3));
                Assert.That(restored.Held,Is.EqualTo(MotorButtons.None));
                Assert.That(restored.LastMove,Is.EqualTo(Vector2.zero));
                var sample=new FusionInput {Present=true,Sequence=101,Generation=1,Move=Vector2.up,Pressed=MotorButtons.Dash};
                var input=FusionMotorInput.Schedule(true,sample,restored,100,out int cursor);
                Assert.That(cursor,Is.EqualTo(101)); Assert.That(input.Tick,Is.EqualTo(16));
                Assert.That(input.Sequence,Is.EqualTo(74),"Missing ticks never spend a received sample sequence.");
                Assert.That(predicted.Simulate(input,MotorTuning.TickDelta,16),Is.True);
                Assert.That(predicted.CaptureState().LastFreshInputTick,Is.EqualTo(16));
                Assert.That(predicted.CaptureState().LastInputSequence,Is.EqualTo(74));
            }
            finally {Object.DestroyImmediate(first);Object.DestroyImmediate(second);}
        }
    }
}
