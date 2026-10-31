using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    // Native Unity import/transform checks: compiled by managed QA, not run by its Mono runner.
    [TestFixture,Category("Native")]
    public sealed class PresentationImportTests
    {
        const string Art="Assets/Wildstone/Art/";
        static readonly string[] Weapons={"LineDriver","PulseLobber","ReboundDisc","PushLens","Grenade","StunGun","LaserSword","LaserPistol","LaserShotgun","LaserSniper","LaserSMG"};

        [Test]
        public void ImportedFixtureKeepsItsAxisContract()
        {
            var fixture=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"AxisFixture.fbx");
            Assert.That(fixture,Is.Not.Null);
            var grip=GripAttachment.Find(fixture.transform,"Grip");
            var muzzle=GripAttachment.Find(fixture.transform,"Muzzle");
            Assert.That(grip,Is.Not.Null); Assert.That(muzzle,Is.Not.Null);
            // Retain the imported root's axis and handedness conversion, as ModelPrefab does.
            Assert.That(Vector3.Distance(grip.position,new Vector3(.12f,.10f,-.05f)),Is.LessThan(.005f),"Grip asset position="+grip.position);
            Assert.That(Vector3.Distance(muzzle.position,new Vector3(.95f,.10f,.05f)),Is.LessThan(.005f),"Muzzle asset position="+muzzle.position);
            Assert.That(fixture.transform.localToWorldMatrix.determinant,Is.LessThan(0),"Imported root must retain the handedness correction");
            var tab=GripAttachment.Find(fixture.transform,"PositiveZTab");
            Assert.That(tab,Is.Not.Null);
            var tabMesh=tab.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(Vector3.Distance(tab.TransformPoint(tabMesh.bounds.center),new Vector3(.82f,.25f,.10f)),Is.LessThan(.005f),"Positive axis tab");
        }

        [Test]
        public void SoldierImportsAsAValidHumanoidWithEveryContractClip()
        {
            var model=AssetImporter.GetAtPath(Art+"Soldier.fbx") as ModelImporter;
            Assert.That(model,Is.Not.Null);
            Assert.That(model.animationType,Is.EqualTo(ModelImporterAnimationType.Human));
            var avatar=AssetDatabase.LoadAllAssetsAtPath(Art+"Soldier.fbx").OfType<Avatar>().Single();
            Assert.That(avatar.isHuman && avatar.isValid,Is.True);
            var soldier=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Soldier.fbx");
            foreach(var socket in new[]{"WeaponHand_R","WeaponHand_L","HeadSocket"}) Assert.That(GripAttachment.Find(soldier.transform,socket),Is.Not.Null,socket);
            Assert.That(soldier.GetComponentsInChildren<LODGroup>(true).Length,Is.EqualTo(1),"Soldier_LOD0..2 import as one LOD group.");
            var animation=AssetImporter.GetAtPath(Art+"SoldierAnimations.fbx") as ModelImporter;
            Assert.That(animation,Is.Not.Null);
            Assert.That(animation.animationType,Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(animation.sourceAvatar,Is.EqualTo(avatar),"Clips retarget through the soldier avatar.");
            var clips=AssetDatabase.LoadAllAssetsAtPath(Art+"SoldierAnimations.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            foreach(var name in SoldierRig.Clips)
            {
                var clip=clips.FirstOrDefault(c=>c.name==name);
                Assert.That(clip,Is.Not.Null,name);
                Assert.That(clip.isHumanMotion,Is.True,name);
                Assert.That(clip.isLooping,Is.EqualTo(SoldierRig.LoopingClips.Contains(name)),name);
            }
        }

        [Test]
        public void LocomotionBlendPositionsMatchTheAuthoredStrideSpeeds()
        {
            var sidecar=JsonUtility.FromJson<ClipTable>(System.IO.File.ReadAllText(Art+"SoldierAnimations.clips.json"));
            var speeds=sidecar.clips.ToDictionary(c=>c.name,c=>c.speed);
            Assert.That(SoldierRig.RunCycleSpeed,Is.EqualTo(speeds["RunF"]).Within(.01f),"Runtime cycle-rate speed must match the authored run.");
            Assert.That(SoldierRig.RunBackSpeed,Is.EqualTo(speeds["RunB"]).Within(.01f));
            Assert.That(SoldierRig.RunSideSpeed,Is.EqualTo(speeds["RunL"]).Within(.01f));
            Assert.That(SoldierRig.RunSideSpeed,Is.EqualTo(speeds["RunR"]).Within(.01f));
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Wildstone/Resources/Presentation/Soldier.controller");
            Assert.That(controller,Is.Not.Null);
            var locomotion=controller.layers[0].stateMachine.states.Select(s=>s.state).Single(s=>s.name=="Locomotion");
            Assert.That(locomotion.speedParameterActive && locomotion.speedParameter=="LocomotionRate",Is.True);
            var wall=controller.layers[0].stateMachine.states.Select(s=>s.state).Single(s=>s.name=="WallRun");
            Assert.That(wall.speedParameterActive && wall.speedParameter=="LocomotionRate",Is.True,"Fast wall runs speed the cycle up instead of skating.");
            var tree=(BlendTree)locomotion.motion;
            foreach(var child in tree.children)
            {
                if(child.motion.name=="Idle") { Assert.That(child.position,Is.EqualTo(Vector2.zero)); continue; }
                Assert.That(child.position.magnitude,Is.EqualTo(speeds[child.motion.name]).Within(.001f),child.motion.name);
            }
            Assert.That(controller.layers[1].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg),Is.False,"Upper-body layer leaves the legs to locomotion.");
            Assert.That(controller.layers[2].blendingMode,Is.EqualTo(AnimatorLayerBlendingMode.Additive));
        }
        [System.Serializable] class ClipInfo { public string name=""; public float speed=0; }
        [System.Serializable] class ClipTable { public ClipInfo[] clips=new ClipInfo[0]; }

        // Built weapons carry the weapon frame on every socket, so a grip held along the aim keeps the barrel downrange.
        [Test]
        public void WeaponPrefabSocketsFaceDownTheBarrelTopUp()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<PresentationCatalog>("Assets/Wildstone/Resources/WildstonePresentation.asset");
            Assert.That(catalog,Is.Not.Null);
            foreach(var prefab in catalog.Weapons)
            {
                Assert.That(prefab,Is.Not.Null);
                foreach(var name in new[]{"Grip","Muzzle","Sight","LeftHand"})
                {
                    var socket=GripAttachment.Find(prefab.transform,name);
                    if(socket==null) continue;
                    Assert.That(Vector3.Angle(socket.forward,prefab.transform.forward),Is.LessThan(.5f),prefab.name+" "+name+" forward");
                    Assert.That(Vector3.Angle(socket.up,prefab.transform.up),Is.LessThan(.5f),prefab.name+" "+name+" up");
                }
                var grip=GripAttachment.Find(prefab.transform,"Grip");
                Assert.That(Vector3.Dot(GripAttachment.Find(prefab.transform,"Muzzle").position-grip.position,grip.forward),Is.GreaterThan(.1f),prefab.name);
            }
        }
        // Hand sockets run wrist->knuckles with the thumb/index side up, the frame the weapon Grip is matched to.
        [Test]
        public void HandSocketsRunAlongTheKnucklesWithTheIndexSideUp()
        {
            foreach(var (asset,socketName) in new[]{("Soldier.fbx","WeaponHand_R"),("SoldierArms.fbx","Grip_R"),("Soldier.fbx","WeaponHand_L"),("SoldierArms.fbx","Grip_L")})
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+asset).transform;
                string side=socketName.EndsWith("R")?"Right":"Left";
                var socket=GripAttachment.Find(model,socketName); var hand=GripAttachment.Find(model,side+"Hand");
                var index=GripAttachment.Find(model,side+"IndexProximal"); var little=GripAttachment.Find(model,side+"LittleProximal");
                var middle=GripAttachment.Find(model,side+"MiddleProximal");
                Assert.That(socket!=null && hand!=null && index!=null && little!=null && middle!=null,Is.True,asset+" "+socketName+" bones");
                string context=asset+" "+socketName+" fwd "+socket.forward+" up "+socket.up;
                Assert.That(Vector3.Dot(socket.forward,(middle.position-hand.position).normalized),Is.GreaterThan(.9f),context+" knuckles");
                Assert.That(Vector3.Dot(socket.up,(index.position-little.position).normalized),Is.GreaterThan(.8f),context+" index side");
            }
        }

        [Test]
        public void EveryAuthoredWeaponGripMatchesBothHandSockets()
        {
            var arms=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"SoldierArms.fbx"));
            var soldier=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"Soldier.fbx"));
            try
            {
                var fp=GripAttachment.Find(arms.transform,"Grip_R");
                var tp=GripAttachment.Find(soldier.transform,"WeaponHand_R");
                Assert.That(fp,Is.Not.Null); Assert.That(tp,Is.Not.Null);
                Assert.That(GripAttachment.Find(arms.transform,"Grip_L"),Is.Not.Null);
                foreach(var name in Weapons)
                {
                    foreach(var socket in new[]{fp,tp})
                    {
                        var weapon=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+name+".fbx"));
                        try
                        {
                            weapon.transform.SetParent(socket,false);
                            GripAttachment.Align(weapon.transform,socket);
                            var grip=GripAttachment.Find(weapon.transform,"Grip");
                            Assert.That(Vector3.Distance(grip.position,socket.position),Is.LessThan(.005f),name+" position");
                            Assert.That(Quaternion.Angle(grip.rotation,socket.rotation),Is.LessThan(.5f),name+" rotation");
                        }
                        finally { Object.DestroyImmediate(weapon); }
                    }
                }
            }
            finally { Object.DestroyImmediate(arms); Object.DestroyImmediate(soldier); }
        }
    }
}
