using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
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
            player=new CombatState { Alive=true, LifeGeneration=1 };
            state=new MotorState { Generation=1, Grounded=true, CapsuleHeight=.9f };
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }

        [Test] public void AllSixLegMeshesAreEnabledWithVisibleSkinGeometry()
        {
            state.Sliding=true; state.Velocity=Vector3.forward*16;
            for(int i=0;i<120;i++) body.Present(player,state,1f/120,false);
            var all=root.GetComponentsInChildren<Renderer>(true);
            foreach(var renderer in all) Debug.Log("LOCAL_BODY_RENDERER "+renderer.name+" enabled="+renderer.enabled+" type="+renderer.GetType().Name+" bounds="+renderer.bounds);
            foreach(string name in new[]{"ThighL","ThighR","ShinL","ShinR","FootL","FootR"})
            {
                var renderer=System.Array.Find(all,r=>r.name==name);
                Assert.That(renderer,Is.Not.Null,name);
                Assert.That(renderer.enabled,Is.True,name);
                var mesh=new Mesh();
                try
                {
                    ((SkinnedMeshRenderer)renderer).BakeMesh(mesh);
                    Assert.That(mesh.vertexCount,Is.GreaterThan(0),name);
                    Debug.Log("LOCAL_BODY_BAKED "+name+" bounds="+mesh.bounds+" object="+renderer.transform.localToWorldMatrix);
                    var vertices=mesh.vertices; var triangles=mesh.triangles;
                    Vector3 eye=new Vector3(0,.75f,-.05f);
                    int facing=0,outward=0,projected=0;
                    Vector3 center=renderer.transform.TransformPoint(mesh.bounds.center);
                    Vector3 viewForward=Quaternion.Euler(45,0,0)*Vector3.forward;
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        Vector3 a=renderer.transform.TransformPoint(vertices[triangles[i]]),b=renderer.transform.TransformPoint(vertices[triangles[i+1]]),c=renderer.transform.TransformPoint(vertices[triangles[i+2]]);
                        Vector3 middle=(a+b+c)/3,normal=Vector3.Cross(b-a,c-a);
                        if(Vector3.Dot(normal,eye-middle)>0) facing++;
                        if(Vector3.Dot(normal,middle-center)>0) outward++;
                        if(Vector3.Angle(viewForward,middle-eye)<45) projected++;
                    }
                    Debug.Log("LOCAL_BODY_TRIANGLES "+name+" facing="+facing+" outward="+outward+" projected="+projected+" total="+triangles.Length/3+" material="+renderer.sharedMaterial.name);
                    Assert.That(facing,Is.GreaterThan(0),name+" has no outward triangles toward camera");
                    Assert.That(projected,Is.GreaterThan(0),name+" has no triangles in downward camera frustum");
                    Assert.That(outward,Is.GreaterThan(triangles.Length/6),name+" has inward surface winding");
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test,Category("Graphics"),Explicit("Requires a native graphics device.")]
        public void EachLegSegmentRendersWithBackfaceCulling()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            state.Sliding=true; state.Velocity=Vector3.forward*16;
            for(int i=0;i<120;i++) body.Present(player,state,1f/120,false);
            var cameraObject=new GameObject("LegRenderProbe");
            var target=new RenderTexture(128,128,24);
            var pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var previous=RenderTexture.active;
            try
            {
                material.SetColor("_BaseColor",Color.magenta);
                var camera=cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(Vector3.up*.75f,Quaternion.Euler(30,0,0));
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
                camera.cullingMask=1<<30; camera.fieldOfView=90; camera.targetTexture=target;
                foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=30;
                var renderers=root.GetComponentsInChildren<Renderer>(true);
                foreach(var renderer in renderers) renderer.enabled=false;
                foreach(string name in new[]{"ThighL","ThighR","ShinL","ShinR","FootL","FootR"})
                {
                    var renderer=System.Array.Find(renderers,r=>r.name==name);
                    renderer.enabled=true; renderer.sharedMaterial=material;
                    int[] counts=new int[2];
                    for(int mode=0;mode<2;mode++)
                    {
                        material.SetFloat("_Cull",mode==0?2:0);
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination=target });
                        RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,128,128),0,0); pixels.Apply();
                        string output=System.IO.Path.Combine(Application.dataPath,"../Artifacts/Resume");
                        System.IO.Directory.CreateDirectory(output);
                        System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"body-probe-"+name+"-"+(mode==0?"back":"double")+".png"),pixels.EncodeToPNG());
                        foreach(var pixel in pixels.GetPixels32()) if(pixel.r>64 && pixel.b>64 && pixel.g<32) counts[mode]++;
                    }
                    renderer.enabled=false;
                    Debug.Log("LOCAL_BODY_PIXELS "+name+" back="+counts[0]+" double="+counts[1]);
                    Assert.That(counts[0],Is.GreaterThan(10),name+" did not render with ordinary culling");
                }
            }
            finally
            {
                RenderTexture.active=previous; cameraObject.GetComponent<Camera>().targetTexture=null;
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(material); Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
            }
        }

        [TestCase(90f)] [TestCase(60f)]
        public void SlideBootsAreInFrameWithoutUpperBody(float fov)
        {
            state.Sliding=true; state.Velocity=Vector3.forward*16;
            for(int i=0;i<120;i++) body.Present(player,state,1f/120,false);
            var cameraObject=new GameObject("BodyCamera");
            try
            {
                var camera=cameraObject.AddComponent<Camera>(); camera.fieldOfView=fov; camera.aspect=16f/9;
                camera.transform.position=Vector3.up*.75f;
                foreach(string side in new[]{"L","R"})
                {
                    var foot=GripAttachment.Find(root.transform,"foot."+side);
                    Assert.That(foot,Is.Not.Null);
                    Vector3 viewport=camera.WorldToViewportPoint(foot.position);
                    Assert.That(viewport.z,Is.GreaterThan(0));
                    Assert.That(viewport.x,Is.InRange(0f,1f));
                    Assert.That(viewport.y,Is.InRange(.01f,1f));
                }
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer.enabled) Assert.That(renderer.name,Does.StartWith("Thigh").Or.StartWith("Shin").Or.StartWith("Foot"));
                    if(!renderer.enabled || !renderer.name.StartsWith("Foot")) continue;
                    var mesh=new Mesh();
                    try
                    {
                        ((SkinnedMeshRenderer)renderer).BakeMesh(mesh);
                        int visible=0;
                        foreach(var vertex in mesh.vertices)
                        {
                            Vector3 world=renderer.transform.TransformPoint(vertex);
                            Assert.That(world.y,Is.GreaterThan(-.04f),"Boot must stay above the floor.");
                            Vector3 projected=camera.WorldToViewportPoint(world);
                            if(projected.z>0 && projected.x>0 && projected.x<1 && projected.y>0 && projected.y<1) visible++;
                        }
                        Assert.That(visible,Is.GreaterThan(mesh.vertexCount/2),"Most of each boot should be visible.");
                    }
                    finally { Object.DestroyImmediate(mesh); }
                }
                Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        [Test] public void SlideSegmentsHaveDistinctScreenAnglesInsteadOfEndOnStumps()
        {
            state.Sliding=true;
            for(int i=0;i<120;i++) body.Present(player,state,1f/120,false);
            Vector3 eye=Vector3.up*.75f;
            foreach(string side in new[]{"L","R"})
            {
                Vector3 hip=GripAttachment.Find(root.transform,"thigh."+side).position;
                Vector3 knee=GripAttachment.Find(root.transform,"shin."+side).position;
                Vector3 ankle=GripAttachment.Find(root.transform,"foot."+side).position;
                Assert.That(Vector3.Angle(hip-eye,knee-eye),Is.GreaterThan(15),side+" thigh must read as a connected limb");
                Assert.That(Vector3.Angle(knee-eye,ankle-eye),Is.GreaterThan(3),side+" shin must separate from thigh and boot");
            }
        }

        [Test] public void StandingThighCutsStayAtBottomOfDownwardView()
        {
            body.Present(player,state,0,false);
            var cameraObject=new GameObject("StandingBodyFrame");
            try
            {
                var camera=cameraObject.AddComponent<Camera>(); camera.fieldOfView=90;
                camera.transform.SetPositionAndRotation(Vector3.up*1.65f,Quaternion.Euler(65,0,0));
                foreach(string side in new[]{"L","R"})
                {
                    Vector3 hip=GripAttachment.Find(root.transform,"thigh."+side).position;
                    Vector3 ankle=GripAttachment.Find(root.transform,"foot."+side).position;
                    float hipY=camera.WorldToViewportPoint(hip).y,footY=camera.WorldToViewportPoint(ankle).y;
                    Assert.That(hipY,Is.LessThan(.015f),"Thigh roots must enter from the bottom edge.");
                    Assert.That(footY-hipY,Is.GreaterThan(.08f),"Leg length must extend upward from the frame edge.");
                }
                Assert.That(GripAttachment.Find(root.transform,"LocalWaist"),Is.Null);
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        [Test] public void SpringIsFrameRateIndependentAndDoesNotWriteMotor()
        {
            state.Sliding=true; state.Velocity=Vector3.forward*16;
            string before=JsonUtility.ToJson(state);
            for(int i=0;i<12;i++) body.Present(player,state,1f/120,false);
            Vector3 fast=GripAttachment.Find(root.transform,"foot.R").position;
            Object.DestroyImmediate(root);
            SetUp(); state.Sliding=true; state.Velocity=Vector3.forward*16;
            for(int i=0;i<2;i++) body.Present(player,state,1f/20,false);
            Assert.That(Vector3.Distance(fast,GripAttachment.Find(root.transform,"foot.R").position),Is.LessThan(.0001f));
            Assert.That(JsonUtility.ToJson(state),Is.EqualTo(before));
        }

        [Test] public void DeathAndNewLifeClearTheSlidePose()
        {
            state.Sliding=true;
            for(int i=0;i<30;i++) body.Present(player,state,1f/60,false);
            player.Alive=false; body.Present(player,state,1f/60,false);
            Assert.That(root.GetComponentInChildren<Renderer>(true).gameObject.activeInHierarchy,Is.False);
            player.Alive=true; player.LifeGeneration++; state.Generation++; state.Sliding=false;
            body.Present(player,state,0,false);
            Assert.That(GripAttachment.Find(root.transform,"foot.R").position.z,Is.EqualTo(-.30f).Within(.02f));
        }

        [Test] public void LookingDownFindsStandingFeetAndTeleportClearsSlide()
        {
            var cameraObject=new GameObject("DownCamera");
            try
            {
                body.Present(player,state,0,false);
                var camera=cameraObject.AddComponent<Camera>(); camera.fieldOfView=60;
                camera.transform.SetPositionAndRotation(Vector3.up*1.65f,Quaternion.Euler(80,0,0));
                Vector3 point=camera.WorldToViewportPoint(GripAttachment.Find(root.transform,"foot.R").position);
                Assert.That(point.y,Is.InRange(0f,1f));
                state.Sliding=true;
                for(int i=0;i<30;i++) body.Present(player,state,1f/60,false);
                state.Position=Vector3.right*20; state.Sliding=false;
                body.Present(player,state,0,false);
                Assert.That(GripAttachment.Find(root.transform,"foot.R").position.z,Is.EqualTo(-.30f).Within(.02f));
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        [TestCase(false)] [TestCase(true)]
        public void ShortBlinkClearsGaitAndSlideWithoutChangingLife(bool sliding)
        {
            state.Velocity=Vector3.forward*11;
            body.Present(player,state,0,false);
            Vector3 fresh=GripAttachment.Find(root.transform,"foot.R").position;
            state.Sliding=sliding;
            for(int i=0;i<30;i++) body.Present(player,state,1f/60,false);
            Assert.That(Vector3.Distance(fresh,GripAttachment.Find(root.transform,"foot.R").position),Is.GreaterThan(.05f));
            state.Position=Vector3.right*.5f; state.Sliding=false; player.DiscontinuityTick=31;
            body.Present(player,state,0,false);
            Assert.That(Vector3.Distance(fresh+state.Position,GripAttachment.Find(root.transform,"foot.R").position),Is.LessThan(.0001f));
        }

        [Test] public void ReducedMotionRetainsSlideWithoutThrowOvershoot()
        {
            state.Sliding=true;
            for(int i=0;i<60;i++)
            {
                body.Present(player,state,1f/120,true);
                Assert.That(GripAttachment.Find(root.transform,"foot.R").position.z,Is.LessThan(1.34f));
            }
            Assert.That(GripAttachment.Find(root.transform,"foot.R").position.z,Is.GreaterThan(.70f));
        }
    }
}
