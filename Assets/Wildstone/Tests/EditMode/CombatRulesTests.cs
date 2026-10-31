using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Match;
using Wildstone.Maps;
using System.Linq;

namespace Wildstone.Tests
{
    [TestFixture, Category("Managed")]
    public class CombatRulesTests
    {
        static ArenaDefinition Map() => new ArenaDefinition { Id="test", Revision=1, Minimum=new Vector3(-100,-10,-100), Maximum=new Vector3(100,100,100), KillHeight=-20,
            ZoneRoute=new[]{Vector3.zero,new Vector3(10,0,0),new Vector3(10,0,10)},
            Pickups=new[]{new ArenaPickup { Item="shield", Position=Vector3.zero }},
            Spawns=new[]{new ArenaSpawn{Team=0},new ArenaSpawn{Team=1,Position=new Vector3(0,0,5)}} };
        static ArenaMatch Match()
        {
            var match=new ArenaMatch(Map(),60); match.AddPlayer(1,0,1,1,Vector3.zero); match.AddPlayer(2,1,1,1,new Vector3(0,0,5));
            Assert.That(match.Begin(),Is.True); return match;
        }
        static CombatAction Act(int player,int sequence,int tick,CombatActionKind kind) => new CombatAction {
            Participant=player,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1,Sequence=sequence,Tick=tick,Kind=kind,Direction=Vector3.forward,PickupGeneration=1 };
        [Test] public void CommittedPendingAttackSurvivesMigrationRebindAndResolvesOnce()
        {
            var source=Match(); var shooter=source.Find(1);
            shooter.SpecialWeapon=WeaponKind.PushLens; shooter.SpecialAmmo=6; shooter.SelectedSpecial=true;
            var committed=Act(1,1,1,CombatActionKind.Fire);
            Assert.That(source.Step(1,new[]{committed},new EmptyCombatWorld()),Is.True);
            var checkpoint=source.Capture();
            checkpoint.Players[0].Connected=false; checkpoint.Players[0].DisconnectUntil=checkpoint.Tick+300;
            var recovered=Match(); Assert.That(recovered.TryRestore(checkpoint),Is.True);
            Assert.That(recovered.AdvanceAuthorityGeneration(2),Is.True);
            Assert.That(recovered.SetConnection(1,2,true),Is.True);
            var rebound=recovered.Find(1);
            Assert.That(rebound.LifeGeneration,Is.EqualTo(1));
            Assert.That(rebound.Pending.Action,Is.EqualTo(committed),"Committed action provenance must survive transport rebinding.");
            var replica=Match(); Assert.That(replica.TryRestore(recovered.Capture()),Is.True,"Every post-rebind checkpoint must remain valid for replicas and later migrations.");
            int resolve=rebound.Pending.ResolveTick;
            Advance(recovered,resolve-1);
            Assert.That(recovered.Step(resolve,new CombatAction[0],new EmptyCombatWorld()),Is.True);
            var impulse=recovered.Events.Single(e=>e.Kind==CombatEventKind.Impulse);
            Assert.That(impulse.Action,Is.EqualTo(committed)); Assert.That(impulse.Target,Is.EqualTo(2));
            Assert.That(rebound.Pending,Is.Null);
            Assert.That(recovered.Step(resolve+1,new CombatAction[0],new EmptyCombatWorld()),Is.True);
            Assert.That(recovered.Events.Any(e=>e.Kind==CombatEventKind.Impulse),Is.False);
            var stale=Act(1,2,resolve+2,CombatActionKind.Fire); stale.AuthorityGeneration=2;
            int ammo=rebound.SpecialAmmo;
            Assert.That(recovered.Step(resolve+2,new[]{stale},new EmptyCombatWorld()),Is.True);
            Assert.That(recovered.Events.Single(e=>e.Kind==CombatEventKind.Rejected).Reason,Does.Contain("Stale"));
            Assert.That(rebound.SpecialAmmo,Is.EqualTo(ammo)); Assert.That(rebound.LastActionSequence,Is.EqualTo(1));
        }
        [TestCase(0),TestCase(3)] public void PendingCheckpointRejectsNonpositiveOrFutureConnectionGeneration(int connection)
        {
            var source=Match(); var shooter=source.Find(1);
            shooter.SpecialWeapon=WeaponKind.PushLens; shooter.SpecialAmmo=6; shooter.SelectedSpecial=true;
            source.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new EmptyCombatWorld());
            var saved=source.Capture(); saved.Players[0].ConnectionGeneration=2;
            saved.Players[0].Pending.Action.ConnectionGeneration=connection;
            Assert.That(Match().TryRestore(saved),Is.False);
        }
        sealed class LaserWorld : EmptyCombatWorld,IHitscanCombatWorld
        {
            public int Queries;
            public bool Cover,InvalidPoint;
            public Vector3[] Directions=new Vector3[8];
            public bool Hitscan(CombatAction action,Vector3 origin,Vector3 delta,int ignored,out CombatHit hit)
            {
                Directions[Queries%8]=delta.normalized; Queries++;
                hit=new CombatHit(Cover?0:2,InvalidPoint?new Vector3(float.NaN,0,0):origin+delta.normalized*5,-delta.normalized,5);
                return true;
            }
            public override bool Sweep(Vector3 origin,Vector3 delta,float radius,int owner,int ignored,out CombatHit hit)
            { throw new System.InvalidOperationException("Laser shots must use historical query path."); }
        }
        [TestCase(WeaponKind.LaserPistol,32,18,1)]
        [TestCase(WeaponKind.LaserShotgun,72,8,8)]
        [TestCase(WeaponKind.LaserSniper,80,6,1)]
        [TestCase(WeaponKind.LaserSMG,10,32,1)]
        public void LaserSpecialUsesOneAmmoAndDistinctHistoricalPelletHits(WeaponKind kind,int damage,int ammo,int pellets)
        {
            var m=Match(); Equip(m.Find(1),kind); var world=new LaserWorld();
            var action=Act(1,1,1,CombatActionKind.Fire);
            m.Step(1,new[]{action,action},world);
            Assert.That(m.Find(1).SpecialAmmo,Is.EqualTo(ammo-1));
            Assert.That(m.Find(2).Health,Is.EqualTo(100-damage));
            Assert.That(world.Queries,Is.EqualTo(pellets));
            Assert.That(m.Events.Count(e=>e.Kind==CombatEventKind.Shot),Is.EqualTo(1));
            var hits=m.Events.Where(e=>e.Kind==CombatEventKind.Hit).ToArray();
            Assert.That(hits.Length,Is.EqualTo(pellets));
            Assert.That(hits.Select(e=>e.Id).Distinct().Count(),Is.EqualTo(pellets));
            CollectionAssert.AreEqual(Enumerable.Range(0,pellets),hits.Select(e=>e.SubIndex));
            Assert.That(hits.All(e=>e.Weapon==kind),Is.True);
            Assert.That(Match().TryRestore(m.Capture()),Is.True);
            m.Step(2,new[]{Act(1,2,2,CombatActionKind.Fire)},world);
            Assert.That(world.Queries,Is.EqualTo(pellets),"Cooldown rejects the whole trigger.");
            m.Find(1).FireReadyTick=0; m.Find(1).SpecialAmmo=0;
            m.Step(3,new[]{Act(1,3,3,CombatActionKind.Fire)},world);
            Assert.That(world.Queries,Is.EqualTo(pellets),"Empty special cannot shoot.");
        }
        [TestCase(WeaponKind.LaserPistol)] [TestCase(WeaponKind.LaserShotgun)] [TestCase(WeaponKind.LaserSniper)] [TestCase(WeaponKind.LaserSMG)]
        public void LaserSpecialRespectsCoverFriendliesAndFiniteHitPoints(WeaponKind kind)
        {
            for(int scenario=0;scenario<3;scenario++)
            {
                var m=Match(); Equip(m.Find(1),kind);
                var world=new LaserWorld {Cover=scenario==0,InvalidPoint=scenario==2};
                if(scenario==1) m.Find(2).Team=0;
                m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},world);
                Assert.That(m.Find(2).Health,Is.EqualTo(100));
                Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Hit),Is.False);
            }
        }
        [TestCase("laser-pistol",WeaponKind.LaserPistol)]
        [TestCase("laser-shotgun",WeaponKind.LaserShotgun)]
        [TestCase("laser-sniper",WeaponKind.LaserSniper)]
        [TestCase("laser-smg",WeaponKind.LaserSMG)]
        public void LaserPickupEquipsFiniteSpecialAndRejectsOverfullSnapshot(string item,WeaponKind kind)
        {
            var map=Map(); map.Pickups[0].Item=item;
            var m=new ArenaMatch(map,60); m.AddPlayer(1,0,1,1,Vector3.zero); m.AddPlayer(2,1,1,1,Vector3.forward*5); m.Begin();
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Pickup)},new EmptyCombatWorld());
            Assert.That(m.Find(1).SelectedSpecial,Is.True); Assert.That(m.Find(1).SpecialWeapon,Is.EqualTo(kind));
            Assert.That(m.Find(1).SpecialAmmo,Is.EqualTo(WeaponCatalog.Get(kind).Ammo*WeaponCatalog.SpecialMagazines),"Two magazines as one count.");
            Assert.That(m.TryRestore(m.Capture()),Is.True,"A snapshot carrying both magazines restores.");
            var saved=m.Capture(); saved.Players[0].SpecialAmmo++;
            Assert.That(m.TryRestore(saved),Is.False);
        }
        [Test] public void ShotgunPatternIsDeterministicFiniteAndBoundedAtVerticalAim()
        {
            float ring=WeaponCatalog.SpreadDegrees(WeaponKind.LaserShotgun,0,0);
            foreach(var aim in new[]{Vector3.forward,Vector3.up,Vector3.down,new Vector3(1,2,3).normalized})
            {
                var rays=new Vector3[8];
                for(int pellet=0;pellet<8;pellet++)
                {
                    var ray=WeaponCatalog.HitscanDirection(WeaponKind.LaserShotgun,aim,pellet,ring,17u); rays[pellet]=ray;
                    Assert.That(CombatRules.Direction(ray),Is.True);
                    Assert.That(Degrees(aim,ray),Is.LessThanOrEqualTo(ring+.01));
                    Assert.That(ray,Is.EqualTo(WeaponCatalog.HitscanDirection(WeaponKind.LaserShotgun,aim,pellet,ring,99u)),"The shotgun ring is fixed, not seeded.");
                }
                Assert.That(rays.Distinct().Count(),Is.EqualTo(8));
            }
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>WeaponCatalog.HitscanDirection(WeaponKind.LaserShotgun,Vector3.forward,8,ring,0));
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>WeaponCatalog.HitscanDirection(WeaponKind.LaserShotgun,Vector3.zero,0,ring,0));
            Assert.That((int)WeaponKind.LaserSword,Is.EqualTo(7));
            Assert.That((int)WeaponKind.LaserPistol,Is.EqualTo(8));
            Assert.That((int)WeaponKind.LaserShotgun,Is.EqualTo(9));
            Assert.That((int)WeaponKind.LaserSniper,Is.EqualTo(10));
            Assert.That((int)WeaponKind.LaserSMG,Is.EqualTo(11),"Append-only: replicated events carry the numeric kind.");
            Assert.That(WeaponCatalog.FromPickup("laser-pistol"),Is.EqualTo(WeaponKind.LaserPistol));
            Assert.That(WeaponCatalog.FromPickup("laser-shotgun"),Is.EqualTo(WeaponKind.LaserShotgun));
            Assert.That(WeaponCatalog.FromPickup("laser-sniper"),Is.EqualTo(WeaponKind.LaserSniper));
            Assert.That(WeaponCatalog.FromPickup("laser-smg"),Is.EqualTo(WeaponKind.LaserSMG));
        }
        // Angle in double precision so float rounding cannot hide a cone escape.
        static double Degrees(Vector3 a,Vector3 b)
        {
            double dot=((double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z)/System.Math.Sqrt(((double)a.x*a.x+(double)a.y*a.y+(double)a.z*a.z)*((double)b.x*b.x+(double)b.y*b.y+(double)b.z*b.z));
            return System.Math.Acos(System.Math.Max(-1,System.Math.Min(1,dot)))*180/System.Math.PI;
        }
        [TestCase(WeaponKind.LineDriver)] [TestCase(WeaponKind.LaserSMG)] [TestCase(WeaponKind.LaserPistol)] [TestCase(WeaponKind.LaserSniper)]
        public void SeededSpreadIsReproducibleBoundedAndBoundToShotIdentity(WeaponKind kind)
        {
            float spread=WeaponCatalog.SpreadDegrees(kind,0,1); var aim=new Vector3(1,2,3).normalized;
            var distinct=new System.Collections.Generic.HashSet<Vector3>(); double widest=0;
            for(int sequence=1;sequence<=256;sequence++)
            {
                uint seed=WeaponCatalog.ShotSeed(Act(1,sequence,sequence,CombatActionKind.Fire));
                var ray=WeaponCatalog.HitscanDirection(kind,aim,0,spread,seed);
                Assert.That(ray,Is.EqualTo(WeaponCatalog.HitscanDirection(kind,aim,0,spread,seed)));
                double angle=Degrees(aim,ray); widest=System.Math.Max(widest,angle);
                Assert.That(angle,Is.LessThanOrEqualTo(spread+.01),"Pellet escaped the cone.");
                distinct.Add(ray);
            }
            Assert.That(distinct.Count,Is.GreaterThan(250));
            Assert.That(widest,Is.GreaterThan(spread*.8),"Seeds must reach the outer cone, not cluster at the centre.");
            var a=Act(1,7,7,CombatActionKind.Fire); var tick=a; tick.Tick++; var authority=a; authority.AuthorityGeneration++;
            var life=a; life.LifeGeneration++; var owner=a; owner.Participant++; var sequenceOnly=a; sequenceOnly.Sequence++;
            foreach(var other in new[]{tick,authority,life,owner,sequenceOnly}) Assert.That(WeaponCatalog.ShotSeed(other),Is.Not.EqualTo(WeaponCatalog.ShotSeed(a)));
            var shot=new CombatEvent(CombatEventKind.Shot,a,7,position:Vector3.zero,vector:aim,amount:spread,weapon:kind);
            Assert.That(WeaponCatalog.ShotDirection(shot,0),Is.EqualTo(WeaponCatalog.HitscanDirection(kind,aim,0,spread,WeaponCatalog.ShotSeed(a))),"Presentation replays the authoritative ray.");
            Assert.That(WeaponCatalog.HitscanDirection(kind,aim,0,0,123u),Is.EqualTo(aim.normalized),"Zero spread is the exact aim.");
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>WeaponCatalog.HitscanDirection(kind,aim*3,0,0,0),"Aim must be a unit direction.");
            foreach(float bad in new[]{float.NaN,-1f,46f})
                Assert.Throws<System.ArgumentOutOfRangeException>(()=>WeaponCatalog.HitscanDirection(kind,aim,0,bad,0));
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>WeaponCatalog.HitscanDirection(WeaponKind.PulseLobber,aim,0,1,0));
        }
        [Test] public void AdsTightensEveryConeAndOnlyTheScopedSniperIsExact()
        {
            foreach(var kind in new[]{WeaponKind.LineDriver,WeaponKind.LaserSMG,WeaponKind.LaserPistol,WeaponKind.LaserSniper,WeaponKind.LaserShotgun})
            {
                float hip=WeaponCatalog.SpreadDegrees(kind,0,0), half=WeaponCatalog.SpreadDegrees(kind,.5f,0), ads=WeaponCatalog.SpreadDegrees(kind,1,0);
                Assert.That(hip,Is.GreaterThan(0),kind.ToString()); Assert.That(half,Is.LessThan(hip),kind.ToString()); Assert.That(ads,Is.LessThan(half),kind.ToString());
                Assert.That(WeaponCatalog.SpreadDegrees(kind,0,1),Is.GreaterThanOrEqualTo(hip));
                Assert.That(WeaponCatalog.SpreadDegrees(kind,float.NaN,float.NaN),Is.EqualTo(hip));
                Assert.That(WeaponCatalog.CanAim(kind),Is.True);
            }
            Assert.That(WeaponCatalog.SpreadDegrees(WeaponKind.LaserSniper,1,1),Is.Zero);
            Assert.That(WeaponCatalog.SpreadDegrees(WeaponKind.LaserSniper,WeaponCatalog.ScopeThreshold,1),Is.Zero,"Exact as soon as the scope shows.");
            Assert.That(WeaponCatalog.ScopeActive(WeaponKind.LaserSniper,WeaponCatalog.ScopeThreshold-.05f),Is.False);
            Assert.That(WeaponCatalog.SpreadDegrees(WeaponKind.LaserSniper,WeaponCatalog.ScopeThreshold-.05f,0),Is.GreaterThan(0),"Still a gamble before it shows.");
            Assert.That(WeaponCatalog.SpreadDegrees(WeaponKind.LaserSniper,0,0),Is.GreaterThan(WeaponCatalog.SpreadDegrees(WeaponKind.LaserShotgun,0,0)),"A no-scope sniper shot is a gamble.");
            foreach(var kind in new[]{WeaponKind.PulseLobber,WeaponKind.ReboundDisc,WeaponKind.PushLens,WeaponKind.StunGun,WeaponKind.LaserSword,WeaponKind.Grenade})
            { Assert.That(WeaponCatalog.CanAim(kind),Is.False); Assert.That(WeaponCatalog.SpreadDegrees(kind,0,1),Is.Zero); }
            Assert.That(WeaponCatalog.ScopeActive(WeaponKind.LaserSniper,1),Is.True); Assert.That(WeaponCatalog.ScopeActive(WeaponKind.LaserSniper,.5f),Is.False);
            Assert.That(WeaponCatalog.ScopeActive(WeaponKind.LaserPistol,1),Is.False);
            Assert.That(WeaponCatalog.HipCrosshairVisible(WeaponKind.LaserSMG,1),Is.False); Assert.That(WeaponCatalog.HipCrosshairVisible(WeaponKind.LaserSMG,0),Is.True);
            Assert.That(WeaponCatalog.HipCrosshairVisible(WeaponKind.LaserShotgun,1),Is.True);
            Assert.That(WeaponCatalog.AdsVerticalFov(60,WeaponKind.LaserSniper,0),Is.EqualTo(60).Within(.001f));
            Assert.That(Mathf.Tan(WeaponCatalog.AdsVerticalFov(60,WeaponKind.LaserSniper,1)*.5f*Mathf.Deg2Rad),Is.EqualTo(Mathf.Tan(30*Mathf.Deg2Rad)/4).Within(1e-5f));
            Assert.That(WeaponCatalog.AdsVerticalFov(60,WeaponKind.PulseLobber,1),Is.EqualTo(60).Within(.001f));
        }
        [Test] public void HeldAimReachesFullAdsAndScopedSniperFiresAlongAim()
        {
            var aim=new Vector3(.1f,.05f,1).normalized;
            var m=Match(); var p=m.Find(1); Equip(p,WeaponKind.LaserSniper); var world=new LaserWorld();
            Assert.That(m.SetPose(1,1,p.Feet,aim,1.8f,true),Is.True);
            int full=WeaponCatalog.Ticks(WeaponCatalog.AdsSeconds(WeaponKind.LaserSniper),60);
            Advance(m,full-1); Assert.That(p.AdsProgress,Is.InRange(.01f,.999f));
            Advance(m,full); Assert.That(p.AdsProgress,Is.EqualTo(1));
            var fire=Act(1,1,m.Tick+1,CombatActionKind.Fire); fire.Direction=aim;
            m.Step(m.Tick+1,new[]{fire},world);
            Assert.That(m.Events.Single(e=>e.Kind==CombatEventKind.Shot).Amount,Is.Zero);
            Assert.That(Degrees(aim,world.Directions[0]),Is.LessThan(.001));
            Assert.That(p.Bloom,Is.EqualTo(WeaponCatalog.BloomPerShot(WeaponKind.LaserSniper)));
            var hip=Match(); Equip(hip.Find(1),WeaponKind.LaserSniper); var hipWorld=new LaserWorld();
            fire=Act(1,1,1,CombatActionKind.Fire); fire.Direction=aim; hip.Step(1,new[]{fire},hipWorld);
            Assert.That(hip.Events.Single(e=>e.Kind==CombatEventKind.Shot).Amount,Is.EqualTo(WeaponCatalog.SpreadDegrees(WeaponKind.LaserSniper,0,0)));
            Assert.That(Degrees(aim,hipWorld.Directions[0]),Is.GreaterThan(.001).And.LessThanOrEqualTo(WeaponCatalog.SpreadDegrees(WeaponKind.LaserSniper,0,0)+.01));
        }
        [Test] public void AdsDropsOnSwitchReloadDeathAndNeverRaisesForNonAimingWeapons()
        {
            var m=Match(); var p=m.Find(1); Equip(p,WeaponKind.LaserPistol);
            m.SetPose(1,1,p.Feet,Vector3.forward,1.8f,true); Advance(m,30); Assert.That(p.AdsProgress,Is.EqualTo(1));
            m.Step(31,new[]{Act(1,1,31,CombatActionKind.SelectStarter)},new EmptyCombatWorld());
            Assert.That(p.AdsProgress,Is.EqualTo(CombatRules.AdvanceAds(0,true,WeaponKind.LineDriver,1f/60)),"Switching restarts ADS from zero.");
            Advance(m,60); Assert.That(p.AdsProgress,Is.EqualTo(1));
            m.Step(61,new[]{Act(1,2,61,CombatActionKind.Fire)},new EmptyCombatWorld());
            m.Step(62,new[]{Act(1,3,62,CombatActionKind.Reload)},new EmptyCombatWorld());
            Assert.That(p.ReloadUntil,Is.GreaterThan(0)); Assert.That(p.AdsProgress,Is.Zero,"Reload holds ADS at zero.");
            Advance(m,m.Tick+5); Assert.That(p.AdsProgress,Is.Zero);
            Advance(m,p.ReloadUntil+10); Assert.That(p.ReloadUntil,Is.Zero); Assert.That(p.AdsProgress,Is.GreaterThan(0),"Held aim resumes after the reload.");
            m.SetPose(1,1,p.Feet,Vector3.forward,1.8f,false); Advance(m,m.Tick+60); Assert.That(p.AdsProgress,Is.Zero,"Releasing aim returns to the hip.");
            m.SetPose(1,1,p.Feet,Vector3.forward,1.8f,true); Advance(m,m.Tick+5);
            p.Health=0; Advance(m,m.Tick+1); Assert.That(p.Alive,Is.False); Assert.That(p.AdsProgress,Is.Zero);
            var lobber=Match(); var q=lobber.Find(1); Equip(q,WeaponKind.PulseLobber);
            lobber.SetPose(1,1,q.Feet,Vector3.forward,1.8f,true); Advance(lobber,30); Assert.That(q.AdsProgress,Is.Zero);
            Assert.That(CombatRules.AdvanceAds(float.NaN,false,WeaponKind.LineDriver,1),Is.Zero);
            Assert.That(CombatRules.AdvanceAds(.5f,true,WeaponKind.LineDriver,float.NaN),Is.EqualTo(.5f));
        }
        [Test] public void BloomGrowsPerAcceptedShotHoldsThenDecaysToHipCone()
        {
            var m=Match(); var p=m.Find(1); Equip(p,WeaponKind.LaserSMG); var world=new LaserWorld();
            int interval=WeaponCatalog.Ticks(WeaponCatalog.Get(WeaponKind.LaserSMG).Interval,60); var spreads=new System.Collections.Generic.List<float>();
            for(int shot=0;shot<5;shot++)
            {
                int tick=m.Tick+1; m.Step(tick,new[]{Act(1,shot+1,tick,CombatActionKind.Fire)},world);
                spreads.Add(m.Events.Single(e=>e.Kind==CombatEventKind.Shot).Amount);
                if(shot<4) Advance(m,m.Tick+interval-1);
            }
            Assert.That(spreads[0],Is.EqualTo(WeaponCatalog.SpreadDegrees(WeaponKind.LaserSMG,0,0)));
            for(int i=1;i<spreads.Count;i++) Assert.That(spreads[i],Is.GreaterThan(spreads[i-1]),"Sustained fire widens the cone.");
            float held=p.Bloom; Assert.That(held,Is.InRange(.01f,1f));
            Advance(m,p.BloomRecoverTick-1); Assert.That(p.Bloom,Is.EqualTo(held),"Bloom holds briefly after a shot.");
            Advance(m,m.Tick+60); Assert.That(p.Bloom,Is.Zero);
            int last=m.Tick+1; m.Step(last,new[]{Act(1,6,last,CombatActionKind.Fire)},world);
            Assert.That(m.Events.Single(e=>e.Kind==CombatEventKind.Shot).Amount,Is.EqualTo(spreads[0]));
            p.Bloom=.95f; p.FireReadyTick=0; m.Step(last+1,new[]{Act(1,7,last+1,CombatActionKind.Fire)},world); Assert.That(p.Bloom,Is.EqualTo(1),"Bloom saturates.");
        }
        sealed class HeadWorld : EmptyCombatWorld,IHitscanCombatWorld
        {
            public bool Head=true;
            public bool Hitscan(CombatAction action,Vector3 origin,Vector3 delta,int ignored,out CombatHit hit)
            { hit=new CombatHit(2,origin+delta.normalized*5,-delta.normalized,5,Head); return true; }
        }
        [TestCase(WeaponKind.LaserPistol,48,true)] [TestCase(WeaponKind.LaserSMG,15,true)] [TestCase(WeaponKind.LineDriver,19.5f,true)]
        [TestCase(WeaponKind.LaserShotgun,72,false)]
        public void HeadHitsMultiplyOnlyPrecisionWeapons(WeaponKind kind,float damage,bool headshot)
        {
            var m=Match(); if(kind!=WeaponKind.LineDriver) Equip(m.Find(1),kind);
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new HeadWorld());
            var hits=m.Events.Where(e=>e.Kind==CombatEventKind.Hit).ToArray();
            Assert.That(hits.Sum(e=>e.Amount),Is.EqualTo(damage).Within(.001f)); Assert.That(hits.All(e=>e.Headshot==headshot),Is.True);
            Assert.That(m.Find(2).Health,Is.EqualTo(100-damage).Within(.001f));
            var body=Match(); if(kind!=WeaponKind.LineDriver) Equip(body.Find(1),kind);
            body.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new HeadWorld {Head=false});
            Assert.That(body.Events.Where(e=>e.Kind==CombatEventKind.Hit).All(e=>!e.Headshot),Is.True);
            Assert.That(body.Find(2).Health,Is.EqualTo(100-WeaponCatalog.Get(kind).Damage*WeaponCatalog.HitscanPellets(kind)).Within(.001f));
        }
        [Test] public void SniperHeadshotDefeatsFullShieldAndHealth()
        {
            var m=Match(); Equip(m.Find(1),WeaponKind.LaserSniper); m.Find(2).Shield=WeaponCatalog.Shield; m.Find(2).ShieldUntil=1000;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new HeadWorld());
            Assert.That(m.Find(2).Alive,Is.False); Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Death),Is.True);
            Assert.That(m.Events.Single(e=>e.Kind==CombatEventKind.Hit).Headshot,Is.True);
        }
        [Test] public void HeadSphereFollowsCapsuleHeightAndRequiresAForwardCrossing()
        {
            var feet=new Vector3(0,0,10); var eye=new Vector3(0,1.62f,0);
            var standing=CombatRules.HeadCenter(feet,1.8f); var sliding=CombatRules.HeadCenter(feet,.9f);
            Assert.That(CombatRules.RayHitsHead(eye,(standing-eye).normalized,20,feet,1.8f),Is.True);
            Assert.That(CombatRules.RayHitsHead(eye,(feet+Vector3.up*.9f-eye).normalized,20,feet,1.8f),Is.False,"Chest aim is a body hit.");
            Assert.That(CombatRules.RayHitsHead(eye,(standing-eye).normalized,20,feet,.9f),Is.False,"A slider's head drops with the capsule.");
            Assert.That(CombatRules.RayHitsHead(eye,(sliding-eye).normalized,20,feet,.9f),Is.True);
            Assert.That(CombatRules.RayHitsHead(eye,(standing+Vector3.up*(CombatRules.HeadRadius+.02f)-eye).normalized,20,feet,1.8f),Is.False);
            Assert.That(CombatRules.RayHitsHead(eye,(eye-standing).normalized,20,feet,1.8f),Is.False,"Behind the shooter.");
            Assert.That(CombatRules.RayHitsHead(eye,(standing-eye).normalized,5,feet,1.8f),Is.False,"Beyond the ray length.");
            Assert.That(CombatRules.RayHitsHead(eye,Vector3.zero,20,feet,1.8f),Is.False);
            Assert.That(standing.y+CombatRules.HeadRadius,Is.LessThanOrEqualTo(feet.y+1.8f+.001f),"Head sphere stays inside the capsule top.");
        }
        [Test] public void TorsoBoxStopsUnderTheHeadAndTurnsWithTheAvatar()
        {
            var feet=new Vector3(0,0,10); var eye=new Vector3(0,1.62f,0);
            Assert.That(CombatRules.RayHitsBody(eye,(feet+Vector3.up*1.1f-eye).normalized,20,feet,0,1.8f,out float entry),Is.True,"Chest.");
            Assert.That(entry,Is.EqualTo(9.73f).Within(.02f));
            Assert.That(CombatRules.RayHitsBody(new Vector3(.3f,1.55f,0),Vector3.forward,20,feet,0,1.8f,out _),Is.False,"Past the ear above the torso.");
            Assert.That(CombatRules.RayHitsHead(new Vector3(.3f,1.55f,0),Vector3.forward,20,feet,1.8f),Is.False,"Past the ear beside the head.");
            Assert.That(CombatRules.RayHitsBody(new Vector3(.31f,1f,0),Vector3.forward,20,feet,0,1.8f,out _),Is.False,"Beside the torso.");
            Assert.That(CombatRules.RayHitsBody(new Vector3(.31f,1f,0),Vector3.forward,20,feet,45,1.8f,out _),Is.True,"A turned torso's corner reaches 0.38 m.");
            Assert.That(CombatRules.RayHitsBody(new Vector3(0,1.2f,0),Vector3.forward,20,feet,0,.9f,out _),Is.False,"A slider's torso drops.");
            Assert.That(CombatRules.RayHitsBody(new Vector3(0,.4f,0),Vector3.forward,20,feet,0,.9f,out _),Is.True);
            Assert.That(CombatRules.RayHitsBody(new Vector3(0,1f,0),Vector3.forward,5,feet,0,1.8f,out _),Is.False,"Beyond the ray length.");
            Assert.That(CombatRules.RayHitsBody(new Vector3(0,1f,0),Vector3.back,20,feet,0,1.8f,out _),Is.False,"Behind the shooter.");
            Assert.That(CombatRules.BodyTop(1.8f),Is.EqualTo(CombatRules.HeadCenter(Vector3.zero,1.8f).y-CombatRules.HeadRadius).Within(1e-5f));
        }
        [Test] public void LaserSmgIsAutomaticFastAndFinite()
        {
            var smg=WeaponCatalog.Get(WeaponKind.LaserSMG);
            Assert.That(WeaponCatalog.IsHitscan(WeaponKind.LaserSMG),Is.True); Assert.That(WeaponCatalog.IsAutomatic(WeaponKind.LaserSMG),Is.True);
            Assert.That(WeaponCatalog.IsAutomatic(WeaponKind.LaserPistol),Is.False); Assert.That(WeaponCatalog.IsAutomatic(WeaponKind.LaserSniper),Is.False);
            Assert.That(smg.Interval,Is.LessThan(WeaponCatalog.Get(WeaponKind.LineDriver).Interval));
            Assert.That(smg.Range,Is.LessThan(WeaponCatalog.Get(WeaponKind.LineDriver).Range));
            Assert.That(smg.Damage*smg.Ammo,Is.GreaterThan(WeaponCatalog.Health));
        }
        [TestCase("ads-nan")] [TestCase("ads-high")] [TestCase("ads-low")] [TestCase("bloom-high")] [TestCase("bloom-recover")] [TestCase("dead-ads")] [TestCase("smg-ammo")]
        public void CheckpointRejectsInvalidAimState(string corruption)
        {
            var m=Match(); Equip(m.Find(1),WeaponKind.LaserSMG);
            var valid=m.Capture(); valid.Players[0].AdsProgress=.5f; valid.Players[0].Bloom=.5f; valid.Players[0].AimHeld=true;
            Assert.That(Match().TryRestore(valid),Is.True);
            var saved=m.Capture(); var p=saved.Players[0];
            switch(corruption)
            {
                case "ads-nan": p.AdsProgress=float.NaN; break;
                case "ads-high": p.AdsProgress=1.01f; break;
                case "ads-low": p.AdsProgress=-.01f; break;
                case "bloom-high": p.Bloom=1.5f; break;
                case "bloom-recover": p.BloomRecoverTick=saved.Tick+1000; break;
                case "dead-ads": p.Health=0; p.SpecialWeapon=WeaponKind.None; p.SpecialAmmo=0; p.SelectedSpecial=false; p.AdsProgress=.5f; break;
                case "smg-ammo": p.SpecialAmmo=WeaponCatalog.PickupAmmo(WeaponKind.LaserSMG)+1; break;
            }
            Assert.That(Match().TryRestore(saved),Is.False);
        }
        [Test] public void StarterCadenceClipReloadAndRevisionStayConsistent()
        {
            Assert.That(WeaponCatalog.Revision,Is.EqualTo(5));
            Assert.That(WeaponCatalog.Get(WeaponKind.LineDriver).Damage,Is.EqualTo(13));
            Assert.That(WeaponCatalog.Get(WeaponKind.LineDriver).Ammo,Is.EqualTo(24));
            // 60 Hz: the Line Driver interval is 9 ticks and the D036 automatic slack 2.
            Assert.That(WeaponCatalog.Ticks(WeaponCatalog.FireSlackSeconds,60),Is.EqualTo(2));
            var m=Match(); var p=m.Find(1);
            Advance(m,4);
            m.Step(5,new[]{Act(1,1,5,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(p.FireReadyTick,Is.EqualTo(14),"A shot long after the ready tick starts a fresh schedule.");
            Advance(m,10);
            m.Step(11,new[]{Act(1,2,11,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(p.StarterAmmo,Is.EqualTo(23),"Earlier than the slack is still a cooldown rejection.");
            m.Step(12,new[]{Act(1,3,12,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(p.StarterAmmo,Is.EqualTo(22));
            Assert.That(p.FireReadyTick,Is.EqualTo(23),"An early repeat keeps the schedule instead of pulling it forward.");
            Advance(m,24);
            m.Step(25,new[]{Act(1,4,25,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(p.FireReadyTick,Is.EqualTo(32),"A late repeat inside the slack keeps the schedule.");
            Advance(m,39);
            m.Step(40,new[]{Act(1,5,40,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(p.StarterAmmo,Is.EqualTo(20)); Assert.That(p.FireReadyTick,Is.EqualTo(49));
            // Per-press weapons get no slack: the pistol (17 ticks) rejects one tick early and restarts from the shot.
            p.SpecialWeapon=WeaponKind.LaserPistol; p.SpecialAmmo=18; p.SelectedSpecial=true;
            m.Step(41,new[]{Act(1,6,41,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(p.SpecialAmmo,Is.EqualTo(18));
            Advance(m,48);
            m.Step(49,new[]{Act(1,7,49,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(p.SpecialAmmo,Is.EqualTo(17)); Assert.That(p.FireReadyTick,Is.EqualTo(66));
            Advance(m,64);
            m.Step(65,new[]{Act(1,8,65,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(p.SpecialAmmo,Is.EqualTo(17));
            var saved=m.Capture(); saved.CatalogRevision=1;
            Assert.That(Match().TryRestore(saved),Is.False);
            saved=m.Capture(); saved.Players[0].StarterAmmo=25;
            Assert.That(Match().TryRestore(saved),Is.False);
        }
        [Test] public void DuplicateAttackSpendsOnceAndStaleLifeFails()
        {
            var m=Match(); var a=Act(1,1,1,CombatActionKind.Fire); m.Step(1,new[]{a,a},new EmptyCombatWorld());
            Assert.That(m.Players[0].StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo-1));
            a.Tick=2; a.Sequence=2; a.LifeGeneration=2; m.Step(2,new[]{a},new EmptyCombatWorld());
            Assert.That(m.Players[0].StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo-1));
        }
        [Test] public void ShieldAbsorbsOverflowAndStunNeverRefreshes()
        {
            var p=new CombatState { Health=100, Shield=60, ShieldUntil=100 };
            CombatRules.Damage(p,80); Assert.That(p.Health,Is.EqualTo(80)); Assert.That(p.Shield,Is.Zero);
            Assert.That(CombatRules.Stun(p,10,60),Is.True); int end=p.StunUntil, immune=p.ImmunityUntil;
            Assert.That(CombatRules.Stun(p,11,60),Is.False); Assert.That(p.StunUntil,Is.EqualTo(end)); Assert.That(p.ImmunityUntil,Is.EqualTo(immune));
        }
        [Test] public void PickupClaimsAreOrderedAndFullSlotDoesNotDelete()
        {
            var m=Match(); m.Players[1].Feet=Vector3.zero;
            m.Step(1,new[]{Act(2,1,1,CombatActionKind.Pickup),Act(1,1,1,CombatActionKind.Pickup)},new EmptyCombatWorld());
            Assert.That(m.Players[0].ShieldCharges,Is.EqualTo(1)); Assert.That(m.Players[1].ShieldCharges,Is.Zero);
            Assert.That(m.Pickups[0].Generation,Is.EqualTo(2));
        }
        [Test] public void InvalidCheckpointLeavesLiveStateIntact()
        {
            var m=Match(); var saved=m.Capture(); saved.Players[0].Health=float.NaN;
            Assert.That(m.TryRestore(saved),Is.False); Assert.That(m.Players[0].Health,Is.EqualTo(100));
        }
        [Test] public void ZoneUsesArcLengthAndSupportingFloor()
        {
            var zone=new MovingZone(new[]{Vector3.zero,new Vector3(10,0,0),new Vector3(10,0,10)},60);
            Assert.That(zone.Position(150).x,Is.EqualTo(5).Within(.001));
            Assert.That(zone.Contains(0,Vector3.zero,true,Vector3.zero,Vector3.up),Is.True);
            Assert.That(zone.Contains(0,new Vector3(0,4,0),true,new Vector3(0,4,0),Vector3.up),Is.False);
        }
        sealed class TargetWorld : EmptyCombatWorld
        {
            public bool Cover, BlinkAllowed;
            public int Target=2;
            public override bool Sweep(Vector3 origin,Vector3 delta,float radius,int owner,int ignored,out CombatHit hit)
            {
                int target=owner==1?Target:1;
                if(Cover || (ignored&(1<<target))!=0) { hit=default; return false; }
                hit=new CombatHit(target,origin+delta.normalized*.1f,-delta.normalized,.1f); return true;
            }
            public override bool Visible(Vector3 from,Vector3 to) => !Cover;
            public override bool TryBlink(CombatState p,Vector3 direction,float distance,out Vector3 feet)
            { feet=p.Feet+direction*distance; return BlinkAllowed; }
        }
        static void Advance(ArenaMatch m,int to,ICombatWorld world=null)
        { while(m.Tick<to) Assert.That(m.Step(m.Tick+1,new CombatAction[0],world??new EmptyCombatWorld()),Is.True); }
        [Test] public void OutsideRunningAndNonAuthorityCannotSpend()
        {
            var m=new ArenaMatch(Map()); m.AddPlayer(1,0,1,1,Vector3.zero);
            Assert.That(m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new EmptyCombatWorld()),Is.False);
            Assert.That(m.Begin(),Is.False); Assert.That(m.Players[0].StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo));
            m.AddPlayer(2,1,1,1,Vector3.forward); m.Begin();
            Assert.That(m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new EmptyCombatWorld(),false),Is.False);
            Assert.That(m.Tick,Is.Zero);
        }
        static ArenaMatch HealthMatch()
        {
            var map=Map(); map.Pickups[0].Item="health";
            var m=new ArenaMatch(map,60); m.AddPlayer(1,0,1,1,Vector3.zero); m.AddPlayer(2,1,1,1,Vector3.forward*5); m.Begin(); return m;
        }
        [TestCase(20f,70f)] [TestCase(70f,100f)]
        public void HealthPackRestoresUpToFullHealth(float before,float after)
        {
            var m=HealthMatch(); m.Find(1).Health=before;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Pickup)},new EmptyCombatWorld());
            Assert.That(m.Find(1).Health,Is.EqualTo(after)); Assert.That(m.Pickups[0].Generation,Is.EqualTo(2));
        }
        // The host's copy of a running claimant trails its prediction, so it accepts PickupLagMargin past the 2.5 m reach.
        [TestCase(2.8f,2)] [TestCase(3.1f,1)]
        public void HostAcceptsAPickupWithinTheLagMargin(float distance,int generation)
        {
            var m=Match(); m.Find(1).Feet=new Vector3(distance,0,0);
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Pickup)},new EmptyCombatWorld());
            Assert.That(m.Pickups[0].Generation,Is.EqualTo(generation));
        }
        [Test] public void FullHealthLeavesTheHealthPackInPlace()
        {
            var m=HealthMatch();
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Pickup)},new EmptyCombatWorld());
            Assert.That(m.Find(1).Health,Is.EqualTo(WeaponCatalog.Health));
            Assert.That(m.Pickups[0].Generation,Is.EqualTo(1)); Assert.That(m.Pickups[0].AvailableTick,Is.Zero);
        }
        [Test] public void FullConsumableDoesNotConsumePickupOrChangeGeneration()
        {
            var m=Match(); m.Players[0].ShieldCharges=1;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Pickup)},new EmptyCombatWorld());
            Assert.That(m.Pickups[0].Generation,Is.EqualTo(1)); Assert.That(m.Pickups[0].AvailableTick,Is.Zero);
        }
        [Test] public void AcceptedSameTickShotsTradeAndDeathClearsTemporaryInventory()
        {
            var m=Match(); foreach(var p in m.Players) { p.Health=WeaponCatalog.Get(WeaponKind.LineDriver).Damage; p.GrenadeAmmo=2; p.BlinkCharges=1; }
            m.Step(1,new[]{Act(2,1,1,CombatActionKind.Fire),Act(1,1,1,CombatActionKind.Fire)},new TargetWorld());
            Assert.That(m.Players.All(p=>!p.Alive && p.GrenadeAmmo==0 && p.BlinkCharges==0),Is.True);
            Assert.That(m.Events.Count(e=>e.Kind==CombatEventKind.Death),Is.EqualTo(2));
            m.Step(2,new[]{Act(1,2,2,CombatActionKind.Fire)},new TargetWorld());
            Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Shot),Is.False);
            Advance(m,181); Assert.That(m.Players.All(p=>p.Alive && p.Health==100 && p.StarterAmmo==WeaponCatalog.StarterAmmo && p.LifeGeneration==2),Is.True);
        }
        [Test] public void SameTickShieldPrecedesImpactAndReuseConsumesNothing()
        {
            var m=Match(); m.Players[1].ShieldCharges=1;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire),Act(2,1,1,CombatActionKind.Shield)},new TargetWorld());
            Assert.That(m.Players[1].Health,Is.EqualTo(100)); Assert.That(m.Players[1].Shield,Is.EqualTo(WeaponCatalog.Shield-WeaponCatalog.Get(WeaponKind.LineDriver).Damage));
            m.Players[1].ShieldCharges=1;
            m.Step(2,new[]{Act(2,2,2,CombatActionKind.Shield)},new EmptyCombatWorld());
            Assert.That(m.Players[1].ShieldCharges,Is.EqualTo(1)); Assert.That(m.Players[1].ShieldUntil,Is.EqualTo(361));
            Advance(m,361); Assert.That(m.Players[1].Shield,Is.Zero);
            Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.ShieldExpired),Is.True);
        }
        [Test] public void ReloadUsesTicksAndCannotBeRestartedOrOverfill()
        {
            var m=Match(); m.Players[0].StarterAmmo=0;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Reload)},new EmptyCombatWorld());
            m.Step(2,new[]{Act(1,2,2,CombatActionKind.Reload),Act(1,3,2,CombatActionKind.Fire)},new TargetWorld());
            Assert.That(m.Players[0].StarterAmmo,Is.Zero); Assert.That(m.Players[0].ReloadUntil,Is.EqualTo(73));
            Advance(m,73); Assert.That(m.Players[0].StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo));
        }
        [Test] public void BlinkRejectsWithoutCostAndSuccessfulReplayIsOneDiscontinuity()
        {
            var m=Match(); m.Players[0].BlinkCharges=1; var world=new TargetWorld();
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Blink)},world);
            Assert.That(m.Players[0].BlinkCharges,Is.EqualTo(1)); world.BlinkAllowed=true;
            var a=Act(1,2,2,CombatActionKind.Blink); m.Step(2,new[]{a,a},world);
            Assert.That(m.Players[0].BlinkCharges,Is.Zero); Assert.That(m.Players[0].Feet.z,Is.EqualTo(6));
            Assert.That(m.Events.Count(e=>e.Kind==CombatEventKind.Blink),Is.EqualTo(1));
        }
        [Test] public void BlinkFollowsTheHorizontalAim()
        {
            var m=Match(); m.Players[0].BlinkCharges=1; var world=new TargetWorld{BlinkAllowed=true}; var start=m.Players[0].Feet;
            var up=Act(1,1,1,CombatActionKind.Blink); up.Direction=new Vector3(0,1,1).normalized;
            m.Step(1,new[]{up},world);
            Assert.That(m.Players[0].BlinkCharges,Is.Zero,"45 degrees up still blinks.");
            Assert.That(m.Players[0].Feet.y,Is.EqualTo(start.y).Within(1e-4f)); Assert.That(m.Players[0].Feet.z-start.z,Is.EqualTo(6).Within(1e-4f));
            m.Players[0].BlinkCharges=1; var straight=Act(1,2,2,CombatActionKind.Blink); straight.Direction=Vector3.up;
            m.Step(2,new[]{straight},world);
            Assert.That(m.Players[0].BlinkCharges,Is.EqualTo(1),"Straight up has no horizontal aim; the charge is kept.");
        }
        [Test] public void TeamScoreHasNoOutnumberMultiplierAndContestsStopIt()
        {
            var m=Match(); m.AddPlayer(3,0,1,1,Vector3.zero);
            m.Step(1,new CombatAction[0],new EmptyCombatWorld()); Assert.That(m.Score(0),Is.EqualTo(1f/60));
            m.Players[1].Feet=Vector3.zero; m.Step(2,new CombatAction[0],new EmptyCombatWorld());
            Assert.That(m.Contested,Is.True); Assert.That(m.Score(0),Is.EqualTo(1f/60));
        }
        [Test] public void DisconnectCannotManufactureWinAndAbortsAfterGrace()
        {
            var m=Match(); m.SetConnection(2,1,false); Advance(m,1801);
            Assert.That(m.Phase,Is.EqualTo(ArenaMatchPhase.Aborted)); Assert.That(m.Winner,Is.EqualTo(-1)); Assert.That(m.Score(0),Is.Zero);
        }
        [Test] public void TimeLimitTiedTopScoresDraw()
        {
            var m=Match(); var s=m.Capture(); s.Tick=480*60-1; s.ScoreTicks=new[]{10,10,0,0};
            s.Players[0].Feet=new Vector3(-50,0,0); s.Players[1].Feet=new Vector3(-50,0,1);
            Assert.That(m.TryRestore(s),Is.True); m.Step(s.Tick+1,new CombatAction[0],new EmptyCombatWorld());
            Assert.That(m.Phase,Is.EqualTo(ArenaMatchPhase.Finished)); Assert.That(m.Winner,Is.EqualTo(-1));
        }
        [Test] public void SnapshotRestoresFuseAmmoAndReplayWithoutDuplicateProjectile()
        {
            var m=Match(); m.Players[0].GrenadeAmmo=2; var action=Act(1,1,1,CombatActionKind.Grenade);
            m.Step(1,new[]{action},new EmptyCombatWorld()); var snapshot=m.Capture();
            var restored=Match(); Assert.That(restored.TryRestore(snapshot),Is.True);
            action.Tick=2; restored.Step(2,new[]{action},new EmptyCombatWorld());
            Assert.That(restored.Projectiles.Count,Is.EqualTo(1)); Assert.That(restored.Players[0].GrenadeAmmo,Is.EqualTo(1));
            snapshot.Players[0].GrenadeAmmo=0; Assert.That(restored.Players[0].GrenadeAmmo,Is.EqualTo(1));
            Advance(restored,97); Assert.That(restored.Projectiles,Is.Empty);
        }
        [Test] public void CheckpointRejectsImmortalShieldAndMismatchedProjectileTeamAtomically()
        {
            var m=Match(); var saved=m.Capture(); saved.Players[0].Shield=60; saved.Players[0].ShieldUntil=int.MaxValue;
            Assert.That(m.TryRestore(saved),Is.False);
            m.Players[0].GrenadeAmmo=1; m.Step(1,new[]{Act(1,1,1,CombatActionKind.Grenade)},new EmptyCombatWorld());
            saved=m.Capture(); saved.Projectiles[0].OwnerTeam=3;
            Assert.That(m.TryRestore(saved),Is.False); Assert.That(m.Projectiles[0].OwnerTeam,Is.Zero);
        }
        [Test] public void AdvancingAuthorityKeepsCommittedEventIdentity()
        {
            var m=Match(); m.Players[0].GrenadeAmmo=1; m.Step(1,new[]{Act(1,1,1,CombatActionKind.Grenade)},new EmptyCombatWorld());
            int generation=m.Projectiles[0].Action.AuthorityGeneration;
            Assert.That(m.AdvanceAuthorityGeneration(2),Is.True);
            Assert.That(m.Projectiles[0].Action.AuthorityGeneration,Is.EqualTo(generation));
            Assert.That(m.TryRestore(m.Capture()),Is.True);
        }
        [Test] public void ZoneRampPlaneCountsWhileAirborneAndOtherStoreyDoNot()
        {
            var zone=new MovingZone(new[]{Vector3.zero,new Vector3(10,5,0),new Vector3(10,5,10)},60);
            var normal=new Vector3(-.5f,1,0).normalized;
            Assert.That(zone.Contains(0,new Vector3(3,1.5f,0),true,new Vector3(3,1.5f,0),normal),Is.True);
            Assert.That(zone.Contains(0,new Vector3(3,1.5f,0),false,new Vector3(3,1.5f,0),normal),Is.False);
            Assert.That(zone.Contains(0,new Vector3(3,5,0),true,new Vector3(3,5,0),Vector3.up),Is.False);
        }
        static void Equip(CombatState p,WeaponKind kind)
        { p.SpecialWeapon=kind; p.SpecialAmmo=WeaponCatalog.Get(kind).Ammo; p.SelectedSpecial=true; }
        [TestCase(WeaponKind.LineDriver)] [TestCase(WeaponKind.PulseLobber)] [TestCase(WeaponKind.ReboundDisc)] [TestCase(WeaponKind.StunGun)]
        public void RangedWeaponsDamageEnemyAndReserveOneRound(WeaponKind kind)
        {
            var m=Match(); m.Players[1].Feet=Vector3.forward*2;
            if(kind!=WeaponKind.LineDriver) Equip(m.Players[0],kind);
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new TargetWorld());
            Assert.That(m.Players[1].Health,Is.LessThan(100));
            Assert.That(kind==WeaponKind.LineDriver?m.Players[0].StarterAmmo:m.Players[0].SpecialAmmo,Is.EqualTo(WeaponCatalog.Get(kind).Ammo-1));
        }
        [TestCase(WeaponKind.PushLens)] [TestCase(WeaponKind.LaserSword)]
        public void WindupShotOccursOnceAtReleaseWithOriginalAction(WeaponKind kind)
        {
            var m=Match(); Equip(m.Find(1),kind);
            var action=Act(1,1,1,CombatActionKind.Fire);
            m.Step(1,new[]{action},new EmptyCombatWorld());
            Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Shot),Is.False);
            int release=m.Find(1).Pending.ResolveTick;
            for(int tick=2;tick<release;tick++)
            {
                m.Step(tick,new CombatAction[0],new EmptyCombatWorld());
                Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Shot),Is.False);
            }
            m.Step(release,new CombatAction[0],new EmptyCombatWorld());
            var shot=m.Events.Single(e=>e.Kind==CombatEventKind.Shot);
            Assert.That(shot.Action,Is.EqualTo(action)); Assert.That(shot.Tick,Is.EqualTo(release));
            Assert.That(shot.Weapon,Is.EqualTo(kind)); Assert.That(shot.Position,Is.EqualTo(m.Find(1).Eye));
            Assert.That(shot.Vector,Is.EqualTo(action.Direction));
            var restored=Match(); Assert.That(restored.TryRestore(m.Capture()),Is.True);
            for(int tick=release+1;tick<=release+20;tick++)
            {
                restored.Step(tick,new CombatAction[0],new EmptyCombatWorld());
                Assert.That(restored.Events.Any(e=>e.Kind==CombatEventKind.Shot),Is.False);
            }
        }
        [TestCase(WeaponKind.PushLens,"stun")] [TestCase(WeaponKind.LaserSword,"stun")]
        [TestCase(WeaponKind.PushLens,"blink")] [TestCase(WeaponKind.LaserSword,"blink")]
        [TestCase(WeaponKind.PushLens,"death")] [TestCase(WeaponKind.LaserSword,"death")]
        public void CancelledWindupNeverEmitsShot(WeaponKind kind,string cancellation)
        {
            var m=Match(); var player=m.Find(1); Equip(player,kind);
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new EmptyCombatWorld());
            int release=player.Pending.ResolveTick;
            if(cancellation=="stun") CombatRules.Stun(player,2,m.TickRate);
            else if(cancellation=="blink")
            {
                player.BlinkCharges=1;
                m.Step(2,new[]{Act(1,2,2,CombatActionKind.Blink)},new TargetWorld {BlinkAllowed=true});
            }
            else { player.Health=0; player.Alive=false; player.RespawnTick=release+100; CombatRules.ClearLife(player); }
            for(int tick=m.Tick+1;tick<=release+20;tick++)
            {
                m.Step(tick,new CombatAction[0],new EmptyCombatWorld());
                Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Shot || e.Kind==CombatEventKind.Impulse),Is.False);
            }
            Assert.That(player.Pending,Is.Null);
        }
        [Test] public void PushHasWindupNoDamageAndCoverBlocks()
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.PushLens);
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new TargetWorld());
            Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Impulse),Is.False);
            Advance(m,12,new TargetWorld());
            Assert.That(m.Events.Single(e=>e.Kind==CombatEventKind.Impulse).Vector,Is.EqualTo(new Vector3(0,4,14)));
            Assert.That(m.Players[1].Health,Is.EqualTo(100));
            Advance(m,52); m.Step(53,new[]{Act(1,2,53,CombatActionKind.Fire)},new TargetWorld());
            Advance(m,64,new TargetWorld { Cover=true }); Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Impulse),Is.False);
        }
        // Players are 0.5 m spheres around the eye; the sweep owner and its hit mask are skipped, as in the native world.
        sealed class Bodies : EmptyCombatWorld
        {
            readonly ArenaMatch match; public Bodies(ArenaMatch m) { match=m; }
            public override bool Sweep(Vector3 origin,Vector3 delta,float radius,int owner,int ignored,out CombatHit hit)
            {
                hit=default; float best=float.MaxValue; var direction=delta.normalized;
                foreach(var p in match.Players)
                {
                    float along=Vector3.Dot(p.Eye-origin,direction);
                    if(p.Participant==owner || (ignored&(1<<p.Participant))!=0 || along<0 || along>delta.magnitude || along>=best ||
                        (origin+direction*along-p.Eye).sqrMagnitude>.25f) continue;
                    best=along; hit=new CombatHit(p.Participant,origin+direction*along,-direction,along);
                }
                return best<float.MaxValue;
            }
        }
        [Test] public void PushReflectsEnemyOrbOnceAndItsBlastCreditsThePusherAgainstTheShooter()
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.PushLens); Equip(m.Players[1],WeaponKind.PulseLobber);
            var push=Act(1,1,1,CombatActionKind.Fire); var orb=Act(2,1,1,CombatActionKind.Fire); orb.Direction=Vector3.back;
            m.Step(1,new[]{push,orb},new Bodies(m));
            int release=m.Players[0].Pending.ResolveTick; Advance(m,release-1,new Bodies(m));
            var shot=m.Projectiles.Single(); int fuse=shot.ExpireTick;
            Assert.That(shot.Action.Participant,Is.EqualTo(2)); Assert.That(shot.Position.z,Is.GreaterThan(1),"Still in flight toward the pusher.");
            m.Step(release,new CombatAction[0],new Bodies(m));
            var cue=m.Events.Single(e=>e.Kind==CombatEventKind.Deflected);
            Assert.That(cue.Action,Is.EqualTo(push)); Assert.That(cue.Target,Is.EqualTo(2)); Assert.That(cue.Weapon,Is.EqualTo(WeaponKind.PulseLobber));
            Assert.That(shot.Deflected,Is.True); Assert.That(shot.OwnerTeam,Is.Zero); Assert.That(shot.Action.Participant,Is.EqualTo(1));
            Assert.That(shot.Action.Tick,Is.EqualTo(1),"Lifetime stays anchored to the launch."); Assert.That(shot.ExpireTick,Is.EqualTo(fuse));
            Assert.That(shot.Velocity,Is.EqualTo(Vector3.forward*18),"Relaunched along the push at the orb's own speed.");
            var replica=Match(); Assert.That(replica.TryRestore(m.Capture()),Is.True,"Clients and migrations restore a deflected projectile.");
            foreach(var copy in new[]{m,replica})
            {
                int blasts=0,hits=0;
                for(int tick=release+1;tick<=release+40;tick++)
                {
                    copy.Step(tick,new CombatAction[0],new Bodies(copy));
                    foreach(var e in copy.Events)
                    {
                        Assert.That(e.Kind,Is.Not.EqualTo(CombatEventKind.Deflected));
                        if(e.Kind==CombatEventKind.Explosion) { blasts++; Assert.That(e.Action,Is.EqualTo(shot.Action)); }
                        if(e.Kind==CombatEventKind.Hit) { hits++; Assert.That(e.Action.Participant,Is.EqualTo(1),"Credit transfers to the pusher."); Assert.That(e.Target,Is.EqualTo(2)); }
                    }
                }
                Assert.That(blasts,Is.EqualTo(1)); Assert.That(hits,Is.EqualTo(1)); Assert.That(copy.Projectiles,Is.Empty);
                Assert.That(copy.Players[1].Health,Is.EqualTo(40).Within(.01f),"The original shooter takes the blast once.");
                Assert.That(copy.Players[0].Health,Is.EqualTo(100));
            }
            // The online deflection check caught the pooled orb keeping its flag: the next launch reusing it is the shooter's own.
            var next=Act(2,2,release+100,CombatActionKind.Fire); next.Direction=Vector3.back;
            Advance(m,release+99,new Bodies(m)); Assert.That(m.Step(release+100,new[]{next},new Bodies(m)),Is.True);
            var fresh=m.Projectiles.Single();
            Assert.That(fresh.Deflected,Is.False,"A recycled projectile is not born deflected."); Assert.That(fresh.Action,Is.EqualTo(next)); Assert.That(fresh.OwnerTeam,Is.EqualTo(1));
        }
        [TestCase("grenade",true)] [TestCase("cone",false)] [TestCase("range",false)] [TestCase("cover",false)] [TestCase("friendly",false)] [TestCase("deflected",false)]
        public void PushReflectsOnlyAVisibleEnemyProjectileInItsConeOnce(string setup,bool reflected)
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.PushLens);
            var s=m.Capture(); s.Players[0].LastActionSequence=1; s.Players[1].LastActionSequence=1;
            bool grenade=setup=="grenade"; int owner=setup=="friendly"?1:2;
            s.Projectiles=new[]{ new ArenaProjectile { Action=Act(owner,1,0,grenade?CombatActionKind.Grenade:CombatActionKind.Fire),
                Weapon=grenade?WeaponKind.Grenade:WeaponKind.PulseLobber, OwnerTeam=owner-1, ExpireTick=grenade?96:300, Deflected=setup=="deflected",
                Position=setup=="cone"?new Vector3(4,1.65f,3):setup=="range"?new Vector3(0,1.65f,8.5f):new Vector3(0,1.65f,3) } };
            Assert.That(m.TryRestore(s),Is.True);
            ICombatWorld world=setup=="cover"?new TargetWorld { Cover=true }:new EmptyCombatWorld();
            m.Step(1,new[]{Act(1,2,1,CombatActionKind.Fire)},world);
            int release=m.Players[0].Pending.ResolveTick; Advance(m,release-1,world);
            var shot=m.Projectiles.Single(); int fuse=shot.ExpireTick;
            m.Step(release,new CombatAction[0],world);
            Assert.That(m.Events.Count(e=>e.Kind==CombatEventKind.Deflected),Is.EqualTo(reflected?1:0));
            Assert.That(shot.Action.Participant,Is.EqualTo(reflected?1:owner)); Assert.That(shot.OwnerTeam,Is.EqualTo(reflected?0:owner-1));
            Assert.That(shot.Deflected,Is.EqualTo(reflected || setup=="deflected")); Assert.That(shot.ExpireTick,Is.EqualTo(fuse),"The fuse continues.");
            if(grenade) Assert.That(shot.Velocity.z,Is.EqualTo(ArenaMatch.GrenadeLaunch(Vector3.forward).z).Within(.0001f),"A caught grenade is re-thrown along the push.");
            else Assert.That(shot.Velocity,Is.EqualTo(Vector3.zero));
        }
        [TestCase(0)] [TestCase(1)] public void LatePushNeitherResurrectsNorRecreditsAConfirmedBlast(int late)
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.PushLens); m.Players[1].GrenadeAmmo=1;
            var toss=Act(2,1,1,CombatActionKind.Grenade); toss.Direction=Vector3.back;
            m.Step(1,new[]{toss},new EmptyCombatWorld());
            int fuse=m.Projectiles[0].ExpireTick, windup=WeaponCatalog.Ticks(WeaponCatalog.Get(WeaponKind.PushLens).Windup,m.TickRate);
            var s=m.Capture(); s.Tick=fuse-windup-1+late; s.Projectiles[0].Position=new Vector3(0,1.65f,3); s.Projectiles[0].Velocity=Vector3.zero;
            Assert.That(m.TryRestore(s),Is.True);
            m.Step(s.Tick+1,new[]{Act(1,1,s.Tick+1,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(m.Players[0].Pending.ResolveTick,Is.EqualTo(fuse+late),late==0?"Resolves on the fuse tick.":"Resolves after the blast.");
            int blasts=0;
            while(m.Tick<fuse+late+2)
            {
                m.Step(m.Tick+1,new CombatAction[0],new EmptyCombatWorld());
                Assert.That(m.Events.Any(e=>e.Kind==CombatEventKind.Deflected),Is.False);
                foreach(var e in m.Events.Where(x=>x.Kind==CombatEventKind.Explosion)) { blasts++; Assert.That(e.Action.Participant,Is.EqualTo(2),"The thrower keeps a committed blast."); }
            }
            Assert.That(blasts,Is.EqualTo(1)); Assert.That(m.Projectiles,Is.Empty);
        }
        [Test] public void SwordDeduplicatesActiveSamplesAndBlinkBreaksOldSweep()
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.LaserSword); m.Players[1].Feet=Vector3.forward*2;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new TargetWorld());
            Advance(m,20,new TargetWorld()); Assert.That(m.Players[1].Health,Is.EqualTo(55));
            Advance(m,40); m.Step(41,new[]{Act(1,2,41,CombatActionKind.Fire)},new TargetWorld());
            m.Players[0].BlinkCharges=1; m.Step(42,new[]{Act(1,3,42,CombatActionKind.Blink)},new TargetWorld { BlinkAllowed=true });
            Advance(m,60,new TargetWorld()); Assert.That(m.Players[1].Health,Is.EqualTo(55)); Assert.That(m.Players[0].Pending,Is.Null);
        }
        [Test] public void StunImmunityDoesNotRemoveDamageAndFriendlyAttacksDoNothing()
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.StunGun);
            m.Players[1].ImmunityUntil=100;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new TargetWorld());
            Assert.That(m.Players[1].Health,Is.EqualTo(85)); Assert.That(m.Players[1].StunUntil,Is.Zero); Assert.That(m.Players[1].ImmunityUntil,Is.EqualTo(100));
            m.Players[1].Team=0; Advance(m,73); m.Step(74,new[]{Act(1,2,74,CombatActionKind.Fire)},new TargetWorld());
            Assert.That(m.Players[1].Health,Is.EqualTo(85)); Assert.That(m.Players[1].StunUntil,Is.Zero);
        }
        [Test] public void GrenadeFuseExplodesOnceWithSelfDamageAndCoverProtection()
        {
            var m=Match(); m.Players[0].GrenadeAmmo=1;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Grenade)},new EmptyCombatWorld());
            var saved=m.Capture(); saved.Tick=96; saved.Projectiles[0].Position=m.Players[0].Eye; saved.Projectiles[0].Velocity=Vector3.zero;
            Assert.That(m.TryRestore(saved),Is.True); m.Step(97,new CombatAction[0],new EmptyCombatWorld());
            Assert.That(m.Players[0].Health,Is.EqualTo(10)); Assert.That(m.Projectiles,Is.Empty);
            Assert.That(m.Events.Count(e=>e.Kind==CombatEventKind.Explosion),Is.EqualTo(1));
            Assert.That(m.TryRestore(saved),Is.True); m.Step(97,new CombatAction[0],new TargetWorld { Cover=true });
            Assert.That(m.Players[0].Health,Is.EqualTo(100));
        }
        sealed class Walls : EmptyCombatWorld
        {
            public override bool Sweep(Vector3 origin,Vector3 delta,float radius,int owner,int mask,out CombatHit hit)
            { hit=new CombatHit(0,origin+delta.normalized*.05f,-delta.normalized,.05f); return true; }
        }
        [Test] public void DiscBoundsRicochetsAndEachCueHasDistinctStableIdentity()
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.ReboundDisc);
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new Walls());
            Assert.That(m.Projectiles,Is.Empty);
            var bounces=m.Events.Where(e=>e.Kind==CombatEventKind.Ricochet).ToArray(); Assert.That(bounces.Length,Is.EqualTo(2));
            Assert.That(bounces.Select(e=>e.Id).Distinct().Count(),Is.EqualTo(2));
        }
        [Test] public void CheckpointReplaysMeleeDedupeAndShieldTimers()
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.LaserSword); m.Players[1].Feet=Vector3.forward*2;
            m.Players[0].ShieldCharges=1;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Shield),Act(1,2,1,CombatActionKind.Fire)},new TargetWorld());
            Advance(m,9,new TargetWorld()); Assert.That(m.Players[1].Health,Is.EqualTo(55));
            var copy=Match(); Assert.That(copy.TryRestore(m.Capture()),Is.True); Advance(copy,16,new TargetWorld());
            Assert.That(copy.Players[1].Health,Is.EqualTo(55)); Assert.That(copy.Players[0].ShieldUntil,Is.EqualTo(361));
        }
        [Test] public void BriefReconnectCannotResetOpponentAbsenceDeadline()
        {
            var m=Match(); m.SetConnection(2,1,false); Advance(m,1500); m.SetConnection(2,2,true);
            Advance(m,1510); m.SetConnection(2,2,false); Advance(m,1801);
            Assert.That(m.Phase,Is.EqualTo(ArenaMatchPhase.Aborted));
        }
        [Test] public void DeadCheckpointCannotCarryTemporaryItems()
        {
            var m=Match(); var s=m.Capture(); s.Players[0].Alive=false; s.Players[0].Health=0; s.Players[0].ShieldCharges=1; s.Players[0].RespawnTick=180;
            Assert.That(m.TryRestore(s),Is.False);
        }
        [Test] public void WrongEpochAndConnectionRejectBeforeCostOrSequenceMutation()
        {
            var m=Match(); var a=Act(1,1,1,CombatActionKind.Fire); a.AuthorityGeneration=2;
            var b=a; b.AuthorityGeneration=1; b.ConnectionGeneration=2;
            m.Step(1,new[]{a,b},new TargetWorld());
            Assert.That(m.Players[0].StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo)); Assert.That(m.Players[0].LastActionSequence,Is.EqualTo(-1));
            Assert.That(m.Events.All(e=>e.Kind==CombatEventKind.Rejected),Is.True);
        }
        [Test] public void PickupBeforeSameTickDeathIsConsumedAndDeathClearsNewCharge()
        {
            var m=Match(); m.Players[0].Health=WeaponCatalog.Get(WeaponKind.LineDriver).Damage;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Pickup),Act(2,1,1,CombatActionKind.Fire)},new TargetWorld());
            Assert.That(m.Players[0].Alive,Is.False); Assert.That(m.Players[0].ShieldCharges,Is.Zero); Assert.That(m.Pickups[0].Generation,Is.EqualTo(2));
        }
        [Test] public void MovementCallbackRunsAfterReservationAndBeforeScoring()
        {
            var m=Match(); m.Players[0].ShieldCharges=1;
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Shield)},new EmptyCombatWorld(),true,()=> {
                Assert.That(m.Players[0].Shield,Is.EqualTo(60)); m.SetPose(1,1,new Vector3(-50,0,0),Vector3.forward);
            });
            Assert.That(m.Score(0),Is.Zero);
        }
        [Test] public void ProjectileCapacityRejectsWithoutSpending()
        {
            var m=Match(); var s=m.Capture(); s.Players[0].LastActionSequence=64; s.Players[0].GrenadeAmmo=2;
            s.Projectiles=Enumerable.Range(1,64).Select(sequence=>new ArenaProjectile {
                Action=Act(1,sequence,0,CombatActionKind.Grenade),Weapon=WeaponKind.Grenade,OwnerTeam=0,ExpireTick=96,
                Position=Vector3.up*10,Velocity=Vector3.zero }).ToArray();
            Assert.That(m.TryRestore(s),Is.True); m.Step(1,new[]{Act(1,65,1,CombatActionKind.Grenade)},new EmptyCombatWorld());
            Assert.That(m.Projectiles.Count,Is.EqualTo(64)); Assert.That(m.Players[0].GrenadeAmmo,Is.EqualTo(2));
        }
        [Test] public void SlidePoseUsesTheSameEyeHeightAsFirstPersonView()
        {
            var m=Match(); Assert.That(m.SetPose(1,1,Vector3.zero,Vector3.forward,.9f),Is.True);
            Assert.That(m.Players[0].Eye.y,Is.EqualTo(.75f).Within(.0001f));
            Assert.That(m.SetPose(1,1,Vector3.zero,Vector3.forward,float.NaN),Is.False);
        }
        [TestCase(60)] [TestCase(120)] public void PulseEndpointDetonatesExactlyOnceWithoutCollision(int rate)
        {
            var m=new ArenaMatch(Map(),rate); m.AddPlayer(1,0,1,1,Vector3.zero); m.AddPlayer(2,1,1,1,new Vector3(0,0,89)); m.Begin();
            Equip(m.Players[0],WeaponKind.PulseLobber); var world=new EmptyCombatWorld(); var empty=new CombatAction[0];
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},world);
            int explosions=0,hits=0,impulses=0;
            for(int tick=2;tick<=rate*5+2;tick++)
            {
                m.Step(tick,empty,world);
                foreach(var e in m.Events) { if(e.Kind==CombatEventKind.Explosion) explosions++; if(e.Kind==CombatEventKind.Hit && e.Target==2) hits++; if(e.Kind==CombatEventKind.Impulse && e.Target==2) impulses++; }
            }
            Assert.That(explosions,Is.EqualTo(1)); Assert.That(hits,Is.EqualTo(1)); Assert.That(impulses,Is.EqualTo(1));
            Assert.That(m.Players[1].Health,Is.LessThan(100)); Assert.That(m.Projectiles,Is.Empty);
        }
        [TestCase(true)] [TestCase(false)] public void ExhaustedCheckpointCountersRejectWithoutMutation(bool life)
        {
            var m=Match(); var saved=m.Capture();
            if(life) saved.Players[0].LifeGeneration=int.MaxValue; else saved.Players[0].LastActionSequence=int.MaxValue;
            Assert.That(m.TryRestore(saved),Is.False); Assert.That(m.Tick,Is.Zero); Assert.That(m.Find(1).LifeGeneration,Is.EqualTo(1));
            Assert.That(m.Find(1).LastActionSequence,Is.EqualTo(-1)); Assert.That(m.Find(1).Health,Is.EqualTo(100));
        }
        [Test] public void LastUsableLifeAbortsBeforeRespawnCanOverflow()
        {
            var m=Match(); var saved=m.Capture(); var p=saved.Players[0]; p.LifeGeneration=int.MaxValue-1;
            p.Alive=false; p.Health=0; p.RespawnTick=1; Assert.That(m.TryRestore(saved),Is.True);
            m.Step(1,new CombatAction[0],new EmptyCombatWorld());
            Assert.That(m.Phase,Is.EqualTo(ArenaMatchPhase.Aborted)); Assert.That(m.Find(1).LifeGeneration,Is.EqualTo(int.MaxValue-1)); Assert.That(m.Find(1).Alive,Is.False);
        }
        static System.Func<long> AllocationCounter()
        {
            var method=typeof(System.GC).GetMethod("GetAllocatedBytesForCurrentThread",System.Type.EmptyTypes);
            if(method==null) { Assert.Ignore("Installed managed runtime lacks per-thread allocation measurement."); return null; }
            return (System.Func<long>)System.Delegate.CreateDelegate(typeof(System.Func<long>),method);
        }
        [TestCase(false)] [TestCase(true)] public void Warm120HzSimulationDoesNotAllocate(bool combat)
        {
            var bytes=AllocationCounter(); var m=new ArenaMatch(Map(),120); var world=new EmptyCombatWorld();
            var actions=combat?new CombatAction[8]:new CombatAction[0];
            for(int id=1;id<=8;id++) m.AddPlayer(id,(id-1)/2,1,1,new Vector3(-50,0,id*3));
            m.Begin();
            for(int pass=0;pass<2;pass++)
            {
                long before=bytes();
                for(int n=0;n<600;n++)
                {
                    if(combat) for(int i=0;i<8;i++)
                    {
                        var p=m.Players[i]; p.StarterAmmo=WeaponCatalog.StarterAmmo; p.GrenadeAmmo=2;
                        if(i<4) { p.SpecialWeapon=WeaponKind.PulseLobber; p.SelectedSpecial=true; }
                        else { p.SpecialWeapon=WeaponKind.LaserSword; p.SelectedSpecial=true; }
                        p.SpecialAmmo=WeaponCatalog.Get(p.SpecialWeapon).Ammo;
                        actions[i]=Act(i+1,m.Tick+1,m.Tick+1,n%2==0?CombatActionKind.Fire:CombatActionKind.Grenade);
                    }
                    m.Step(m.Tick+1,actions,world);
                }
                long allocated=bytes()-before;
                if(pass==1) { TestContext.Out.WriteLine("120Hz "+(combat?"combat":"empty")+" 600 ticks allocated "+allocated+" bytes"); Assert.That(allocated,Is.Zero); }
            }
        }
        [Test] public void NearTerminalSequenceRestoreRejectsAndReconnectCannotOverflowLife()
        {
            var m=Match(); var saved=m.Capture(); saved.Players[0].LastActionSequence=int.MaxValue-2;
            Assert.That(m.TryRestore(saved),Is.False);
            Assert.That(m.Phase,Is.EqualTo(ArenaMatchPhase.Running)); Assert.That(m.Find(1).StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo));
            Assert.That(m.Find(1).LastActionSequence,Is.EqualTo(-1)); Assert.That(m.Tick,Is.Zero);
            m=Match(); saved=m.Capture(); saved.Players[0].LifeGeneration=int.MaxValue-1;
            Assert.That(m.TryRestore(saved),Is.True); m.SetConnection(1,1,false);
            Advance(m,300);
            Assert.That(m.SetConnection(1,2,true),Is.False); Assert.That(m.Phase,Is.EqualTo(ArenaMatchPhase.Aborted));
            Assert.That(m.Find(1).LifeGeneration,Is.EqualTo(int.MaxValue-1)); Assert.That(m.Find(1).ConnectionGeneration,Is.EqualTo(1));
        }
        [Test] public void ReconnectBeforeAvatarHoldExpiresKeepsHealthAmmoAndLife()
        {
            var m=Match(); var player=m.Find(1);
            player.Health=37; player.StarterAmmo=3; player.SpecialWeapon=WeaponKind.PulseLobber;
            player.SpecialAmmo=2; player.BlinkCharges=1; player.FireReadyTick=100; player.LastActionSequence=7;
            var feet=new Vector3(3,0,2); player.Feet=feet;
            Assert.That(m.SetConnection(1,1,false),Is.True); Advance(m,299);
            Assert.That(m.SetConnection(1,2,true),Is.True);
            Assert.That(player.Alive,Is.True); Assert.That(player.Health,Is.EqualTo(37)); Assert.That(player.StarterAmmo,Is.EqualTo(3));
            Assert.That(player.SpecialWeapon,Is.EqualTo(WeaponKind.PulseLobber)); Assert.That(player.SpecialAmmo,Is.EqualTo(2));
            Assert.That(player.BlinkCharges,Is.EqualTo(1)); Assert.That(player.LifeGeneration,Is.EqualTo(1)); Assert.That(player.Feet,Is.EqualTo(feet));
            Assert.That(player.LastActionSequence,Is.EqualTo(7)); Assert.That(player.ConnectionGeneration,Is.EqualTo(2)); Assert.That(player.DisconnectUntil,Is.Zero);
            Assert.That(m.SetConnection(1,1,false),Is.False,"Retired connection cannot disconnect the rebound avatar.");
        }
        [Test] public void ReconnectAfterHoldExpiresRespawnsWithStarterGear()
        {
            var m=Match(); var player=m.Find(1); player.Health=37; player.StarterAmmo=3;
            player.SpecialWeapon=WeaponKind.PulseLobber; player.SpecialAmmo=2;
            Assert.That(m.SetConnection(1,1,false),Is.True); Advance(m,300);
            Assert.That(player.Alive,Is.False);
            Assert.That(m.SetConnection(1,2,true),Is.True);
            Assert.That(player.Alive,Is.True); Assert.That(player.Health,Is.EqualTo(100)); Assert.That(player.StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo));
            Assert.That(player.LifeGeneration,Is.EqualTo(2)); Assert.That(player.SpecialWeapon,Is.EqualTo(WeaponKind.None)); Assert.That(player.SpecialAmmo,Is.Zero);
        }
        [Test] public void ClientTerminalSequencePairCannotAbortMatchOrPoisonWatermark()
        {
            var m=Match(); var world=new EmptyCombatWorld();
            m.Step(1,new[]{Act(1,int.MaxValue-2,1,CombatActionKind.SelectStarter),Act(1,int.MaxValue-1,1,CombatActionKind.SelectStarter),
                Act(2,1,1,CombatActionKind.Fire)},world);
            Assert.That(m.Phase,Is.EqualTo(ArenaMatchPhase.Running)); Assert.That(m.Find(1).LastActionSequence,Is.EqualTo(-1));
            Assert.That(m.Find(2).StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo-1)); Assert.That(m.Events.Count(e=>e.Kind==CombatEventKind.Rejected),Is.EqualTo(2));
            m.Step(2,new[]{Act(1,1,2,CombatActionKind.Fire)},world);
            Assert.That(m.Find(1).StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo-1)); Assert.That(m.Phase,Is.EqualTo(ArenaMatchPhase.Running));
        }
        [Test] public void ContinuedPracticeSequenceAfterRespawnRemainsValid()
        {
            var m=Match(); m.Players[0].Health=WeaponCatalog.Get(WeaponKind.LineDriver).Damage;
            m.Step(1,new[]{Act(2,1,1,CombatActionKind.Fire)},new TargetWorld()); Advance(m,181);
            var a=Act(1,1000,182,CombatActionKind.Fire); a.LifeGeneration=2;
            m.Step(182,new[]{a},new EmptyCombatWorld());
            Assert.That(m.Find(1).StarterAmmo,Is.EqualTo(WeaponCatalog.StarterAmmo-1)); Assert.That(m.Find(1).LastActionSequence,Is.EqualTo(1000));
        }
        [Test] public void ReusedProjectileAndPendingStorageClearPreviousHitState()
        {
            var m=Match(); Equip(m.Players[0],WeaponKind.ReboundDisc);
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new EmptyCombatWorld());
            var first=m.Projectiles[0]; first.HitMask=1<<2; first.Bounces=2;
            Advance(m,241); m.Step(242,new[]{Act(1,2,242,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(m.Projectiles[0],Is.SameAs(first)); Assert.That(first.HitMask,Is.Zero); Assert.That(first.Bounces,Is.Zero);
            Equip(m.Players[0],WeaponKind.LaserSword); Advance(m,300);
            m.Step(301,new[]{Act(1,3,301,CombatActionKind.Fire)},new EmptyCombatWorld()); var swing=m.Find(1).Pending; swing.HitMask=1<<2;
            Advance(m,340); m.Step(341,new[]{Act(1,4,341,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(m.Find(1).Pending,Is.SameAs(swing)); Assert.That(swing.HitMask,Is.Zero); Assert.That(swing.LastSampleTick,Is.EqualTo(-1));
        }
    }
}
