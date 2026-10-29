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
        void AssertNeutral(float fov)
        {
            Assert.That(camera.fieldOfView,Is.EqualTo(fov).Within(.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation,Quaternion.Euler(20,35,0)),Is.LessThan(.01f));
        }
    }
}
