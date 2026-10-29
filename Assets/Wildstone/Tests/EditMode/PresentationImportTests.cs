using NUnit.Framework;
using UnityEditor;
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
        static readonly string[] Weapons={"LineDriver","PulseLobber","ReboundDisc","PushLens","Grenade","StunGun","LaserSword","LaserPistol","LaserShotgun","LaserSniper"};

        [Test]
        public void ImportedFixtureAndHumanClipsKeepTheirAxisAndOneShotContracts()
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
            foreach(var name in new[]{"WildstoneHuman","WildstoneHumanTeam02","WildstoneHumanTeam03","WildstoneHumanTeam04"})
            {
                var importer=AssetImporter.GetAtPath(Art+name+".fbx") as ModelImporter;
                Assert.That(importer,Is.Not.Null,name);
                Assert.That(importer.animationType,Is.EqualTo(ModelImporterAnimationType.Generic),name);
                var clips=AssetDatabase.LoadAllAssetsAtPath(Art+name+".fbx");
                foreach(var action in new[]{"Idle","Run","Strafe","Jump","Slide","Aim","Death"})
                {
                    AnimationClip clip=null;
                    foreach(var asset in clips) if(asset is AnimationClip candidate && (candidate.name==action || candidate.name.EndsWith("|"+action))) clip=candidate;
                    Assert.That(clip,Is.Not.Null,name+" "+action);
                    Assert.That(clip.isLooping,Is.EqualTo(action=="Idle" || action=="Run" || action=="Strafe" || action=="Aim"),name+" "+action);
                }
            }
        }

        [Test]
        public void EveryAuthoredWeaponGripMatchesBothHandSockets()
        {
            var hands=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"FirstPersonHands.fbx"));
            var human=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"WildstoneHuman.fbx"));
            try
            {
                var fp=GripAttachment.Find(hands.transform,"Grip_R");
                var tp=GripAttachment.Find(human.transform,"WeaponHand_R");
                Assert.That(fp,Is.Not.Null); Assert.That(tp,Is.Not.Null);
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
            finally { Object.DestroyImmediate(hands); Object.DestroyImmediate(human); }
        }

    }
}
