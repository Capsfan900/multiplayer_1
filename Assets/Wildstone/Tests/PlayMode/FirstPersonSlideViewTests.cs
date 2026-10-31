using NUnit.Framework;
using UnityEngine;
using Wildstone.Movement;
using Wildstone.Presentation;
using Object=UnityEngine.Object;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class FirstPersonSlideViewTests
    {
        GameObject root;
        Camera camera;
        FirstPersonView view;
        [SetUp] public void SetUp()
        {
            root=new GameObject("Slide camera",typeof(Camera),typeof(FirstPersonView));
            camera=root.GetComponent<Camera>(); camera.fieldOfView=90;
            view=root.GetComponent<FirstPersonView>(); view.ViewCamera=camera; view.OwnsLocalView=true;
            view.ResetView(35,20);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }
        static MotorState Slide() => new MotorState { Generation=1,Position=new Vector3(1,2,3),
            CapsuleHeight=1.1f,Sliding=true,LastMove=Vector2.right };
        [Test] public void SlideAddsFeedbackWithoutMovingAimOriginOrDirection()
        {
            var state=Slide();
            for(int i=0;i<120;i++) view.RenderFrame(state,1f/60);
            Assert.That(Vector3.Distance(camera.transform.position,state.Position+Vector3.up*(state.CapsuleHeight-.15f)),Is.LessThan(.00001f));
            Assert.That(Vector3.Distance(camera.transform.forward,Quaternion.Euler(20,35,0)*Vector3.forward),Is.LessThan(.00001f));
            Assert.That(camera.fieldOfView,Is.EqualTo(96).Within(.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation,Quaternion.Euler(20,35,-2.5f)),Is.LessThan(.01f));
        }
        [Test] public void RepeatedFramesAndSettingsKeepAnAbsoluteBaseFov()
        {
            var state=Slide();
            for(int i=0;i<300;i++) { view.SetBaseFieldOfView(100); view.RenderFrame(state,1f/60); }
            Assert.That(camera.fieldOfView,Is.EqualTo(106).Within(.001f));
            state.Sliding=false;
            for(int i=0;i<120;i++) view.RenderFrame(state,1f/60);
            Assert.That(camera.fieldOfView,Is.EqualTo(100).Within(.001f));
        }
        [Test] public void ReducedMotionAndResetsRemoveFeedback()
        {
            var state=Slide(); view.RenderFrame(state,1);
            view.ReducedMotion=true; view.RenderFrame(state,1f/60);
            AssertNeutral(90);
            view.ReducedMotion=false; view.RenderFrame(state,1);
            state.Generation++; state.Sliding=false;
            view.RenderFrame(state,0); AssertNeutral(90);
            state.Sliding=true; view.RenderFrame(state,1);
            state.Position+=Vector3.forward*10; state.Sliding=false;
            view.RenderFrame(state,0); AssertNeutral(90);
            state.Sliding=true; view.RenderFrame(state,1);
            view.ResetPresentation(); AssertNeutral(90);
        }
        [Test] public void EasingIsIndependentOfFramePartition()
        {
            var state=Slide(); view.RenderFrame(state,.1f);
            float single=camera.fieldOfView;
            view.ResetPresentation();
            for(int i=0;i<10;i++) view.RenderFrame(state,.01f);
            Assert.That(camera.fieldOfView,Is.EqualTo(single).Within(.0001f));
        }
        // The shot punch tips the camera up, then recovers; the aim angles never move.
        [Test] public void ShotPunchRecoversWithoutMovingAim()
        {
            var state=new MotorState { Generation=1,Position=new Vector3(1,2,3),CapsuleHeight=1.8f };
            view.RenderFrame(state,1f/60);
            view.AddRecoil(WeaponView.ViewPunch(Wildstone.Combat.WeaponKind.LaserSniper)); view.RenderFrame(state,1f/60);
            Assert.That(camera.transform.eulerAngles.x,Is.LessThan(20-.8f),"The camera tips up.");
            Assert.That(view.Pitch,Is.EqualTo(20)); Assert.That(view.Yaw,Is.EqualTo(35));
            for(int i=0;i<30;i++) view.RenderFrame(state,1f/60);
            AssertNeutral(90);
            view.ReducedMotion=true; view.AddRecoil(1); view.RenderFrame(state,1f/60);
            AssertNeutral(90);
        }
        // A dash widens the lens, a wall run holds it wider, ADS fades both; all settle back without moving aim.
        [Test] public void MovementKicksWidenTheLensThenSettle()
        {
            var state=new MotorState { Generation=1,Position=new Vector3(1,2,3),CapsuleHeight=1.8f,Grounded=true };
            view.RenderFrame(state,1f/60); AssertNeutral(90);
            state.Events=MotorEvents.Dash; state.EventTick=2; view.RenderFrame(state,1f/60);
            Assert.That(camera.fieldOfView,Is.GreaterThan(93));
            Assert.That(view.Pitch,Is.EqualTo(20)); Assert.That(view.Yaw,Is.EqualTo(35));
            for(int i=0;i<60;i++) view.RenderFrame(state,1f/60);
            AssertNeutral(90);
            state.WallRunning=true; state.WallNormal=Vector3.right;
            for(int i=0;i<120;i++) view.RenderFrame(state,1f/60);
            Assert.That(camera.fieldOfView,Is.EqualTo(90+FirstPersonView.WallRunHold).Within(.001f));
            view.SetAds(Wildstone.Combat.WeaponKind.LineDriver,1); view.RenderFrame(state,1f/60);
            Assert.That(camera.fieldOfView,Is.EqualTo(Wildstone.Combat.WeaponCatalog.AdsVerticalFov(90,Wildstone.Combat.WeaponKind.LineDriver,1)).Within(.001f));
            view.SetAds(Wildstone.Combat.WeaponKind.LineDriver,0); state.WallRunning=false;
            for(int i=0;i<120;i++) view.RenderFrame(state,1f/60);
            view.ReducedMotion=true; state.Events=MotorEvents.Dash; state.EventTick=9; view.RenderFrame(state,1f/60);
            AssertNeutral(90);
        }
        // A hard landing nods the view down and narrows the lens in proportion to the fall; the eye never moves, and
        // stepping off a kerb does nothing.
        [Test] public void HardLandingsNodTheViewAndKerbStepsDoNot()
        {
            float eye=2+1.8f-.15f;
            void Land(float fall,int tick)
            {
                var air=new MotorState { Generation=1,Position=new Vector3(1,2,3),CapsuleHeight=1.8f,Velocity=Vector3.down*fall };
                view.RenderFrame(air,1f/60);
                var landed=air; landed.Grounded=true; landed.Velocity=Vector3.zero; landed.Events=MotorEvents.Land; landed.EventTick=tick;
                view.RenderFrame(landed,1f/60);
            }
            Land(1,3); AssertNeutral(90);
            Land(FirstPersonView.HardLanding,6);
            Assert.That(camera.transform.position.y,Is.EqualTo(eye).Within(.00001f),"The eye stays authoritative.");
            Assert.That(camera.transform.eulerAngles.x,Is.GreaterThan(21.5f),"The view nods down.");
            Assert.That(camera.fieldOfView,Is.LessThan(88.5f));
            Assert.That(view.Pitch,Is.EqualTo(20));
            var rest=new MotorState { Generation=1,Position=new Vector3(1,2,3),CapsuleHeight=1.8f,Grounded=true };
            for(int i=0;i<60;i++) view.RenderFrame(rest,1f/60);
            AssertNeutral(90);
        }
        // A perfect wall jump (its stamina refund is otherwise invisible) kicks the lens wider than a plain one.
        [Test] public void PerfectWallJumpKicksWiderThanAPlainOne()
        {
            var state=new MotorState { Generation=1,Position=new Vector3(1,2,3),CapsuleHeight=1.8f,LastWallNormal=Vector3.right };
            view.RenderFrame(state,1f/60);
            state.Events=MotorEvents.WallJump; state.EventTick=2; view.RenderFrame(state,1f/60);
            float plain=camera.fieldOfView;
            Assert.That(plain,Is.GreaterThan(92));
            view.ResetPresentation();
            state.Events=MotorEvents.WallJump|MotorEvents.Perfect; state.EventTick=4; view.RenderFrame(state,1f/60);
            Assert.That(camera.fieldOfView,Is.GreaterThan(plain+1.5f));
        }
        // A strafe dash banks the view into its travel and a slide entry dips the nose; both recover, aim untouched.
        [Test] public void StrafeDashBanksAndSlideEntryNods()
        {
            var state=new MotorState { Generation=1,Position=new Vector3(1,2,3),CapsuleHeight=1.8f,Grounded=true };
            view.RenderFrame(state,1f/60);
            state.DashDirection=Quaternion.Euler(0,35,0)*Vector3.right; state.Events=MotorEvents.Dash; state.EventTick=2;
            view.RenderFrame(state,1f/60);
            Assert.That(Mathf.DeltaAngle(0,camera.transform.eulerAngles.z),Is.LessThan(-1),"A right dash banks clockwise into the travel.");
            state.Events=MotorEvents.Slide; state.EventTick=4; view.RenderFrame(state,1f/60);
            Assert.That(camera.transform.eulerAngles.x,Is.GreaterThan(20.8f),"The slide entry dips the nose.");
            Assert.That(view.Pitch,Is.EqualTo(20)); Assert.That(view.Yaw,Is.EqualTo(35));
            for(int i=0;i<60;i++) view.RenderFrame(state,1f/60);
            AssertNeutral(90);
        }
        void AssertNeutral(float fov)
        {
            Assert.That(camera.fieldOfView,Is.EqualTo(fov).Within(.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation,Quaternion.Euler(20,35,0)),Is.LessThan(.01f));
        }
    }
}
