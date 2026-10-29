using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Movement;
using Wildstone.Networking;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class FusionCombatWorldRunnerTests
    {
        NetworkRunner runner;
        GameObject prefab,wall;
        CancellationTokenSource cancellation;
        [UnityTest] public IEnumerator RealSdkHitboxesRewindWhileAdapterHonorsCoverAliveAndOwnerFilters()
        {
            prefab=new GameObject("lag compensation fixture"); prefab.layer=MotorTuning.PlayerLayer;
            prefab.transform.position=Vector3.up*10000;
            prefab.AddComponent<NetworkObject>(); prefab.AddComponent<ArenaMotor>();
            var root=prefab.AddComponent<HitboxRoot>(); root.Offset=new Vector3(0,.9f,0); root.BroadRadius=1.2f;
            var box=prefab.AddComponent<Hitbox>(); box.Type=HitboxTypes.Box;
            box.BoxExtents=new Vector3(.35f,.9f,.35f); box.Offset=new Vector3(0,.9f,0); root.Hitboxes=new[]{box};
            new NetworkObjectBaker().Bake(prefab);
            var config=NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            config.LagCompensation.Enabled=true;
            var id=config.PrefabTable.AddSource(new NetworkPrefabSourceStatic {Object=prefab.GetComponent<NetworkObject>(),
                AssetGuid=NetworkObjectGuid.Parse(Guid.NewGuid().ToString("N")),AssetPath="runtime lag compensation fixture"});
            runner=new GameObject("lag compensation test runner").AddComponent<NetworkRunner>();
            cancellation=new CancellationTokenSource();
            var start=runner.StartGame(new StartGameArgs {GameMode=GameMode.Single,Config=config,
                SceneManager=runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider=runner.gameObject.AddComponent<NetworkObjectProviderDefault>(),StartGameCancellationToken=cancellation.Token});
            float deadline=Time.realtimeSinceStartup+15;
            while(!start.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(start.IsCompleted,Is.True); Assert.That(start.IsFaulted,Is.False,start.Exception?.ToString());
            Assert.That(start.Result.Ok,Is.True,start.Result.ToString()); Assert.That(runner.LagCompensation,Is.Not.Null);
            var shooter=runner.Spawn(id,new Vector3(0,50,0),Quaternion.identity,runner.LocalPlayer);
            var target=runner.Spawn(id,new Vector3(0,50,5),Quaternion.identity,runner.LocalPlayer);
            // ArenaMotor.Awake can capture the template pose; bind it to the actual admitted spawn transform.
            SetPosition(shooter,new Vector3(0,50,0)); SetPosition(target,new Vector3(0,50,5));
            bool alive=true; int epoch=1;
            var world=new FusionCombatWorld(runner,ArenaLayouts.CreateAll()[0]);
            world.Bind(1,runner.LocalPlayer,shooter.GetComponent<ArenaMotor>(),shooter.GetComponent<HitboxRoot>(),()=>true);
            world.Bind(2,runner.LocalPlayer,target.GetComponent<ArenaMotor>(),target.GetComponent<HitboxRoot>(),()=>alive,()=>epoch);
            int wait=runner.Tick.Raw+4; deadline=Time.realtimeSinceStartup+5;
            while(runner.Tick.Raw<wait && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(runner.Tick.Raw,Is.GreaterThanOrEqualTo(wait));
            var action=new CombatAction {Participant=1,Sequence=7,Tick=1,Kind=CombatActionKind.Fire};
            var origin=new Vector3(0,50.9f,0); var displacement=Vector3.forward*10;
            FusionHitscanRecord record=default; world.ShotResolved+=value=>record=value;
            Assert.That(world.Hitscan(action,origin,displacement,0,out var hit),Is.True);
            Assert.That(hit.Participant,Is.EqualTo(2)); Assert.That(record.Action.Sequence,Is.EqualTo(7));
            Assert.That(record.RewindSeconds,Is.InRange(0,FusionCombatWorld.MaximumRewindSeconds+.000001f));
            Assert.That(world.Hitscan(action,origin,displacement,1<<2,out _),Is.False,"Ignored target and shooter must both be filtered.");
            alive=false; Assert.That(world.Hitscan(action,origin,displacement,0,out _),Is.False,"Dead avatars cannot receive historical damage."); alive=true;
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=0;
            wall.transform.position=new Vector3(0,50.9f,2); wall.transform.localScale=new Vector3(2,3,.2f); Physics.SyncTransforms();
            Assert.That(world.Hitscan(action,origin,displacement,0,out hit),Is.True);
            Assert.That(hit.Participant,Is.Zero,"Present static cover must win over a farther historical player.");
            Object.DestroyImmediate(wall); wall=null;
            int historicalTick=runner.Tick.Raw-1;
            SetPosition(target,new Vector3(8,50,5));
            wait=runner.Tick.Raw+3; deadline=Time.realtimeSinceStartup+5;
            while(runner.Tick.Raw<wait && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(runner.Tick.Raw,Is.GreaterThanOrEqualTo(wait));
            var hits=new List<LagCompensatedHit>();
            runner.LagCompensation.RaycastAll(origin,Vector3.forward,10,historicalTick,null,null,hits,
                1<<MotorTuning.PlayerLayer,true,HitOptions.None,QueryTriggerInteraction.Ignore);
            Assert.That(hits.Exists(candidate=>candidate.Hitbox!=null && candidate.Hitbox.Root==target.GetComponent<HitboxRoot>()),Is.True,
                "The real SDK must retain the target's earlier pose after its current collider moves away.");
            Assert.That(world.Sweep(origin,displacement,0,1,0,out _),Is.False,"Travelling collision must use the target's new position.");
            runner.Despawn(shooter); runner.Despawn(target);
        }
        static void SetPosition(NetworkObject avatar,Vector3 position)
        {
            var motor=avatar.GetComponent<ArenaMotor>(); var state=motor.CaptureState(); state.Position=position; motor.RestoreState(state);
            Physics.SyncTransforms();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            cancellation?.Cancel();
            try
            {
                if(runner!=null)
                {
                    var shutdown=runner.Shutdown(); float deadline=Time.realtimeSinceStartup+15;
                    while(!shutdown.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
                    Assert.That(shutdown.IsCompleted,Is.True); Assert.That(shutdown.IsFaulted,Is.False,shutdown.Exception?.ToString());
                }
            }
            finally
            {
                if(runner!=null) Object.Destroy(runner.gameObject); if(prefab!=null) Object.Destroy(prefab); if(wall!=null) Object.Destroy(wall);
                cancellation?.Dispose(); cancellation=null; runner=null; prefab=null; wall=null;
            }
            yield return null;
        }
    }
}
