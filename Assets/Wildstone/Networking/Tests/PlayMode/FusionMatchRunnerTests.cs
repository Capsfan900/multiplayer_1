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
    public sealed class FusionMatchRunnerTests
    {
        NetworkRunner runner;
        GameObject matchPrefab, playerPrefab, floor;
        CancellationTokenSource cancellation;

        [UnityTest]
        public IEnumerator RealSingleMatchComposesCombatMotorAndCompleteCheckpoint()
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
            // A predicting client resimulates from these snapshots, so the motor carries the stun's end tick itself.
            Assert.That(CombatRules.Stun(combat, match.Model.Tick, match.Model.TickRate), Is.True);
            int stunned = match.Model.Tick + 2;
            float stunDeadline = Time.realtimeSinceStartup + 5;
            while (match.Model.Tick < stunned && Time.realtimeSinceStartup < stunDeadline) yield return null;
            Assert.That((bool)player.State.Suppressed, Is.True);
            Assert.That(player.State.SuppressedUntil - player.State.Tick, Is.EqualTo(combat.StunUntil - match.Model.Tick));
            stunDeadline = Time.realtimeSinceStartup + 5;
            while (match.Model.Tick < combat.StunUntil && Time.realtimeSinceStartup < stunDeadline) yield return null;
            Assert.That((bool)player.State.Suppressed, Is.False);
            Assert.That(player.State.SuppressedUntil, Is.Zero);
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
            player.RPC_Combat(request);
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
            Assert.That(match.SetConnection(1, 1, false), Is.True);
            Assert.That((bool)match.Players[0].Connected, Is.False);
            Assert.That((bool)player.State.Suppressed, Is.True);
            Assert.That(match.SubmitAction(player, request), Is.False, "Disconnected seats cannot enqueue combat");
            player.LastSampleSequence=1000;
            Assert.That(match.BindPlayer(1,player,2,0), Is.True);
            Assert.That(player.LastSampleSequence, Is.EqualTo(-1), "A fresh connection can start a new input stream without waiting out the old cursor");
            Assert.That(match.Model.Find(1).LifeGeneration, Is.EqualTo(1), "A live held avatar keeps its life");
            Assert.That(player.State.Generation, Is.EqualTo(1));
            Assert.That((bool)match.Players[0].Connected, Is.True);
            Assert.That(match.SubmitAction(player, request), Is.False, "Pre-reconnect commands retain a stale connection/life epoch");
            Assert.That(match.SetConnection(1,2,false),Is.True);
            playerObject.RemoveInputAuthority();
            int expiry=combat.DisconnectUntil;
            deadline=Time.realtimeSinceStartup+8;
            while(match.Model.Tick<expiry && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(match.Model.Tick,Is.GreaterThanOrEqualTo(expiry));
            Assert.That(combat.Alive,Is.False,"Disconnected avatar loses its live life after five seconds.");
            Assert.That(match.UnbindAvatar(1,player),Is.True);
            Assert.That(match.UnbindAvatar(1,player),Is.False,"Retired bindings cannot unbind a later replacement.");
            runner.Despawn(playerObject);
            yield return null;
            Assert.That(match.Model.Find(1),Is.SameAs(combat),"Despawn preserves held seat/combat state.");
            firstTick=runner.Tick.Raw+1;
            var replacementObject=runner.Spawn(playerId,position,Quaternion.identity,runner.LocalPlayer,
                (r,obj)=>
                {
                    var state=obj.GetComponent<ArenaMotor>().CaptureState(); state.Position=position; state.Generation=1;
                    obj.GetComponent<FusionPlayer>().Initialize(state,firstTick);
                });
            var replacement=replacementObject.GetComponent<FusionPlayer>();
            Assert.That(match.BindPlayer(1,replacement,3,0),Is.True,"A newer admitted peer can replace the despawned held avatar.");
            Assert.That(combat.Alive,Is.True); Assert.That(combat.LifeGeneration,Is.EqualTo(2));
            Assert.That(combat.StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo)); Assert.That(combat.SpecialWeapon,Is.EqualTo(WeaponKind.None));
            Assert.That(match.UnbindAvatar(1,player),Is.False,"Destroyed old wrapper cannot remove replacement binding.");
            int frozenRunnerTick=runner.Tick.Raw, frozenMatchTick=match.Model.Tick, frozenMotorTick=replacement.State.Tick;
            FusionRecovery.FreezeSimulation(runner);
            float freezeEnd=Time.realtimeSinceStartup+.25f;
            while(Time.realtimeSinceStartup<freezeEnd) yield return null;
            Assert.That(runner.Tick.Raw,Is.GreaterThan(frozenRunnerTick),"Transport keeps pumping while simulation objects are frozen.");
            Assert.That(match.Model.Tick,Is.EqualTo(frozenMatchTick));
            Assert.That(replacement.State.Tick,Is.EqualTo(frozenMotorTick));
            Assert.DoesNotThrow(()=>FusionRecovery.ValidateAvatar(replacement.State,replacement.LastSampleSequence,replacement.PoseEpoch,
                replacement.FirstRunnerTick,combat,frozenRunnerTick),"Frozen world remains coherent at its final tick despite newer transport ticks.");
            Assert.That(match.UnbindAvatar(1,replacement),Is.True);
            runner.Despawn(replacementObject);
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
