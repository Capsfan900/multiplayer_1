using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wildstone.Game;
using Wildstone.Maps;
using Wildstone.Movement;
using Object = UnityEngine.Object;

namespace Wildstone.Tests
{
    public sealed class RecoveryMapRetentionTests
    {
        Scene environment, previous;
        GameObject owner, floor, avatar;
        ArenaCatalog catalog;
        PracticeSession session;

        [UnityTest]
        public IEnumerator RetainedNetworkMapKeepsExactSceneAndCollisionDuringRecoveryWarmup()
        {
            previous=SceneManager.GetActiveScene();
            environment=SceneManager.CreateScene("Recovery retained map fixture");
            owner=new GameObject("Recovery presentation fixture");
            session=owner.AddComponent<PracticeSession>(); session.enabled=false;
            var mapObject=new GameObject("Recovery map authoring");
            SceneManager.MoveGameObjectToScene(mapObject,environment);
            var definition=ArenaLayouts.CreateAll()[0]; mapObject.AddComponent<ArenaMap>().Definition=definition;
            catalog=ScriptableObject.CreateInstance<ArenaCatalog>(); catalog.Maps=new[]{definition}; session.Catalog=catalog;
            typeof(PracticeSession).GetField("mapScene",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,environment);
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(0,-.5f,0); floor.transform.localScale=new Vector3(20,1,20);
            floor.AddComponent<MotorSurface>().Id=771;
            SceneManager.MoveGameObjectToScene(floor,environment);
            avatar=new GameObject("Recovered persistent motor fixture"); avatar.transform.position=new Vector3(0,.02f,0);
            var motor=avatar.AddComponent<ArenaMotor>();
            var handle=environment.handle; var floorId=floor.GetEntityId();
            Assert.That(session.TryRetainNetworkMap("atrium"),Is.True);
            Assert.That(SceneManager.GetActiveScene().handle,Is.EqualTo(handle));
            Physics.SyncTransforms();
            for(int tick=0;tick<180;tick++)
            {
                motor.SetSuppressed(true);
                Assert.That(motor.Simulate(new MotorInput {Present=true,Tick=tick,Sequence=tick,Generation=1},MotorTuning.TickDelta,tick),Is.True);
                if(tick%30==0) yield return null;
            }
            Assert.That(environment.isLoaded,Is.True);
            Assert.That(floor.GetEntityId(),Is.EqualTo(floorId));
            Assert.That(motor.CaptureState().Position.y,Is.GreaterThan(-.05f),"Retaining the environment must prevent a collider gap while recovered motors continue sequential warmup ticks.");
            Assert.That(motor.CaptureState().Tick,Is.EqualTo(179));
            Assert.That(session.TryRetainNetworkMap("split-gallery"),Is.False);
            var stale=ArenaLayouts.CreateAll()[0]; stale.Revision++;
            catalog.Maps=new[]{stale};
            Assert.That(session.TryRetainNetworkMap("atrium"),Is.False,"Stale scene revisions require the normal scene loader.");
            catalog.Maps=new[]{definition};
            typeof(PracticeSession).GetField("requestedMap",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(session,"atrium");
            Assert.That(session.TryRetainNetworkMap("atrium"),Is.False,"A pending scene operation cannot be bypassed by recovery.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(avatar!=null) Object.Destroy(avatar);
            if(owner!=null) Object.Destroy(owner);
            if(catalog!=null) Object.Destroy(catalog);
            if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if(environment.IsValid() && environment.isLoaded)
            {
                var unload=SceneManager.UnloadSceneAsync(environment);
                if(unload!=null) yield return unload;
            }
            yield return null;
        }
    }
}
