using System;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Networking;
using Assert = NUnit.Framework.Assert;

namespace Wildstone.Tests
{
    public sealed class FusionCombatWorldTests
    {
        [TestCase(100,101,.5f,100,101,.5f)]
        [TestCase(-100,-99,.5f,90,90,.5f)]
        [TestCase(125,130,.5f,120,120,.5f)]
        [TestCase(115,110,2f,115,115,1f)]
        public void ProviderViewIntervalStaysWithinTheRewindCap(int sdkFrom,int sdkTo,float sdkAlpha,int expectedFrom,int expectedTo,float expectedAlpha)
        {
            // The first normal interval is below the cap; use a later host tick for that case.
            int now=sdkFrom==100?110:120;
            FusionCombatWorld.BoundView(now,1f/120,sdkFrom,sdkTo,sdkAlpha,out int from,out int to,out float alpha);
            Assert.That(from,Is.EqualTo(expectedFrom)); Assert.That(to,Is.EqualTo(expectedTo)); Assert.That(alpha,Is.EqualTo(expectedAlpha));
            Assert.That((now-(from+(to-from)*alpha))/120f,Is.InRange(0,FusionCombatWorld.MaximumRewindSeconds+.000001f));
        }
        [Test] public void MissingOrInvalidProviderInterpolationUsesBoundedCurrentTime()
        {
            FusionCombatWorld.BoundView(500,1f/120,null,null,float.NaN,out int from,out int to,out float alpha);
            Assert.That(from,Is.EqualTo(500)); Assert.That(to,Is.EqualTo(500)); Assert.That(alpha,Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(()=>FusionCombatWorld.BoundView(500,0,null,null,null,out _,out _,out _));
        }
        sealed class QueryWorld : EmptyCombatWorld,IHitscanCombatWorld
        {
            public int Historical, Present;
            public bool Hitscan(CombatAction action,Vector3 origin,Vector3 displacement,int ignored,out CombatHit hit)
            { Historical++; hit=default; return false; }
            public override bool Sweep(Vector3 origin,Vector3 displacement,float radius,int owner,int ignored,out CombatHit hit)
            { Present++; hit=default; return false; }
        }
        [TestCase(WeaponKind.LineDriver)] [TestCase(WeaponKind.LaserPistol)]
        [TestCase(WeaponKind.LaserShotgun)] [TestCase(WeaponKind.LaserSniper)]
        public void LaserWeaponsUseHistoricalQueriesWhileTravellingProjectileStaysPresentTime(WeaponKind kind)
        {
            var map=ArenaLayouts.CreateAll()[0];
            var match=new ArenaMatch(map,120,true);
            Assert.That(match.AddPlayer(1,0,1,1,Vector3.zero),Is.True); Assert.That(match.Begin(),Is.True);
            var world=new QueryWorld();
            if(kind!=WeaponKind.LineDriver)
            {
                var equipped=match.Find(1); equipped.SpecialWeapon=kind; equipped.SpecialAmmo=WeaponCatalog.Get(kind).Ammo; equipped.SelectedSpecial=true;
            }
            int expected=WeaponCatalog.HitscanPellets(kind);
            CombatAction Fire(int tick,int sequence) => new CombatAction {Participant=1,ConnectionGeneration=1,LifeGeneration=1,
                AuthorityGeneration=1,Sequence=sequence,Tick=tick,Kind=CombatActionKind.Fire,Direction=Vector3.forward};
            Assert.That(match.Step(1,new[]{Fire(1,1)},world),Is.True);
            Assert.That(world.Historical,Is.EqualTo(expected)); Assert.That(world.Present,Is.Zero);
            var player=match.Find(1);
            // Grenades have their own accepted action and ammunition path.
            var grenade=Fire(2,2); grenade.Kind=CombatActionKind.Grenade; player.GrenadeAmmo=1;
            Assert.That(match.Step(2,new[]{grenade},world),Is.True);
            Assert.That(match.Projectiles.Count,Is.EqualTo(1)); Assert.That(world.Historical,Is.EqualTo(expected)); Assert.That(world.Present,Is.GreaterThan(0));
        }
    }
}
