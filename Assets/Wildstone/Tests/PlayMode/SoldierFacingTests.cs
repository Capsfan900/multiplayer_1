using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    // D028 soldier: the imported Humanoid and its clips must face the transform's +Z, or remote players run backwards.
    [TestFixture,Category("Native")]
    public sealed class SoldierFacingTests
    {
        GameObject model;
        Animator animator;
        [SetUp] public void SetUp()
        {
            var catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            model=Object.Instantiate(catalog.Humans[0]);
            animator=model.GetComponentInChildren<Animator>();
            animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; // no camera in batch tests
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(model); }
        Vector3 Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone).position;
        string Pose() => "hips "+Bone(HumanBodyBones.Hips)+" head "+Bone(HumanBodyBones.Head)+" feet L "+Bone(HumanBodyBones.LeftFoot)+" R "+Bone(HumanBodyBones.RightFoot)+
            " toes L "+Bone(HumanBodyBones.LeftToes)+" R "+Bone(HumanBodyBones.RightToes)+" hand R "+Bone(HumanBodyBones.RightHand);
        void Play(MotorState motor,float seconds)
        {
            for(float t=0;t<seconds;t+=1f/60)
            {
                SoldierRig.DriveLocomotion(animator,motor,0,true,false);
                animator.Update(1f/60);
            }
        }

        [Test] public void IdleToesAndAimingHandPointAlongTransformForward()
        {
            Play(new MotorState { Grounded=true },.5f);
            string pose=Pose();
            Assert.That(Bone(HumanBodyBones.LeftToes).z,Is.GreaterThan(Bone(HumanBodyBones.LeftFoot).z),pose);
            Assert.That(Bone(HumanBodyBones.RightToes).z,Is.GreaterThan(Bone(HumanBodyBones.RightFoot).z),pose);
            Assert.That(Bone(HumanBodyBones.RightHand).z,Is.GreaterThan(Bone(HumanBodyBones.Hips).z),pose);
        }
        [Test] public void SlideExtendsALegAheadOfTheHips()
        {
            Play(new MotorState { Grounded=true,Sliding=true,Velocity=Vector3.forward*14 },1);
            string pose=Pose();
            float lead=Mathf.Max(Bone(HumanBodyBones.LeftFoot).z,Bone(HumanBodyBones.RightFoot).z);
            Assert.That(lead-Bone(HumanBodyBones.Hips).z,Is.GreaterThan(.5f),pose);
            Assert.That(Bone(HumanBodyBones.Hips).y,Is.LessThan(.7f),pose);
        }
    }
}
