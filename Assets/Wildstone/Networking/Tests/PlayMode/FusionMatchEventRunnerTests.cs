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
    public sealed class FusionMatchEventRunnerTests
    {
        NetworkRunner runner;
        GameObject matchPrefab, playerPrefab, floor;
        CancellationTokenSource cancellation;

        [UnityTest]
        public IEnumerator RealNetworkEventRingPreservesBetweenTicksAndHandlesEviction()
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
            Assert.That(match.Begin(), Is.True);
            var combat = match.Model.Find(1);
            combat.ShieldCharges = 1; combat.GrenadeAmmo = 1;
            var request = new FusionCombatAction
            {
                Participant = 8, ConnectionGeneration = 0, AuthorityGeneration = match.Model.AuthorityGeneration,
                LifeGeneration = combat.LifeGeneration, Sequence = 0, Kind = CombatActionKind.Shield, Direction = Vector3.forward
            };
            Assert.That(match.SubmitAction(player, request), Is.False, "Reject stale connection epochs before host stamping");
            request.ConnectionGeneration = combat.ConnectionGeneration;
            request.AuthorityGeneration++;
            Assert.That(match.SubmitAction(player, request), Is.False, "Reject stale authority epochs");
            request.AuthorityGeneration = match.Model.AuthorityGeneration;
            Assert.That(match.SubmitAction(player, request), Is.True);
            request.Sequence = 1; request.Kind = CombatActionKind.Grenade;
            Assert.That(match.SubmitAction(player, request), Is.True);
            int target = match.Model.Tick + 3;
            deadline = Time.realtimeSinceStartup + 5;
            while (match.Model.Tick < target && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(match.Model.Tick, Is.GreaterThanOrEqualTo(target));
            Assert.That(combat.ShieldCharges, Is.Zero);
            Assert.That(combat.Shield, Is.GreaterThan(0));
            Assert.That(combat.GrenadeAmmo, Is.Zero);
            Assert.That(combat.LastActionSequence, Is.EqualTo(1));
            Assert.That(match.Model.Players.Count, Is.EqualTo(1), "Client participant claims must never create or control another seat");
            Assert.That(match.Header.PlayerCount, Is.EqualTo(1));
            Assert.That(match.Header.ProjectileCount, Is.EqualTo(1));
            Assert.That(match.Projectiles[0].Action.Participant, Is.EqualTo(1));
            Assert.That(match.Projectiles[0].Action.ConnectionGeneration, Is.EqualTo(1));
            Assert.That(match.Players[0].ShieldCharges, Is.Zero);
            Assert.That(player.State.Tick, Is.EqualTo(runner.Tick.Raw - firstTick), "Match schedules host movement once per runner tick");
            Assert.That(Vector3.Distance(combat.Feet, player.State.Position), Is.LessThan(.0001f));
            var snapshot = match.CaptureSnapshot();
            var recovered = new ArenaMatch(map, WeaponCatalog.DefaultTickRate, true);
            Assert.That(recovered.TryRestore(snapshot), Is.True, "Actual network checkpoint must restore the canonical match rules");
            Assert.That(recovered.Tick, Is.EqualTo(match.Header.Tick));
            Assert.That(recovered.Find(1).Shield, Is.EqualTo(combat.Shield));
            Assert.That(recovered.Projectiles.Count, Is.EqualTo(1));
            Assert.That(recovered.Pickups.Count, Is.EqualTo(map.Pickups.Length));
            var events = new System.Collections.Generic.List<CombatEvent>(FusionCombatEvent.Capacity);
            int cursor = 0;
            match.CopyPresentationEvents(events, ref cursor);
            Assert.That(events.Exists(e => e.Kind == CombatEventKind.ShieldActivated), Is.True);
            Assert.That(events.Exists(e => e.Kind == CombatEventKind.Shot && e.Weapon == WeaponKind.Grenade), Is.True);
            Assert.That(events.TrueForAll(e => e.Kind != CombatEventKind.Accepted && e.Kind != CombatEventKind.Rejected), Is.True);
            int observed = cursor;
            match.CopyPresentationEvents(events, ref cursor);
            Assert.That(events.Count, Is.Zero, "A repeated render must not replay effects");
            Assert.That(cursor, Is.EqualTo(observed));
            // Direct host fixture writes exercise actual SDK NetworkArray storage and reader bounds at eviction.
            for (int sequence = 1; sequence <= 300; sequence++)
            {
                var entry = new CombatEvent(CombatEventKind.Shot, new CombatAction { AuthorityGeneration = 1 }, sequence);
                match.EventRing.Set((sequence - 1) % FusionCombatEvent.Capacity, FusionCombatEvent.FromCombat(entry, sequence, 1));
            }
            match.EventSequence = 300; cursor = 0;
            match.CopyPresentationEvents(events, ref cursor);
            Assert.That(events.Count, Is.EqualTo(FusionCombatEvent.Capacity));
            Assert.That(events[0].Tick, Is.EqualTo(300-FusionCombatEvent.Capacity+1));
            Assert.That(events[events.Count - 1].Tick, Is.EqualTo(300));
            Assert.That(cursor, Is.EqualTo(300));
            match.CopyPresentationEvents(events, ref cursor);
            Assert.That(events.Count, Is.Zero);
            var header = match.Header; header.AuthorityGeneration = 2; match.Header = header;
            var migrated = new CombatEvent(CombatEventKind.Explosion, new CombatAction { AuthorityGeneration = 1 }, 301);
            match.EventRing.Set(300 % FusionCombatEvent.Capacity, FusionCombatEvent.FromCombat(migrated, 301, 2));
            match.EventSequence = 301;
            match.CopyPresentationEvents(events, ref cursor);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].Action.AuthorityGeneration, Is.EqualTo(1), "Committed projectile identity survives the publishing authority change");
            Assert.That(cursor, Is.EqualTo(301));
            Assert.Throws<ArgumentException>(() => match.CopyPresentationEvents(new System.Collections.Generic.List<CombatEvent>(), ref cursor));
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
