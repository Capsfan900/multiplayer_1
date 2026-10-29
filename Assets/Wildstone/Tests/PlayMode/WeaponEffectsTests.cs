using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class WeaponEffectsTests
    {
        GameObject root;
        ArenaEffects effects;
        ArenaMatch match;
        PresentationCatalog catalog;
        [SetUp] public void SetUp()
        {
            catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            match=new ArenaMatch(new ArenaDefinition { Id="effects",Spawns=System.Array.Empty<ArenaSpawn>(),Pickups=System.Array.Empty<ArenaPickup>(),ZoneRoute=new[]{Vector3.zero,Vector3.forward*8,Vector3.right*8} },practice:true);
            root=new GameObject("WeaponEffectsTest"); effects=root.AddComponent<ArenaEffects>(); effects.Initialize(catalog,match);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }

        [Test] public void SaturatedBurstsReuseObjectsAndExpireWithoutChangingMatch()
        {
            int objects=root.GetComponentsInChildren<Transform>(true).Length;
            var action=new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 };
            var shot=new CombatEvent(CombatEventKind.Shot,action,0,position:Vector3.up*2,vector:Vector3.forward,weapon:WeaponKind.LineDriver);
            for(int i=0;i<200;i++) effects.Play(shot,match,false,Vector3.zero);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(objects));
            Assert.That(match.Projectiles.Count,Is.Zero);
            effects.AnimateTransient(Time.realtimeSinceStartup+10);
            foreach(var line in root.GetComponentsInChildren<LineRenderer>(true)) if(line.name.StartsWith("Burst ")) Assert.That(line.enabled,Is.False);
        }

        [Test] public void ImpactWeaponContextIsIsolatedByLife()
        {
            var action=new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 };
            effects.Play(new CombatEvent(CombatEventKind.Shot,action,0,vector:Vector3.forward,weapon:WeaponKind.PulseLobber),match,false,Vector3.zero);
            Assert.That(effects.ContextWeapon(action),Is.EqualTo(WeaponKind.PulseLobber));
            action.LifeGeneration++;
            Assert.That(effects.ContextWeapon(action),Is.EqualTo(WeaponKind.None));
        }

        [TestCase(WeaponKind.LineDriver)] [TestCase(WeaponKind.PulseLobber)] [TestCase(WeaponKind.ReboundDisc)]
        [TestCase(WeaponKind.PushLens)] [TestCase(WeaponKind.Grenade)] [TestCase(WeaponKind.StunGun)] [TestCase(WeaponKind.LaserSword)]
        [TestCase(WeaponKind.LaserPistol)] [TestCase(WeaponKind.LaserShotgun)] [TestCase(WeaponKind.LaserSniper)]
        public void EveryWeaponHasBoundedDistinctShotGeometry(WeaponKind weapon)
        {
            effects.Play(new CombatEvent(CombatEventKind.Shot,new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 },0,position:Vector3.up*100,vector:Vector3.forward,weapon:weapon),match,false,Vector3.zero);
            int active=0;
            foreach(var line in root.GetComponentsInChildren<LineRenderer>(true)) if(line.name.StartsWith("Burst ") && line.enabled)
            { active++; Assert.That(line.positionCount,Is.InRange(2,24)); }
            if(weapon==WeaponKind.Grenade) Assert.That(active,Is.Zero,"Thrown grenades use their projectile/fuse, not a gun muzzle flash.");
            else Assert.That(active,Is.InRange(1,weapon==WeaponKind.LaserShotgun?12:6));
        }

        [Test] public void ShotgunFanMatchesSimulationDirectionsAndEachPelletUsesItsOwnHit()
        {
            var action=new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 };
            var shot=new CombatEvent(CombatEventKind.Shot,action,0,position:Vector3.up*100,vector:Vector3.forward,weapon:WeaponKind.LaserShotgun);
            var hits=new CombatEvent[WeaponCatalog.HitscanPellets(shot.Weapon)];
            for(int pellet=0;pellet<hits.Length;pellet++)
                hits[pellet]=new CombatEvent(CombatEventKind.Hit,action,0,position:shot.Position+WeaponCatalog.HitscanDirection(shot.Weapon,shot.Vector,pellet)*(3+pellet),subIndex:pellet,weapon:shot.Weapon);
            effects.Play(shot,match,false,Vector3.zero,hits);
            for(int pellet=0;pellet<hits.Length;pellet++)
            {
                var line=GripAttachment.Find(root.transform,"Burst "+pellet).GetComponent<LineRenderer>();
                Assert.That(line.positionCount,Is.EqualTo(2));
                Assert.That(Vector3.Distance(line.GetPosition(1),hits[pellet].Position),Is.LessThan(.0001f));
                Assert.That(Vector3.Dot((line.GetPosition(1)-line.GetPosition(0)).normalized,WeaponCatalog.HitscanDirection(shot.Weapon,shot.Vector,pellet)),Is.GreaterThan(.99999f));
            }
        }

        [Test] public void CosmeticMuzzlePreservesEyeHitEndpointAndStopsAtNearCover()
        {
            var action=new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 };
            var shot=new CombatEvent(CombatEventKind.Shot,action,0,position:Vector3.up*100,vector:Vector3.forward,weapon:WeaponKind.LineDriver);
            var hit=new CombatEvent(CombatEventKind.Hit,action,0,position:shot.Position+Vector3.forward*5);
            Vector3 muzzle=shot.Position+Vector3.right;
            ArenaEffects.ShotSegment(shot,new[]{hit},muzzle,out var start,out var end);
            Assert.That(start,Is.EqualTo(muzzle)); Assert.That(end,Is.EqualTo(hit.Position));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.layer=0; wall.transform.position=shot.Position+new Vector3(.8f,0,1); wall.transform.localScale=new Vector3(.25f,2,.25f);
                Physics.SyncTransforms();
                ArenaEffects.ShotSegment(shot,new[]{hit},muzzle,out start,out end);
                Assert.That(end.z,Is.LessThan(hit.Position.z));
            }
            finally { Object.DestroyImmediate(wall); }
        }

        [Test] public void RingStartsAtItsAnimationOriginInsteadOfPoppingToASmallerRadius()
        {
            Vector3 center=Vector3.up*100;
            effects.Play(new CombatEvent(CombatEventKind.Impulse,default,0,position:center),match,false,Vector3.zero);
            var line=GripAttachment.Find(root.transform,"Burst 0").GetComponent<LineRenderer>();
            Assert.That(Vector3.Distance(line.GetPosition(0),center),Is.EqualTo(.8f*.35f).Within(.0001f));
            effects.AnimateTransient(Time.realtimeSinceStartup);
            Assert.That(Vector3.Distance(line.GetPosition(0),center),Is.GreaterThanOrEqualTo(.8f*.35f-.0001f));
        }

        [Test] public void ProjectileSlotReuseResetsTrailAndRenderingDoesNotWriteState()
        {
            Assert.That(match.AddPlayer(1,0,1,1,Vector3.zero),Is.True); Assert.That(match.Begin(),Is.True);
            var saved=match.Capture(); saved.Players[0].LastActionSequence=10;
            saved.Projectiles=new[]{new ArenaProjectile { Action=new CombatAction { Participant=1,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1,Sequence=1,Direction=Vector3.forward },Weapon=WeaponKind.PulseLobber,Position=Vector3.up*100,Velocity=Vector3.forward*18,ExpireTick=600 }};
            Assert.That(match.TryRestore(saved),Is.True);
            effects.Present(match,false);
            match.Projectiles[0].Position+=Vector3.forward*.1f;
            string before=JsonUtility.ToJson(match.Capture());
            effects.Present(match,false);
            Assert.That(JsonUtility.ToJson(match.Capture()),Is.EqualTo(before));
            var trail=GripAttachment.Find(root.transform,"Projectile trail 0").GetComponent<LineRenderer>();
            Assert.That(trail.enabled,Is.True); Assert.That(trail.positionCount,Is.EqualTo(2));
            var replacement=match.Projectiles[0].Action; replacement.LifeGeneration++; match.Projectiles[0].Action=replacement;
            effects.Present(match,false);
            Assert.That(trail.enabled,Is.False,"A new life cannot inherit another action's trail.");
            effects.ResetPresentation();
            Assert.That(effects.ContextWeapon(replacement),Is.EqualTo(WeaponKind.None));
        }
    }
}
