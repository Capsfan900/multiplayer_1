using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;

namespace Wildstone.DevTools.Tests
{
    // The recorder is only useful if its file says exactly what the match did: every combat event once, movement events
    // on the ticks they happened, and state samples that match the simulation.
    public class SessionLogTests
    {
        [System.Serializable] class Probe { public string type, kind, reason, events; public int tick, actor; public float[] pos; public float t; }

        static ArenaMatch Match()
        {
            var map=new ArenaDefinition { Id="test", Revision=1, Minimum=new Vector3(-100,-10,-100), Maximum=new Vector3(100,100,100), KillHeight=-20,
                ZoneRoute=new[]{Vector3.zero,new Vector3(10,0,0),new Vector3(10,0,10)}, Pickups=new ArenaPickup[0],
                Spawns=new[]{new ArenaSpawn{Team=0},new ArenaSpawn{Team=1,Position=new Vector3(0,0,5)}} };
            var match=new ArenaMatch(map,120,true); match.AddPlayer(1,0,1,1,Vector3.zero); match.AddPlayer(2,1,1,1,new Vector3(0,0,5));
            Assert.That(match.Begin(),Is.True); return match;
        }

        [Test] public void RecordingMatchesWhatTheMatchDid()
        {
            var match=Match(); var text=new StringWriter(); var log=new SessionLog(text);
            var motors=new Dictionary<int,MotorState>();
            var expected=new List<CombatEvent>(); int moves=0, samples=0, sequence=0; Vector3 lastSampled=default;
            int first=match.Tick+1;
            for(int tick=first;tick<first+240;tick++)
            {
                // Fire every tick (cooldown refusals are real Rejected events) and reload twice.
                var kind=tick%100==0?CombatActionKind.Reload:CombatActionKind.Fire;
                var action=new CombatAction { Participant=1,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1,Sequence=++sequence,Tick=tick,Kind=kind,Direction=Vector3.forward };
                Assert.That(match.Step(tick,new[]{action},new EmptyCombatWorld()),Is.True);
                var local=new MotorState { Position=new Vector3(tick*.01f,0,0), Velocity=new Vector3(3,0,4), Tick=tick,
                    Events=tick%30==0?MotorEvents.Jump|MotorEvents.Dash:MotorEvents.None };
                motors[1]=local; motors[2]=new MotorState { Position=new Vector3(0,0,5) };
                if(local.Events!=MotorEvents.None) moves++;
                if(match.Tick%log.SampleEvery==0) { samples++; lastSampled=local.Position; }
                expected.AddRange(match.Events);
                log.Tick(tick/120f,match,match.Events,1,motors,-1,false,0);
            }
            var lines=text.ToString().Split('\n').Where(l=>l.Length>0).Select(l=>JsonUtility.FromJson<Probe>(l)).ToList();
            var combat=lines.Where(l=>l.type=="combat").ToList();

            Assert.That(expected.Count(e=>e.Kind==CombatEventKind.Shot),Is.GreaterThan(0),"The fixture must produce real shots.");
            Assert.That(expected.Count(e=>e.Kind==CombatEventKind.Rejected),Is.GreaterThan(0),"The fixture must produce real refusals.");
            Assert.That(combat.Select(c=>c.kind+"@"+c.tick),Is.EqualTo(expected.Select(e=>e.Kind+"@"+e.Tick)),"Every combat event exactly once, in order.");
            Assert.That(combat.Where(c=>c.kind=="Rejected").Select(c=>c.reason),Is.EqualTo(expected.Where(e=>e.Kind==CombatEventKind.Rejected).Select(e=>e.Reason)));
            Assert.That(lines.Count(l=>l.type=="move"),Is.EqualTo(moves));
            Assert.That(lines.Where(l=>l.type=="move").All(l=>l.events=="Jump, Dash"),Is.True);
            var sampled=lines.Where(l=>l.type=="sample").ToList();
            Assert.That(sampled.Count,Is.EqualTo(samples));
            Assert.That(sampled.Last().pos[0],Is.EqualTo(lastSampled.x).Within(1e-3f));
            Assert.That(sampled.All(s=>s.tick%log.SampleEvery==0),Is.True);
        }

        [Test] public void TextIsEscapedAndNonFiniteNumbersStayValidJson()
        {
            var text=new StringWriter(); var log=new SessionLog(text);
            log.Line("combat",1).Str("reason","quote \" slash \\ newline \n tab \t").Num("amount",float.NaN).Num("bloom",float.PositiveInfinity).End();
            var probe=JsonUtility.FromJson<Probe>(text.ToString());
            Assert.That(probe.reason,Is.EqualTo("quote \" slash \\ newline \n tab \t"));
            Assert.That(text.ToString(),Does.Contain("\"amount\":null,\"bloom\":null"));
            Assert.That(text.ToString().Trim().Split('\n').Length,Is.EqualTo(1),"One event per line even when text has newlines.");
        }
    }
}
