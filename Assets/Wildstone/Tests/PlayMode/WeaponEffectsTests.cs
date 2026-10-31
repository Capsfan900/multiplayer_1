using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
            effects.AnimateTransient(Time.unscaledTime+10);
            foreach(var line in root.GetComponentsInChildren<LineRenderer>(true)) if(line.name.StartsWith("Burst ")) Assert.That(line.enabled,Is.False);
        }

        // A ready pickup shows a full bright ring; a taken one shows a dim arc that refills with the timer.
        [Test] public void PickupPadsShowReadinessAndRefillWithTheRespawn()
        {
            Object.DestroyImmediate(root);
            match=new ArenaMatch(new ArenaDefinition { Id="effects",Spawns=System.Array.Empty<ArenaSpawn>(),
                Pickups=new[]{ new ArenaPickup { Item="laser-pistol",Position=new Vector3(0,.45f,4) },new ArenaPickup { Item="shield",Position=new Vector3(3,.45f,4) } },
                ZoneRoute=new[]{Vector3.zero,Vector3.forward*8,Vector3.right*8} },practice:true);
            root=new GameObject("WeaponEffectsTest"); effects=root.AddComponent<ArenaEffects>(); effects.Initialize(catalog,match);
            LineRenderer Ring(int id) => root.transform.Find("Pickup ring "+id).GetComponent<LineRenderer>();
            effects.Present(match,false);
            Assert.That(Ring(0).enabled && Ring(0).loop,Is.True);
            Assert.That(Ring(0).positionCount,Is.EqualTo(ArenaEffects.PickupRingPoints));
            Assert.That(Ring(0).GetPosition(0).y,Is.EqualTo(.03f).Within(.001f),"The ring sits on the floor under the floating item.");
            int full=WeaponCatalog.Ticks(WeaponCatalog.PickupSeconds,match.TickRate);
            match.Pickups[0].AvailableTick=match.Tick+full*3/4;
            effects.Present(match,false);
            int quarter=Ring(0).positionCount;
            Assert.That(Ring(0).loop,Is.False);
            Assert.That(Ring(0).startColor.r,Is.LessThan(.5f),"The refill arc is dim.");
            Assert.That(root.transform.Find("Pickup track 0").GetComponent<LineRenderer>().enabled,Is.True,"The dark track still marks the pad.");
            match.Pickups[0].AvailableTick=match.Tick+full/4;
            effects.Present(match,false);
            Assert.That(Ring(0).positionCount,Is.GreaterThan(quarter).And.LessThan(ArenaEffects.PickupRingPoints));
            Assert.That(Ring(1).loop && Ring(1).startColor.r>.5f,Is.True,"Other pads are unaffected.");
        }

        // A shielded player wears a shell sized to its capsule that dims as the shield drains; a stunned one a crackling halo.
        [Test] public void ShieldShellAndStunHaloFollowTheirPlayers()
        {
            match.AddPlayer(1,0,1,1,Vector3.zero); match.AddPlayer(2,1,1,1,Vector3.forward*5); match.Begin();
            var shielded=match.Find(2);
            shielded.Shield=WeaponCatalog.Shield;
            effects.Present(match,false);
            var shell=root.transform.Find("Shield shell 1");
            Assert.That(shell.gameObject.activeSelf,Is.True);
            Assert.That(root.transform.Find("Shield shell 0").gameObject.activeSelf,Is.False,"Only the shielded player has a shell.");
            Assert.That(shell.position.x,Is.EqualTo(0).Within(.001f)); Assert.That(shell.position.z,Is.EqualTo(5).Within(.001f));
            float standing=shell.localScale.y;
            float Strength() { var block=new MaterialPropertyBlock(); shell.GetComponent<Renderer>().GetPropertyBlock(block); return block.GetFloat("_Strength"); }
            float full=Strength();
            shielded.Shield=WeaponCatalog.Shield*.2f; shielded.CapsuleHeight=.9f;
            effects.Present(match,false);
            Assert.That(Strength(),Is.LessThan(full),"The shell dims as the shield drains.");
            Assert.That(shell.localScale.y,Is.LessThan(standing),"A sliding player's shell crouches with the capsule.");
            shielded.Shield=0; shielded.StunUntil=match.Tick+60;
            effects.Present(match,false);
            Assert.That(shell.gameObject.activeSelf,Is.False);
            var halo=root.transform.Find("Stun 1").GetComponent<LineRenderer>();
            Assert.That(halo.enabled && halo.loop,Is.True);
            Assert.That(halo.positionCount,Is.EqualTo(ArenaEffects.StunPoints));
            Assert.That(Vector3.Distance(halo.GetPosition(0),shielded.Eye),Is.LessThan(.6f),"The halo rings the head.");
        }

        // A broken shield's shell flares and swells for a moment, then clears; reduced motion skips the pop.
        [UnityTest] public IEnumerator BrokenShieldShellPopsThenClears()
        {
            match.AddPlayer(1,0,1,1,Vector3.zero); match.Begin();
            var player=match.Find(1);
            player.Shield=WeaponCatalog.Shield;
            effects.Present(match,false);
            var shell=root.transform.Find("Shield shell 0");
            float steady=shell.localScale.x;
            float Strength() { var block=new MaterialPropertyBlock(); shell.GetComponent<Renderer>().GetPropertyBlock(block); return block.GetFloat("_Strength"); }
            var broken=new CombatEvent(CombatEventKind.ShieldBroken,new CombatAction { Participant=2,LifeGeneration=1,Sequence=1 },match.Tick,target:1);
            player.Shield=0;
            effects.Play(broken,match,true,Vector3.zero); effects.Present(match,false);
            Assert.That(shell.gameObject.activeSelf,Is.False,"Reduced motion: the shell simply goes.");
            effects.Play(broken,match,false,Vector3.zero); effects.Present(match,false);
            Assert.That(shell.gameObject.activeSelf,Is.True);
            Assert.That(Strength(),Is.GreaterThan(1),"The breaking shell flares brighter than a full shield.");
            yield return new WaitForSecondsRealtime(ArenaEffects.ShellBreakSeconds*.5f);
            effects.Present(match,false);
            Assert.That(shell.localScale.x,Is.GreaterThan(steady),"It swells as it breaks.");
            yield return new WaitForSecondsRealtime(ArenaEffects.ShellBreakSeconds);
            effects.Present(match,false);
            Assert.That(shell.gameObject.activeSelf,Is.False);
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
            {
                active++; Assert.That(line.positionCount,Is.InRange(2,24));
                if(weapon==WeaponKind.LaserSword)
                {
                    Assert.That(line.widthCurve.Evaluate(0)+line.widthCurve.Evaluate(1),Is.LessThan(.01f),"The slash tapers to points.");
                    Assert.That(line.widthCurve.Evaluate(.5f),Is.EqualTo(1).Within(.01f),"Full width mid-swing.");
                }
            }
            if(weapon==WeaponKind.Grenade) Assert.That(active,Is.Zero,"Thrown grenades use their projectile/fuse, not a gun muzzle flash.");
            else Assert.That(active,Is.InRange(1,weapon==WeaponKind.LaserShotgun?16:6));
            // Lasers draw a dark sheath under the glow so they read against bright stone and sky.
            bool laser=WeaponCatalog.IsHitscan(weapon);
            int sheaths=0;
            foreach(var line in root.GetComponentsInChildren<LineRenderer>(true))
                if(line.name.StartsWith("Burst ") && line.enabled && line.sharedMaterial.name=="Weapon beam sheath")
                { sheaths++; Assert.That(line.sharedMaterial.renderQueue,Is.LessThan(line.sharedMaterial.shader.renderQueue)); }
            Assert.That(sheaths,Is.EqualTo(laser?WeaponCatalog.HitscanPellets(weapon):0));
        }

        [Test] public void ShotgunFanMatchesSimulationDirectionsAndEachPelletUsesItsOwnHit()
        {
            var action=new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 };
            var shot=new CombatEvent(CombatEventKind.Shot,action,0,position:Vector3.up*100,vector:Vector3.forward,amount:WeaponCatalog.SpreadDegrees(WeaponKind.LaserShotgun,0,0),weapon:WeaponKind.LaserShotgun);
            var hits=new CombatEvent[WeaponCatalog.HitscanPellets(shot.Weapon)];
            for(int pellet=0;pellet<hits.Length;pellet++)
                hits[pellet]=new CombatEvent(CombatEventKind.Hit,action,0,position:shot.Position+WeaponCatalog.ShotDirection(shot,pellet)*(3+pellet),subIndex:pellet,weapon:shot.Weapon);
            effects.Play(shot,match,false,Vector3.zero,hits);
            for(int pellet=0;pellet<hits.Length;pellet++)
            {
                var line=GripAttachment.Find(root.transform,"Burst "+pellet).GetComponent<LineRenderer>();
                Assert.That(line.positionCount,Is.EqualTo(2));
                Assert.That(Vector3.Distance(line.GetPosition(1),hits[pellet].Position),Is.LessThan(.0001f));
                Assert.That(Vector3.Dot((line.GetPosition(1)-line.GetPosition(0)).normalized,WeaponCatalog.ShotDirection(shot,pellet)),Is.GreaterThan(.99999f));
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

        // A beam keeps a readable width far from the viewer and stays slim beside the lens; striking a wall sparks.
        [Test] public void BeamWidensAwayFromTheViewerAndSparksOnTheWallItStrikes()
        {
            var viewer=new GameObject("Viewer",typeof(Camera)) { tag="MainCamera" };
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Vector3 eye=Vector3.up*100; viewer.transform.position=eye;
                wall.layer=0; wall.transform.position=eye+Vector3.forward*20; wall.transform.localScale=new Vector3(4,4,.5f);
                Physics.SyncTransforms();
                var sparks=GripAttachment.Find(root.transform,"Vfx Sparks").GetComponent<ParticleSystem>();
                int before=sparks.particleCount;
                var shot=new CombatEvent(CombatEventKind.Shot,new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 },0,position:eye,vector:Vector3.forward,weapon:WeaponKind.LineDriver);
                effects.Play(shot,match,false,Vector3.zero,null,eye+new Vector3(.2f,-.2f,.5f));
                var glow=GripAttachment.Find(root.transform,"Burst 0").GetComponent<LineRenderer>();
                Assert.That(glow.GetPosition(1).z,Is.EqualTo(eye.z+19.75f).Within(.01f),"Beam ends on the wall face.");
                Assert.That(glow.widthCurve.Evaluate(1),Is.GreaterThan(glow.widthCurve.Evaluate(0)*4));
                int wallSparks=sparks.particleCount-before;
                effects.Play(new CombatEvent(CombatEventKind.Shot,new CombatAction { Participant=1,LifeGeneration=1,Sequence=2 },0,position:eye,vector:Vector3.back,weapon:WeaponKind.LineDriver),match,false,Vector3.zero);
                int airSparks=sparks.particleCount-before-wallSparks;
                Assert.That(wallSparks,Is.GreaterThan(airSparks),"Only the shot that ends on a wall adds strike sparks to the muzzle sparks.");
                var burn=GripAttachment.Find(root.transform,"Scorch "+ArenaVfx.ScorchCapacity);
                Assert.That(burn.gameObject.activeSelf,Is.True,"The wall keeps a burn where the beam stopped.");
                Assert.That(burn.position.z,Is.EqualTo(eye.z+19.75f).Within(.01f));
                Assert.That(Vector3.Angle(burn.up,Vector3.back),Is.LessThan(1),"The burn projects along the wall normal.");
                Assert.That(GripAttachment.Find(root.transform,"Scorch "+(ArenaVfx.ScorchCapacity+1)).gameObject.activeSelf,Is.False,"A shot into open air leaves no burn.");
            }
            finally { Object.DestroyImmediate(wall); Object.DestroyImmediate(viewer); }
        }

        // Laser burns cycle their own ring and expire sooner, so sustained fire never erases a blast scorch.
        [Test] public void LaserBurnsAreBoundedAndNeverEvictBlastScorches()
        {
            var vfx=root.GetComponentInChildren<ArenaVfx>();
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                ground.transform.position=new Vector3(0,-50.5f,0); ground.transform.localScale=new Vector3(10,1,10);
                Physics.SyncTransforms();
                vfx.Scorch(new Vector3(0,-50,0),1);
                for(int i=0;i<ArenaVfx.BurnCapacity*3;i++) vfx.Burn(new Vector3(i,2,5),Vector3.back,.1f);
                int Active(int from,int count) { int n=0; for(int i=from;i<from+count;i++) if(GripAttachment.Find(root.transform,"Scorch "+i).gameObject.activeSelf) n++; return n; }
                Assert.That(Active(0,ArenaVfx.ScorchCapacity),Is.EqualTo(1),"The blast scorch survives.");
                Assert.That(Active(ArenaVfx.ScorchCapacity,ArenaVfx.BurnCapacity),Is.EqualTo(ArenaVfx.BurnCapacity));
                vfx.Animate(Time.unscaledTime+ArenaVfx.BurnSeconds+.1f);
                Assert.That(Active(ArenaVfx.ScorchCapacity,ArenaVfx.BurnCapacity),Is.Zero,"Burns cool off first.");
                Assert.That(Active(0,ArenaVfx.ScorchCapacity),Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(ground); }
        }

        [Test] public void RingStartsAtItsAnimationOriginInsteadOfPoppingToASmallerRadius()
        {
            Vector3 center=Vector3.up*100;
            effects.Play(new CombatEvent(CombatEventKind.Impulse,default,0,position:center),match,false,Vector3.zero);
            var line=GripAttachment.Find(root.transform,"Burst 0").GetComponent<LineRenderer>();
            Assert.That(Vector3.Distance(line.GetPosition(0),center),Is.EqualTo(.8f*.35f).Within(.0001f));
            effects.AnimateTransient(Time.unscaledTime);
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
