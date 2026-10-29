using System;
using System.Collections;
using System.Threading;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildstone.Networking;
using Wildstone.Session;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class FusionRecoveryLifecycleTests
    {
        NetworkRunner runner;
        CancellationTokenSource cancellation;
        [UnityTest] public IEnumerator MissingWholeCheckpointCannotBeginRecoveryOrCreateASecondRunner()
        {
            runner=new GameObject("credential free recovery lifecycle").AddComponent<NetworkRunner>();
            cancellation=new CancellationTokenSource();
            var config=NetworkProjectConfig.Deserialize(NetworkProjectConfig.Serialize(NetworkProjectConfig.Global));
            var start=runner.StartGame(new StartGameArgs {GameMode=GameMode.Single,Config=config,
                SceneManager=runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider=runner.gameObject.AddComponent<NetworkObjectProviderDefault>(),StartGameCancellationToken=cancellation.Token});
            float deadline=Time.realtimeSinceStartup+15;
            while(!start.IsCompleted && Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(start.IsCompleted,Is.True); Assert.That(start.IsFaulted,Is.False,start.Exception?.ToString());
            Assert.That(start.Result.Ok,Is.True,start.Result.ToString());
            var before=Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include);
            var session=new FusionSession(new ContentVersion("1","test","atrium-1","rules"),"ROOM1");
            var recovery=new FusionRecovery(session);
            Assert.Throws<InvalidOperationException>(()=>recovery.Begin(runner));
            CollectionAssert.AreEquivalent(before,Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include));
            Assert.That(runner.IsRunning,Is.True,"Validation must not partially replace a usable runner.");
            Assert.That(session.Runner,Is.Null); Assert.That(session.Phase,Is.EqualTo(SessionPhase.Closed));
            // SDK callbacks from an unrelated live runner cannot turn a closed session into a recovery attempt.
            session.OnHostMigration(runner,null);
            Assert.That(session.Recovering,Is.False); Assert.That(session.Failure,Is.Null);
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
                if(runner!=null) Object.Destroy(runner.gameObject);
                cancellation?.Dispose(); cancellation=null; runner=null;
            }
            yield return null;
        }
    }
}
