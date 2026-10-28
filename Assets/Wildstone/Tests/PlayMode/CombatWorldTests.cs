using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Movement;

namespace Wildstone.Tests
{
    // Native physics fixtures: compile evidence is not a passing Unity PlayMode run.
    public class CombatWorldTests
    {
        readonly List<GameObject> objects=new List<GameObject>();
        ArenaDefinition map;
        UnityCombatWorld world;
        ArenaMotor attacker, target;
        GameObject Box(string name,Vector3 position,Vector3 size)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(go);
            go.name=name; go.transform.position=position; go.transform.localScale=size; return go;
        }
        ArenaMotor Player(int participant,Vector3 feet)
        {
            var go=new GameObject("player "+participant); objects.Add(go); go.transform.position=feet;
            var motor=go.AddComponent<ArenaMotor>(); world.Bind(participant,motor); return motor;
        }
        [SetUp] public void Setup()
        {
            map=new ArenaDefinition { Minimum=new Vector3(-20,-1,-20), Maximum=new Vector3(20,20,20) };
            world=new UnityCombatWorld(map);
            Box("floor",new Vector3(0,-.5f,0),new Vector3(40,1,40));
            attacker=Player(1,new Vector3(0,.05f,0)); target=Player(2,new Vector3(0,.05f,5)); Physics.SyncTransforms();
        }
        [TearDown] public void Cleanup() { foreach(var go in objects) Object.DestroyImmediate(go); objects.Clear(); }
        [Test] public void NativeMotorStartupPreservesDisabledController()
        {
            var go=new GameObject("Disabled controller fixture",typeof(CharacterController)); objects.Add(go);
            var capsule=go.GetComponent<CharacterController>(); capsule.enabled=false;
            go.AddComponent<ArenaMotor>();
            Assert.That(capsule.enabled,Is.False);
        }
        [Test] public void NativeMotorStartupKeepsBoundCapsuleQueryable()
        {
            Vector3 origin=attacker.transform.position+Vector3.up*1.5f;
            target.Simulate(new MotorInput { Present=true,Tick=0,Sequence=0,Generation=1 },MotorTuning.TickDelta,0);
            Physics.SyncTransforms();
            Assert.That(world.Sweep(origin,Vector3.forward*10,0,1,0,out var hit),Is.True);
            Assert.That(hit.Participant,Is.EqualTo(2));
        }
        [Test] public void NativeHitscanAndSphereSweepHitBoundCapsuleAndRespectWall()
        {
            Vector3 origin=attacker.transform.position+Vector3.up*1.5f;
            Assert.That(world.Sweep(origin,Vector3.forward*10,0,1,0,out var ray),Is.True); Assert.That(ray.Participant,Is.EqualTo(2));
            Assert.That(world.Sweep(origin,Vector3.forward*10,.12f,1,0,out var sphere),Is.True); Assert.That(sphere.Participant,Is.EqualTo(2));
            Box("cover",new Vector3(0,1.5f,2),new Vector3(3,3,.4f)); Physics.SyncTransforms();
            Assert.That(world.Sweep(origin,Vector3.forward*10,.12f,1,0,out var blocked),Is.True); Assert.That(blocked.Participant,Is.Zero);
            Assert.That(world.Visible(origin,target.transform.position+Vector3.up*1.5f),Is.False);
        }
        [Test] public void NativeBlinkClampsBeforeWallAndRejectsNoDisplacement()
        {
            Box("wall",new Vector3(0,2,3),new Vector3(10,4,.4f)); Physics.SyncTransforms();
            var p=new CombatState { Participant=1, Feet=attacker.transform.position };
            Assert.That(world.TryBlink(p,Vector3.forward,6,out var landing),Is.True);
            Assert.That(landing.z,Is.LessThan(2.5f)); Assert.That(landing.z,Is.GreaterThan(1));
            attacker.Teleport(new Vector3(0,.05f,2.35f),0); p.Feet=attacker.transform.position; Physics.SyncTransforms();
            Assert.That(world.TryBlink(p,Vector3.forward,6,out _),Is.False);
        }
        [Test] public void NativeBlinkRejectsUnsupportedDestinationAndCeiling()
        {
            // A vertical request has no legal short supported displacement.
            var p=new CombatState { Participant=1, Feet=attacker.transform.position };
            Assert.That(world.TryBlink(p,Vector3.up,6,out _),Is.False);
            Box("low ceiling",new Vector3(0,1.2f,3),new Vector3(4,.3f,5.8f)); Physics.SyncTransforms();
            Assert.That(world.TryBlink(p,Vector3.forward,6,out _),Is.False);
        }
        [Test] public void NativeSupportSeparatesGroundFromUpperFloorAndAir()
        {
            Box("upper floor",new Vector3(4,3.8f,0),new Vector3(4,.4f,4)); Physics.SyncTransforms();
            Assert.That(world.TrySupport(new Vector3(4,4.05f,0),out var floor,out var normal),Is.True);
            Assert.That(floor.y,Is.EqualTo(4).Within(.01)); Assert.That(normal.y,Is.GreaterThan(.99f));
            Assert.That(world.TrySupport(new Vector3(0,2,0),out _,out _),Is.False);
        }
        [Test] public void DeadBoundAvatarDoesNotBlockLiveShots()
        {
            world.Bind(2,target,()=>false);
            Assert.That(world.Sweep(attacker.transform.position+Vector3.up*1.5f,Vector3.forward*10,0,1,0,out _),Is.False);
        }
    }
}
