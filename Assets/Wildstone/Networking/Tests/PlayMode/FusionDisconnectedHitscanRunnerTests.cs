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
    public sealed class FusionDisconnectedHitscanRunnerTests
    {
        NetworkRunner runner;
        GameObject matchPrefab, playerPrefab, floor;
        CancellationTokenSource cancellation;

        [UnityTest]
        public IEnumerator RealSdkHitscanDamagesAnAliveDisconnectedHeldAvatar()
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
            var targetPosition = new Vector3(0, .02f, -10);
            var targetObject = runner.Spawn(playerId, targetPosition, Quaternion.identity, runner.LocalPlayer,
                (r, obj) =>
                {
                    var state = obj.GetComponent<ArenaMotor>().CaptureState();
                    state.Position = targetPosition; state.Generation = 1;
                    obj.GetComponent<FusionPlayer>().Initialize(state, firstTick);
                });
            Assert.That(match.BindPlayer(2, targetObject.GetComponent<FusionPlayer>(), 1, 1), Is.True);
            Assert.That(match.Begin(), Is.True);
            int targetTick = match.Model.Tick + 4;
            deadline = Time.realtimeSinceStartup + 5;
            while (match.Model.Tick < targetTick && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(match.Model.Tick, Is.GreaterThanOrEqualTo(targetTick), "Real runner must populate hitbox history");
            Assert.That(match.SetConnection(2, 1, false), Is.True);
            var target = match.Model.Find(2);
            Assert.That(target.Alive, Is.True);
            Assert.That(target.Connected, Is.False);
            Assert.That(target.Health, Is.EqualTo(100));
            var request = new FusionCombatAction
            {
                Participant = 1, ConnectionGeneration = 1, AuthorityGeneration = match.Model.AuthorityGeneration,
                // Chest height: the fixture has no head hitbox and the torso box stops under the head (D040).
                LifeGeneration = 1, Sequence = 0, Kind = CombatActionKind.Fire, Direction = new Vector3(0, -.06f, 1).normalized
            };
            Assert.That(match.SubmitAction(player, request), Is.True);
            targetTick = match.Model.Tick + 3;
            deadline = Time.realtimeSinceStartup + 1;
            while (match.Model.Tick < targetTick && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(match.Model.Tick, Is.GreaterThanOrEqualTo(targetTick));
            Assert.That(match.Model.Find(1).StarterAmmo, Is.EqualTo(WeaponCatalog.StarterAmmo-1), "Authoritative shot was accepted");
            Assert.That(target.Alive, Is.True, "This checks the held vulnerability window before expiry");
            Assert.That(target.Health, Is.EqualTo(WeaponCatalog.Health-WeaponCatalog.Get(WeaponKind.LineDriver).Damage), "Real SDK historical hitboxes must remain damageable while an alive avatar is disconnected");
            var snapshot = match.CaptureSnapshot();
            Assert.That(snapshot.Players[1].Health, Is.EqualTo(WeaponCatalog.Health-WeaponCatalog.Get(WeaponKind.LineDriver).Damage), "The verdict must join replicated checkpoint state");
            runner.Despawn(targetObject);
            runner.Despawn(playerObject);
            runner.Despawn(matchObject);
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
                cancellation?.Dispose(); cancellation = null; runner = null;
                matchPrefab = null; playerPrefab = null; floor = null;
            }
            yield return null;
        }
    }
}
