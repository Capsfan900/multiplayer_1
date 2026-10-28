using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Game;
using Object=UnityEngine.Object;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class LightProbeRefreshTests
    {
        // Inspect the installed Unity event storage without requiring baked content or raising unrelated subscribers.
        static Delegate[] Subscribers(string name)
        {
            var field=typeof(LightProbes).GetField(name,BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(field,Is.Not.Null,"Update this pinned-Unity lifecycle check if its event storage changes.");
            return ((Action)field.GetValue(null))?.GetInvocationList()??Array.Empty<Delegate>();
        }

        [Test] public void CoordinatorRefreshesCurrentProbeSetAndUnsubscribesOnDestroy()
        {
            Action observer=()=>{};
            LightProbes.needsRetetrahedralization+=observer;
            GameObject owner=null;
            PracticeSession session=null;
            try
            {
                owner=new GameObject("Probe lifecycle fixture");
                session=owner.AddComponent<PracticeSession>();
                session.enabled=false; session.enabled=true; session.enabled=false;
                var handlers=Subscribers("needsRetetrahedralization");
                Assert.That(handlers.Count(d=>ReferenceEquals(d.Target,session)),Is.EqualTo(1));
                Assert.That(handlers,Does.Contain(observer));
                // Call only this owner. An empty/unbaked probe set must also return without waiting for an async completion.
                Assert.DoesNotThrow(()=>handlers.Single(d=>ReferenceEquals(d.Target,session)).DynamicInvoke());
                Object.DestroyImmediate(owner); owner=null;
                handlers=Subscribers("needsRetetrahedralization");
                Assert.That(handlers.Any(d=>ReferenceEquals(d.Target,session)),Is.False,"Destroyed coordinators must not receive delayed probe events.");
                Assert.That(handlers,Does.Contain(observer),"Teardown must retain other listeners.");
            }
            finally
            {
                if(owner!=null) Object.DestroyImmediate(owner);
                LightProbes.needsRetetrahedralization-=observer;
            }
        }
    }
}
