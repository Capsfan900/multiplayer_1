using System;
using System.Collections;
using System.Threading;
using Fusion;
using Assert = NUnit.Framework.Assert;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.Networking;
using Object = UnityEngine.Object;

namespace Wildstone.Tests
{
    [TestFixture, Category("Native")]
    public sealed class FusionOrbRunnerTests
    {
        NetworkRunner runner;
        GameObject matchPrefab, playerPrefab, floor, orbObject;
        CancellationTokenSource cancellation;

        [UnityTest]
        public IEnumerator RealSdkSharesOrbStateAcrossMotorClocksAndRestoresCheckpointCooldown()
        {
            var map = ArenaLayouts.CreateAll()[0];
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "match fixture floor";
            floor.layer = 0;
            floor.transform.position = new Vector3(0, -.5f, 0);
            floor.transform.localScale = new Vector3(150, 1, 150);
            matchPrefab = new GameObject("runtime match fixture");
            matchPrefab.AddComponent<FusionMatch>();
            new NetworkObjectBaker().Bake(matchPrefab);
            playerPrefab = new GameObject("runtime combat player fixture");
            playerPrefab.transform.position = Vector3.up * 10000;
            playerPrefab.AddComponent<FusionPlayer>();
            var root = playerPrefab.AddComponent<HitboxRoot>();
            root.Offset = new Vector3(0, .9f, 0); root.BroadRadius = 1.2f;
            var box = playerPrefab.AddComponent<Hitbox>();
            box.Type = HitboxTypes.Box; box.BoxExtents = new Vector3(.35f, .9f, .35f); box.Offset = new Vector3(0, .9f, 0);
            root.Hitboxes = new[] { box };
            new NetworkObjectBaker().Bake(playerPrefab);
            var config = NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            var matchId = AddPrefab(config, matchPrefab);
            var playerId = AddPrefab(config, playerPrefab);
            runner = new GameObject("match test runner").AddComponent<NetworkRunner>();
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
            orbObject = new GameObject("stable shared orb fixture");
            orbObject.transform.position = new Vector3(0, 1, -20);
            var orb = orbObject.AddComponent<MotorLaunchOrb>(); orb.Id = 71;
            var matchObject = runner.Spawn(matchId, onBeforeSpawned: (r, obj) => obj.GetComponent<FusionMatch>().Initialize(map, true));
            var match = matchObject.GetComponent<FusionMatch>();
            var position = new Vector3(0, .02f, -20);
            int firstTick = runner.Tick.Raw + 1;
            var playerObject = runner.Spawn(playerId, position, Quaternion.identity, runner.LocalPlayer,
                (r, obj) =>
                {
                    var state = obj.GetComponent<ArenaMotor>().CaptureState();
                    state.Position = position; state.Generation = 1;
                    obj.GetComponent<FusionPlayer>().Initialize(state, firstTick);
                });
            var player = playerObject.GetComponent<FusionPlayer>();
            Assert.That(match.BindPlayer(1, player, 1, 0), Is.True);
            var secondObject = runner.Spawn(playerId, position, Quaternion.identity, runner.LocalPlayer,
                (r, obj) =>
                {
                    var state = ArenaMotor.InitialState(position, 0);
                    obj.GetComponent<FusionPlayer>().Initialize(state, firstTick + 1);
                });
            var second = secondObject.GetComponent<FusionPlayer>();
            Assert.That(match.BindPlayer(2, second, 2, 1), Is.True);
            Assert.That(match.Begin(), Is.True);
            int target = match.Model.Tick + 2;
            deadline = Time.realtimeSinceStartup + 5;
            while (match.Model.Tick < target && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(match.Model.Tick, Is.GreaterThanOrEqualTo(target));
            Assert.That(match.OrbCount, Is.EqualTo(1));
            Assert.That(match.Orbs[0].Id, Is.EqualTo(71));
            Assert.That(match.Orbs[0].ActionSequence, Is.EqualTo(1));
            Assert.That(match.Orbs[0].ReadyTick, Is.EqualTo(firstTick + MotorLaunchOrb.RespawnTicks));
            Assert.That(player.State.Tick, Is.EqualTo(second.State.Tick + 1), "Different local motor clocks share the same runner-clock orb deadline");
            Assert.That(player.State.LastLaunchId, Is.EqualTo(71), "Stable host participant order owns the contested launch");
            Assert.That(second.State.LastLaunchId, Is.Zero);
            Assert.That(player.State.LastLaunchActionId, Is.EqualTo(1));
            Assert.That(player.State.Velocity.y, Is.GreaterThan(0));
            Assert.That(orb.CaptureState().ActionSequence, Is.Zero, "Network play must never mutate the untracked offline orb state");
            match.ValidateOrbCheckpoint(runner.Tick.Raw);
            match.RecoveryWarmup = true;
            var saved = match.CaptureSnapshot();
            var orbState = match.Orbs[0];
            int oldTick = match.OrbSnapshotRunnerTick;
            var restoredObject = runner.Spawn(matchId, onBeforeSpawned: (r, obj) =>
            {
                obj.GetComponent<FusionMatch>().CopyStateFrom(match);
                obj.GetComponent<FusionMatch>().RestoreCheckpoint(map, saved, 2);
            });
            var restored = restoredObject.GetComponent<FusionMatch>();
            Assert.That(restored.Orbs[0].ActionSequence, Is.EqualTo(1));
            Assert.That(restored.Orbs[0].ReadyTick - runner.Tick.Raw, Is.EqualTo(orbState.ReadyTick - oldTick));
            Assert.That(restored.TryConsume(orb, out _), Is.False, "Migration cannot reset a shared cooldown");
            restored.RecoveryWarmup = false;
            // Replaying the actual SDK array snapshot produces the same shared-world verdict.
            var before = new FusionOrbState { Id = 71, ActionSequence = 4, ReadyTick = runner.Tick.Raw };
            restored.Orbs.Set(0, before);
            Assert.That(restored.TryConsume(orb, out int action), Is.True);
            var after = restored.Orbs[0];
            restored.Orbs.Set(0, before);
            Assert.That(restored.TryConsume(orb, out int replay), Is.True);
            Assert.That(replay, Is.EqualTo(action));
            Assert.That(restored.Orbs[0].ReadyTick, Is.EqualTo(after.ReadyTick));
            Assert.That(restored.TryConsume(orb, out _), Is.False);
            runner.Despawn(playerObject); runner.Despawn(secondObject); runner.Despawn(matchObject); runner.Despawn(restoredObject);
        }

        static NetworkPrefabId AddPrefab(NetworkProjectConfig config, GameObject source) => config.PrefabTable.AddSource(new NetworkPrefabSourceStatic
        {
            Object = source.GetComponent<NetworkObject>(),
            AssetGuid = NetworkObjectGuid.Parse(Guid.NewGuid().ToString("N")), AssetPath = source.name
        });

        [UnityTearDown]
        public IEnumerator Cleanup()
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
                if (matchPrefab != null) Object.Destroy(matchPrefab);
                if (playerPrefab != null) Object.Destroy(playerPrefab);
                if (floor != null) Object.Destroy(floor);
                if (orbObject != null) Object.Destroy(orbObject);
                cancellation?.Dispose(); cancellation = null; runner = null;
                matchPrefab = null; playerPrefab = null; floor = null;
            }
            yield return null;
        }
    }
}
