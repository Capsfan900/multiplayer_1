using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;
using Wildstone.Presentation;
using Wildstone.UI;

namespace Wildstone.Tests
{
    // D028 local body: animated soldier legs in view, nothing above the chest cut, full-body shadow kept.
    [TestFixture,Category("Native")]
    public sealed class FirstPersonBodyTests
    {
        GameObject root;
        FirstPersonBody body;
        CombatState player;
        MotorState state;
        [SetUp] public void SetUp()
        {
            root=new GameObject("LocalBodyTest");
            body=root.AddComponent<FirstPersonBody>();
            body.Initialize(Resources.Load<PresentationCatalog>("WildstonePresentation"),0);
            player=new CombatState { Alive=true, Connected=true, LifeGeneration=1 };
            state=new MotorState { Generation=1, Grounded=true, CapsuleHeight=1.8f };
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }
        Transform Visible => root.transform.Find("Local body");
        Transform Bone(string name) => GripAttachment.Find(Visible,name);
        static bool Shown(Transform model) => System.Array.Exists(model.GetComponentsInChildren<Renderer>(false),renderer=>renderer.enabled);

        [Test] public void NothingVisibleRisesAboveTheChestCut()
        {
            state.Velocity=Vector3.forward*6;
            for(int i=0;i<90;i++) body.Present(player,state,1f/60,false);
            int skinned=0;
            foreach(var renderer in Visible.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if(!renderer.enabled) continue;
                Assert.That(renderer.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),renderer.name);
                var mesh=new Mesh();
                try
                {
                    renderer.BakeMesh(mesh,true);
                    var space=Matrix4x4.TRS(renderer.transform.position,renderer.transform.rotation,Vector3.one);
                    foreach(var vertex in mesh.vertices)
                        Assert.That(space.MultiplyPoint3x4(vertex).y,Is.LessThan(FirstPersonBody.VisibleCeiling+.06f),renderer.name+" pokes into the lens");
                    skinned++;
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            Assert.That(skinned,Is.GreaterThan(0),"The visible body renders legs.");
            foreach(var renderer in root.transform.Find("Local body shadow").GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly),renderer.name);
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
        }

        // Players saw their own legs in the view mid-slide (folded shins, cut knees, a boot pile under the crosshair): from
        // the run into the slide and until it settles the visible body hides at every frame, the shadow copy stays whole,
        // and the legs return once the slide has settled.
        [TestCase(false)] [TestCase(true)]
        public void SlideHidesTheVisibleBodyAndKeepsItsShadow(bool reducedMotion)
        {
            var shadow=root.transform.Find("Local body shadow");
            state.Velocity=Vector3.forward*9;
            for(int i=0;i<60;i++) body.Present(player,state,1f/60,reducedMotion);
            Assert.That(Shown(Visible),Is.True,"Running shows the legs.");
            state.Sliding=true; state.Velocity=Vector3.forward*16; state.CapsuleHeight=.9f;
            string before=JsonUtility.ToJson(state);
            for(int frame=1;frame<=120;frame++)
            {
                body.Present(player,state,1f/120,reducedMotion);
                Assert.That(Shown(Visible),Is.False,"Slide frame "+frame+" shows the visible body.");
                Assert.That(Shown(shadow),Is.True,"Slide frame "+frame+" drops the shadow.");
            }
            Assert.That(JsonUtility.ToJson(state),Is.EqualTo(before),"Presentation writes no motor state.");
            state.Sliding=false; state.Velocity=Vector3.forward*9; state.CapsuleHeight=1.8f;
            body.Present(player,state,1f/120,reducedMotion);
            Assert.That(Shown(Visible),Is.False,"The legs stay hidden while the slide settles.");
            for(int i=0;i<60;i++) body.Present(player,state,1f/120,reducedMotion);
            Assert.That(Shown(Visible),Is.True,"The legs return once the slide has settled.");
        }

        [Test] public void LookingDownFindsStandingFeet()
        {
            var cameraObject=new GameObject("DownCamera");
            try
            {
                for(int i=0;i<10;i++) body.Present(player,state,1f/60,false);
                var camera=cameraObject.AddComponent<Camera>(); camera.fieldOfView=60;
                camera.transform.SetPositionAndRotation(Vector3.up*1.65f,Quaternion.Euler(80,0,0));
                foreach(string side in new[]{"Left","Right"})
                    Assert.That(camera.WorldToViewportPoint(Bone(side+"Foot").position).y,Is.InRange(0f,1f),side);
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        [Test] public void DeathTeleportAndNewLifeClearTheSlideHide()
        {
            state.Sliding=true;
            for(int i=0;i<30;i++) body.Present(player,state,1f/60,false);
            Assert.That(Shown(Visible),Is.False);
            player.Alive=false; body.Present(player,state,1f/60,false);
            Assert.That(Visible.gameObject.activeSelf,Is.False);
            player.Alive=true; player.LifeGeneration++; state.Generation++; state.Sliding=false;
            body.Present(player,state,0,false);
            Assert.That(Shown(Visible),Is.True,"A new life shows the legs at once.");
            state.Sliding=true;
            for(int i=0;i<30;i++) body.Present(player,state,1f/60,false);
            state.Position=Vector3.right*20; state.Sliding=false;
            body.Present(player,state,0,false);
            Assert.That(Shown(Visible),Is.True,"A teleport shows the legs at once.");
        }
    }
}
