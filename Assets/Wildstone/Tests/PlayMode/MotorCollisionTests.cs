using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;

namespace Wildstone.Tests
{
    public class MotorCollisionTests
    {
        const float Dt=1f/120f;
        readonly List<GameObject> objects=new List<GameObject>();
        ArenaMotor motor;
        GameObject Box(string name,Vector3 position,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(go);
            go.name=name; go.transform.position=position; go.transform.localScale=scale;
            go.AddComponent<MotorSurface>().Id=objects.Count; return go;
        }
        [SetUp] public void Setup()
        {
            Box("ground",new Vector3(0,-.5f,0),new Vector3(100,1,100));
            var go=new GameObject("motor"); objects.Add(go); go.transform.position=Vector3.up*.1f;
            motor=go.AddComponent<ArenaMotor>(); Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup() { foreach(var go in objects) Object.DestroyImmediate(go); objects.Clear(); }
        void Step(Vector2 move=default,MotorButtons press=MotorButtons.None,MotorButtons held=MotorButtons.None,float yaw=0)
        {
            var s=motor.CaptureState(); int tick=s.Tick+1;
            Assert.That(motor.Simulate(new MotorInput { Present=true,Tick=tick,Sequence=tick+1,Generation=s.Generation,
                Move=move,Pressed=press,Held=held,Yaw=yaw },Dt,tick),Is.True);
        }
        void Settle() { for(int i=0;i<30;i++) Step(); }
        void Replay(int count,Vector2 input)
        {
            var saved=motor.CaptureState(); var expected=new MotorState[count];
            float maxPosition=0,maxNormal=0,maxVelocity=0;
            for(int i=0;i<count;i++) { Step(input); expected[i]=motor.CaptureState(); }
            motor.RestoreState(saved);
            for(int i=0;i<count;i++)
            {
                Step(input); var actual=motor.CaptureState();
                float positionError=Vector3.Distance(actual.Position,expected[i].Position);
                float normalError=Vector3.Distance(actual.GroundNormal,expected[i].GroundNormal);
                float velocityError=Vector3.Distance(actual.Velocity,expected[i].Velocity);
                maxPosition=Mathf.Max(maxPosition,positionError); maxNormal=Mathf.Max(maxNormal,normalError); maxVelocity=Mathf.Max(maxVelocity,velocityError);
                Assert.That(positionError,Is.LessThan(.0001f),"pose at replay tick "+i);
                // Re-inserted PhysX contacts may differ by <0.006 degrees; timers/events stay exact.
                Assert.That(normalError,Is.LessThan(.0001f),"ground normal at replay tick "+i);
                Assert.That(velocityError,Is.LessThan(.0001f),"velocity at replay tick "+i);
                actual.Position=expected[i].Position;
                actual.GroundNormal=expected[i].GroundNormal;
                actual.Velocity=expected[i].Velocity;
                Assert.That(JsonUtility.ToJson(actual),Is.EqualTo(JsonUtility.ToJson(expected[i])),"state at replay tick "+i);
            }
            TestContext.Out.WriteLine($"Replay {count} ticks: max position={maxPosition:R} m, ground-normal distance={maxNormal:R}, velocity={maxVelocity:R} m/s");
        }
        [Test] public void GroundRunAndJumpUseShippedLaws()
        {
            Settle(); for(int i=0;i<120;i++) Step(Vector2.up);
            Assert.That(motor.CaptureState().Grounded,Is.True);
            Assert.That(motor.CaptureState().Velocity.z,Is.EqualTo(11).Within(.0001));
            Step(Vector2.up,MotorButtons.Jump,MotorButtons.Jump);
            Assert.That(motor.CaptureState().Velocity.y,Is.EqualTo(12).Within(.0001));
            Assert.That(motor.CaptureState().Grounded,Is.False);
        }
        [Test] public void MidSlideRestoreUnderCeilingKeepsCrouchAndReplaysNextSweep()
        {
            Box("low roof",new Vector3(0,1.35f,6),new Vector3(8,.5f,12)); Physics.SyncTransforms();
            var s=motor.CaptureState(); s.Position=new Vector3(0,.05f,0); s.Grounded=true;
            s.LastGroundedAt=0; s.Velocity=Vector3.forward*11; motor.RestoreState(s);
            Step(Vector2.up,MotorButtons.Slide); for(int i=0;i<12;i++) Step(Vector2.up);
            Assert.That(motor.CaptureState().Sliding,Is.True);
            Replay(60,Vector2.up);
        }
        [Test] public void WallRunEntryChargesBudgetAndRestoresAcrossASeam()
        {
            Box("wall a",new Vector3(1.1f,3,1),new Vector3(1,6,4));
            Box("wall b",new Vector3(1.1f,3,7.05f),new Vector3(1,6,8)); Physics.SyncTransforms();
            var s=motor.CaptureState(); s.Position=new Vector3(0,2,0); s.Velocity=Vector3.forward*11;
            motor.RestoreState(s); Step(Vector2.up);
            s=motor.CaptureState(); Assert.That(s.WallRunning,Is.True);
            Assert.That(s.WallRunsUsed,Is.EqualTo(1));
            Assert.That(s.Stamina,Is.EqualTo(87.816666f).Within(.001));
            Replay(65,Vector2.up);
        }
        [Test] public void AirRestoreCannotReuseOldGroundContact()
        {
            Settle(); Assert.That(motor.CaptureState().Grounded,Is.True);
            var s=motor.CaptureState(); s.Position=Vector3.up*12; s.Grounded=false; s.GroundStale=true;
            s.LastGroundedAt=-99; s.Velocity=Vector3.zero; motor.RestoreState(s);
            Step(press:MotorButtons.Jump);
            Assert.That(motor.CaptureState().Grounded,Is.False);
            Assert.That(motor.CaptureState().Velocity.y,Is.LessThan(0));
            Replay(25,Vector2.up);
        }
        [Test] public void SolidWorldStopsDashAndCharacterLayerDoesNotPush()
        {
            Box("cover",new Vector3(0,1,3),new Vector3(5,4,.5f));
            var other=Box("other player",new Vector3(0,1,1),new Vector3(.8f,2,.8f)); other.layer=6;
            Physics.SyncTransforms(); Settle(); Step(Vector2.up,MotorButtons.Dash);
            for(int i=0;i<20;i++) Step(Vector2.up);
            Assert.That(motor.CaptureState().Position.z,Is.InRange(2.2f,2.5f));
        }
        [Test] public void WaterExitSnapshotReplaysContactExpiryAndDrySlide()
        {
            var water=new GameObject("water"); objects.Add(water); water.transform.position=new Vector3(0,.1f,0);
            water.AddComponent<BoxCollider>().size=new Vector3(4,.4f,4);
            var flow=water.AddComponent<MotorWater>(); flow.Id=100; flow.FlowSpeed=0;
            Physics.SyncTransforms(); Settle();
            var s=motor.CaptureState(); s.Velocity=Vector3.forward*11; motor.RestoreState(s);
            Step(Vector2.up,MotorButtons.Slide);
            Assert.That(motor.CaptureState().WaterContactId,Is.EqualTo(100));
            for(int i=0;i<12;i++) Step(Vector2.up);
            Replay(55,Vector2.up);
            Assert.That(motor.CaptureState().WaterContactId,Is.Zero);
        }
        [Test] public void OrbWorldAndPlayerSnapshotReplayExactlyOnePop()
        {
            var go=new GameObject("orb"); objects.Add(go); go.transform.position=new Vector3(0,3,3);
            var orb=go.AddComponent<MotorLaunchOrb>(); orb.Id=101;
            var s=motor.CaptureState(); s.Position=new Vector3(0,2,0); s.Velocity=Vector3.forward*11;
            motor.RestoreState(s); Physics.SyncTransforms();
            var initial=motor.CaptureState(); var world=orb.CaptureState();
            for(int i=0;i<36;i++) Step(Vector2.up);
            var expected=motor.CaptureState(); Assert.That(orb.CaptureState().ActionSequence,Is.EqualTo(1));
            Assert.That(expected.LastLaunchId,Is.EqualTo(101));
            motor.RestoreState(initial); orb.RestoreState(world);
            for(int i=0;i<36;i++) Step(Vector2.up);
            Assert.That(orb.CaptureState().ActionSequence,Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(motor.CaptureState()),Is.EqualTo(JsonUtility.ToJson(expected)));
        }
        [Test] public void WallExhaustionAndGraceRestoreWithoutSpendingAnotherEntry()
        {
            Box("wall",new Vector3(1.1f,3,6),new Vector3(1,6,20)); Physics.SyncTransforms();
            var s=motor.CaptureState(); s.Position=new Vector3(0,2,0); s.Velocity=Vector3.forward*11;
            motor.RestoreState(s); Step(Vector2.up);
            s=motor.CaptureState(); Assert.That(s.WallRunning,Is.True); s.Stamina=.1f; motor.RestoreState(s);
            Step(Vector2.up); s=motor.CaptureState();
            Assert.That(s.WallRunning,Is.False); Assert.That(s.WallEndReason,Is.EqualTo((int)WallRunEnd.Exhausted));
            Assert.That(s.WallPerfectWindow,Is.True); var saved=s;
            Step(Vector2.up,MotorButtons.Jump); var expected=motor.CaptureState();
            Assert.That(expected.Velocity.y,Is.EqualTo(10)); Assert.That(expected.Stamina,Is.EqualTo(20));
            motor.RestoreState(saved); Step(Vector2.up,MotorButtons.Jump);
            Assert.That(JsonUtility.ToJson(motor.CaptureState()),Is.EqualTo(JsonUtility.ToJson(expected)));
        }
        [Test] public void AwayStrafeCancelsWallWithoutNaturalExitGrace()
        {
            Box("wall",new Vector3(1.1f,3,6),new Vector3(1,6,20)); Physics.SyncTransforms();
            var s=motor.CaptureState(); s.Position=new Vector3(0,2,0); s.Velocity=Vector3.forward*11;
            motor.RestoreState(s); Step(Vector2.up); Assert.That(motor.CaptureState().WallRunning,Is.True);
            Step(Vector2.left); s=motor.CaptureState();
            Assert.That(s.WallRunning,Is.False); Assert.That(s.WallLeftAt,Is.LessThan(-90));
            Assert.That(s.WallEndReason,Is.EqualTo((int)WallRunEnd.Cancelled));
        }
        [Test] public void WalkableRampKeepsDownhillSlideGroundedAndReplays()
        {
            var ramp=Box("ramp",new Vector3(0,1,0),new Vector3(8,.5f,20));
            ramp.transform.rotation=Quaternion.Euler(14,0,0); Physics.SyncTransforms();
            var s=motor.CaptureState(); s.Position=new Vector3(0,2.4f,-4); motor.RestoreState(s); Settle();
            Assert.That(motor.CaptureState().Grounded,Is.True);
            s=motor.CaptureState(); s.Velocity=Vector3.forward*11; motor.RestoreState(s); Step(Vector2.up,MotorButtons.Slide);
            Assert.That(motor.CaptureState().Sliding,Is.True);
            Replay(35,Vector2.up);
            Assert.That(motor.CaptureState().Velocity.z,Is.GreaterThan(15));
        }
    }
}
