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
        [Test] public void StarterCadenceClipReloadAndRevisionStayConsistent()
        {
            Assert.That(WeaponCatalog.Revision,Is.EqualTo(2));
            Assert.That(WeaponCatalog.Get(WeaponKind.LineDriver).Damage,Is.EqualTo(13));
            Assert.That(WeaponCatalog.Get(WeaponKind.LineDriver).Ammo,Is.EqualTo(24));
            var m=Match();
            m.Step(1,new[]{Act(1,1,1,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(m.Find(1).FireReadyTick,Is.EqualTo(10));
            Advance(m,8);
            m.Step(9,new[]{Act(1,2,9,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(m.Find(1).StarterAmmo,Is.EqualTo(23));
            m.Step(10,new[]{Act(1,3,10,CombatActionKind.Fire)},new EmptyCombatWorld());
            Assert.That(m.Find(1).StarterAmmo,Is.EqualTo(22));
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
