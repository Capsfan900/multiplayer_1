using System;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    // Native PlayMode lifecycle checks: compiled by managed QA, not run by its Mono runner.
    [TestFixture,Category("Native")]
    public sealed class PresentationRuntimeTests
    {
        [Test]
        public void OverlayKeepsItsOwnFovAndRestoresOnlyItsCameraMaskBit()
        {
            var catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            Assert.That(catalog,Is.Not.Null,"Build presentation assets first.");
            var world=new GameObject("World",typeof(Camera)).GetComponent<Camera>();
            var view=new GameObject("View",typeof(WeaponView)).GetComponent<WeaponView>();
            try
            {
                int original=world.cullingMask;
                view.Initialize(world,catalog);
                Assert.That((world.cullingMask&(1<<11)),Is.Zero);
                world.fieldOfView=105; view.SyncProjection(world);
                var cameras=world.GetComponentsInChildren<Camera>();
                Assert.That(cameras.Length,Is.EqualTo(2));
                var overlay=cameras[1];
                Assert.That(overlay,Is.Not.SameAs(world));
                // D028: the viewmodel has its own projection, so a wide settings FOV no longer stretches the gun.
                Assert.That(overlay.fieldOfView,Is.EqualTo(WeaponView.ViewmodelFieldOfView).Within(.001f));
                world.fieldOfView=80; view.SyncProjection(world);
                Assert.That(overlay.fieldOfView,Is.EqualTo(WeaponView.ViewmodelFieldOfView).Within(.001f));
                world.cullingMask^=1<<9;
                view.Teardown(world);
                Assert.That((world.cullingMask&(1<<11)),Is.EqualTo(original&(1<<11)));
                Assert.That((world.cullingMask&(1<<9)),Is.EqualTo((original^(1<<9))&(1<<9)));
            }
            finally { Object.Destroy(view.gameObject); Object.Destroy(world.gameObject); }
        }

        [Test]
        public void WarmProjectileAndStatusRenderPathDoesNotCreatePerFrameGarbage()
        {
            var catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            Assert.That(catalog,Is.Not.Null,"Build presentation assets first.");
            var definition=new ArenaDefinition { Id="atrium",Spawns=Array.Empty<ArenaSpawn>(),Pickups=Array.Empty<ArenaPickup>(),
                ZoneRoute=new[]{Vector3.zero,Vector3.forward*8,Vector3.right*8} };
            var match=new ArenaMatch(definition,practice:true);
            Assert.That(match.AddPlayer(1,0,1,1,Vector3.zero),Is.True);
            Assert.That(match.Begin(),Is.True);
            var saved=match.Capture();
            saved.Players[0].LastActionSequence=1;
            saved.Players[0].Shield=30; saved.Players[0].ShieldUntil=10;
            saved.Players[0].StunUntil=5; saved.Players[0].ImmunityUntil=20;
            saved.Projectiles=new[] { new ArenaProjectile { Action=new CombatAction { Participant=1,ConnectionGeneration=1,
                LifeGeneration=1,AuthorityGeneration=1,Tick=0,Sequence=1,Kind=CombatActionKind.Grenade,Direction=Vector3.forward },
                Weapon=WeaponKind.Grenade,OwnerTeam=0,Position=Vector3.up,Velocity=Vector3.forward,ExpireTick=192 } };
            Assert.That(match.TryRestore(saved),Is.True);
            var effects=new GameObject("Effects",typeof(ArenaEffects)).GetComponent<ArenaEffects>();
            try
            {
                effects.Initialize(catalog,match);
                for(int i=0;i<20;i++) effects.Present(match,false);
                GC.Collect();
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<100;i++) effects.Present(match,false);
                long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
                Assert.That(allocated,Is.LessThanOrEqualTo(1024),"Warm render loop should not allocate per projectile/frame.");
            }
            finally { Object.Destroy(effects.gameObject); }
        }
    }
}
