using System;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;
namespace Wildstone.Tests
{
    [Category("Managed")]
    public class SourcePrefabParityTests
    {
        // Captured independently from Player.prefab at 2cb6e576, not from MotorTuning.
        // airStrafeWishSpeed (source 2) is no longer retained: D041 retunes it to 2.5 (foundation spec, section 5).
        const string Baseline=@"
groundSpeed=11
groundAccel=90
groundFriction=14
airAccel=35
gravity=-30
jumpHeight=2.4
jumpCutGravityMultiplier=1.2
coyoteTime=0.12
jumpBuffer=0.12
dashSpeed=22
dashDuration=0.16
dashCooldown=0.6
slideBoost=5
slideMaxSpeed=22
slideMinEntrySpeed=5
slideFriction=2
slideEndSpeed=8
slideMaxDuration=0.9
slideSlopeAccel=0.85
slideCooldown=0.3
slideHeight=0.9
slideSteerAccel=8
wallCheckDistance=0.55
wallJumpUpSpeed=11
wallJumpPushSpeed=12
maxWallJumps=5
sameWallCosineLimit=0.85
wallRunMinEntrySpeed=6
wallRunMaxEntryFallSpeed=9
wallRunMaxApproachCos=0.8
wallRunMinLookAlongCos=-0.05
wallRunMaxDuration=1.75
wallRunGravityStartScale=0.1
wallRunGravityEndScale=0.6
wallRunEntryUpSpeed=3
wallRunSpeedDecay=0.35
wallRunMinSustainSpeed=4
wallRunAccel=14
wallRunStickSpeed=2.5
wallRunReleaseCos=0.5
wallRunExitUpSpeed=10
wallRunExitPushSpeed=7
wallRunExitTangentBoost=4
maxWallRuns=6
wallRunCooldown=0.2
wallRunCameraRoll=13
wallRunExitRollKick=7
wallRunTopSpeed=13.75
wallRunLostGrace=0.15
wallRunExitGrace=0.3
airSoftCap=17.6
airDrag=3
groundOverspeedDecay=4
maxHorizontalSpeed=27.5
slideChainWindow=1.2
slideChainFalloff=0.6
fallGravityMultiplier=1.5
airCarryDecay=0.8
airSteerDegPerSec=120
landingSoftSpeed=16
landingHardSpeed=26
landingSpeedLoss=0.35
groundSnapDistance=0.12
launchFloatSeconds=0.45
launchGravityScale=0.55
launchSteerBoost=1.6
launchCarryCap=9
waterSpeedScale=1.35
waterAccel=30
waterGrace=0.15
perfectWallJumpWindow=0.2
perfectWallJumpRefund=20
perfectDashJumpMinDelay=0.06
perfectDashJumpWindow=0.1
perfectDashJumpRefund=30
cornerCorrectionMetres=0.18
ledgeCatchMetres=0.22
ledgeCatchLiftSpeed=6
ledgeCatchMinSpeed=1.5
max=100
regenPerSecondGrounded=45
regenPerSecondAirborne=18
regenDelay=0.45
dashCost=30
wallRunEntryCost=12
wallRunDrainPerSecond=22
wallJumpCost=12
slideCost=12
height=1.8
radius=0.4
slopeLimit=45
stepOffset=0.4
skinWidth=0.05
minMoveDistance=0.001
";
        [Test] public void EveryRetainedTuningValueMatchesTheAuditedSourcePrefab()
        {
            foreach(var row in Baseline.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))
            {
                var pair=row.Split('='); var field=typeof(MotorTuning).GetField(pair[0]);
                Assert.That(field,Is.Not.Null,pair[0]);
                Assert.That(Convert.ToSingle(field.GetValue(MotorTuning.Source)),
                    Is.EqualTo(float.Parse(pair[1],CultureInfo.InvariantCulture)),pair[0]);
            }
            Assert.That(MotorTuning.Source.center,Is.EqualTo(new Vector3(0,.9f,0)));
        }
    }
}
