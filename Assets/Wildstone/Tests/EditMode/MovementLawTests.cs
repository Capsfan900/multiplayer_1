using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;

namespace Wildstone.Tests
{
    [Category("Managed")]
    public class MovementLawTests
    {
        static WallRunMath.Params Wall => new WallRunMath.Params {
            gravity=-30,minEntrySpeed=6,maxEntryFallSpeed=9,maxApproachCos=.8f,minLookAlongCos=-.05f,
            maxDuration=1.75f,gravityStartScale=.1f,gravityEndScale=.6f,entryUpSpeed=3,speedDecay=.35f,
            minSustainSpeed=4,accel=14,topSpeed=13.75f,maxSpeed=22,exitUpSpeed=10,exitPushSpeed=7,exitTangentBoost=4 };
        [Test] public void AimBitIsCombatIntentThatTheMotorNeverSees()
        {
            var input=new MotorInput { Present=true,Held=MotorButtons.Aim|MotorButtons.Slide,Pressed=MotorButtons.Aim|MotorButtons.Jump };
            Assert.That(input.Aiming,Is.True);
            var motor=input.ForMotor();
            Assert.That(motor.Held,Is.EqualTo(MotorButtons.Slide)); Assert.That(motor.Pressed,Is.EqualTo(MotorButtons.Jump)); Assert.That(motor.Aiming,Is.False);
            Assert.That(input.Held,Is.EqualTo(MotorButtons.Aim|MotorButtons.Slide),"ForMotor copies; the combat sample keeps its aim bit.");
        }
        [Test] public void BackwardIntentBrakesThroughAcceleration()
        {
            Assert.That(AirMath.AirAccelerate(Vector3.forward*11,Vector3.back,11,35,.1f).z,Is.EqualTo(7.5f));
        }
        [Test] public void SidewaysStrafeCanOnlyAddItsTwoMetreWishSpeed()
        {
            var v=AirMath.AirStrafe(Vector3.forward*11,Vector3.right,2,35,1);
            Assert.That(v,Is.EqualTo(new Vector3(2,0,11)));
            Assert.That(AirMath.AirStrafe(v,Vector3.right,2,35,1),Is.EqualTo(v));
        }
        [Test] public void ExcessDecayIsExponentialAndCannotCrossTheCap()
        {
            Assert.That(AirMath.DecayExcess(Vector3.forward*21,11,1,1).z,Is.EqualTo(14.678795f).Within(.00001));
            Assert.That(AirMath.DecayExcess(Vector3.forward*8,11,1,1).z,Is.EqualTo(8));
        }
        [TestCase(-1,false,-45)] [TestCase(1,false,-30)] [TestCase(-1,true,-30)]
        public void ExtraGravityOnlyAppliesWhenFallingAirborne(float vertical,bool grounded,float expected)
            => Assert.That(AirMath.FallGravity(-30,vertical,grounded,1.5f),Is.EqualTo(expected));
        [TestCase(12,1)] [TestCase(21,.825f)] [TestCase(26,.65f)]
        public void LandingKeepsNormalJumpMomentum(float falling,float expected)
            => Assert.That(AirMath.LandingSpeedFactor(falling,16,26,.35f),Is.EqualTo(expected).Within(.00001));
        [Test] public void SlopeUsesWorldGravityInsteadOfMotorGravity()
        {
            Assert.That(TraversalMath.SlopeAccel(Vector3.up,.85f),Is.EqualTo(Vector3.zero));
            var a=TraversalMath.SlopeAccel(new Vector3(1,4,0),.85f);
            Assert.That(a.x,Is.EqualTo(1.962f).Within(.00001));
            Assert.That(a.y,Is.Zero);
        }
        [Test] public void DownhillRequiresRealContactAndPreservesOverspeed()
        {
            var normal=new Vector3(1,4,0).normalized;
            Assert.That(TraversalMath.SustainsDownhillSlide(Vector3.right*20,normal,.85f,45,false),Is.False);
            Assert.That(TraversalMath.SustainsDownhillSlide(Vector3.right*20,normal,.85f,45,true),Is.True);
            Assert.That(TraversalMath.DrySlideStep(Vector3.right*25,normal,.85f,2,22,1,true).x,Is.EqualTo(25).Within(.00001));
        }
        [Test] public void DownhillSnapIncludesSkinContactMargin()
            => Assert.That(TraversalMath.DownhillSnapY(new Vector3(1,-.02f,0),new Vector3(1,4,0),.12f),Is.EqualTo(-.37f).Within(.00001));
        [Test] public void StillWaterDoesNotPullAnIdleBodyAndFlowIsAddedOnce()
        {
            Assert.That(TraversalMath.WaterStep(Vector3.zero,Vector3.zero,14.85f,30,.1f),Is.EqualTo(Vector3.zero));
            Assert.That(TraversalMath.WaterVelocity(Vector3.forward*20,Vector3.right*6),Is.EqualTo(new Vector3(6,0,20)));
        }
        [Test] public void WaterRaisesFloorAndPreservesTwentyMetresPerSecondCarry()
        {
            Assert.That(TraversalMath.WaterStep(Vector3.forward*14.8f,Vector3.forward,14.85f,30,1f/120).z,Is.EqualTo(14.85f));
            Assert.That(TraversalMath.WaterStep(Vector3.forward*20,Vector3.forward,14.85f,30,1f/120).z,Is.EqualTo(20));
        }
        [Test] public void PopCapsHorizontalCarryAndReplacesVerticalSpeed()
            => Assert.That(TraversalMath.Launch(new Vector3(22,-18,0),11,9),Is.EqualTo(new Vector3(9,11,0)));
        [Test] public void WallEntryRedirectsTotalSpeedAndNeverSubtractsExistingRise()
        {
            Assert.That(WallRunMath.CanEnter(new Vector3(4,8,8),Vector3.left,Vector3.forward,Wall,out var dir,out _),Is.True);
            var v=WallRunMath.Enter(new Vector3(4,8,8),dir,Wall);
            Assert.That(v.x,Is.EqualTo(0).Within(.00001)); Assert.That(v.y,Is.EqualTo(8));
            Assert.That(v.z,Is.EqualTo(8.944272f).Within(.00001));
        }
        [TestCase(0,0,0,0)] [TestCase(11,0,0,1)] [TestCase(0,-10,11,2)]
        public void WallRefusesStandstillHeadOnAndFastFall(float x,float y,float z,int unused)
            => Assert.That(WallRunMath.CanEnter(new Vector3(x,y,z),Vector3.left,Vector3.forward,Wall,out _,out _),Is.False);
        [Test] public void WallRefusesLookingBackwardAndEntryFloorsRise()
        {
            Assert.That(WallRunMath.CanEnter(Vector3.forward*11,Vector3.left,Vector3.back,Wall,out _,out _),Is.False);
            Assert.That(WallRunMath.Enter(Vector3.forward*11,Vector3.forward,Wall).y,Is.EqualTo(3));
        }
        [Test] public void WallAdvanceConsumesOnlyRemainingDuration()
        {
            WallRunMath.Advance(new Vector3(0,3,11),Vector3.forward,1.749f,true,Wall,1f/120,out var disp,out float used);
            Assert.That(used,Is.EqualTo(.001f).Within(.000001));
            Assert.That(disp.z,Is.InRange(.0109f,.0111f));
        }
        [Test] public void HoldingForwardCannotAcceleratePastWallTopSpeed()
        {
            var v=new Vector3(0,3,11);
            for(int i=0;i<120;i++) v=WallRunMath.Advance(v,Vector3.forward,i/120f,true,Wall,1f/120,out _,out _);
            Assert.That(v.z,Is.EqualTo(13.75f).Within(.0001));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)] [TestCase(240)]
        public void WallIntegrationRemainsWithinACentimetreOfAnalyticUnforcedRun(int rate)
        {
            Vector3 v=new Vector3(0,3,11),position=Vector3.zero;
            float elapsed=0;
            for(int i=0;i<1000 && elapsed<1.75f;i++)
            {
                v=WallRunMath.Advance(v,Vector3.forward,elapsed,false,Wall,1f/rate,out var disp,out var used);
                elapsed+=used; position+=disp;
            }
            // Integrals of v=11 exp(-.35t) and y''=-3-15(t/1.75)^2, starting y'=3.
            Assert.That(elapsed,Is.EqualTo(1.75f).Within(.000001));
            Assert.That(position.z,Is.EqualTo(14.395944f).Within(.01));
            Assert.That(position.y,Is.EqualTo(-3.171875f).Within(.02));
        }
        [Test] public void WallExitRemainsCappedAndSameFaceCannotBeReused()
        {
            var exit=WallRunMath.PerfectExit(Vector3.forward*22,Vector3.left,Vector3.forward,Wall,22);
            Assert.That(new Vector2(exit.x,exit.z).magnitude,Is.EqualTo(22).Within(.00001));
            Assert.That(exit.y,Is.EqualTo(10));
            Assert.That(WallRunMath.IsSameWall(Vector3.left,Vector3.left,.85f),Is.True);
            Assert.That(WallRunMath.IsSameWall(Vector3.left,Vector3.right,.85f),Is.False);
        }
        [TestCase(0,false)] [TestCase(.05f,false)] [TestCase(.06f,true)] [TestCase(.16f,true)] [TestCase(.17f,false)]
        public void DashPerfectWindowCannotRewardASimultaneousPress(float since,bool expected)
            => Assert.That(PerfectMath.DashJumpIsPerfect(since,.06f,.1f),Is.EqualTo(expected));
        [Test] public void RefundNeverOverfillsOrDebitsStamina()
        {
            Assert.That(PerfectMath.Refund(88,100,30),Is.EqualTo(100));
            Assert.That(PerfectMath.Refund(88,100,-30),Is.EqualTo(88));
        }
        [Test] public void ForgivenessIsBoundedAndNeverCatchesAWallOrStandingDrop()
        {
            Assert.That(ForgivenessMath.CornerCorrects(.18f,.18f,3),Is.True);
            Assert.That(ForgivenessMath.CornerCorrects(.19f,.18f,3),Is.False);
            Assert.That(ForgivenessMath.CornerCorrects(.1f,.18f,-3),Is.False);
            Assert.That(ForgivenessMath.LedgeCatches(0,.2f,.22f,-3,1,.7f,2,1.5f),Is.True);
            Assert.That(ForgivenessMath.LedgeCatches(0,.2f,.22f,-3,0,.7f,2,1.5f),Is.False);
            Assert.That(ForgivenessMath.LedgeCatches(0,.2f,.22f,-3,1,.7f,0,1.5f),Is.False);
            Assert.That(ForgivenessMath.NudgeStep(.2f,6,1f/120),Is.EqualTo(.05f).Within(.00001));
        }
        [Test] public void PressAndReleaseBetweenTicksFiresOnce()
        {
            var buffer=new MotorInputBuffer();
            buffer.Sample(Vector2.up,MotorButtons.Jump|MotorButtons.Dash);
            buffer.Sample(Vector2.up,MotorButtons.None);
            var first=buffer.Consume(0,1,0,0); var second=buffer.Consume(1,1,0,0);
            Assert.That(first.Pressed,Is.EqualTo(MotorButtons.Jump|MotorButtons.Dash));
            Assert.That(first.Held,Is.EqualTo(MotorButtons.None));
            Assert.That(second.Pressed,Is.EqualTo(MotorButtons.None));
            Assert.That(second.Sequence,Is.GreaterThan(first.Sequence));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)] [TestCase(240)]
        public void RenderSamplingCannotMultiplyHeldActionEdges(int framesPerSecond)
        {
            var buffer=new MotorInputBuffer(); int presses=0;
            int lastFrame=-1;
            for(int tick=0;tick<120;tick++)
            {
                int frame=(tick+1)*framesPerSecond/120;
                while(lastFrame<frame) { buffer.Sample(Vector2.up,MotorButtons.Dash); lastFrame++; }
                if((buffer.Consume(tick,1,0,0).Pressed&MotorButtons.Dash)!=0) presses++;
            }
            Assert.That(presses,Is.EqualTo(1));
        }
    }

    public class NativeAirLawTests
    {
        [Test] public void BackwardIntentNeverRotatesMomentum()
            => Assert.That(AirMath.AirSteer(Vector3.forward*11,Vector3.back,120,.1f),Is.EqualTo(Vector3.forward*11));
        [Test] public void AirSteerTurnsThirtyDegreesWithoutCreatingSpeed()
        {
            var v=AirMath.AirSteer(Vector3.forward*10,Vector3.right,120,.25f);
            Assert.That(v.x,Is.EqualTo(5).Within(.00001));
            Assert.That(v.z,Is.EqualTo(8.660254f).Within(.00001));
            Assert.That(v.magnitude,Is.EqualTo(10).Within(.00001));
        }
    }
}
