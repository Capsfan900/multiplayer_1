using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildstone.Movement;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.UI;
using Wildstone.Networking;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace Wildstone.Tests
{
    [TestFixture, Category("Native")]
    public sealed class FusionPlayerRunnerTests
    {
        NetworkRunner runner;
        GameObject prefab,discoveryOwner,discoveryCamera;
        CancellationTokenSource cancellation;

        [UnityTest]
        public IEnumerator RealSingleRunnerSpawnsBakedPlayerAndAdvancesAt120Hz()
        {
            prefab = new GameObject("runtime baked movement fixture");
            prefab.transform.position = Vector3.up * 10000;
            var source = prefab.AddComponent<FusionPlayer>();
            var baked = new NetworkObjectBaker().Bake(prefab);
            Assert.That(baked.BehaviourCount, Is.EqualTo(1));
            var config = NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            var prefabId = config.PrefabTable.AddSource(new NetworkPrefabSourceStatic
            {
                Object = source.GetComponent<NetworkObject>(),
                AssetGuid = NetworkObjectGuid.Parse(Guid.NewGuid().ToString("N")), AssetPath = "runtime fixture"
            });
            runner = new GameObject("movement test runner").AddComponent<NetworkRunner>();
            cancellation = new CancellationTokenSource();
            var start = runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single, Config = config,
                SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runner.gameObject.AddComponent<NetworkObjectProviderDefault>(),
                StartGameCancellationToken = cancellation.Token
            });
            float deadline = Time.realtimeSinceStartup + 15;
            while (!start.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(start.IsCompleted, Is.True, "Single runner startup timed out");
            Assert.That(start.IsFaulted, Is.False, start.Exception?.ToString());
            Assert.That(start.Result.Ok, Is.True, start.Result.ToString());
            Assert.That(runner.DeltaTime, Is.EqualTo(MotorTuning.TickDelta));

            int firstTick = runner.Tick.Raw + 1;
            var spawnPosition = new Vector3(0, 10, 0);
            var spawned = runner.Spawn(prefabId, spawnPosition, Quaternion.identity,
                onBeforeSpawned: (r, instance) =>
                {
                    var initial = instance.GetComponent<ArenaMotor>().CaptureState();
                    TestContext.Out.WriteLine($"Spawn callback transform={instance.transform.position}, Awake motor snapshot={initial.Position}");
                    Assert.That(Vector3.Distance(instance.transform.position, spawnPosition), Is.LessThan(.0001f), "SDK spawn pose before authoritative initialization");
                    // Awake can run at the source prefab pose; admission supplies the trusted spawn pose.
                    initial.Position = spawnPosition;
                    initial.Yaw = 0;
                    initial.Generation = 3;
                    instance.GetComponent<FusionPlayer>().Initialize(initial, firstTick);
                });
            Assert.That(spawned, Is.Not.Null);
            var player = spawned.GetComponent<FusionPlayer>();
            Assert.That(Vector3.Distance(player.State.Position, spawnPosition), Is.LessThan(.0001f), "Authoritative initial spawn position");
            Assert.That(Vector3.Distance(spawned.transform.position, spawnPosition), Is.LessThan(.0001f), "Collider initial spawn position");
            deadline = Time.realtimeSinceStartup + 5;
            while (player.State.Tick < 3 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.State.Tick, Is.GreaterThanOrEqualTo(3));
            Assert.That(player.State.Generation, Is.EqualTo(3));
            Assert.That(player.State.Tick, Is.EqualTo(runner.Tick.Raw - firstTick));
            Assert.That(player.State.Position.y, Is.LessThan(10), "Real fixed ticks must apply gravity");
            Assert.That(player.GetComponent<ArenaMotor>().CaptureState().Tick, Is.EqualTo(player.State.Tick));
            Assert.Throws<InvalidOperationException>(() => player.Initialize(player.State.ToMotor(), firstTick));
            int epoch = player.PoseEpoch;
            player.Teleport(new Vector3(4, 12, 6), 35);
            Assert.That(player.PoseEpoch, Is.EqualTo(epoch + 1));
            Assert.That(Vector3.Distance(player.State.Position, new Vector3(4, 12, 6)), Is.LessThan(.0001f));
            // Discovery uses a real runner registry; no service identity/admission is implied by this fixture.
            player.Participant=1;
            discoveryOwner=new GameObject("runner-local discovery fixture");
            var presentation=discoveryOwner.AddComponent<PracticeSession>(); presentation.enabled=false;
            discoveryCamera=new GameObject("discovery presentation camera",typeof(Camera));
            discoveryCamera.GetComponent<Camera>().enabled=false; presentation.Camera=discoveryCamera.GetComponent<Camera>();
            var settings=new LocalSettings();
            typeof(PracticeSession).GetField("<Settings>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(presentation,settings);
            typeof(PracticeSession).GetField("<Input>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(presentation,new PracticeInput(settings));
            discoveryOwner.AddComponent<FusionMenuController>().enabled=false;
            var coordinator=discoveryOwner.AddComponent<FusionGameCoordinator>(); coordinator.enabled=false;
            const BindingFlags privateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
            var discover=(Action<NetworkRunner>)Delegate.CreateDelegate(typeof(Action<NetworkRunner>),coordinator,
                typeof(FusionGameCoordinator).GetMethod("RefreshAvatars",privateInstance));
            var registry=(Dictionary<int,ArenaMotor>)typeof(FusionGameCoordinator).GetField("motors",privateInstance).GetValue(coordinator);
            discover(runner); Assert.That(registry.Count,Is.EqualTo(1)); Assert.That(registry[1],Is.SameAs(player.GetComponent<ArenaMotor>()));
            var model=new ArenaMatch(ArenaLayouts.CreateAll()[0],120);
            Assert.That(model.AddPlayer(1,0,1,player.State.Generation,player.State.Position),Is.True);
            Assert.That(model.AddPlayer(2,1,2,1,new Vector3(8,12,6)),Is.True);
            presentation.AttachNetworkMatch(model,1,registry);
            typeof(FusionGameCoordinator).GetField("attached",privateInstance).SetValue(coordinator,true);
            var late=runner.Spawn(prefabId,new Vector3(8,12,6),Quaternion.identity,onBeforeSpawned:(r,instance)=>
            {
                var state=instance.GetComponent<ArenaMotor>().CaptureState(); state.Position=new Vector3(8,12,6); state.Generation=1;
                var avatar=instance.GetComponent<FusionPlayer>(); avatar.Initialize(state,r.Tick.Raw+1); avatar.Participant=2;
            });
            discover(runner); Assert.That(registry.Count,Is.EqualTo(2),"A later registered spawn must join the presentation registry.");
            Assert.That(presentation.Motors[2],Is.SameAs(late.GetComponent<ArenaMotor>()));
            var replacementVisual=new GameObject("replacement render root").transform;
            replacementVisual.SetParent(player.transform,false); player.VisualRoot=replacementVisual;
            discover(runner);
            Assert.That(presentation.RenderPose(1),Is.SameAs(replacementVisual),"Changing only a render root must refresh the attached presentation bridge.");
            var counter=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",Type.EmptyTypes);
            if(counter!=null)
            {
                var bytes=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),counter);
                for(int i=0;i<32;i++) discover(runner);
                bytes(); long before=bytes();
                var calibration=new byte[64*1024]; calibration[0]=1;
                long calibrationBytes=bytes()-before; GC.KeepAlive(calibration);
                bool calibrated=calibrationBytes>=64*1024;
                before=bytes();
                for(int i=0;i<128;i++) discover(runner);
                long discoveryBytes=bytes()-before;
                var legacyResults=new FusionPlayer[128][];
                Object.FindObjectsByType<FusionPlayer>(); before=bytes();
                for(int i=0;i<legacyResults.Length;i++) legacyResults[i]=Object.FindObjectsByType<FusionPlayer>();
                long sceneQueryBytes=bytes()-before;
                int retainedObjects=0; foreach(var result in legacyResults) retainedObjects+=result.Length;
                GC.KeepAlive(legacyResults);
                if(calibrated)
                {
                    Assert.That(discoveryBytes,Is.Zero,"Warmed attached discovery must reuse buffers and skip unchanged presentation copies.");
                    Assert.That(sceneQueryBytes,Is.GreaterThan(0),"The calibrated counter must observe retained scene-query arrays.");
                    TestContext.Out.WriteLine($"Calibrated allocation counter: probe={calibrationBytes} bytes; 128 attached discovery scans={discoveryBytes} bytes; 128 retained scene queries={sceneQueryBytes} bytes ({retainedObjects} returned objects). Snapshots and frame presentation are outside this measurement.");
                }
                else TestContext.Out.WriteLine($"Allocation measurement unavailable: a retained 65536-byte probe reported {calibrationBytes} bytes. Discovery={discoveryBytes} and scene-query={sceneQueryBytes} counter results are untrusted ({retainedObjects} scene-query objects retained); no zero-allocation or savings claim. Spawn/despawn/render-root behavior remains tested.");
            }
            else TestContext.Out.WriteLine("Allocation measurement unavailable: no per-thread allocation counter. Spawn/despawn/render-root behavior remains tested; no allocation claim.");
            runner.Despawn(late); discover(runner); Assert.That(registry.Count,Is.EqualTo(1),"A despawn must leave the presentation registry.");
            Assert.That(presentation.Motors.ContainsKey(2),Is.False);
            runner.Despawn(spawned); discover(runner); Assert.That(registry.Count,Is.Zero);

        }

        [UnityTearDown]
        public IEnumerator CleanupRunnerAndCancelPendingStartup()
        {
            cancellation?.Cancel();
            try
            {
                if (runner != null)
                {
                    var shutdown = runner.Shutdown();
                    float deadline = Time.realtimeSinceStartup + 15;
                    while (!shutdown.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(shutdown.IsCompleted, Is.True, "Runner shutdown timed out");
                    Assert.That(shutdown.IsFaulted, Is.False, shutdown.Exception?.ToString());
                }
            }
            finally
            {
                if (runner != null) Object.Destroy(runner.gameObject);
                if (prefab != null) Object.Destroy(prefab);
                if (discoveryOwner != null) Object.Destroy(discoveryOwner);
                if (discoveryCamera != null) Object.Destroy(discoveryCamera);
                cancellation?.Dispose(); cancellation = null; runner = null; prefab = null; discoveryOwner=null; discoveryCamera=null;
            }
            yield return null;
        }

        [TestCase(false), TestCase(true)]
        public void ProxyVisualsInterpolateButColliderAndStanceStayAuthoritative(bool discontinuity)
        {
            var go = new GameObject("proxy pose fixture");
            try
            {
                var motor = go.AddComponent<ArenaMotor>();
                var visual = new GameObject("visual root").transform;
                visual.SetParent(go.transform, false);
                var from = motor.CaptureState(); from.Tick = 10; from.Position = new Vector3(0, 5, 0); from.Yaw = 170;
                var to = from; to.Tick = 11; to.Position = new Vector3(2, 5, 0); to.Yaw = -170;
                to.CapsuleHeight = 1; to.CapsuleCenter = new Vector3(0, .5f, 0); to.Sliding = true;
                FusionProxyPose.Apply(motor, visual, to, from, to, .5f, discontinuity);
                Assert.That(Vector3.Distance(go.transform.position, to.Position), Is.LessThan(.0001f));
                Assert.That(go.GetComponent<CharacterController>().height, Is.EqualTo(1));
                Assert.That(motor.CaptureState().Tick, Is.EqualTo(11));
                Assert.That(Vector3.Distance(visual.position, discontinuity ? to.Position : new Vector3(1, 5, 0)), Is.LessThan(.0001f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(visual.eulerAngles.y, discontinuity ? -170 : 180)), Is.LessThan(.001f));
                from.Generation = 1; to.Generation = 2;
                FusionProxyPose.Apply(motor, visual, to, from, to, .5f, false);
                Assert.That(Vector3.Distance(visual.position, to.Position), Is.LessThan(.0001f), "Never interpolate across a life change");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
