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
        Camera camera;
        WeaponView view;
        PresentationCatalog catalog;
        CombatState player;
        MotorState motor;
        [SetUp] public void Setup()
        {
            catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            Assert.That(catalog,Is.Not.Null);
            camera=new GameObject("Motion camera",typeof(Camera)).GetComponent<Camera>();
            view=new GameObject("Motion view",typeof(WeaponView)).GetComponent<WeaponView>();
            view.Initialize(camera,catalog);
            player=new CombatState { Participant=1,LifeGeneration=1 };
            motor=new MotorState { Grounded=true };
            Step();
        }
        [TearDown] public void Cleanup()
        {
            view.Teardown(camera);
            Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(camera.gameObject);
        }
        void Step(float dt=.016f,bool reduced=false) => view.Present(player,catalog,in motor,10,dt,reduced);
        Transform Find(string name) => GripAttachment.Find(camera.transform,name);
        [Test] public void BothHandsKeepTheirAuthoredContactsAfterWeaponChangeAndSlide()
        {
            foreach(var kind in new[] { WeaponKind.LineDriver,WeaponKind.PulseLobber,WeaponKind.PushLens })
            {
                player.SpecialWeapon=kind; player.SelectedSpecial=true;
                motor.Sliding=true; motor.Velocity=Vector3.forward*12;
                Step();
                Assert.That(Vector3.Distance(Find("Grip_R").position,GripAttachment.Find(Find("Grip_R").GetChild(Find("Grip_R").childCount-1),"Grip").position),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(Find("Wrist_L").position,GripAttachment.Find(Find("Grip_R").GetChild(Find("Grip_R").childCount-1),"LeftHand").position),Is.LessThan(.0001f));
            }
        }
        [Test] public void OnlyConfirmedLocalShotsKickTheViewAndPauseFreezesIt()
        {
            var hands=Find("Wrist_R").parent;
            Vector3 rest=hands.localPosition;
            var action=new CombatAction { Participant=2 };
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1); Step();
            Assert.That(hands.localPosition,Is.EqualTo(rest));
            action.Participant=1;
            view.Play(new CombatEvent(CombatEventKind.Rejected,action,10),1); Step();
            Assert.That(hands.localPosition,Is.EqualTo(rest));
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1); Step();
            Assert.That(hands.localPosition.z,Is.LessThan(rest.z));
            var kicked=hands.localPosition; Step(0);
            Assert.That(hands.localPosition,Is.EqualTo(kicked));
            player.Alive=false; Step();
            Assert.That(hands.localPosition,Is.EqualTo(rest));
        }
        [TestCase(WeaponKind.LaserPistol)] [TestCase(WeaponKind.LaserShotgun)] [TestCase(WeaponKind.LaserSniper)]
        public void FamiliarLaserWeaponsHaveAuthoredContactsDistinctMechanismsAndBoundedFlashes(WeaponKind kind)
        {
            player.SelectedSpecial=true; player.SpecialWeapon=kind; Step();
            Assert.That(view.TryGetMuzzle(out var muzzle),Is.True);
            var model=Find("Grip_R").GetChild(Find("Grip_R").childCount-1);
            Assert.That(Vector3.Distance(Find("Grip_R").position,GripAttachment.Find(model,"Grip").position),Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(Find("Wrist_L").position,GripAttachment.Find(model,"LeftHand").position),Is.LessThan(.0001f));
            var mechanism=GripAttachment.Find(model,"Weapon mechanism");
            Assert.That(mechanism,Is.Not.Null);
            var rest=mechanism.localPosition;
            view.Play(new CombatEvent(CombatEventKind.Shot,new CombatAction { Participant=1 },10,vector:Vector3.forward,weapon:kind),1); Step();
            Assert.That(Vector3.Distance(rest,mechanism.localPosition),Is.GreaterThan(.001f));
            Assert.That(Find("Local muzzle 0").GetComponent<LineRenderer>().enabled,Is.True);
            Step(.6f);
            Assert.That(Find("Local muzzle 0").GetComponent<LineRenderer>().enabled,Is.False);
        }
        [Test] public void MuzzleUsesAuthoredSocketAndFourReusedElementsExpireAndReset()
        {
            Assert.That(view.TryGetMuzzle(out var muzzle),Is.True);
            Assert.That(Vector3.Distance(muzzle,Find("Muzzle").position),Is.LessThan(.0001f));
            var lines=camera.GetComponentsInChildren<LineRenderer>(true);
            Assert.That(lines.Length,Is.EqualTo(4));
            var action=new CombatAction { Participant=1 };
            for(int i=0;i<30;i++)
            {
                view.Play(new CombatEvent(CombatEventKind.Shot,action,10,vector:Vector3.forward,weapon:WeaponKind.LineDriver),1); Step();
                Assert.That(lines[0].enabled,Is.True);
                Assert.That(Vector3.Distance(lines[0].GetPosition(0),Find("Muzzle").position),Is.LessThan(.0001f));
            }
            Assert.That(camera.GetComponentsInChildren<LineRenderer>(true).Length,Is.EqualTo(4));
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
            var hands=Find("Wrist_R").parent;
            var rest=hands.localPosition;
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10,vector:Vector3.forward,weapon:WeaponKind.LineDriver),1); Step();
            float light=rest.z-hands.localPosition.z;
            player.SelectedSpecial=true; player.SpecialWeapon=WeaponKind.PulseLobber; Step();
            Assert.That(Find("Local muzzle 0").GetComponent<LineRenderer>().enabled,Is.False);
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10,vector:Vector3.forward,weapon:WeaponKind.PulseLobber),1); Step();
            Assert.That(rest.z-hands.localPosition.z,Is.GreaterThan(light*1.5f));
        }
        [Test] public void ImportedSleeveGeometryEndsAtItsAuthoredShoulderAndMovingWristCuff()
        {
            var reference=Object.Instantiate(catalog.Hands);
            try
            {
                var gripR=GripAttachment.Find(reference.transform,"Grip_R");
                var gripL=GripAttachment.Find(reference.transform,"Grip_L");
                var palm=GripAttachment.Find(reference.transform,"PalmR");
                Vector3 right=(gripR.position-gripL.position)/.20f;
                Vector3 forward=(gripR.position-palm.position)/.08f;
                Vector3 up=(gripR.parent.position+right*.10f-forward*.07f-gripR.position)/.11f;
                Vector3 shoulder=gripR.parent.position-right*.20f-up*.17f-forward*.38f;
                Vector3 cuff=gripR.parent.position-right*.12f-up*.12f-forward*.18f;
                var sourceSleeve=GripAttachment.Find(reference.transform,"SleeveL").GetComponent<MeshFilter>();
                Vector3[] vertices;
#if UNITY_EDITOR
                using(var data=UnityEditor.MeshUtility.AcquireReadOnlyMeshData(sourceSleeve.sharedMesh))
                using(var positions=new Unity.Collections.NativeArray<Vector3>(data[0].vertexCount,Unity.Collections.Allocator.Temp))
                {
                    data[0].GetVertices(positions);
                    vertices=positions.ToArray();
                }
#else
                vertices=sourceSleeve.sharedMesh.vertices;
#endif
                Vector3 axis=(cuff-shoulder).normalized;
                var ring=new System.Collections.Generic.List<int>();
                for(int i=0;i<vertices.Length;i++)
                    if(Mathf.Abs(Vector3.Dot(sourceSleeve.transform.TransformPoint(vertices[i])-cuff,axis))<.0001f) ring.Add(i);
                Assert.That(ring.Count,Is.GreaterThanOrEqualTo(8),"Find the original imported cuff ring, not a synthetic socket.");
                Vector3 expectedShoulder=Find("Wrist_L").parent.TransformPoint(reference.transform.InverseTransformPoint(shoulder));
                Assert.That(Vector3.Distance(Find("Forearm_L").position,expectedShoulder),Is.LessThan(.0001f));
                Vector3 cuffOffset=Quaternion.Inverse(gripL.rotation)*(cuff-gripL.position);
                player.ReloadUntil=82; motor.Sliding=true; Step();
                var sleeve=Find("SleeveL").GetComponent<MeshFilter>();
                var bounds=new Bounds(sleeve.transform.TransformPoint(vertices[ring[0]]),Vector3.zero);
                foreach(int index in ring) bounds.Encapsulate(sleeve.transform.TransformPoint(vertices[index]));
                Assert.That(Vector3.Distance(bounds.center,Find("Wrist_L").TransformPoint(cuffOffset)),Is.LessThan(.001f),"Rendered sleeve cuff must follow the articulated palm without a gap.");
            }
            finally { Object.DestroyImmediate(reference); }
        }
        [Test] public void DisconnectHidesAndNeutralizesTheWeaponAndReconnectStartsWithoutAKick()
        {
            var hands=Find("Wrist_R").parent;
            var rest=hands.localPosition;
            var overlay=hands.GetComponentInParent<Camera>();
            var action=new CombatAction { Participant=1 };
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1); Step();
            Assert.That(hands.localPosition,Is.Not.EqualTo(rest));
            player.Connected=false; Step();
            Assert.That(overlay.enabled,Is.False);
            Assert.That(hands.localPosition,Is.EqualTo(rest));
            player.Connected=true; Step();
            Assert.That(overlay.enabled,Is.True);
            Assert.That(hands.localPosition,Is.EqualTo(rest));
        }
        [Test] public void SlowFramesConsumeRealActionTimeAndTeleportClearsTheKick()
        {
            var hands=Find("Wrist_R").parent;
            var rest=hands.localPosition;
            var action=new CombatAction { Participant=1 };
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1);
            Step(.2f);
            Assert.That(hands.localPosition,Is.EqualTo(rest),"Recoil must finish after its real duration even below 20 FPS.");
            view.Play(new CombatEvent(CombatEventKind.Shot,action,10),1); Step();
            Assert.That(hands.localPosition,Is.Not.EqualTo(rest));
            player.DiscontinuityTick=11; Step();
            Assert.That(hands.localPosition,Is.EqualTo(rest));
        }
        [Test] public void ReducedMotionSuppressesBobAndForearmWidthStaysFixedAsSupportMoves()
        {
            var hands=Find("Wrist_R").parent;
            var rest=hands.localPosition;
            motor.Velocity=Vector3.forward*8;
            for(int i=0;i<10;i++) Step(reduced:true);
            Assert.That(hands.localPosition,Is.EqualTo(rest),"Decorative walk bob is disabled.");
            Assert.That(Find("Forearm_L").localScale.x,Is.EqualTo(1));
            Assert.That(Find("Forearm_L").localScale.y,Is.EqualTo(1));
            var knuckle=Find("Knuckle_L0");
            var closed=knuckle.localRotation;
            player.ReloadUntil=82; Step();
            Assert.That(Quaternion.Angle(closed,knuckle.localRotation),Is.GreaterThan(50));
            player.ReloadUntil=0; Step();
            Assert.That(Quaternion.Angle(closed,knuckle.localRotation),Is.LessThan(.001f));
        }
        [Test] public void SlideIsDistinctReducedMotionIsSmallerAndReloadReleasesSupport()
        {
            var hands=Find("Wrist_R").parent;
            var rest=hands.localPosition;
            motor.Sliding=true;
            for(int i=0;i<20;i++) Step();
            Assert.That(hands.localPosition.y,Is.LessThan(rest.y-.07f));
            float full=Vector3.Distance(rest,hands.localPosition);
            view.ResetMotion();
            for(int i=0;i<20;i++) Step(reduced:true);
            Assert.That(Vector3.Distance(rest,hands.localPosition),Is.LessThan(full*.3f));
            player.ReloadUntil=82; Step();
            Assert.That(Vector3.Distance(Find("Wrist_L").position,GripAttachment.Find(Find("Grip_R").GetChild(Find("Grip_R").childCount-1),"LeftHand").position),Is.GreaterThan(.05f));
        }
    }
}

