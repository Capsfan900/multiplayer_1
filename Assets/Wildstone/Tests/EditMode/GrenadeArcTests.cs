using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    // D029 hold-to-aim: the preview is the simulated flight, so it must end exactly where the fuse detonates.
    public sealed class GrenadeArcTests
    {
        sealed class Floor : EmptyCombatWorld
        {
            public override bool Sweep(Vector3 origin,Vector3 displacement,float radius,int owner,int ignoredMask,out CombatHit hit)
            {
                hit=default;
                float height=origin.y-radius;
                if(displacement.y>=0 || height+displacement.y>0) return false;
                float t=Mathf.Clamp01(height/-displacement.y);
                hit=new CombatHit(0,origin+displacement*t,Vector3.up,displacement.magnitude*t);
                return true;
            }
        }
        static ArenaMatch Match()
        {
            var map=new ArenaDefinition { Id="arc", Revision=1, Minimum=new Vector3(-100,-10,-100), Maximum=new Vector3(100,100,100), KillHeight=-20,
                ZoneRoute=new[]{Vector3.zero,new Vector3(10,0,0),new Vector3(10,0,10)},
                Pickups=new[]{new ArenaPickup { Item="shield", Position=Vector3.zero }},
                Spawns=new[]{new ArenaSpawn{Team=0},new ArenaSpawn{Team=1,Position=new Vector3(0,0,40)}} };
            var match=new ArenaMatch(map,60); match.AddPlayer(1,0,1,1,Vector3.zero); match.AddPlayer(2,1,1,1,new Vector3(0,0,40));
            Assert.That(match.Begin(),Is.True); return match;
        }
        [TestCase(false)] [TestCase(true)]
        public void PreviewEndsWhereTheSimulatedGrenadeDetonates(bool floor)
        {
            ICombatWorld world=floor?new Floor():new EmptyCombatWorld();
            var m=Match(); m.Players[0].GrenadeAmmo=1;
            var direction=new Vector3(.2f,.35f,1).normalized;
            var path=new Vector3[ArenaEffects.GrenadePathCapacity];
            int count=ArenaEffects.PredictGrenade(world,1,m.Players[0].Eye,direction,m.TickRate,path);
            Assert.That(count,Is.GreaterThan(2));
            var throwAction=new CombatAction { Participant=1,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1,Sequence=1,Tick=1,
                Kind=CombatActionKind.Grenade,Direction=direction,PickupGeneration=1 };
            Vector3? blast=null;
            for(int tick=1;tick<400 && blast==null;tick++)
            {
                m.Step(tick,tick==1?new[]{throwAction}:new CombatAction[0],world);
                foreach(var e in m.Events) if(e.Kind==CombatEventKind.Explosion) blast=e.Position;
            }
            Assert.That(blast.HasValue,Is.True,"The thrown grenade must detonate.");
            Assert.That(Vector3.Distance(blast.Value,path[count-1]),Is.LessThan(.0005f),"Preview end "+path[count-1]+" vs blast "+blast.Value);
            if(floor)
            {
                bool bounced=false;
                for(int i=1;i<count;i++) if(path[i].y>=path[i-1].y && path[i-1].y<.2f) bounced=true;
                Assert.That(bounced,Is.True,"A floor-landing preview shows the bounce the simulation applies.");
                for(int i=0;i<count;i++) Assert.That(path[i].y,Is.GreaterThan(-.01f));
            }
        }
        [Test] public void PreviewRejectsMissingInputsAndRespectsCapacity()
        {
            Assert.That(ArenaEffects.PredictGrenade(null,1,Vector3.zero,Vector3.forward,60,new Vector3[8]),Is.EqualTo(0));
            Assert.That(ArenaEffects.PredictGrenade(new EmptyCombatWorld(),1,Vector3.zero,Vector3.forward,0,new Vector3[8]),Is.EqualTo(0));
            Assert.That(ArenaEffects.PredictGrenade(new EmptyCombatWorld(),1,Vector3.zero,Vector3.forward,60,new Vector3[8]),Is.EqualTo(8));
        }
    }
}
