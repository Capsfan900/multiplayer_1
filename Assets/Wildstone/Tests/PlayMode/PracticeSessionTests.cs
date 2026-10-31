using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Movement;
using Wildstone.UI;

namespace Wildstone.Tests
{
    // Native coordinator checks; managed type checking does not execute these tests.
    public sealed class PracticeSessionTests
    {
        [Test] public void WarmedIdleCoordinatorTicksAllocateNoManagedBytes()
        {
            var step=(System.Action<bool>)System.Delegate.CreateDelegate(typeof(System.Action<bool>),session,typeof(PracticeSession).GetMethod("Step",BindingFlags.Instance|BindingFlags.NonPublic));
            var counter=typeof(System.GC).GetMethod("GetAllocatedBytesForCurrentThread",System.Type.EmptyTypes);
            Assert.That(counter,Is.Not.Null,"Native runtime must expose the allocation counter.");
            var bytes=(System.Func<long>)System.Delegate.CreateDelegate(typeof(System.Func<long>),counter);
            for(int i=0;i<256;i++) step(false);
            bytes(); long before=bytes();
            for(int i=0;i<256;i++) step(false);
            long allocated=bytes()-before;
            Assert.That(allocated,Is.Zero,"Warm coordinator/movement/combat path must not allocate; no presentation subscribers are attached.");
        }
        [Test] public void ApplyingSettingsKeepsTheOnlineMovementSequenceRising()
        {
            int last=0; for(int i=1;i<=5;i++) last=session.Input.ConsumeMovement(i,1,0,0).Sequence;
            session.ApplySettings(session.Settings,false);
            Assert.That(session.Input.ConsumeMovement(6,1,0,0).Sequence,Is.EqualTo(last+1),"Host and client drop samples whose sequence does not rise.");
        }
        [Test] public void SteadyHudRefreshAllocatesNoManagedBytes()
        {
            var hud=new ArenaHud();
            var bytes=(System.Func<long>)System.Delegate.CreateDelegate(typeof(System.Func<long>),typeof(System.GC).GetMethod("GetAllocatedBytesForCurrentThread",System.Type.EmptyTypes));
            for(int i=0;i<8;i++) hud.Refresh(session);
            bytes(); long before=bytes();
            for(int i=0;i<64;i++) hud.Refresh(session);
            Assert.That(bytes()-before,Is.Zero,"Unchanged HUD values must not rebuild their strings every frame.");
            session.LocalPlayer.Health-=7.5f; hud.Refresh(session);
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(hud.Root,className:"hud-health").text,Is.EqualTo(UnityEngine.Mathf.CeilToInt(session.LocalPlayer.Health).ToString()),"A changed value still updates.");
        }
        // A Blink press whose charge is still held after the round trip was refused; a spent one is not a refusal.
        [UnityTest] public IEnumerator RefusedBlinkShowsADenyCueButASpentOneDoesNot()
        {
            var hud=new ArenaHud(); var p=session.LocalPlayer; var presses=typeof(PracticeInput).GetProperty("BlinkPresses");
            p.BlinkCharges=1; hud.Refresh(session);
            presses.SetValue(session.Input,session.Input.BlinkPresses+1); hud.Refresh(session); p.BlinkCharges=0;
            yield return new WaitForSecondsRealtime(ArenaHud.BlinkDenySeconds+.1f);
            hud.Refresh(session); Assert.That(hud.BlinkDenied,Is.False,"The charge was spent.");
            p.BlinkCharges=1; presses.SetValue(session.Input,session.Input.BlinkPresses+1); hud.Refresh(session);
            yield return new WaitForSecondsRealtime(ArenaHud.BlinkDenySeconds+.1f);
            hud.Refresh(session); Assert.That(hud.BlinkDenied,Is.True,"The charge is still held.");
            Assert.That(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(hud.Root,className:"hud-prompt").text,Does.StartWith("NO FLOOR TO BLINK"));
        }
        GameObject owner,floor,camera;
        PracticeSession session;
        static void Invoke(PracticeSession target,string method,params object[] args) => typeof(PracticeSession).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static void Set(PracticeSession target,string name,object value) => typeof(PracticeSession).GetField("<"+name+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        [SetUp] public void Setup()
        {
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position=new Vector3(0,-.5f,0); floor.transform.localScale=new Vector3(60,1,60);
            floor.AddComponent<MotorSurface>().Id=1;
            camera=new GameObject("Test camera",typeof(Camera)); camera.GetComponent<Camera>().enabled=false;
            owner=new GameObject("Practice coordinator test"); session=owner.AddComponent<PracticeSession>(); session.enabled=false; session.Camera=camera.GetComponent<Camera>();
            var settings=new LocalSettings(); Set(session,"Settings",settings); Set(session,"Input",new PracticeInput(settings));
            var map=new ArenaDefinition { Id="atrium",Minimum=new Vector3(-30,-1,-30),Maximum=new Vector3(30,20,30),
                Spawns=new[]{new ArenaSpawn{Team=0,Position=new Vector3(0,.05f,0)},new ArenaSpawn{Team=1,Position=new Vector3(0,.05f,6)}},
                ZoneRoute=new[]{new Vector3(12,0,0),new Vector3(18,0,0),new Vector3(18,0,6)} };
            Physics.SyncTransforms(); Invoke(session,"Begin",map);
        }
        [TearDown] public void Cleanup()
        {
            foreach(var motor in session.Motors.Values.ToArray()) Object.DestroyImmediate(motor.gameObject);
            Object.DestroyImmediate(owner); Object.DestroyImmediate(camera); Object.DestroyImmediate(floor);
        }
        [Test] public void FirstTickAndSettingsInputReplacementKeepMotorSequenceValid()
        {
            Invoke(session,"Step",true);
            Assert.That(session.Match.Tick,Is.EqualTo(1)); Assert.That(session.Motor.CaptureState().Tick,Is.EqualTo(1));
            session.Input.Dispose(); Set(session,"Input",new PracticeInput(session.Settings));
            Invoke(session,"Step",true); Assert.That(session.Motor.CaptureState().Tick,Is.EqualTo(2));
        }
        [Test] public void PausedMenuContinuesSimulationWithoutHeldMovement()
        {
            session.Show(MenuScreen.Pause); var before=session.Motor.CaptureState().Position;
            Invoke(session,"Step",false);
            Assert.That(session.Match.Tick,Is.EqualTo(1)); Assert.That(session.Motor.CaptureState().Suppressed,Is.True);
            Assert.That(session.Motor.CaptureState().Position.x,Is.EqualTo(before.x)); Assert.That(session.Motor.CaptureState().Position.z,Is.EqualTo(before.z));
        }
        [Test] public void RespawnInstallsFreshMotorLifeBeforeSameTickMovement()
        {
            session.LocalPlayer.Health=0; Invoke(session,"Step",true); Assert.That(session.LocalPlayer.Alive,Is.False);
            session.LocalPlayer.RespawnTick=session.Match.Tick+1; Invoke(session,"Step",true);
            Assert.That(session.LocalPlayer.Alive,Is.True); Assert.That(session.Motor.CaptureState().Generation,Is.EqualTo(session.LocalPlayer.LifeGeneration));
            Assert.That(session.Motor.CaptureState().Tick,Is.EqualTo(session.Match.Tick));
            Assert.That(session.Motor.CaptureState().AirDashUsed,Is.False); Assert.That(session.LocalPlayer.Feet,Is.EqualTo(session.Motor.CaptureState().Position));
        }
        [Test] public void ReducedMotionSuppressesRollWithoutSuppressingLookOrEvents()
        {
            var state=session.Motor.CaptureState(); state.WallRunning=true; state.WallNormal=Vector3.left;
            state.LastWallNormal=Vector3.left; state.Events=MotorEvents.WallJump; state.EventTick=state.Tick;
            session.Motor.RestoreState(state); session.View.ReducedMotion=true; session.View.SampleLook(new Vector2(15,5));
            int events=0; session.View.LocalEvents+=_=>events++;
            typeof(Wildstone.Presentation.FirstPersonView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session.View,null);
            Assert.That(Mathf.DeltaAngle(0,session.Camera.transform.eulerAngles.z),Is.EqualTo(0).Within(.01f));
            Assert.That(session.View.Yaw,Is.EqualTo(15)); Assert.That(session.View.Pitch,Is.EqualTo(-5)); Assert.That(events,Is.EqualTo(1));
        }
    }
}
