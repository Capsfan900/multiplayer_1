using System.Reflection;
using System.Collections.Generic;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.UI;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class RemoteWeaponHandlingTests
    {
        PlayerVisual visual;
        PresentationCatalog catalog;
        CombatState player;
        Transform hand;
        [SetUp] public void Setup()
        {
            catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            visual=new GameObject("Remote handling",typeof(PlayerVisual)).GetComponent<PlayerVisual>();
            visual.Initialize(catalog,0);
            player=new CombatState {Participant=2,LifeGeneration=1};
            Step();
            hand=GripAttachment.Find(visual.transform,"WeaponHand_R");
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(visual.gameObject);
        void Step(int tick=10,float dt=.016f,bool reduced=false)
        {
            visual.Present(player,default,catalog);
            var method=typeof(PlayerVisual).GetMethod("PresentHandling");
            Assert.That(method,Is.Not.Null,"Remote handling must consume authoritative ticks and cosmetic delta time.");
            method.Invoke(visual,new object[]{player,tick,dt,reduced,WeaponCatalog.DefaultTickRate});
            visual.SendMessage("LateUpdate");
        }
        void Shot(CombatEventKind kind=CombatEventKind.Shot,int life=1,WeaponKind weapon=WeaponKind.LineDriver)
        {
            var method=typeof(PlayerVisual).GetMethod("Play");
            Assert.That(method,Is.Not.Null,"Confirmed remote shots must reach the held weapon.");
            method.Invoke(visual,new object[]{new CombatEvent(kind,new CombatAction {Participant=2,LifeGeneration=life},10,weapon:weapon)});
        }
        [Test] public void ConfirmedShotMovesHandAndKeepsGripThenRecovers()
        {
            var rest=hand.rotation;
            Shot(CombatEventKind.Rejected); Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
            Shot(); Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.GreaterThan(1));
            var model=hand.GetChild(hand.childCount-1);
            Assert.That(Vector3.Distance(GripAttachment.Find(model,"Grip").position,hand.position),Is.LessThan(.0001f));
            Step(dt:1);
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
        }
        [Test] public void LateObservedReloadUsesTimerAndReturnsToAimAtCompletion()
        {
            var rest=hand.rotation;
            int duration=Mathf.RoundToInt(WeaponCatalog.ReloadSeconds*WeaponCatalog.DefaultTickRate);
            player.ReloadUntil=10+duration;
            Step(10+duration/2);
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.GreaterThan(10));
            Step(player.ReloadUntil);
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
            Assert.That(player.ReloadUntil,Is.EqualTo(10+duration));
        }
        [Test] public void LifeWeaponDeathDisconnectAndDiscontinuityClearShotImpulse()
        {
            var rest=hand.rotation;
            Shot(); player.LifeGeneration++; Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
            Shot(life:1); Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f),"Old life events cannot kick a respawn.");
            Shot(life:2); player.DiscontinuityTick++; Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
            Shot(life:2); player.SelectedSpecial=true; player.SpecialWeapon=WeaponKind.LaserPistol; Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
            Shot(life:2,weapon:WeaponKind.LaserPistol); player.Connected=false; Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
            player.Connected=true; Shot(life:2,weapon:WeaponKind.LaserPistol); player.Alive=false; Step();
            player.Alive=true; Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
        }
        [Test] public void ConnectionChangeClearsImpulseButCommittedHistoricalShotStillPresents()
        {
            var rest=hand.rotation;
            Shot(); Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.GreaterThan(1));
            player.ConnectionGeneration=2; Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.LessThan(.01f));
            typeof(PlayerVisual).GetMethod("Play").Invoke(visual,new object[]{new CombatEvent(CombatEventKind.Shot,
                new CombatAction {Participant=2,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1},10,weapon:WeaponKind.LineDriver)});
            Step();
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.GreaterThan(1));
        }
        [Test] public void PauseFreezesImpulseAndReducedMotionKeepsSmallerCue()
        {
            var rest=hand.rotation;
            Shot(); Step(dt:0); var full=Quaternion.Angle(hand.rotation,rest);
            Step(dt:0);
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.EqualTo(full).Within(.001f));
            Step(dt:0,reduced:true);
            Assert.That(Quaternion.Angle(hand.rotation,rest),Is.InRange(.1f,full*.5f));
        }
        [Test] public void NetworkBridgeDeliversRemoteShotOnceAfterEstablishingItsWeapon()
        {
            var owner=new GameObject("Remote bridge");
            var camera=new GameObject("Remote bridge camera",typeof(Camera));
            var local=new GameObject("Local bridge motor",typeof(ArenaMotor));
            var remote=new GameObject("Remote bridge motor",typeof(ArenaMotor));
            try
            {
                var session=owner.AddComponent<PracticeSession>(); session.enabled=false;
                camera.GetComponent<Camera>().enabled=false; session.Camera=camera.GetComponent<Camera>();
                var settings=new LocalSettings();
                typeof(PracticeSession).GetField("<Settings>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,settings);
                typeof(PracticeSession).GetField("<Input>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,new PracticeInput(settings));
                var map=new ArenaDefinition {Id="remote-bridge",Minimum=new Vector3(-30,-1,-30),Maximum=new Vector3(30,20,30),
                    Spawns=new[]{new ArenaSpawn{Team=0,Position=Vector3.zero},new ArenaSpawn{Team=1,Position=Vector3.forward*6}},
                    ZoneRoute=new[]{Vector3.zero,Vector3.right*6,Vector3.forward*6}};
                var match=new ArenaMatch(map,120);
                Assert.That(match.AddPlayer(1,0,1,1,Vector3.zero),Is.True);
                Assert.That(match.AddPlayer(2,1,2,1,Vector3.forward*6),Is.True);
                var presentation=owner.AddComponent<ArenaPresentation>();
                session.AttachNetworkMatch(match,1,new Dictionary<int,ArenaMotor>{{1,local.GetComponent<ArenaMotor>()},{2,remote.GetComponent<ArenaMotor>()}});
                presentation.SendMessage("LateUpdate");
                var rendered=owner.GetComponentInChildren<PlayerVisual>();
                rendered.SendMessage("LateUpdate");
                var wrist=GripAttachment.Find(rendered.transform,"WeaponHand_R"); var rest=wrist.rotation;
                var shot=new CombatEvent(CombatEventKind.Shot,new CombatAction {Participant=2,ConnectionGeneration=2,LifeGeneration=1,Sequence=1},1,weapon:WeaponKind.LineDriver);
                session.PresentNetworkTick(new[]{shot},-1,false);
                presentation.SendMessage("LateUpdate"); rendered.SendMessage("LateUpdate");
                Assert.That(Quaternion.Angle(wrist.rotation,rest),Is.GreaterThan(1),"Remote event must reach the third-person wrist.");
                typeof(PlayerVisual).GetMethod("PresentHandling").Invoke(rendered,new object[]{match.Find(2),match.Tick,1f,false,120});
                rendered.SendMessage("LateUpdate");
                Assert.That(Quaternion.Angle(wrist.rotation,rest),Is.LessThan(.01f));
                session.PresentNetworkTick(new[]{shot},-1,false);
                presentation.SendMessage("LateUpdate"); rendered.SendMessage("LateUpdate");
                Assert.That(Quaternion.Angle(wrist.rotation,rest),Is.LessThan(.01f),"Duplicate network snapshot must not restart recoil.");
            }
            finally
            {
                Object.DestroyImmediate(owner); Object.DestroyImmediate(camera);
                Object.DestroyImmediate(local); Object.DestroyImmediate(remote);
            }
        }
    }
}
