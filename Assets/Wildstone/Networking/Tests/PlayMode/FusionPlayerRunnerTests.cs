using System;
using System.Collections;
using System.Threading;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildstone.Movement;
using Wildstone.Networking;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace Wildstone.Tests
{
    [TestFixture, Category("Native")]
    public sealed class FusionPlayerRunnerTests
    {
        NetworkRunner runner;
        GameObject prefab;
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
            runner.Despawn(spawned);
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
                cancellation?.Dispose(); cancellation = null; runner = null; prefab = null;
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
