using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    public class ArenaMotorTests
    {
        const float Dt = 1f / 120f;
        GameObject body;
        ArenaMotor motor;
        [SetUp] public void Setup() { body = new GameObject("motor test"); motor = body.AddComponent<ArenaMotor>(); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(body); }
        MotorInput Command(int tick, MotorButtons pressed = MotorButtons.None, Vector2 move = default) => new MotorInput
        { Present = true, Tick = tick, Sequence = tick + 1, Generation = 1, Move = move, Pressed = pressed, Held = pressed };
        MotorState Air() { var s = motor.CaptureState(); s.Position = Vector3.up * 20; return s; }

        [Test] public void InitialStateMatchesShippedCapsuleAndStamina()
        {
            var s = motor.CaptureState();
            Assert.That(s.Stamina, Is.EqualTo(100));
            Assert.That(s.CapsuleHeight, Is.EqualTo(1.8f));
            Assert.That(s.Generation, Is.EqualTo(1));
            Assert.That(s.Tick, Is.EqualTo(-1));
        }
        [Test] public void AirDashCostsThirtyAndSetsTwentyTwoWithNoVerticalSpeed()
        {
            motor.RestoreState(Air());
            Assert.That(motor.Simulate(Command(0, MotorButtons.Dash), Dt, 0), Is.True);
            var s = motor.CaptureState();
            Assert.That(s.Velocity, Is.EqualTo(new Vector3(0, 0, 22)));
            Assert.That(s.Stamina, Is.EqualTo(70));
            Assert.That(s.AirDashUsed, Is.True);
        }
        [Test] public void RestoreRebuildsTheCapsulePoseWithoutClearingTraversalState()
        {
            var s = Air(); s.Sliding = true; s.CapsuleHeight = .9f; s.CapsuleCenter = new Vector3(0,.45f,0);
            s.Velocity = new Vector3(3,4,5); s.DashUntil = 5; s.WallLostTime = .1f;
            motor.RestoreState(s);
            Assert.That(body.transform.position, Is.EqualTo(s.Position));
            Assert.That(body.GetComponent<CharacterController>().height, Is.EqualTo(.9f));
            Assert.That(JsonUtility.ToJson(motor.CaptureState()), Is.EqualTo(JsonUtility.ToJson(s)));
        }
        [Test] public void DashReplayFromMidFlightProducesIdenticalEntireState()
        {
            motor.RestoreState(Air()); motor.Simulate(Command(0, MotorButtons.Dash), Dt, 0);
            for (int i=1;i<8;i++) motor.Simulate(Command(i),Dt,i);
            var saved = motor.CaptureState();
            for (int i=8;i<40;i++) motor.Simulate(Command(i),Dt,i);
            string expected = JsonUtility.ToJson(motor.CaptureState());
            motor.RestoreState(saved);
            for (int i=8;i<40;i++) motor.Simulate(Command(i),Dt,i);
            Assert.That(motor.CaptureState().Position.z, Is.GreaterThan(3));
            Assert.That(JsonUtility.ToJson(motor.CaptureState()), Is.EqualTo(expected));
        }
        // D041: the A key held through a plain jump (12 m/s up, held, ~88 ticks of air) with the facing turned so the sideways
        // wish leads the velocity by the gain angle, as a player turning into the strafe does.
        MotorState Jumping(){ var s=Air(); s.Velocity=new Vector3(0,12,11); return s; }
        float Strafe(bool forwardHeld,int ticks,out float peak)
        {
            peak=0;
            for(int i=0;i<ticks;i++)
            {
                var s=motor.CaptureState(); var hv=new Vector3(s.Velocity.x,0,s.Velocity.z);
                float heading=Mathf.Atan2(hv.x,hv.z)*Mathf.Rad2Deg, sweet=Mathf.Acos(Mathf.Clamp01((2.5f-18*Dt)/hv.magnitude))*Mathf.Rad2Deg;
                var c=Command(i,MotorButtons.Jump,new Vector2(-1,forwardHeld?1:0)); c.Yaw=Mathf.Repeat(heading+90-sweet+180,360)-180;
                motor.Simulate(c,Dt,i); hv=motor.CaptureState().Velocity; hv.y=0; peak=Mathf.Max(peak,hv.magnitude);
            }
            var v=motor.CaptureState().Velocity; return new Vector3(v.x,0,v.z).magnitude;
        }
        [Test] public void ScriptedStrafeBuildsSpeedWithinOneJumpAndStaysUnderTheSoftCap()
        {
            var start=Jumping(); // captured once: a second capture would carry the first run's tick and refuse the replayed commands
            motor.RestoreState(start); float a=Strafe(false,88,out float peakA);
            motor.RestoreState(start); float wa=Strafe(true,88,out float peakWa);
            Assert.That(a,Is.GreaterThanOrEqualTo(12.8f));
            Assert.That(Mathf.Max(peakA,peakWa),Is.LessThanOrEqualTo(17.6f+.5f));
            Assert.That(wa,Is.EqualTo(a).Within(.2f));
        }
        [Test] public void AirStrafeReplayFromMidFlightProducesIdenticalEntireState()
        {
            motor.RestoreState(Jumping()); Strafe(false,30,out _);
            var saved=motor.CaptureState();
            for(int i=30;i<88;i++){ var c=Command(i,MotorButtons.Jump,new Vector2(1,1)); c.Yaw=-80+i*.5f; motor.Simulate(c,Dt,i); }
            string expected=JsonUtility.ToJson(motor.CaptureState());
            motor.RestoreState(saved);
            for(int i=30;i<88;i++){ var c=Command(i,MotorButtons.Jump,new Vector2(1,1)); c.Yaw=-80+i*.5f; motor.Simulate(c,Dt,i); }
            Assert.That(JsonUtility.ToJson(motor.CaptureState()),Is.EqualTo(expected));
        }
        [Test] public void RejectsNonFiniteAndWrongGenerationWithoutChangingAnything()
        {
            string initial = JsonUtility.ToJson(motor.CaptureState());
            var c=Command(0); c.Move.x=float.NaN;
            Assert.That(motor.Simulate(c,Dt,0),Is.False);
            c=Command(0); c.Generation=2;
            Assert.That(motor.Simulate(c,Dt,0),Is.False);
            Assert.That(JsonUtility.ToJson(motor.CaptureState()),Is.EqualTo(initial));
            Assert.That(motor.Simulate(Command(0),Dt,0),Is.True);
        }
        [Test] public void MissingInputNeverRepeatsDashAndNeutralizesAfterOneHundredMilliseconds()
        {
            motor.RestoreState(Air());
            motor.Simulate(Command(0,MotorButtons.Dash,Vector2.up),Dt,0);
            for(int i=1;i<=13;i++) motor.Simulate(default,Dt,i);
            var s=motor.CaptureState();
            Assert.That(s.LastMove,Is.EqualTo(Vector2.zero));
            Assert.That(s.Stamina,Is.EqualTo(70));
            Assert.That(s.EventSequence,Is.EqualTo(1));
        }
        [Test] public void CoyoteJumpUsesSourceTwelveMetresPerSecond()
        {
            var s=Air(); s.LastGroundedAt=-.05; motor.RestoreState(s);
            motor.Simulate(Command(0,MotorButtons.Jump),Dt,0);
            Assert.That(motor.CaptureState().Velocity.y,Is.EqualTo(12).Within(.00001));
            Assert.That(motor.CaptureState().LastGroundedAt,Is.LessThan(-90));
        }
        [Test] public void StaleInputCannotExecuteOrAdvanceTheMotor()
        {
            motor.Simulate(Command(0),Dt,0);
            var s=motor.CaptureState(); var c=Command(1,MotorButtons.Dash); c.Sequence=1;
            Assert.That(motor.Simulate(c,Dt,1),Is.False);
            Assert.That(JsonUtility.ToJson(motor.CaptureState()),Is.EqualTo(JsonUtility.ToJson(s)));
            Assert.That(s.Tick,Is.EqualTo(0));
        }
        [Test] public void SlideEntrySpendsOnlyAfterSpeedGateAndPreservesItsCommittedState()
        {
            var s=Air(); s.Grounded=true; s.LastGroundedAt=0; s.Velocity=Vector3.forward*11;
            motor.RestoreState(s); motor.Simulate(Command(0,MotorButtons.Slide),Dt,0);
            s=motor.CaptureState();
            Assert.That(s.Sliding,Is.True);
            Assert.That(s.Stamina,Is.EqualTo(88));
            Assert.That(s.CapsuleHeight,Is.EqualTo(.9f));
            Assert.That(s.Velocity.z,Is.EqualTo(15.733333f).Within(.0001));
            motor.Simulate(Command(1),Dt,1);
            Assert.That(motor.CaptureState().Sliding,Is.True);
        }
        [Test] public void StationarySlideRefusalCostsNothing()
        {
            var s=Air(); s.Grounded=true; s.LastGroundedAt=0;
            motor.RestoreState(s); motor.Simulate(Command(0,MotorButtons.Slide),Dt,0);
            Assert.That(motor.CaptureState().Stamina,Is.EqualTo(100));
            Assert.That(motor.CaptureState().Sliding,Is.False);
        }
        [Test] public void WaterSlideEarnsFloorWithoutSlowingCarriedSpeed()
        {
            var s=Air(); s.Grounded=true; s.LastGroundedAt=0; s.Sliding=true;
            s.CapsuleHeight=.9f; s.CapsuleCenter=new Vector3(0,.45f,0);
            s.Velocity=Vector3.forward*20; s.WaterUntil=1; s.WaterContactId=7;
            motor.RestoreState(s); motor.Simulate(Command(0,move:Vector2.up),Dt,0);
            Assert.That(motor.CaptureState().Velocity.z,Is.EqualTo(20).Within(.0001));
            s.Velocity=Vector3.forward*14.8f; motor.RestoreState(s);
            motor.Simulate(Command(0,move:Vector2.up),Dt,0);
            Assert.That(motor.CaptureState().Velocity.z,Is.EqualTo(14.85f).Within(.0001));
        }
        [Test] public void LaunchCapsCarryResetsAirKitAndRejectsDuplicateAction()
        {
            var s=Air(); s.Velocity=new Vector3(22,-18,0); s.AirDashUsed=true; s.WallRunsUsed=6;
            motor.RestoreState(s);
            Assert.That(motor.ApplyLaunch(5,1,11),Is.True);
            s=motor.CaptureState();
            Assert.That(s.Velocity,Is.EqualTo(new Vector3(9,11,0)));
            Assert.That(s.AirDashUsed,Is.False); Assert.That(s.WallRunsUsed,Is.Zero);
            Assert.That(motor.ApplyLaunch(5,1,11),Is.False);
            Assert.That(motor.CaptureState().EventSequence,Is.EqualTo(s.EventSequence));
        }
        [Test] public void LaunchDuringDashRearmsAndEndsDashWithoutVerticalPop()
        {
            motor.RestoreState(Air()); motor.Simulate(Command(0,MotorButtons.Dash),Dt,0);
            Assert.That(motor.ApplyLaunch(5,1,11),Is.True);
            var s=motor.CaptureState();
            Assert.That(s.Velocity,Is.EqualTo(new Vector3(0,0,9)));
            Assert.That(s.AirDashUsed,Is.False);
            Assert.That(s.DashUntil,Is.LessThanOrEqualTo(Dt));
        }
        [Test] public void ImpulseIsAppliedOnceAndRejectsNonfiniteValues()
        {
            Assert.That(motor.ApplyImpulse(new Vector3(3,4,5),1,-1),Is.True);
            Assert.That(motor.ApplyImpulse(new Vector3(3,4,5),1,-1),Is.False);
            Assert.That(motor.ApplyImpulse(new Vector3(float.PositiveInfinity,0,0),2,-1),Is.False);
            Assert.That(motor.CaptureState().Velocity,Is.EqualTo(new Vector3(3,4,5)));
        }
        [TestCase(-.1,true)] [TestCase(-.13,false)]
        public void CoyoteBoundaryExpiresAfterPointTwelveSeconds(double groundedAt,bool jumps)
        {
            var s=Air(); s.LastGroundedAt=groundedAt; motor.RestoreState(s);
            motor.Simulate(Command(0,MotorButtons.Jump),Dt,0);
            Assert.That(motor.CaptureState().Velocity.y>0,Is.EqualTo(jumps));
        }
        [TestCase(-.1,true)] [TestCase(-.13,false)]
        public void BufferedJumpBoundaryExpiresAfterPointTwelveSeconds(double pressedAt,bool jumps)
        {
            var s=Air(); s.LastGroundedAt=0; s.JumpPressedAt=pressedAt; motor.RestoreState(s);
            motor.Simulate(Command(0),Dt,0);
            Assert.That(motor.CaptureState().Velocity.y>0,Is.EqualTo(jumps));
        }
        [Test] public void HeldDashCannotSpendAgainWhenCooldownExpires()
        {
            motor.RestoreState(Air());
            for(int i=0;i<90;i++)
            {
                var c=Command(i,i==0?MotorButtons.Dash:MotorButtons.None); c.Held=MotorButtons.Dash; motor.Simulate(c,Dt,i);
            }
            Assert.That(motor.CaptureState().EventSequence,Is.EqualTo(1));
            Assert.That(motor.CaptureState().Stamina,Is.GreaterThan(70));
        }
        [Test] public void BufferReleaseAndRepressBetweenTicksExecutesASecondRearmedDashOnce()
        {
            motor.RestoreState(Air()); var buffer=new MotorInputBuffer();
            buffer.Sample(Vector2.up,MotorButtons.Dash);
            Assert.That(motor.Simulate(buffer.Consume(0,1,0,0),Dt,0),Is.True);
            Assert.That(motor.ApplyLaunch(5,1,11),Is.True);
            buffer.Sample(Vector2.up,MotorButtons.None);
            buffer.Sample(Vector2.up,MotorButtons.Dash);
            var command=buffer.Consume(1,1,0,0);
            Assert.That(command.Held,Is.EqualTo(MotorButtons.Dash));
            Assert.That(command.Pressed,Is.EqualTo(MotorButtons.Dash));
            Assert.That(motor.Simulate(command,Dt,1),Is.True);
            Assert.That(motor.CaptureState().Stamina,Is.EqualTo(40));
            Assert.That(motor.CaptureState().Velocity.z,Is.EqualTo(22));
            Assert.That(motor.Simulate(command,Dt,1),Is.False);
            Assert.That(motor.CaptureState().Stamina,Is.EqualTo(40));
        }
        [Test] public void OrdinaryOrbPopAfterDashEndsPreservesTheOutstandingCooldown()
        {
            motor.RestoreState(Air()); motor.Simulate(Command(0,MotorButtons.Dash),Dt,0);
            double ready=motor.CaptureState().DashReadyAt;
            for(int i=1;i<=23;i++) motor.Simulate(Command(i),Dt,i);
            Assert.That(motor.ApplyLaunch(5,1,11),Is.True);
            Assert.That(motor.CaptureState().DashReadyAt,Is.EqualTo(ready));
            Assert.That(motor.CaptureState().AirDashUsed,Is.False);
            motor.Simulate(Command(24,MotorButtons.Dash),Dt,24);
            Assert.That(motor.CaptureState().Stamina,Is.EqualTo(70));
            Assert.That(motor.CaptureState().Events&MotorEvents.Dash,Is.EqualTo(MotorEvents.None));
            for(int i=25;i<=72;i++) motor.Simulate(Command(i),Dt,i);
            motor.Simulate(Command(73,MotorButtons.Dash),Dt,73);
            Assert.That(motor.CaptureState().Events&MotorEvents.Dash,Is.EqualTo(MotorEvents.Dash));
            Assert.That(motor.CaptureState().Velocity.z,Is.EqualTo(22));
        }
        [Test] public void ReturningHeldInputAfterTimeoutCannotInventAPress()
        {
            motor.RestoreState(Air());
            var c=Command(0); c.Held=MotorButtons.Dash; motor.Simulate(c,Dt,0);
            for(int i=1;i<=13;i++) motor.Simulate(default,Dt,i);
            c=Command(14); c.Held=MotorButtons.Dash; motor.Simulate(c,Dt,14);
            Assert.That(motor.CaptureState().EventSequence,Is.Zero);
            Assert.That(motor.CaptureState().Stamina,Is.EqualTo(100));
        }
        [Test] public void SuppliedDeltaAndNonfiniteAimCannotChangeSimulation()
        {
            string before=JsonUtility.ToJson(motor.CaptureState());
            Assert.That(motor.Simulate(Command(0),1f/60,0),Is.False);
            var c=Command(0); c.Yaw=float.PositiveInfinity;
            Assert.That(motor.Simulate(c,Dt,0),Is.False);
            c=Command(0); c.Pitch=float.NaN;
            Assert.That(motor.Simulate(c,Dt,0),Is.False);
            Assert.That(JsonUtility.ToJson(motor.CaptureState()),Is.EqualTo(before));
        }
        [Test] public void StaminaDelayCrossingWaitsUntilNextTickThenRegeneratesAtAirRate()
        {
            var s=Air(); s.Stamina=50; s.StaminaDelay=.004f; motor.RestoreState(s);
            motor.Simulate(Command(0),Dt,0);
            Assert.That(motor.CaptureState().Stamina,Is.EqualTo(50));
            motor.Simulate(Command(1),Dt,1);
            Assert.That(motor.CaptureState().Stamina,Is.EqualTo(50.15f).Within(.00001));
        }
        [Test] public void SuppressionDropsActionsButNeverLocksLocalLook()
        {
            motor.RestoreState(Air()); motor.SetSuppressed(true);
            motor.Simulate(Command(0,MotorButtons.Jump|MotorButtons.Dash|MotorButtons.Slide,Vector2.up),Dt,0);
            Assert.That(motor.CaptureState().Stamina,Is.EqualTo(100));
            Assert.That(motor.CaptureState().Velocity.z,Is.Zero);
            var view=body.AddComponent<FirstPersonView>(); view.OwnsLocalView=true; view.SampleLook(new Vector2(20,5));
            Assert.That(view.Yaw,Is.EqualTo(20)); Assert.That(view.Pitch,Is.EqualTo(-5));
        }
        [Test] public void StunSuppressionLiftsAtItsEndTickWithoutAHostUpdate()
        {
            motor.RestoreState(Air()); motor.SetSuppressed(true,2);
            motor.Simulate(Command(0,MotorButtons.None,Vector2.up),Dt,0);
            motor.Simulate(Command(1,MotorButtons.None,Vector2.up),Dt,1);
            Assert.That(motor.CaptureState().Suppressed,Is.True);
            Assert.That(motor.CaptureState().Velocity.z,Is.Zero);
            motor.Simulate(Command(2,MotorButtons.None,Vector2.up),Dt,2);
            Assert.That(motor.CaptureState().Suppressed,Is.False);
            Assert.That(motor.CaptureState().SuppressedUntil,Is.Zero);
            Assert.That(motor.CaptureState().Velocity.z,Is.GreaterThan(0));
            motor.SetSuppressed(false,9);
            Assert.That(motor.CaptureState().SuppressedUntil,Is.Zero,"Only an active suppression carries a lift tick");
        }
        [Test] public void ReplayingOrPresentingRemoteEventsCannotDuplicateLocalFeedback()
        {
            var view=body.AddComponent<FirstPersonView>(); view.OwnsLocalView=true; int count=0;
            view.LocalEvents+=events=>count++;
            var s=Air(); s.Events=MotorEvents.Dash; s.EventSequence=1; view.Present(s); view.Present(s);
            s.EventSequence=0; view.Present(s); s.EventSequence=1; view.Present(s);
            Assert.That(count,Is.EqualTo(1));
            view.OwnsLocalView=false; s.EventSequence=2; view.Present(s);
            Assert.That(count,Is.EqualTo(1));
        }
        [Test] public void CorrectionRemovingDashStillPresentsALaterJumpWithTheSameOrdinal()
        {
            motor.RestoreState(Air()); var initial=motor.CaptureState();
            var view=body.AddComponent<FirstPersonView>(); view.OwnsLocalView=true;
            int dashes=0,jumps=0;
            view.LocalEvents+=events=> { if((events&MotorEvents.Dash)!=0) dashes++; if((events&MotorEvents.Jump)!=0) jumps++; };
            motor.Simulate(Command(0,MotorButtons.Dash),Dt,0); view.Present(motor.CaptureState());
            initial.Stamina=0; motor.RestoreState(initial);
            motor.Simulate(Command(0,MotorButtons.Dash),Dt,0); view.Present(motor.CaptureState());
            var grounded=motor.CaptureState(); grounded.LastGroundedAt=Dt; motor.RestoreState(grounded);
            motor.Simulate(Command(1,MotorButtons.Jump),Dt,1); var jumped=motor.CaptureState();
            Assert.That(jumped.EventSequence,Is.EqualTo(1));
            Assert.That(jumped.Events,Is.EqualTo(MotorEvents.Jump));
            view.Present(jumped); view.Present(jumped);
            Assert.That(dashes,Is.EqualTo(1)); Assert.That(jumps,Is.EqualTo(1));
        }
        [Test] public void TeleportDoesNotSweepGapOrReuseBufferedCoyoteAndWater()
        {
            var s=Air(); s.LastGroundedAt=0; s.JumpPressedAt=0; s.WaterUntil=10; s.WaterContactId=7;
            s.AirDashUsed=true; s.Stamina=44; motor.RestoreState(s);
            motor.Teleport(new Vector3(30,20,0),90);
            s=motor.CaptureState();
            Assert.That(s.Position,Is.EqualTo(new Vector3(30,20,0)));
            Assert.That(s.LastGroundedAt,Is.LessThan(-90)); Assert.That(s.JumpPressedAt,Is.LessThan(-90));
            Assert.That(s.WaterContactId,Is.Zero); Assert.That(s.Stamina,Is.EqualTo(44)); Assert.That(s.AirDashUsed,Is.True);
            motor.Simulate(Command(0),Dt,0);
            Assert.That(motor.CaptureState().Velocity.y,Is.LessThan(0));
        }
        [Test] public void InvalidTraversalSnapshotCannotPartlyRestorePose()
        {
            var s=Air(); s.WallElapsed=float.NaN;
            string before=JsonUtility.ToJson(motor.CaptureState());
            Assert.Throws<System.ArgumentException>(()=>motor.RestoreState(s));
            Assert.That(JsonUtility.ToJson(motor.CaptureState()),Is.EqualTo(before));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)] [TestCase(240)]
        public void FixedTickTrajectoryIsIndependentOfRenderSampling(int framesPerSecond)
        {
            var initial=Air(); motor.RestoreState(initial);
            for(int tick=0;tick<120;tick++) motor.Simulate(Command(tick,move:Vector2.up),Dt,tick);
            string expected=JsonUtility.ToJson(motor.CaptureState());
            motor.RestoreState(initial); var buffer=new MotorInputBuffer(); int lastFrame=-1;
            for(int tick=0;tick<120;tick++)
            {
                int frame=(tick+1)*framesPerSecond/120;
                while(lastFrame<frame) { buffer.Sample(Vector2.up,MotorButtons.None); lastFrame++; }
                motor.Simulate(buffer.Consume(tick,1,0,0),Dt,tick);
            }
            Assert.That(JsonUtility.ToJson(motor.CaptureState()),Is.EqualTo(expected));
        }
    }
}
