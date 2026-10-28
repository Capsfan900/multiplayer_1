using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
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
    public sealed class FusionSessionLifecycleTests
    {
        static FusionSession Session() => new FusionSession(new ContentVersion("p1", "b1", "atrium1", "r1"), "native-lifecycle");
        static NetworkRunner[] Runners() => Object.FindObjectsByType<NetworkRunner>(FindObjectsInactive.Include);
        static IEnumerator Finish(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 3;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Credential-free lifecycle work must complete without network access.");
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
            Assert.That(task.IsCanceled, Is.False);
        }
        [UnityTest] public IEnumerator MissingProviderRejectsWithoutCreatingRunner()
        {
            var before = Runners();
            var session = Session();
            var error = Assert.Throws<InvalidOperationException>(() => session.StartAsync(true, "ROOM", null, null, false));
            StringAssert.Contains("provider authentication", error.Message);
            CollectionAssert.AreEquivalent(before, Runners());
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Closed));
            yield return Finish(session.StopAsync());
            Assert.That(session.Completion.IsCompleted, Is.True);
        }
        [UnityTest] public IEnumerator AlreadyCancelledAttemptNeverCreatesRunnerAndCanRetry()
        {
            var before = Runners();
            var session = Session();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                // No auth material: cancellation must exit before config loading or service/runner work.
                var first = session.StartAsync(true, "ROOM", null, null, false, cancellation.Token);
                Assert.That(first.IsCompleted, Is.True);
                Assert.That(first.Result, Is.False);
                Assert.That(session.Phase, Is.EqualTo(SessionPhase.Closed));
                Assert.That(session.Failure, Is.Null);
                CollectionAssert.AreEquivalent(before, Runners());
                var retry = session.StartAsync(false, "ROOM", null, null, false, cancellation.Token);
                Assert.That(retry.Result, Is.False);
                Assert.Throws<InvalidOperationException>(() => session.StartAsync(false, "ROOM", null, null, false));
            }
            yield return Finish(session.StopAsync());
            CollectionAssert.AreEquivalent(before, Runners());
        }
        [UnityTest] public IEnumerator StopIsIdempotentAndCompletionReturnsToClosed()
        {
            var before = Runners();
            var session = Session();
            var first = session.StopAsync();
            Assert.That(session.StopAsync(), Is.SameAs(first));
            Assert.That(session.Completion, Is.SameAs(first));
            yield return Finish(first);
            Assert.That(session.Completion.IsCompleted, Is.True);
            Assert.That(session.Phase, Is.EqualTo(SessionPhase.Closed));
            yield return Finish(session.StopAsync());
            CollectionAssert.AreEquivalent(before, Runners());
        }
        [UnityTest] public IEnumerator DestroyedUnownedRunnerCallbacksCannotChangeSession()
        {
            var before = Runners();
            var session = Session();
            var owner = new GameObject("Retired native runner fixture");
            try
            {
                var stale = owner.AddComponent<NetworkRunner>();
                Object.DestroyImmediate(owner);
                Assert.That(stale == null, Is.True, "Exercise an actual Unity destroyed-object wrapper.");
                Assert.That(ReferenceEquals(stale, null), Is.False);
                session.OnShutdown(stale, default);
                session.OnDisconnectedFromServer(stale, default);
                session.OnConnectFailed(stale, default, default);
                session.OnHostMigration(stale, null);
                Assert.That(session.Phase, Is.EqualTo(SessionPhase.Closed));
                Assert.That(session.Failure, Is.Null);
                yield return Finish(session.StopAsync());
                CollectionAssert.AreEquivalent(before, Runners());
            }
            finally { if (owner != null) Object.DestroyImmediate(owner); }
        }
        [Test] public void OnlyCanonicalTickAndCapacityConfigurationIsAccepted()
        {
            var config = new NetworkProjectConfig();
            config.Simulation.PlayerCount = 8;
            config.Simulation.TickRateSelection = new TickRate.Selection
            { Client = 120, ClientSendInterval = 2, ServerTickInterval = 1, ServerSendInterval = 2 };
            Assert.DoesNotThrow(() => FusionSession.ValidateProjectConfig(config));
            config.Simulation.PlayerCount = 7;
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateProjectConfig(config));
            config.Simulation.PlayerCount = 8;
            config.Simulation.TickRateSelection.ServerSendInterval = 1;
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateProjectConfig(config));
            config.Simulation.TickRateSelection.ServerSendInterval = 2;
            config.Simulation.TickRateSelection.ServerTickInterval = 2;
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateProjectConfig(config));
            Assert.Throws<InvalidOperationException>(() => FusionSession.ValidateProjectConfig(null));
        }
    }
}
