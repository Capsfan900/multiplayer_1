using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.Presentation;
using Wildstone.UI;

namespace Wildstone.Tests
{
    public sealed class NetworkPresentationBridgeTests
    {
        GameObject owner, camera, local, remote, pose;
        PracticeSession session;
        ArenaDefinition map;
        ArenaMatch match;
        readonly Dictionary<int,ArenaMotor> avatars = new Dictionary<int,ArenaMotor>();
        readonly Dictionary<int,Transform> poses = new Dictionary<int,Transform>();
        static void Set(PracticeSession target,string property,object value) => typeof(PracticeSession)
            .GetField("<"+property+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        [SetUp] public void Setup()
        {
            owner=new GameObject("Network presentation bridge test"); session=owner.AddComponent<PracticeSession>(); session.enabled=false;
            camera=new GameObject("Network presentation camera",typeof(Camera)); camera.GetComponent<Camera>().enabled=false; session.Camera=camera.GetComponent<Camera>();
            var settings=new LocalSettings(); Set(session,"Settings",settings); Set(session,"Input",new PracticeInput(settings));
            local=new GameObject("Admitted local motor",typeof(ArenaMotor)); remote=new GameObject("Late remote motor",typeof(ArenaMotor));
            pose=new GameObject("Interpolated remote pose");
            map=new ArenaDefinition { Id="bridge",Minimum=new Vector3(-30,-1,-30),Maximum=new Vector3(30,20,30),
                Spawns=new[]{new ArenaSpawn{Team=0,Position=Vector3.zero},new ArenaSpawn{Team=1,Position=Vector3.forward*6}},
                ZoneRoute=new[]{Vector3.zero,Vector3.right*6,Vector3.forward*6} };
            match=new ArenaMatch(map,120);
            Assert.That(match.AddPlayer(1,0,1,1,Vector3.zero),Is.True);
            Assert.That(match.AddPlayer(2,1,2,1,Vector3.forward*6),Is.True);
            avatars.Clear(); poses.Clear(); avatars.Add(1,local.GetComponent<ArenaMotor>());
        }
        [TearDown] public void Cleanup()
        {
            foreach(var motor in new List<ArenaMotor>(session.Motors.Values))
                if(motor!=null && motor.gameObject!=local && motor.gameObject!=remote) Object.DestroyImmediate(motor.gameObject);
            Object.DestroyImmediate(owner); Object.DestroyImmediate(local); Object.DestroyImmediate(remote);
            Object.DestroyImmediate(pose); Object.DestroyImmediate(camera);
        }
        [Test] public void LateReplicatedAvatarsRefreshAndDetachDoesNotDestroyNetworkOwnedMotors()
        {
            session.AttachNetworkMatch(match,1,avatars);
            Assert.That(session.Motors.ContainsKey(2),Is.False);
            avatars.Add(2,remote.GetComponent<ArenaMotor>()); poses.Add(2,pose.transform);
            session.RefreshNetworkAvatars(avatars,poses);
            Assert.That(session.Motors[2],Is.SameAs(remote.GetComponent<ArenaMotor>()));
            Assert.That(session.RenderPose(2),Is.SameAs(pose.transform));
            avatars.Remove(2); poses.Remove(2); session.RefreshNetworkAvatars(avatars,poses);
            Assert.That(session.Motors.ContainsKey(2),Is.False); Assert.That(session.RenderPose(2),Is.Null);
            session.DetachNetworkMatch(); session.DetachNetworkMatch();
            Assert.That(session.ExternalSimulation,Is.False); Assert.That(session.Match,Is.Null);
            Assert.That(local!=null && remote!=null,Is.True);
            Assert.That(session.Motors.Count,Is.Zero);
        }
        [Test] public void NetworkEventsAndZoneMetadataReachStepSubscribersWithoutMutatingSimulation()
        {
            session.AttachNetworkMatch(match,1,avatars);
            var events=new[]{new CombatEvent(CombatEventKind.Hit,new CombatAction{Participant=1},4,2,1)};
            int steps=0; session.Stepped+=()=> { steps++; Assert.That(session.CombatEvents,Is.SameAs(events)); };
            session.PresentNetworkTick(events,2,true);
            Assert.That(steps,Is.EqualTo(1)); Assert.That(session.ZoneCaptor,Is.EqualTo(2)); Assert.That(session.ZoneContested,Is.True);
            Assert.That(match.Events.Count,Is.Zero); Assert.That(match.Captor,Is.EqualTo(-1));
            session.DetachNetworkMatch();
            Assert.That(session.CombatEvents.Count,Is.Zero); Assert.That(session.ZoneCaptor,Is.EqualTo(-1)); Assert.That(session.ZoneContested,Is.False);
        }
        [Test] public void ShortNetworkBlinkClearsCameraFeedbackThroughPresentationBridge()
        {
            var presentation=owner.AddComponent<ArenaPresentation>();
            session.AttachNetworkMatch(match,1,avatars);
            var state=session.Motor.CaptureState(); state.Sliding=true;
            float baseFov=session.Camera.fieldOfView;
            session.View.RenderFrame(state,1);
            Assert.That(session.Camera.fieldOfView,Is.GreaterThan(baseFov+5));
            match.Find(1).DiscontinuityTick=1;
            presentation.SendMessage("LateUpdate");
            Assert.That(session.Camera.fieldOfView,Is.EqualTo(baseFov).Within(.001f));
            Assert.That(owner.GetComponentInChildren<FirstPersonBody>(),Is.Not.Null);
            session.DetachNetworkMatch();
        }
        [Test] public void LocalLaserStartsAtRenderedMuzzleAfterRecoilAndDoesNotReplay()
        {
            var presentation=owner.AddComponent<ArenaPresentation>();
            session.AttachNetworkMatch(match,1,avatars);
            presentation.SendMessage("LateUpdate");
            var action=new CombatAction {Participant=1,LifeGeneration=1,ConnectionGeneration=1,AuthorityGeneration=1,Sequence=1};
            var shot=new CombatEvent(CombatEventKind.Shot,action,1,position:Vector3.up*1.65f,vector:Vector3.forward,weapon:WeaponKind.LineDriver);
            session.PresentNetworkTick(new[]{shot},-1,false);
            var effects=owner.GetComponentInChildren<ArenaEffects>();
            var first=effects.transform.Find("Burst 0").GetComponent<LineRenderer>();
            Assert.That(first.enabled,Is.False,"Local shot waits for the current rendered weapon pose.");
            presentation.SendMessage("LateUpdate");
            Assert.That(owner.GetComponent<WeaponView>().TryGetMuzzle(out var muzzle),Is.True);
            Assert.That(Vector3.Distance(first.GetPosition(0),muzzle),Is.LessThan(.0001f));
            var second=effects.transform.Find("Burst 3").GetComponent<LineRenderer>(); // a Line Driver shot uses bursts 0 (glow), 1 (core) and 2 (sheath)
            bool previouslyEnabled=second.enabled;
            session.PresentNetworkTick(new[]{shot},-1,false); presentation.SendMessage("LateUpdate");
            Assert.That(second.enabled,Is.EqualTo(previouslyEnabled),"Repeated network event does not create another beam.");
        }
        static CombatEvent Shot(int sequence,Vector3 direction) => new CombatEvent(CombatEventKind.Shot,
            new CombatAction {Participant=1,LifeGeneration=1,ConnectionGeneration=1,AuthorityGeneration=1,Sequence=sequence},2,position:Vector3.up*1.65f,vector:direction,weapon:WeaponKind.LineDriver);
        LineRenderer Burst(int index) => owner.GetComponentInChildren<ArenaEffects>().transform.Find("Burst "+index).GetComponent<LineRenderer>();
        // Enabled bursts longer than 5 m are beams; flashes and sparks are short.
        List<LineRenderer> Beams()
        {
            var beams=new List<LineRenderer>();
            foreach(var line in owner.GetComponentInChildren<ArenaEffects>().GetComponentsInChildren<LineRenderer>(true))
                if(line.name.StartsWith("Burst ") && line.enabled && line.positionCount>=2 && Vector3.Distance(line.GetPosition(0),line.GetPosition(line.positionCount-1))>5) beams.Add(line);
            return beams;
        }
        [Test] public void PredictedShotPlaysOnceAndTheHostShotDoesNotRepeatIt()
        {
            var presentation=owner.AddComponent<ArenaPresentation>(); session.AttachNetworkMatch(match,1,avatars); presentation.SendMessage("LateUpdate");
            session.PredictShot(Shot(1,Vector3.forward)); presentation.SendMessage("LateUpdate");
            Assert.That(Burst(0).enabled,Is.True,"The press draws its beam before the host answers.");
            Assert.That(owner.GetComponent<WeaponView>().TryGetMuzzle(out var muzzle),Is.True);
            Assert.That(Vector3.Distance(Burst(0).GetPosition(0),muzzle),Is.LessThan(.0001f));
            Assert.That(presentation.PredictedShotsPending,Is.EqualTo(1)); int drawn=Beams().Count; Assert.That(drawn,Is.GreaterThan(0));
            session.PresentNetworkTick(new[]{Shot(1,Vector3.forward)},-1,false); presentation.SendMessage("LateUpdate");
            Assert.That(Beams().Count,Is.EqualTo(drawn),"The host's Shot with the same action identity draws nothing new.");
            Assert.That(presentation.PredictedShotsPending,Is.Zero);
            var hud=new ArenaHud(); hud.Refresh(session); Assert.That(hud.SpeedVisible,Is.False,"The speed readout is Practice-only.");
        }
        [Test] public void RejectedPredictedShotPlaysNothingFurther()
        {
            var presentation=owner.AddComponent<ArenaPresentation>(); session.AttachNetworkMatch(match,1,avatars); presentation.SendMessage("LateUpdate");
            session.PredictShot(Shot(1,Vector3.forward)); presentation.SendMessage("LateUpdate");
            int drawn=Beams().Count; Assert.That(drawn,Is.GreaterThan(0));
            session.PresentNetworkTick(new[]{new CombatEvent(CombatEventKind.Rejected,Shot(1,Vector3.forward).Action,2,reason:"test")},-1,false); presentation.SendMessage("LateUpdate");
            Assert.That(Beams().Count,Is.EqualTo(drawn)); Assert.That(presentation.PredictedShotsPending,Is.Zero);
            session.PredictShot(Shot(2,Vector3.forward)); // never answered: the deadline alone forgets it
            presentation.SendMessage("LateUpdate"); Assert.That(presentation.PredictedShotsPending,Is.EqualTo(1));
        }
        [Test] public void PredictedBeamIsCorrectedToTheHostRay()
        {
            var presentation=owner.AddComponent<ArenaPresentation>(); session.AttachNetworkMatch(match,1,avatars); presentation.SendMessage("LateUpdate");
            session.PredictShot(Shot(1,Vector3.forward)); presentation.SendMessage("LateUpdate");
            var guessed=Beams(); Assert.That(guessed.Count,Is.GreaterThan(0));
            session.PresentNetworkTick(new[]{Shot(1,Vector3.right)},-1,false); presentation.SendMessage("LateUpdate");
            var after=Beams(); Assert.That(after.Count,Is.GreaterThan(guessed.Count),"A host ray more than a quarter metre off redraws the beam.");
            foreach(var line in after) if(!guessed.Contains(line))
                Assert.That(Vector3.Dot((line.GetPosition(line.positionCount-1)-line.GetPosition(0)).normalized,Vector3.right),Is.GreaterThan(.99f),"The redrawn beam follows the host ray.");
        }
        // Offline practice drives its target fixture with the strafing drill (PracticeSession.Drill) instead of freezing it.
        [Test] public void PracticeTargetFixtureStrafesFacingThePlayer()
        {
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.position=Vector3.down*.5f;
            ground.transform.localScale=new Vector3(80,1,80); ground.AddComponent<MotorSurface>(); Physics.SyncTransforms();
            try
            {
                typeof(PracticeSession).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{map});
                var step=typeof(PracticeSession).GetMethod("Step",BindingFlags.Instance|BindingFlags.NonPublic);
                for(int i=0;i<360;i++) step.Invoke(session,new object[]{true});
                var target=session.Motors[PracticeSession.TargetParticipant].CaptureState();
                Assert.That(Vector3.Distance(target.Position,Vector3.forward*6),Is.GreaterThan(1),"The target moved off its spawn.");
                var toPlayer=session.LocalPlayer.Feet-target.Position; toPlayer.y=0;
                Assert.That(Vector3.Angle(session.Match.Find(PracticeSession.TargetParticipant).Aim,toPlayer),Is.LessThan(10),"It faces the player.");
            }
            finally { Object.DestroyImmediate(ground); }
        }
        [Test] public void ExternalRegistryCallsCannotReplaceOfflinePracticeMotors()
        {
            typeof(PracticeSession).GetMethod("Begin",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{map});
            var original=session.Motor; var originalMatch=session.Match;
            session.RefreshNetworkAvatars(avatars,poses); session.PresentNetworkTick(); session.DetachNetworkMatch();
            Assert.That(session.ExternalSimulation,Is.False); Assert.That(session.Motor,Is.SameAs(original)); Assert.That(session.Match,Is.SameAs(originalMatch));
        }
        [Test] public void VisibleRemoteHumanUsesInterpolatedPoseAndRemainsVisibleUntilDisconnectedLifeEnds()
        {
            var visual=remote.AddComponent<PlayerVisual>(); var catalog=ScriptableObject.CreateInstance<PresentationCatalog>();
            try
            {
                var player=match.Find(2); player.Connected=false; player.Alive=true; player.Feet=Vector3.right*20;
                pose.transform.SetPositionAndRotation(Vector3.right*5,Quaternion.Euler(0,65,0));
                visual.Present(player,default,catalog,pose.transform);
                Assert.That(visual.transform.position,Is.EqualTo(pose.transform.position));
                Assert.That(Quaternion.Angle(visual.transform.rotation,pose.transform.rotation),Is.LessThan(.01f));
                Assert.That(visual.gameObject.activeSelf,Is.True,"Disconnected live avatar remains visible during vulnerability.");
                player.Alive=false; visual.Present(player,default,catalog,pose.transform);
                Assert.That(visual.gameObject.activeSelf,Is.False,"Disconnected expired life is hidden.");
            }
            finally { Object.DestroyImmediate(catalog); }
        }
    }
}
