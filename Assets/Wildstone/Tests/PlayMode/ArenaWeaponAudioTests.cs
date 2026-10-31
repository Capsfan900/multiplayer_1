using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class ArenaWeaponAudioTests
    {
        GameObject root;
        ArenaAudio audio;
        [SetUp] public void SetUp() { root=new GameObject("Audio fixture",typeof(ArenaAudio)); audio=root.GetComponent<ArenaAudio>(); audio.Initialize(); }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); }
        AudioSource[] Voices(string prefix) => root.GetComponentsInChildren<AudioSource>().Where(v=>v.name.StartsWith(prefix)).ToArray();
        bool From(string bank,AudioSource voice) => voice.clip!=null && System.Array.IndexOf(audio.Bank(bank),voice.clip)>=0;

        [Test] public void EveryWeaponFiresARecordedCueOfItsOwnOnFixedVoices()
        {
            var cues=Voices("Cue voice"); Assert.That(cues.Length,Is.EqualTo(16)); Assert.That(Voices("Movement voice").Length,Is.EqualTo(12));
            var identities=new HashSet<string>();
            for(int i=1;i<=(int)WeaponKind.LaserSMG;i++)
            {
                var weapon=(WeaponKind)i; var cue=ArenaAudio.ShotCue(weapon);
                Assert.That(audio.Bank(cue.Bank),Is.Not.Empty,weapon+" bank "+cue.Bank);
                Assert.That(identities.Add(cue.Bank+"@"+cue.Pitch),Is.True,weapon+" shares another weapon's sound");
                foreach(var voice in cues) voice.clip=null;
                audio.Play(new CombatEvent(CombatEventKind.Shot,new CombatAction { Participant=1 },10,weapon:weapon),Vector3.zero,.5f);
                var played=cues.Where(v=>From(cue.Bank,v)).ToArray();
                Assert.That(played.Length,Is.EqualTo(1),weapon.ToString());
                Assert.That(played[0].pitch,Is.EqualTo(cue.Pitch).Within(cue.Pitch*.031f));
                Assert.That(played[0].volume,Is.EqualTo(Mathf.Clamp01(.5f*cue.Gain)).Within(.0001f));
                // Heavy weapons keep a synthesized body under the recording.
                Assert.That(cues.Count(v=>v.clip!=null && v.clip.name.StartsWith("Wildstone shot")),Is.EqualTo(cue.Body>=0?1:0),weapon+" body layer");
                // The sniper and shotgun carry a recorded boom tail under the shot.
                if(cue.Layer==null) continue;
                var tail=cues.Where(v=>From(cue.Layer,v)).ToArray();
                Assert.That(tail.Length,Is.EqualTo(1),weapon+" layer");
                Assert.That(tail[0].pitch,Is.EqualTo(cue.LayerPitch).Within(cue.LayerPitch*.031f));
                Assert.That(tail[0].volume,Is.EqualTo(.5f*cue.LayerGain).Within(.0001f));
            }
            // Guns that share a recording sit at least 20% apart in pitch, so each reads as its own by ear.
            var guns=new[]{WeaponKind.LineDriver,WeaponKind.LaserSMG,WeaponKind.LaserPistol,WeaponKind.LaserShotgun,WeaponKind.LaserSniper,WeaponKind.PulseLobber,WeaponKind.StunGun};
            foreach(var a in guns) foreach(var b in guns)
            {
                var x=ArenaAudio.ShotCue(a); var y=ArenaAudio.ShotCue(b);
                if(a<b && x.Bank==y.Bank) Assert.That(Mathf.Max(x.Pitch,y.Pitch)/Mathf.Min(x.Pitch,y.Pitch),Is.GreaterThanOrEqualTo(1.2f),a+" and "+b+" sound alike");
            }
            Assert.That(guns.Select(g=>ArenaAudio.ShotCue(g).Bank).Distinct().Count(),Is.GreaterThanOrEqualTo(3),"The guns draw on several recordings.");
            for(int i=0;i<50;i++) audio.Play(new CombatEvent(CombatEventKind.Shot,default,10,weapon:WeaponKind.LaserShotgun),Vector3.zero,.5f);
            Assert.That(root.GetComponentsInChildren<AudioSource>().Length,Is.EqualTo(30));
            foreach(var kind in new[]{CombatEventKind.Hit,CombatEventKind.Explosion,CombatEventKind.Death,CombatEventKind.Pickup,CombatEventKind.ShieldBroken})
            {
                Assert.That(ArenaAudio.TryCue(new CombatEvent(kind,default,10),out var cue),Is.True,kind.ToString());
                Assert.That(audio.Bank(cue.Bank),Is.Not.Empty,kind+" bank "+cue.Bank);
            }
            var before=cues.Select(v=>v.clip).ToArray();
            audio.Play(new CombatEvent(CombatEventKind.Rejected,default,10),Vector3.zero,.5f);
            audio.Play(new CombatEvent(CombatEventKind.Shot,default,10,weapon:WeaponKind.LineDriver),Vector3.zero,0);
            Assert.That(cues.Select(v=>v.clip).ToArray(),Is.EqualTo(before),"Rejected events and muted effects play nothing.");
        }

        // One looping wind voice: silent at a run, rising through dash speed, capped, and muted with the effects volume.
        [Test] public void SpeedWindRisesWithSpeedOnOneLoopingVoice()
        {
            var wind=Voices("Speed wind"); Assert.That(wind.Length,Is.EqualTo(1)); Assert.That(wind[0].loop,Is.True);
            Assert.That(audio.Bank("SpeedWind"),Has.Member(wind[0].clip)); Assert.That(wind[0].spatialBlend,Is.Zero);
            audio.PresentWind(new Vector3(11,-8,0),1); Assert.That(wind[0].volume,Is.Zero,"A run is silent, and a fall alone is not speed.");
            audio.PresentWind(new Vector3(0,0,19),1); float dash=wind[0].volume;
            Assert.That(dash,Is.GreaterThan(.1f).And.LessThan(ArenaAudio.WindVolume));
            audio.PresentWind(new Vector3(30,0,0),.5f); Assert.That(wind[0].volume,Is.EqualTo(ArenaAudio.WindVolume*.5f).Within(.0001f));
            Assert.That(wind[0].pitch,Is.EqualTo(1.1f).Within(.0001f));
            audio.PresentWind(new Vector3(30,0,0),0); Assert.That(wind[0].volume,Is.Zero);
        }
        // The slide scrape rides the carried slide speed in gain and pitch, releases over 0.12 s, cuts in menus, and loops
        // without a seam step.
        [Test] public void SlideScrapeRidesSlideSpeedAndReleases()
        {
            var scrape=Voices("Slide scrape"); Assert.That(scrape.Length,Is.EqualTo(1)); Assert.That(scrape[0].loop,Is.True);
            audio.PresentScrape(false,Vector3.forward*20,1,.01f); Assert.That(scrape[0].volume,Is.Zero);
            audio.PresentScrape(true,Vector3.forward*22,1,.01f);
            Assert.That(scrape[0].volume,Is.EqualTo(ArenaAudio.ScrapeVolume).Within(.0001f)); Assert.That(scrape[0].pitch,Is.EqualTo(1.32f).Within(.0001f));
            audio.PresentScrape(true,Vector3.forward*8,1,.01f);
            Assert.That(scrape[0].pitch,Is.EqualTo(.78f).Within(.0001f));
            Assert.That(scrape[0].volume,Is.LessThan(ArenaAudio.ScrapeVolume).And.GreaterThan(ArenaAudio.ScrapeVolume*.4f),"Falling gain releases, not drops.");
            audio.PresentScrape(false,Vector3.zero,1,.06f); Assert.That(scrape[0].volume,Is.GreaterThan(0),"Still releasing.");
            audio.PresentScrape(false,Vector3.zero,1,.12f); Assert.That(scrape[0].volume,Is.Zero);
            audio.PresentScrape(true,Vector3.forward*22,1,.01f); audio.PresentScrape(true,Vector3.forward*22,0,0);
            Assert.That(scrape[0].volume,Is.Zero,"A menu or death cuts it at once.");
            var data=new float[scrape[0].clip.samples]; scrape[0].clip.GetData(data,0); float step=0;
            for(int i=1;i<data.Length;i++) step=Mathf.Max(step,Mathf.Abs(data[i]-data[i-1]));
            Assert.That(Mathf.Abs(data[0]-data[data.Length-1]),Is.LessThanOrEqualTo(step),"The loop point is no louder than any step.");
        }
        // A shotgun reports a Hit per pellet: the target's cue plays once per shot and target, not once per pellet.
        [Test] public void PelletHitsOfOneShotPlayOneCue()
        {
            var cues=Voices("Cue voice"); var action=new CombatAction { Participant=2,Sequence=4 };
            CombatEvent Hit(CombatAction a,int tick,int target,int pellet) =>
                new CombatEvent(CombatEventKind.Hit,a,tick,target:target,subIndex:pellet,weapon:WeaponKind.LaserShotgun);
            for(int pellet=0;pellet<8;pellet++) audio.Play(Hit(action,10,1,pellet),Vector3.zero,1);
            Assert.That(cues.Count(v=>From("Block",v)),Is.EqualTo(1));
            audio.Play(Hit(action,10,3,2),Vector3.zero,1);
            Assert.That(cues.Count(v=>From("Block",v)),Is.EqualTo(2),"A second target is its own cue.");
            audio.Play(Hit(action,10,1,3),Vector3.zero,1); audio.Play(Hit(action,10,3,4),Vector3.zero,1);
            Assert.That(cues.Count(v=>From("Block",v)),Is.EqualTo(2),"Pellets alternating between two targets cue each once.");
            audio.Play(Hit(new CombatAction { Participant=2,Sequence=5 },20,1,0),Vector3.zero,1);
            Assert.That(cues.Count(v=>From("Block",v)),Is.EqualTo(3),"The next shot is its own cue.");
        }
        // Other players' slides scrape from where they slide, on a voice made at their first slide; death or leaving
        // (StopScrape) and menus cut it, and the local player keeps the centred scrape.
        [Test] public void RemoteSlidesScrapeFromTheSlider()
        {
            var state=new MotorState { Grounded=true, Velocity=Vector3.forward*20, Position=new Vector3(3,0,4) };
            audio.PresentMovement(2,state,0,0,1,false,.01f);
            Assert.That(Voices("Remote slide scrape"),Is.Empty,"No voice before a slide.");
            state.Sliding=true; audio.PresentMovement(2,state,0,0,1,false,.01f);
            var voice=Voices("Remote slide scrape 2"); Assert.That(voice.Length,Is.EqualTo(1));
            Assert.That(voice[0].loop,Is.True); Assert.That(voice[0].spatialBlend,Is.GreaterThan(.5f));
            Assert.That(Vector3.Distance(voice[0].transform.position,state.Position),Is.LessThan(.001f));
            Assert.That(voice[0].volume,Is.EqualTo(ArenaAudio.ScrapeVolume*(.4f+.6f*ArenaAudio.SlideFraction(20))).Within(.0001f));
            audio.StopScrape(2); Assert.That(voice[0].volume,Is.Zero,"Death or leaving cuts it.");
            audio.PresentMovement(2,state,0,0,1,false,.01f); audio.PresentMovement(2,state,0,0,1,false,0);
            Assert.That(voice[0].volume,Is.Zero,"A menu cuts it.");
            audio.PresentMovement(3,state,0,0,1,true,.01f);
            Assert.That(Voices("Remote slide scrape").Length,Is.EqualTo(1),"The local player has no positional scrape.");
        }
        // An enemy beam zips by only when it passes near the ear inside its drawn segment: not when it misses wide, starts
        // behind the listener, stops at cover first or ends at (hits) the listener.
        [Test] public void NearMissZipsOnlyWhenTheBeamPassesTheEar()
        {
            var cues=Voices("Cue voice"); var action=new CombatAction { Participant=2,Sequence=1 };
            var shot=new CombatEvent(CombatEventKind.Shot,action,10,position:new Vector3(0,1,-20),vector:Vector3.forward,weapon:WeaponKind.LineDriver);
            var ear=new Vector3(.8f,1,0);
            Assert.That(audio.PlayNearMiss(shot,null,ear,1),Is.True);
            var zip=cues.Where(v=>From("Swing",v)).ToArray(); Assert.That(zip.Length,Is.EqualTo(1));
            Assert.That(Vector3.Distance(zip[0].transform.position,new Vector3(0,1,0)),Is.LessThan(.01f),"It comes from where the beam passed.");
            Assert.That(zip[0].volume,Is.EqualTo(.5f*(1-.8f/ArenaAudio.NearMiss)).Within(.001f));
            Assert.That(audio.PlayNearMiss(shot,null,new Vector3(3,1,0),1),Is.False,"Wide.");
            Assert.That(audio.PlayNearMiss(shot,null,new Vector3(.5f,1,-25),1),Is.False,"Behind the shooter.");
            Assert.That(audio.PlayNearMiss(shot,null,ear,0),Is.False,"Muted.");
            var hit=new CombatEvent(CombatEventKind.Hit,action,10,target:1,position:new Vector3(0,1,-.4f));
            Assert.That(audio.PlayNearMiss(shot,new List<CombatEvent>{hit},new Vector3(0,1.3f,0),1),Is.False,"The beam ends at the listener it hit.");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=new Vector3(0,1,-5); wall.transform.localScale=new Vector3(4,4,.5f);
            try { Physics.SyncTransforms(); Assert.That(audio.PlayNearMiss(shot,null,ear,1),Is.False,"Cover stopped it."); }
            finally { Object.DestroyImmediate(wall); }
        }
        [Test] public void FootstepsFollowDistanceAndMovementEdgesPlayOnce()
        {
            var voices=Voices("Movement voice");
            var state=new MotorState { Grounded=true, Position=Vector3.zero };
            const float dt=1f/60;
            audio.PresentMovement(2,state,0,0,.8f,false,dt);
            state.Velocity=Vector3.forward*9;
            for(int frame=0;frame<120;frame++) { state.Position+=state.Velocity*dt; audio.PresentMovement(2,state,0,frame,.8f,false,dt); }
            int expected=Mathf.FloorToInt(18/ArenaAudio.StepSpacing(9));
            Assert.That(voices.Count(v=>From("Footstep",v)),Is.EqualTo(expected),"One footstep per stride at running speed.");
            Assert.That(voices.Where(v=>v.clip!=null).All(v=>v.spatialBlend>.5f),Is.True,"Other players' steps are positional.");

            foreach(var voice in voices) voice.clip=null;
            state.Grounded=false; state.Velocity=new Vector3(0,5,9); audio.PresentMovement(2,state,0,200,.8f,false,dt);
            for(int frame=0;frame<20;frame++) { state.Position+=state.Velocity*dt; audio.PresentMovement(2,state,0,201,.8f,false,dt); }
            state.Velocity=new Vector3(0,-12,9); audio.PresentMovement(2,state,0,202,.8f,false,dt);
            state.Grounded=true; audio.PresentMovement(2,state,0,203,.8f,false,dt);
            state.Sliding=true; audio.PresentMovement(2,state,0,204,.8f,false,dt);
            audio.PresentMovement(2,state,0,205,.8f,false,dt);
            audio.PresentMovement(2,state,400,206,.8f,false,dt);
            audio.PresentMovement(2,state,400,207,.8f,false,dt);
            Assert.That(voices.Count(v=>From("Jump",v)),Is.EqualTo(1),"Jump");
            Assert.That(voices.Count(v=>From("Land",v)),Is.EqualTo(1),"Land");
            Assert.That(voices.Count(v=>From("Dash",v) && v.pitch<.7f),Is.EqualTo(1),"Slide entry");
            Assert.That(voices.Count(v=>From("LaserReload",v)),Is.EqualTo(1),"Reload start");
            Assert.That(voices.Count(v=>From("Footstep",v)),Is.Zero,"No steps in the air or while sliding.");

            // A respawn or teleport is not a stride, and the local player's own steps are centred.
            foreach(var voice in voices) voice.clip=null;
            state=new MotorState { Grounded=true, Position=Vector3.right*40, Velocity=Vector3.forward*9 };
            audio.PresentMovement(2,state,0,300,.8f,false,dt);
            Assert.That(voices.All(v=>v.clip==null),Is.True,"Teleport plays nothing.");
            var local=new MotorState { Grounded=true };
            audio.PresentMovement(1,local,0,0,.8f,true,dt);
            local.Velocity=Vector3.forward*9;
            for(int frame=0;frame<30;frame++) { local.Position+=local.Velocity*dt; audio.PresentMovement(1,local,0,frame,.8f,true,dt); }
            var own=voices.Where(v=>From("Footstep",v)).ToArray();
            Assert.That(own.Length,Is.GreaterThan(0)); Assert.That(own.All(v=>v.spatialBlend==0),Is.True);
            // Fixed voices plus one scrape loop for the one remote participant that slid.
            Assert.That(Voices("Remote slide scrape").Length,Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<AudioSource>().Length,Is.EqualTo(31));
        }
    }
}
