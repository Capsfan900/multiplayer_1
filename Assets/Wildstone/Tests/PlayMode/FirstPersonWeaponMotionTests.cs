using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class FirstPersonWeaponMotionTests
    {
        // Solved arms may miss by the IK reach clamp; the weapon itself never drifts from the pivot.
        const float Contact=.003f;
        Camera camera;
        WeaponView view;
        PresentationCatalog catalog;
        CombatState player;
        MotorState motor;
        FirstPersonView aimView;
        [SetUp] public void Setup()
        {
            catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            Assert.That(catalog,Is.Not.Null);
            camera=new GameObject("Motion camera",typeof(Camera)).GetComponent<Camera>();
            view=new GameObject("Motion view",typeof(WeaponView)).GetComponent<WeaponView>();
            view.Initialize(camera,catalog);
            player=new CombatState { Participant=1,LifeGeneration=1 };
            motor=new MotorState { Grounded=true };
            // Let the equip raise finish so rest poses are stable.
            Step(); Step(1);
        }
        [TearDown] public void Cleanup()
        {
            view.Teardown(camera);
            if(aimView!=null) Object.DestroyImmediate(aimView.gameObject);
            Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(camera.gameObject);
        }
        void Step(float dt=.016f,bool reduced=false) => view.Present(player,catalog,in motor,10,dt,reduced);
        // One rendered frame as the game runs it: mouse -> FirstPersonView (aim and camera punch) -> weapon.
        void Frame(Vector2 mouse,float dt=.016f)
        {
            if(aimView==null) { aimView=new GameObject("Aim view",typeof(FirstPersonView)).GetComponent<FirstPersonView>(); aimView.OwnsLocalView=true; aimView.ViewCamera=camera; }
            aimView.SampleLook(mouse); aimView.RenderFrame(motor,dt); view.SyncProjection(camera,aimView); Step(dt);
        }
        Transform Find(string name) => GripAttachment.Find(camera.transform,name);
        Transform Pivot => Find("Weapon pivot");
        Transform Model => Pivot.GetChild(Pivot.childCount-1);
        void AssertContacts(string context)
        {
            Assert.That(Vector3.Distance(Pivot.position,GripAttachment.Find(Model,"Grip").position),Is.LessThan(.0001f),context+" weapon grip on pivot");
            Assert.That(Vector3.Distance(Find("Grip_R").position,GripAttachment.Find(Model,"Grip").position),Is.LessThan(Contact),context+" right hand on grip");
            Assert.That(Quaternion.Angle(Find("Grip_R").rotation,GripAttachment.Find(Model,"Grip").rotation),Is.LessThan(.5f),context+" right hand orientation");
            var support=GripAttachment.Find(Model,"LeftHand");
            if(support!=null) Assert.That(Vector3.Distance(Find("Grip_L").position,support.position),Is.LessThan(Contact),context+" left hand on support");
        }
        [Test] public void BothHandsKeepTheirAuthoredContactsAfterWeaponChangeAndSlide()
        {
            foreach(var kind in new[] { WeaponKind.LineDriver,WeaponKind.PulseLobber,WeaponKind.PushLens,WeaponKind.LaserSMG })
            {
                player.SpecialWeapon=kind; player.SelectedSpecial=true;
                motor.Sliding=true; motor.Velocity=Vector3.forward*12;
                Step(); Step();
                AssertContacts(kind.ToString());
            }
        }
        // A dash fires a brief burst of lens streaks that flow against the travel and stay clear of the reticle.
        [Test] public void DashStreaksFlowAgainstTheTravelAndClear()
        {
            var lines=System.Array.FindAll(camera.GetComponentsInChildren<LineRenderer>(true),line=>line.name.StartsWith("Dash streak"));
            Assert.That(lines.Length,Is.EqualTo(WeaponView.StreakCount));
            float fov=Find("Viewmodel").GetComponent<Camera>().fieldOfView;
            float frame=2*WeaponView.StreakDepth*Mathf.Tan(fov*.5f*Mathf.Deg2Rad);
            motor.DashStartedAt=5; motor.DashDirection=Vector3.right; Step();
            foreach(var line in lines)
            {
                Assert.That(line.enabled,Is.True);
                Vector3 head=line.GetPosition(0), tail=line.GetPosition(1);
                Assert.That(new Vector2(head.x,head.y).magnitude,Is.GreaterThanOrEqualTo(WeaponView.StreakInner*frame*.99f),"Clear of the reticle.");
                Assert.That(tail.x-head.x,Is.LessThan(0),"Dashing right streams the frame left.");
                Assert.That(line.startColor.a,Is.LessThanOrEqualTo(.86f),"Under the bloom threshold (8-bit vertex colour).");
            }
            Step(); Assert.That(lines[0].enabled,Is.True,"The same dash does not refire or stop the burst.");
            Step(WeaponView.StreakSeconds); Step();
            Assert.That(System.Array.TrueForAll(lines,line=>!line.enabled),Is.True,"The burst is gone after its life.");
            motor.DashStartedAt=6; Step(.016f,true);
            Assert.That(System.Array.TrueForAll(lines,line=>!line.enabled),Is.True,"Reduced motion shows none.");
        }
        [Test] public void HipGunsPointDownrangeTopUp()
        {
            foreach(var kind in new[] { WeaponKind.LineDriver,WeaponKind.LaserPistol,WeaponKind.LaserSniper,WeaponKind.LaserSMG,WeaponKind.LaserShotgun })
            {
                player.SpecialWeapon=kind; player.SelectedSpecial=kind!=WeaponKind.LineDriver;
                Step(); Step(1);
                var grip=GripAttachment.Find(Model,"Grip"); var barrel=(GripAttachment.Find(Model,"Muzzle").position-grip.position).normalized;
                string context=kind+" grip fwd "+grip.forward+" up "+grip.up+" barrel "+barrel;
                Assert.That(Vector3.Angle(barrel,camera.transform.forward),Is.LessThan(30),context);
                Assert.That(Vector3.Dot(grip.up,camera.transform.up),Is.GreaterThan(.7f),context);
            }
        }
        [Test] public void ArmBonesKeepTheirLengthsAndNeverCastViewmodelShadows()
        {
            float Length(string a,string b) => Vector3.Distance(Find(a).position,Find(b).position);
            float upper=Length("RightUpperArm","RightLowerArm"),lower=Length("RightLowerArm","RightHand");
            motor.Sliding=true; player.ReloadUntil=82;
            for(int i=0;i<20;i++) Step();
            Assert.That(Length("RightUpperArm","RightLowerArm"),Is.EqualTo(upper).Within(.0005f));
            Assert.That(Length("RightLowerArm","RightHand"),Is.EqualTo(lower).Within(.0005f));
            foreach(var renderer in Find("Viewmodel").GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off),renderer.name);
        }
        [Test] public void OnlyConfirmedLocalShotsKickTheViewAndPauseFreezesIt()
        {
            Vector3 rest=Pivot.localPosition;
            var action=new CombatAction { Participant=2 };
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1); Step();
            Assert.That(Pivot.localPosition,Is.EqualTo(rest));
            action.Participant=1;
            view.Play(new CombatEvent(CombatEventKind.Rejected,action,10),1); Step();
            Assert.That(Pivot.localPosition,Is.EqualTo(rest));
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1); Step();
            Assert.That(Pivot.localPosition.z,Is.LessThan(rest.z));
            var kicked=Pivot.localPosition; Step(0);
            Assert.That(Pivot.localPosition,Is.EqualTo(kicked));
            player.Alive=false; Step();
            Assert.That(Pivot.localPosition,Is.EqualTo(rest));
        }
        [TestCase(WeaponKind.LaserPistol)] [TestCase(WeaponKind.LaserShotgun)] [TestCase(WeaponKind.LaserSniper)] [TestCase(WeaponKind.LaserSMG)]
        public void FamiliarLaserWeaponsHaveAuthoredContactsDistinctMechanismsAndBoundedFlashes(WeaponKind kind)
        {
            player.SelectedSpecial=true; player.SpecialWeapon=kind; Step(); Step(1);
            Assert.That(view.TryGetMuzzle(out var muzzle),Is.True);
            AssertContacts(kind.ToString());
            view.Play(new CombatEvent(CombatEventKind.Shot,new CombatAction { Participant=1 },10,vector:Vector3.forward,weapon:kind),1); Step();
            AssertContacts(kind+" at peak kick");
            var mechanism=GripAttachment.Find(Model,"Weapon mechanism");
            if(kind!=WeaponKind.LaserSMG) Assert.That(mechanism,Is.Not.Null,"Recoil part animates.");
            Assert.That(Find("Local muzzle 0").GetComponent<LineRenderer>().enabled,Is.True);
            Step(.6f);
            Assert.That(Find("Local muzzle 0").GetComponent<LineRenderer>().enabled,Is.False);
        }
        [Test] public void MuzzleUsesAuthoredSocketAndFourReusedElementsExpireAndReset()
        {
            Assert.That(view.TryGetMuzzle(out var muzzle),Is.True);
            Assert.That(Vector3.Distance(muzzle,Find("Muzzle").position),Is.LessThan(.0001f),"Equal projections leave the socket in place.");
            // A wider world FOV than the viewmodel's: world effects still leave the barrel the player sees.
            camera.fieldOfView=90; view.SyncProjection(camera);
            var overlay=Find("Viewmodel").GetComponent<Camera>();
            Assert.That(overlay.fieldOfView,Is.Not.EqualTo(camera.fieldOfView));
            Assert.That(view.TryGetMuzzle(out muzzle),Is.True);
            var seen=overlay.WorldToViewportPoint(Find("Muzzle").position);
            var drawn=camera.WorldToViewportPoint(muzzle);
            Assert.That(Vector2.Distance(drawn,seen),Is.LessThan(.0005f),"Same screen point");
            Assert.That(drawn.z,Is.EqualTo(seen.z).Within(.0005f),"Same depth");
            Assert.That(Vector3.Distance(muzzle,Find("Muzzle").position),Is.GreaterThan(.01f));
            var lines=System.Array.FindAll(camera.GetComponentsInChildren<LineRenderer>(true),line=>line.name.StartsWith("Local muzzle"));
            Assert.That(lines.Length,Is.EqualTo(4));
            var action=new CombatAction { Participant=1 };
            for(int i=0;i<30;i++)
            {
                view.Play(new CombatEvent(CombatEventKind.Shot,action,10,vector:Vector3.forward,weapon:WeaponKind.LineDriver),1); Step();
                Assert.That(lines[0].enabled,Is.True);
                Assert.That(Vector3.Distance(lines[0].GetPosition(0),Find("Muzzle").position),Is.LessThan(.0001f));
            }
            Assert.That(camera.GetComponentsInChildren<LineRenderer>(true).Length,Is.EqualTo(4+WeaponView.StreakCount));
            Step(.5f);
            foreach(var line in lines) Assert.That(line.enabled,Is.False);
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10,vector:Vector3.forward,weapon:WeaponKind.LineDriver),1); Step();
            player.Connected=false; Step();
            Assert.That(view.TryGetMuzzle(out _),Is.False);
            foreach(var line in lines) Assert.That(line.enabled,Is.False);
        }
        [Test] public void ChargeRequiresRealLensPendingAndReducedMotionKeepsOnlySmallCore()
        {
            player.SelectedSpecial=true; player.SpecialWeapon=WeaponKind.PushLens; Step();
            var ring=Find("Local muzzle 1").GetComponent<LineRenderer>();
            Assert.That(ring.enabled,Is.False);
            player.Pending=new PendingAttack { Weapon=WeaponKind.PushLens,Action=new CombatAction { Tick=0 },ResolveTick=20,EndTick=21 };
            Step(); Assert.That(ring.enabled,Is.True);
            player.Pending=null; Step(); Assert.That(ring.enabled,Is.False);
            var action=new CombatAction { Participant=1 };
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10,vector:Vector3.forward,weapon:WeaponKind.PushLens),1);
            Step(reduced:true);
            Assert.That(Find("Local muzzle 0").GetComponent<LineRenderer>().enabled,Is.True);
            Assert.That(ring.enabled,Is.False);
            Assert.That(Find("Local muzzle 2").GetComponent<LineRenderer>().enabled,Is.False);
            player.DiscontinuityTick=11; Step();
            Assert.That(Find("Local muzzle 0").GetComponent<LineRenderer>().enabled,Is.False);
        }
        [Test] public void HeavyPlasmaHasDistinctKickAndEquipClearsMuzzle()
        {
            var action=new CombatAction { Participant=1 };
            var rest=Pivot.localPosition;
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10,vector:Vector3.forward,weapon:WeaponKind.LineDriver),1); Step();
            float light=rest.z-Pivot.localPosition.z;
            player.SelectedSpecial=true; player.SpecialWeapon=WeaponKind.PulseLobber; Step(); Step(1);
            Assert.That(Find("Local muzzle 0").GetComponent<LineRenderer>().enabled,Is.False);
            rest=Pivot.localPosition;
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10,vector:Vector3.forward,weapon:WeaponKind.PulseLobber),1); Step();
            Assert.That(rest.z-Pivot.localPosition.z,Is.GreaterThan(light*1.5f));
        }
        [Test] public void DisconnectHidesAndNeutralizesTheWeaponAndReconnectStartsWithoutAKick()
        {
            var rest=Pivot.localPosition;
            var overlay=Find("Viewmodel").GetComponent<Camera>();
            var action=new CombatAction { Participant=1 };
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1); Step();
            Assert.That(Pivot.localPosition,Is.Not.EqualTo(rest));
            player.Connected=false; Step();
            Assert.That(overlay.enabled,Is.False);
            Assert.That(Pivot.localPosition,Is.EqualTo(rest));
            player.Connected=true; Step();
            Assert.That(overlay.enabled,Is.True);
            Assert.That(Pivot.localPosition,Is.EqualTo(rest));
        }
        [Test] public void SlowFramesConsumeRealActionTimeAndTeleportClearsTheKick()
        {
            var rest=Pivot.localPosition;
            var action=new CombatAction { Participant=1 };
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1);
            Step(.2f);
            Assert.That(Pivot.localPosition,Is.EqualTo(rest),"Recoil must finish after its real duration even below 20 FPS.");
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1); Step();
            Assert.That(Pivot.localPosition,Is.Not.EqualTo(rest));
            player.DiscontinuityTick=11; Step();
            Assert.That(Pivot.localPosition,Is.EqualTo(rest));
        }
        [Test] public void ReducedMotionSuppressesBobAndReloadOpensTheSupportFingers()
        {
            var rest=Pivot.localPosition;
            motor.Velocity=Vector3.forward*8;
            for(int i=0;i<10;i++) Step(reduced:true);
            Assert.That(Pivot.localPosition,Is.EqualTo(rest),"Decorative walk bob is disabled.");
            var knuckle=Find("LeftIndexProximal");
            var closed=knuckle.localRotation;
            player.ReloadUntil=82; Step();
            Assert.That(Quaternion.Angle(closed,knuckle.localRotation),Is.GreaterThan(25));
            player.ReloadUntil=0; Step();
            Assert.That(Quaternion.Angle(closed,knuckle.localRotation),Is.LessThan(.01f));
        }
        [Test] public void SlideIsDistinctReducedMotionIsSmallerAndReloadReleasesSupport()
        {
            var rest=Pivot.localPosition;
            motor.Sliding=true;
            for(int i=0;i<20;i++) Step();
            Assert.That(Pivot.localPosition.y,Is.LessThan(rest.y-.07f));
            float full=Vector3.Distance(rest,Pivot.localPosition);
            view.ResetMotion();
            for(int i=0;i<20;i++) Step(reduced:true);
            Assert.That(Vector3.Distance(rest,Pivot.localPosition),Is.LessThan(full*.3f));
            player.ReloadUntil=82; Step();
            Assert.That(Vector3.Distance(Find("Grip_L").position,GripAttachment.Find(Model,"LeftHand").position),Is.GreaterThan(.05f));
        }
        [Test] public void ViewmodelLensIsIndependentOfTheWorldFieldOfView()
        {
            var overlay=Find("Viewmodel").GetComponent<Camera>();
            foreach(float world in new[]{60f,75f,100f})
            {
                camera.fieldOfView=world;
                view.SyncProjection(camera);
                Assert.That(overlay.fieldOfView,Is.EqualTo(WeaponView.ViewmodelFieldOfView));
            }
        }
        [Test] public void AimingPutsTheSightOnTheLensAxisAndAFullScopeHidesTheViewmodel()
        {
            player.AdsProgress=1; Step(); Step();
            var lens=Find("Viewmodel");
            var sight=GripAttachment.Find(Model,"Sight");
            Assert.That(sight,Is.Not.Null,"Line Driver carries a holo sight.");
            var local=lens.InverseTransformPoint(sight.position);
            Assert.That(new Vector2(local.x,local.y).magnitude,Is.LessThan(.002f),"Sight centre "+local);
            Assert.That(Vector3.Angle(lens.forward,sight.forward),Is.LessThan(.5f));
            AssertContacts("ADS");
            player.SelectedSpecial=true; player.SpecialWeapon=WeaponKind.LaserSniper; Step(); Step(1);
            Assert.That(view.Scoped,Is.True);
            Assert.That(lens.GetComponent<Camera>().enabled,Is.False);
            player.AdsProgress=0; Step();
            Assert.That(view.Scoped,Is.False);
            Assert.That(lens.GetComponent<Camera>().enabled,Is.True);
        }
        // Holding the throw key puts a real-size grenade in the support hand, in view; it survives the round trip to the
        // host's Accepted, leaves the hand during the swing, and the hand returns to the gun.
        [Test] public void HeldGrenadeShowsInTheSupportHandAndLeavesItDuringTheThrow()
        {
            var held=Find("Held grenade");
            Assert.That(held,Is.Not.Null);
            Assert.That(held.gameObject.activeSelf,Is.False,"No grenade without the throw key.");
            view.GrenadeReady=true;
            for(int i=0;i<15;i++) Step();
            Assert.That(held.gameObject.activeSelf,Is.True);
            Bounds Measure()
            {
                var box=held.GetComponentInChildren<Renderer>().bounds;
                foreach(var renderer in held.GetComponentsInChildren<Renderer>()) box.Encapsulate(renderer.bounds);
                return box;
            }
            var bounds=Measure();
            Assert.That(Vector3.Distance(bounds.center,Find("Grip_L").position),Is.LessThan(.01f),"Centred in the palm.");
            var pose=held.localRotation; held.rotation=Quaternion.identity;
            var upright=Measure(); held.localRotation=pose;
            Assert.That(Mathf.Max(upright.size.x,Mathf.Max(upright.size.y,upright.size.z)),Is.EqualTo(WeaponView.GrenadeSize).Within(.005f),"Real grenade size.");
            var viewport=Find("Viewmodel").GetComponent<Camera>().WorldToViewportPoint(bounds.center);
            Assert.That(viewport.x,Is.InRange(.05f,.5f),"Left half of the view "+viewport);
            Assert.That(viewport.y,Is.InRange(.2f,.6f),"Below the crosshair and above the HUD vitals "+viewport);
            Assert.That(viewport.z,Is.GreaterThan(.1f));
            Assert.That(Vector3.Distance(Find("Grip_L").position,GripAttachment.Find(Model,"LeftHand").position),Is.GreaterThan(.1f),"The hand left the gun.");
            view.GrenadeReady=false; Step(.2f);
            Assert.That(held.gameObject.activeSelf,Is.True,"Still held while the throw request reaches the host.");
            view.Play(new CombatEvent(CombatEventKind.Accepted,new CombatAction {Participant=1,Kind=CombatActionKind.Grenade},10),1);
            Step(.05f);
            Assert.That(held.gameObject.activeSelf,Is.True,"Wind-up keeps the grenade.");
            Step(.15f);
            Assert.That(held.gameObject.activeSelf,Is.False,"Released during the swing.");
            for(int i=0;i<10;i++) Step(.05f);
            AssertContacts("after the throw");
        }
        // Signal parts flare on a shot, keep some heat under sustained fire, then cool back to their authored glow.
        [Test] public void SignalPartsFlareOnShotsAndCoolBack()
        {
            var renderer=System.Array.Find(Model.GetComponentsInChildren<Renderer>(),r=>System.Array.Exists(r.sharedMaterials,m=>m!=null && m.name.StartsWith("SignalCyan")));
            Assert.That(renderer,Is.Not.Null,"Line Driver has an emissive SignalCyan part.");
            int index=System.Array.FindIndex(renderer.sharedMaterials,m=>m!=null && m.name.StartsWith("SignalCyan"));
            var authored=renderer.sharedMaterials[index].GetColor("_EmissionColor");
            Assert.That(renderer.sharedMaterials[index].IsKeywordEnabled("_EMISSION") && authored.maxColorComponent>.5f,Is.True,"Signal accents emit.");
            float Glow() { var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block,index); var c=block.GetColor("_EmissionColor"); return c.maxColorComponent/authored.maxColorComponent; }
            Step(1);
            Assert.That(Glow(),Is.EqualTo(1).Within(.02f));
            var action=new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 };
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10,weapon:WeaponKind.LineDriver),1); Step();
            float flare=Glow();
            Assert.That(flare,Is.GreaterThan(1.6f));
            Assert.That(flare*authored.maxColorComponent,Is.LessThanOrEqualTo(1.6f+.001f),"The flare is capped so the hue never clips.");
            for(int i=0;i<8;i++) { action.Sequence++; view.Play(new CombatEvent(CombatEventKind.Shot,action,10,weapon:WeaponKind.LineDriver),1); Step(.125f); }
            Step(.12f);
            Assert.That(Glow(),Is.GreaterThan(1.15f).And.LessThan(flare),"Heat outlasts the recoil flare.");
            Step(3); Step(.016f);
            Assert.That(Glow(),Is.EqualTo(1).Within(.02f),"Cools back to the authored glow.");
        }
        // A shot tips the camera in one frame; sway once read that as a fast mouse flick and the weapon bounced.
        [Test] public void ShotCameraPunchWithoutMouseInputLeavesSwayAtZero()
        {
            Frame(Vector2.zero);
            var rest=Pivot.localPosition;
            aimView.AddRecoil(WeaponView.ViewPunch(WeaponKind.LaserSniper));
            view.Play(new CombatEvent(CombatEventKind.Shot,new CombatAction { Participant=1 },10),1);
            for(int i=0;i<40;i++)
            {
                Frame(Vector2.zero);
                if(i==0) Assert.That(Quaternion.Angle(camera.transform.rotation,Quaternion.identity),Is.GreaterThan(.5f),"The shot tipped the camera.");
                Assert.That(Pivot.localPosition.x,Is.EqualTo(rest.x).Within(1e-6f),"frame "+i);
                Assert.That(Pivot.localPosition.y,Is.EqualTo(rest.y).Within(1e-6f),"frame "+i);
            }
        }
        // Critically damped: after a flick the weapon eases back to rest and never swings past it or rebounds.
        [Test] public void MouseFlickSettlesWithoutOvershoot()
        {
            Frame(Vector2.zero);
            var rest=Pivot.localPosition;
            var x=new System.Collections.Generic.List<float>(); var y=new System.Collections.Generic.List<float>();
            for(int i=0;i<130;i++)
            {
                Frame(i<10?new Vector2(3,-2):Vector2.zero);
                x.Add(Pivot.localPosition.x-rest.x); y.Add(Pivot.localPosition.y-rest.y);
            }
            AssertSettles(x,"yaw"); AssertSettles(y,"pitch");
        }
        static void AssertSettles(System.Collections.Generic.List<float> path,string axis)
        {
            int peak=0;
            for(int i=0;i<path.Count;i++) if(Mathf.Abs(path[i])>Mathf.Abs(path[peak])) peak=i;
            Assert.That(Mathf.Abs(path[peak]),Is.GreaterThan(.001f),axis+" sway answers the flick.");
            for(int i=peak+1;i<path.Count;i++)
            {
                Assert.That(path[i]*path[peak],Is.GreaterThanOrEqualTo(-1e-8f),axis+" swung past rest at frame "+i);
                Assert.That(Mathf.Abs(path[i]),Is.LessThanOrEqualTo(Mathf.Abs(path[i-1])+3e-7f),axis+" rebounded at frame "+i);
            }
            Assert.That(Mathf.Abs(path[path.Count-1]),Is.LessThan(1e-4f),axis+" settled.");
        }
    }
}
