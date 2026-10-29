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
    public sealed class FusionRecoveryHookRunnerTests
    {
        NetworkRunner runner;
        GameObject matchPrefab, playerPrefab, floor;
        CancellationTokenSource cancellation;

        [UnityTest]
        public IEnumerator RealSdkCopiesBehaviourCheckpointIntoNewObjectsAndResumesSequentialMotorTicks()
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
            var secondPosition = new Vector3(8, .02f, -20);
            var secondObject = runner.Spawn(playerId, secondPosition, Quaternion.identity, runner.LocalPlayer,
                (r, obj) =>
                {
                    var state = ArenaMotor.InitialState(secondPosition, 0); state.Generation = 2;
                    obj.GetComponent<FusionPlayer>().Initialize(state, firstTick);
                });
            var second = secondObject.GetComponent<FusionPlayer>();
            Assert.That(match.BindPlayer(2, second, 2, 1), Is.True);
            Assert.That(match.Begin(), Is.True);
            var combat = match.Model.Find(1);
            combat.Health = 73; combat.StarterAmmo = 7; combat.GrenadeAmmo = 1;
            combat.SpecialWeapon = WeaponKind.PushLens; combat.SpecialAmmo = 6; combat.SelectedSpecial = true;
            match.Model.Find(2).Health = 41; match.Model.Find(2).StarterAmmo = 3;
            var request = new FusionCombatAction
            {
                Participant = 1, ConnectionGeneration = 1, LifeGeneration = 1, AuthorityGeneration = 1,
                Sequence = 0, Kind = CombatActionKind.Grenade, Direction = Vector3.forward
            };
            Assert.That(match.SubmitAction(player, request), Is.True);
            request.Sequence = 1; request.Kind = CombatActionKind.Fire;
            Assert.That(match.SubmitAction(player, request), Is.True);
            int target = match.Model.Tick + 3;
            deadline = Time.realtimeSinceStartup + 5;
            while (match.Model.Tick < target && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(match.Model.Tick, Is.GreaterThanOrEqualTo(target));
            Assert.That(combat.Pending, Is.Not.Null);
            Assert.That(match.Model.Projectiles.Count, Is.EqualTo(1));
            player.LastSampleSequence = 17; second.LastSampleSequence = 23;
            match.RecoveryWarmup = true;
            var saved = match.CaptureSnapshot();
            var motors = new[] { player.State, second.State };
            var samples = new[] { player.LastSampleSequence, second.LastSampleSequence };
            var epochs = new[] { player.PoseEpoch, second.PoseEpoch };
            int savedEventSequence = match.EventSequence;
            var restoredObject = runner.Spawn(matchId, onBeforeSpawned: (r, obj) =>
            {
                // Single mode assigns new IDs; resume-object identity copying requires an actual migration token.
                obj.GetComponent<FusionMatch>().CopyStateFrom(match);
                var restored = obj.GetComponent<FusionMatch>();
                restored.RestoreCheckpoint(map, saved, 2);
                restored.RecoveryWarmup = true;
            });
            var recovered = restoredObject.GetComponent<FusionMatch>();
            var originals = new[] { playerObject, secondObject };
            var replacements = new NetworkObject[2];
            for (int i = 0; i < replacements.Length; i++)
            {
                int index = i;
                replacements[index] = runner.Spawn(playerId, motors[index].Position, Quaternion.identity, PlayerRef.None,
                    (r, obj) =>
                    {
                        obj.GetComponent<FusionPlayer>().CopyStateFrom(originals[index].GetComponent<FusionPlayer>());
                        obj.GetComponent<FusionPlayer>().RestoreCheckpoint(motors[index], samples[index], epochs[index], index + 1,
                            r.Tick.Raw - motors[index].Tick);
                    });
                replacements[index].RemoveInputAuthority();
                var avatar = replacements[index].GetComponent<FusionPlayer>();
                recovered.BindRecoveredPlayer(index + 1, avatar);
                Assert.That(replacements[index].InputAuthority, Is.EqualTo(PlayerRef.None), "Old SDK peers never retain control after recovery");
                Assert.That(avatar.LastSampleSequence, Is.EqualTo(samples[index]));
                Assert.That(avatar.PoseEpoch, Is.EqualTo(epochs[index] + 1));
                AssertMotor(motors[index], avatar.State);
            }
            Assert.That(recovered.Header.AuthorityGeneration, Is.EqualTo(2));
            Assert.That(recovered.Model.Find(1).Health, Is.EqualTo(73));
            Assert.That(recovered.Model.Find(1).StarterAmmo, Is.EqualTo(7));
            Assert.That(recovered.Model.Find(2).Health, Is.EqualTo(41));
            Assert.That(recovered.Model.Find(2).LifeGeneration, Is.EqualTo(2));
            Assert.That(recovered.Model.Find(2).StarterAmmo, Is.EqualTo(3));
            Assert.That(recovered.Model.Find(1).Pending.Action.AuthorityGeneration, Is.EqualTo(1), "Committed windup identity survives the new publishing authority");
            Assert.That(recovered.Model.Find(1).Pending.ResolveTick, Is.EqualTo(saved.Players[0].Pending.ResolveTick));
            Assert.That(recovered.Model.Projectiles[0].Action.Participant, Is.EqualTo(1));
            Assert.That(recovered.Model.Projectiles[0].Action.AuthorityGeneration, Is.EqualTo(1));
            Assert.That(recovered.Model.Projectiles[0].ExpireTick, Is.EqualTo(saved.Projectiles[0].ExpireTick));
            Assert.That(recovered.EventSequence, Is.EqualTo(savedEventSequence));
            var events = new System.Collections.Generic.List<CombatEvent>(FusionCombatEvent.Capacity);
            int cursor = savedEventSequence;
            recovered.CopyPresentationEvents(events, ref cursor);
            Assert.That(events.Count, Is.Zero, "Restoring state must not replay old VFX");
            runner.Despawn(playerObject); runner.Despawn(secondObject); runner.Despawn(matchObject);
            int startRunnerTick = runner.Tick.Raw;
            deadline = Time.realtimeSinceStartup + 5;
            while (runner.Tick.Raw < startRunnerTick + 4 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(runner.Tick.Raw, Is.GreaterThanOrEqualTo(startRunnerTick + 4));
            Assert.That(recovered.Model.Tick, Is.EqualTo(saved.Tick), "Recovery warmup freezes combat clocks and scoring");
            for (int i = 0; i < replacements.Length; i++)
            {
                var avatar = replacements[i].GetComponent<FusionPlayer>();
                Assert.That(avatar.State.Tick, Is.EqualTo(runner.Tick.Raw - avatar.FirstRunnerTick), "Restored motor advances exactly once per new SDK tick");
                Assert.That((bool)avatar.State.Suppressed, Is.True);
            }
            recovered.RecoveryWarmup = false;
            target = recovered.Model.Tick + 2;
            deadline = Time.realtimeSinceStartup + 5;
            while (recovered.Model.Tick < target && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(recovered.Model.Tick, Is.GreaterThanOrEqualTo(target));
            Assert.That(recovered.Model.Find(1).LastActionSequence, Is.EqualTo(1));
            Assert.That(recovered.Model.Projectiles.Count, Is.EqualTo(1));
            foreach (var obj in replacements) runner.Despawn(obj);
            runner.Despawn(restoredObject);
        }

        static void AssertMotor(FusionMotorState expected, FusionMotorState actual)
        {
            foreach (var field in typeof(FusionMotorState).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                Assert.That(field.GetValue(actual), Is.EqualTo(field.GetValue(expected)), field.Name);
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
