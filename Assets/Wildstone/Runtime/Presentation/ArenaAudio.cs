using System.Collections.Generic;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    // Combat and movement cues. Recorded CC0 banks from vibegame1 (SOURCE-REUSE) carry the sound; the small synthesized
    // clips stay as low body layers and as the fallback when a bank is missing. Voice counts are fixed: nothing
    // creates sources per event, and movement uses its own pool so footsteps never steal a gunshot's voice.
    public sealed class ArenaAudio : MonoBehaviour
    {
        const int Voices=16, StepVoices=12;
        public const string Folder="Audio/Sfx/";
        readonly AudioSource[] sources=new AudioSource[Voices], steps=new AudioSource[StepVoices];
        readonly AudioClip[] clips=new AudioClip[18];
        readonly Dictionary<string,AudioClip[]> banks=new Dictionary<string,AudioClip[]>();
        readonly Dictionary<int,Stride> strides=new Dictionary<int,Stride>();
        int next,nextStep;

        // Gains come from each bank's measured loudest-50 ms level (QA 2026-10-30 audio pass). Body is a synthesized
        // layer index (-1 none) that keeps a heavy weapon's weight under the recording. Layer is a second recorded bank
        // (the nova's low boom tail under the heavy guns), set by K-weighted level below the main shot.
        public struct Cue { public string Bank; public float Pitch,Gain; public int Body; public float BodyGain; public string Layer; public float LayerPitch,LayerGain; }
        static Cue C(string bank,float pitch,float gain,int body=-1,float bodyGain=0,string layer=null,float layerPitch=1,float layerGain=0) =>
            new Cue { Bank=bank, Pitch=pitch, Gain=gain, Body=body, BodyGain=bodyGain, Layer=layer, LayerPitch=layerPitch, LayerGain=layerGain };
        static int ShotBody(WeaponKind weapon) => 6+(int)weapon;
        public static Cue ShotCue(WeaponKind weapon)
        {
            switch(weapon)
            {
                // The SMG and pistol use the turret's mid-heavy knock instead of the starter's sub-heavy zap, so the
                // three bullet guns are told apart by ear and the SMG's 12.5 shots/s do not pile up sub.
                case WeaponKind.LaserSMG: return C("TurretAnswer",1.3f,.64f);
                case WeaponKind.LaserPistol: return C("TurretAnswer",.9f,.8f);
                case WeaponKind.LaserShotgun: return C("LaserFire",.78f,.9f,ShotBody(weapon),.55f,"LaserCellNova",1.5f,.28f);
                case WeaponKind.LaserSniper: return C("LaserFire",.62f,1,ShotBody(weapon),.45f,"LaserCellNova",1,.44f);
                case WeaponKind.PulseLobber: return C("LaserFire",.5f,.7f,ShotBody(weapon),.6f);
                case WeaponKind.ReboundDisc: return C("Swing",1.15f,.5f);
                case WeaponKind.PushLens: return C("Dash",.72f,.6f,ShotBody(weapon),.4f);
                case WeaponKind.StunGun: return C("Stun",1,1);
                case WeaponKind.LaserSword: return C("Swing",.9f,.55f);
                case WeaponKind.Grenade: return C("Swing",.78f,.45f);
                default: return C("LaserFire",1,.83f); // Line Driver
            }
        }
        public static bool TryCue(in CombatEvent e,out Cue cue)
        {
            switch(e.Kind)
            {
                case CombatEventKind.Shot: cue=ShotCue(e.Weapon); return true;
                case CombatEventKind.Hit: cue=e.Weapon==WeaponKind.LaserSword?C("Hit",1,.9f):C("Block",1.15f,.45f); return true;
                case CombatEventKind.Ricochet: cue=C("Block",1.45f,.35f); return true;
                case CombatEventKind.Explosion: cue=C("SpellFireImpact",e.Weapon==WeaponKind.PulseLobber?1.15f:.9f,1,2,.8f); return true;
                case CombatEventKind.Stunned: cue=C("Stun",.8f,1); return true;
                case CombatEventKind.Blink: cue=C("Dash",1.25f,.55f); return true;
                case CombatEventKind.ShieldActivated: cue=C("DeflectorOpen",1,.95f); return true;
                case CombatEventKind.ShieldBroken: cue=C("DeflectorParry",.9f,1); return true;
                case CombatEventKind.Pickup: cue=C("DeflectorOpen",1.35f,.6f); return true;
                case CombatEventKind.Death: cue=C("DeflectorParry",.75f,1,6,.5f); return true; // derez: glass shatter over a falling tone
                case CombatEventKind.Deflected: cue=C("DeflectorParry",1.3f,.9f); return true; // Push Lens catches a projectile (D037)
                default: cue=default; return false;
            }
        }
        public void Initialize()
        {
            for(int i=0;i<Voices;i++) sources[i]=Voice("Cue voice "+i,.8f);
            for(int i=0;i<StepVoices;i++) steps[i]=Voice("Movement voice "+i,.9f);
            for(int i=0;i<clips.Length;i++) clips[i]=i<7?MakeCue(i):MakeShot((WeaponKind)(i-6));
            var air=Bank("SpeedWind");
            if(air.Length>0) { wind=Voice("Speed wind",0); wind.clip=air[0]; wind.loop=true; wind.volume=0; wind.Play(); }
            scrape=Voice("Slide scrape",0); scrape.clip=scrapeClip=MakeScrape(); scrape.loop=true; scrape.volume=0;
        }
        // Speed wind (vibegame1 CameraBeats): one looping centred voice, written every frame so it never starts or stops,
        // rising with the local player's horizontal speed from 9 to 20 m/s (D041 proposal, was 14 to 24): an 11 m/s run
        // is a whisper, a strafe jump audibly builds, dashes and slides reach the full loop.
        public const float WindVolume=.3f;
        AudioSource wind;
        public static float Speed01(float horizontalSpeed) { float t=Mathf.Clamp01((horizontalSpeed-9)/11); return t*t*(3-2*t); }
        public void PresentWind(Vector3 velocity,float volume)
        {
            if(wind==null) return;
            float speed=Speed01(new Vector2(velocity.x,velocity.z).magnitude);
            wind.volume=WindVolume*speed*Mathf.Clamp01(volume); wind.pitch=.9f+.2f*speed;
            if(!wind.isPlaying) wind.Play(); // a device change must not leave the loop dead
        }
        // Slide scrape (vibegame1 SlideFx law): a seamless band-limited noise loop whose gain and pitch ride the speed the
        // slide still carries, from the motor's slide end (8 m/s) to its maximum (22 m/s). Pitch is what the ear reads as
        // speed. Attack is immediate; a 0.12 s release lets the move finish instead of breaking off. Menus and death cut it.
        public const float ScrapeVolume=.22f, ScrapeRelease=.12f;
        AudioSource scrape;
        AudioClip scrapeClip;
        float scrapeGain;
        public static float SlideFraction(float speed) =>
            Mathf.Clamp01((speed-MotorTuning.Source.slideEndSpeed)/(MotorTuning.Source.slideMaxSpeed-MotorTuning.Source.slideEndSpeed));
        public void PresentScrape(bool sliding,Vector3 velocity,float volume,float dt)
        {
            if(scrape==null) return;
            float fraction=SlideFraction(new Vector2(velocity.x,velocity.z).magnitude);
            scrapeGain=ScrapeGain(scrapeGain,sliding,fraction,volume,dt);
            scrape.volume=scrapeGain; scrape.pitch=Mathf.Lerp(.78f,1.32f,fraction);
            if(scrapeGain>0 && !scrape.isPlaying) scrape.Play();
        }
        static float ScrapeGain(float current,bool sliding,float fraction,float volume,float dt)
        {
            float want=sliding?ScrapeVolume*(.4f+.6f*fraction)*Mathf.Clamp01(volume):0;
            return volume<=0 || dt<=0?0:want>current?want:Mathf.MoveTowards(current,want,ScrapeVolume/ScrapeRelease*dt);
        }
        // Other players' slides scrape by the same law from where they slide: one positional loop each, made on their
        // first slide. Death and leaving cut it (StopScrape), menus through a zero dt.
        readonly Dictionary<int,AudioSource> remoteScrapes=new Dictionary<int,AudioSource>();
        void PresentRemoteScrape(int participant,in MotorState state,float volume,float dt)
        {
            if(!remoteScrapes.TryGetValue(participant,out var voice))
            {
                if(!state.Sliding || scrapeClip==null) return;
                voice=remoteScrapes[participant]=Voice("Remote slide scrape "+participant,.9f);
                voice.clip=scrapeClip; voice.loop=true; voice.volume=0;
            }
            float fraction=SlideFraction(new Vector2(state.Velocity.x,state.Velocity.z).magnitude);
            voice.volume=ScrapeGain(voice.volume,state.Sliding,fraction,volume,dt); voice.pitch=Mathf.Lerp(.78f,1.32f,fraction);
            voice.transform.position=state.Position;
            if(voice.volume>0 && !voice.isPlaying) voice.Play();
        }
        public void StopScrape(int participant) { if(remoteScrapes.TryGetValue(participant,out var voice)) voice.volume=0; }
        // One second of one-pole low-passed noise (~2.4 kHz) with the rumble taken out (~50 Hz), its tail crossfaded into
        // its head so the loop point is inaudible. Fixed seed: every run gets the same texture.
        public static AudioClip MakeScrape()
        {
            const int rate=22050, length=rate, fade=2048;
            var raw=new float[length+fade]; var random=new System.Random(1207);
            float low=0,high=0,peak=.0001f;
            for(int i=0;i<raw.Length;i++)
            {
                low+=((float)(random.NextDouble()*2-1)-low)*.34f; high+=(low-high)*.015f;
                raw[i]=low-high; peak=Mathf.Max(peak,Mathf.Abs(raw[i]));
            }
            var data=new float[length];
            for(int i=0;i<length;i++) data[i]=(i<fade?raw[i]*i/fade+raw[length+i]*(1-i/(float)fade):raw[i])*.5f/peak;
            var clip=AudioClip.Create("Wildstone slide scrape",length,1,rate,false);
            clip.SetData(data,0); return clip;
        }
        AudioSource Voice(string name,float spatial)
        {
            var source=new GameObject(name,typeof(AudioSource)).GetComponent<AudioSource>();
            source.transform.SetParent(transform,false);
            source.playOnAwake=false; source.spatialBlend=spatial;
            source.rolloffMode=AudioRolloffMode.Linear; source.maxDistance=30;
            return source;
        }
        public AudioClip[] Bank(string name)
        {
            if(!banks.TryGetValue(name,out var bank)) banks[name]=bank=Resources.LoadAll<AudioClip>(Folder+name);
            return bank;
        }
        // A shotgun reports a Hit per pellet: one cue per shot and target, so pellets neither stack nor steal voices.
        int hitParticipant=-1,hitSequence=-1,hitTick=-1,hitTargets;
        public void Play(in CombatEvent e,Vector3 position,float volume)
        {
            if(e.Kind==CombatEventKind.Hit)
            {
                // Pellets may alternate between two neighbours (A, B, A...): remember every target this shot has cued.
                if(e.Action.Participant!=hitParticipant || e.Action.Sequence!=hitSequence || e.Tick!=hitTick)
                { hitParticipant=e.Action.Participant; hitSequence=e.Action.Sequence; hitTick=e.Tick; hitTargets=0; }
                int bit=1<<(e.Target&31);
                if((hitTargets&bit)!=0) return;
                hitTargets|=bit;
            }
            if(volume<=0 || !TryCue(e,out var cue)) return;
            var bank=Bank(cue.Bank);
            if(bank.Length>0)
            {
                Emit(sources[next++%Voices],bank[Random.Range(0,bank.Length)],position,cue.Pitch*Random.Range(.97f,1.03f),volume*cue.Gain,.8f);
                if(cue.Body>=0) Emit(sources[next++%Voices],clips[cue.Body],position,1,volume*cue.BodyGain*SynthGain,.8f);
                var layer=cue.Layer!=null?Bank(cue.Layer):null;
                if(layer?.Length>0) Emit(sources[next++%Voices],layer[Random.Range(0,layer.Length)],position,cue.LayerPitch*Random.Range(.97f,1.03f),volume*cue.LayerGain,.8f);
            }
            else Emit(sources[next++%Voices],clips[FallbackBody(e)],position,1,volume*SynthGain,.8f);
        }
        // Near miss: an enemy hitscan beam passing within 1.6 m of the listener's ear zips by from the point where it passed,
        // louder the closer it came. The beam ends where its effect does (ArenaEffects.ShotSegment), so cover that stopped it
        // and a hit on the listener (the beam ends at them) stay silent.
        public const float NearMiss=1.6f;
        public bool PlayNearMiss(in CombatEvent shot,IReadOnlyList<CombatEvent> events,Vector3 ear,float volume)
        {
            if(volume<=0 || !WeaponCatalog.IsHitscan(shot.Weapon)) return false;
            ArenaEffects.ShotSegment(shot,events,null,out var start,out var end);
            Vector3 segment=end-start;
            float along=Vector3.Dot(ear-start,segment)/Mathf.Max(segment.sqrMagnitude,1e-4f);
            Vector3 closest=start+segment*Mathf.Clamp01(along);
            float distance=Vector3.Distance(closest,ear);
            var bank=Bank("Swing");
            if(along<=0 || along>=1 || distance>=NearMiss || bank.Length==0) return false;
            Emit(sources[next++%Voices],bank[Random.Range(0,bank.Length)],closest,1.7f*Random.Range(.95f,1.05f),volume*.5f*(1-distance/NearMiss),.8f);
            return true;
        }
        // Level the synthesized set always played at (peak .85).
        const float SynthGain=.26f;
        static int FallbackBody(in CombatEvent e)
        {
            switch(e.Kind)
            {
                case CombatEventKind.Shot: return 7+Mathf.Clamp((int)e.Weapon-1,0,10);
                case CombatEventKind.Hit: case CombatEventKind.Ricochet: return 1;
                case CombatEventKind.Explosion: return 2;
                case CombatEventKind.Stunned: return 5;
                case CombatEventKind.Death: return 6;
                default: return 4;
            }
        }
        static void Emit(AudioSource source,AudioClip clip,Vector3 position,float pitch,float volume,float spatial)
        {
            source.Stop(); source.transform.position=position; source.spatialBlend=spatial;
            source.clip=clip; source.pitch=pitch; source.volume=Mathf.Clamp01(volume); source.Play();
        }

        // Movement and reload per participant, from presentation state only: footsteps by distance travelled on the
        // ground or a wall, jump/land/dash/slide edges and reload starts. Other players' steps are positional so they
        // can be heard; the local player's own are quieter and centred.
        struct Stride { public bool Known,Grounded,Sliding; public Vector3 Last; public float Distance,VerticalSpeed; public double Dash; public int Reload; }
        public const float StepGain=.53f, LocalStepScale=.5f;
        public static float StepSpacing(float speed) => Mathf.Lerp(1.5f,2.6f,Mathf.Clamp01(speed/11));
        public void PresentMovement(int participant,in MotorState state,int reloadUntil,int tick,float volume,bool local,float dt)
        {
            strides.TryGetValue(participant,out var s);
            Vector3 feet=state.Position;
            bool fresh=!s.Known || (feet-s.Last).sqrMagnitude>25;
            if(!fresh && dt>0 && volume>0)
            {
                float scale=local?LocalStepScale:1;
                if(state.Grounded && !s.Grounded && s.VerticalSpeed<-4)
                    Step("Land",feet,1,.24f*scale*Mathf.Lerp(.5f,1,Mathf.InverseLerp(4,16,-s.VerticalSpeed)),volume,local);
                else if(!state.Grounded && s.Grounded && state.Velocity.y>2) Step("Jump",feet,1,.85f*scale,volume,local);
                if(state.DashStartedAt>0 && state.DashStartedAt!=s.Dash) Step("Dash",feet,1,.56f*scale,volume,local);
                if(state.Sliding && !s.Sliding) Step("Dash",feet,.62f,.5f*scale,volume,local);
                if(reloadUntil>tick && reloadUntil!=s.Reload) Step("LaserReload",feet+Vector3.up*1.4f,1,.7f,volume,local);
                float speed=new Vector2(state.Velocity.x,state.Velocity.z).magnitude;
                if((state.Grounded && !state.Sliding || state.WallRunning) && speed>1.5f)
                {
                    s.Distance+=Vector3.Distance(feet,s.Last);
                    if(s.Distance>=StepSpacing(speed)) { s.Distance=0; Step("Footstep",feet,1,StepGain*scale*Mathf.Lerp(.6f,1,speed/11),volume,local); }
                }
                else s.Distance=0;
            }
            if(!local) PresentRemoteScrape(participant,state,volume,dt);
            s.Known=true; s.Last=feet; s.Grounded=state.Grounded; s.Sliding=state.Sliding; s.VerticalSpeed=state.Velocity.y;
            s.Dash=state.DashStartedAt; s.Reload=reloadUntil;
            strides[participant]=s;
        }
        void Step(string name,Vector3 position,float pitch,float gain,float volume,bool local)
        {
            var bank=Bank(name);
            if(bank.Length==0) return;
            Emit(steps[nextStep++%StepVoices],bank[Random.Range(0,bank.Length)],position,pitch*Random.Range(.95f,1.05f),volume*gain,local?0:.9f);
        }
        static AudioClip MakeShot(WeaponKind weapon)
        {
            const int rate=22050;
            float length=weapon==WeaponKind.LaserSMG?.11f:weapon==WeaponKind.LaserSniper?.48f:weapon==WeaponKind.LaserShotgun?.38f:weapon==WeaponKind.LaserPistol?.17f:weapon==WeaponKind.PulseLobber?.34f:weapon==WeaponKind.PushLens?.40f:weapon==WeaponKind.ReboundDisc?.25f:weapon==WeaponKind.LaserSword?.24f:weapon==WeaponKind.Grenade?.18f:.13f;
            var data=new float[Mathf.CeilToInt(rate*length)];
            uint noise=(uint)weapon*747796405u;
            float phase=0,secondPhase=0,peak=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate,x=t/length;
                noise=noise*1664525u+1013904223u;
                float random=((noise>>8)&65535)/32767.5f-1;
                float frequency,ratio,noiseMix;
                switch(weapon)
                {
                    case WeaponKind.LaserPistol: frequency=Mathf.Lerp(1400,250,x); ratio=1.75f; noiseMix=.10f; break;
                    case WeaponKind.LaserShotgun: frequency=Mathf.Lerp(160,45,x); ratio=2.3f; noiseMix=.50f; break;
                    case WeaponKind.LaserSniper: frequency=Mathf.Lerp(2400,80,Mathf.Sqrt(x)); ratio=1.07f; noiseMix=.22f; break;
                    case WeaponKind.LineDriver: frequency=Mathf.Lerp(1900,300,x); ratio=2.03f; noiseMix=.12f; break;
                    case WeaponKind.LaserSMG: frequency=Mathf.Lerp(2600,700,Mathf.Sqrt(x)); ratio=1.6f; noiseMix=.18f; break;
                    case WeaponKind.PulseLobber: frequency=Mathf.Lerp(190,55,x); ratio=1.5f; noiseMix=.07f; break;
                    case WeaponKind.ReboundDisc: frequency=Mathf.Lerp(680,290,x); ratio=1.414f; noiseMix=.04f; break;
                    case WeaponKind.PushLens: frequency=Mathf.Lerp(115,35,x); ratio=2; noiseMix=.32f; break;
                    case WeaponKind.StunGun: frequency=1100+Mathf.Sin(t*2*Mathf.PI*75)*450; ratio=2.7f; noiseMix=.12f; break;
                    case WeaponKind.LaserSword: frequency=Mathf.Lerp(700,120,x); ratio=1.25f; noiseMix=.45f; break;
                    default: frequency=Mathf.Lerp(300,80,x); ratio=1.8f; noiseMix=.25f; break;
                }
                phase+=2*Mathf.PI*frequency/rate;
                secondPhase+=2*Mathf.PI*frequency*ratio/rate;
                float attack=Mathf.Min(1,t/.003f),envelope=attack*(1-x)*(1-x);
                data[i]=((Mathf.Sin(phase)*.65f+Mathf.Sin(secondPhase)*.25f)*(1-noiseMix)+random*noiseMix)*envelope;
                peak=Mathf.Max(peak,Mathf.Abs(data[i]));
            }
            if(peak>0) for(int i=0;i<data.Length;i++) data[i]*=.85f/peak;
            var clip=AudioClip.Create("Wildstone shot "+weapon,data.Length,1,rate,false);
            clip.SetData(data,0); return clip;
        }
        // HUD confirmations for host-confirmed hits: a bright head "ding" or a two-note kill confirmation.
        public static AudioClip MakeConfirm(bool kill)
        {
            const int rate=22050;
            float length=kill?.26f:.16f;
            var data=new float[Mathf.CeilToInt(rate*length)];
            float peak=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate, x=i/(float)data.Length;
                float frequency=kill?(x<.45f?988:740):1760;
                float local=kill && x>=.45f?t-.45f*length:t;
                float envelope=Mathf.Min(1,t/.002f)*Mathf.Exp(-local*(kill?14:22));
                data[i]=(Mathf.Sin(2*Mathf.PI*frequency*t)*.7f+Mathf.Sin(2*Mathf.PI*frequency*1.5f*t)*.3f)*envelope;
                peak=Mathf.Max(peak,Mathf.Abs(data[i]));
            }
            if(peak>0) for(int i=0;i<data.Length;i++) data[i]*=.85f/peak;
            var clip=AudioClip.Create(kill?"Wildstone kill confirm":"Wildstone head confirm",data.Length,1,rate,false);
            clip.SetData(data,0); return clip;
        }
        // A body or shield hit's tick: 50 ms, short and dry beside the head ding, so connecting is heard at any range.
        public static AudioClip MakeTick()
        {
            const int rate=22050;
            var data=new float[Mathf.CeilToInt(rate*.05f)];
            uint noise=12345u; float peak=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate;
                noise=noise*1664525u+1013904223u;
                float random=((noise>>8)&65535)/32767.5f-1;
                data[i]=(Mathf.Sin(2*Mathf.PI*2600*t)*.8f+random*.2f)*Mathf.Min(1,t/.001f)*Mathf.Exp(-t*90);
                peak=Mathf.Max(peak,Mathf.Abs(data[i]));
            }
            for(int i=0;i<data.Length;i++) data[i]*=.85f/peak;
            var clip=AudioClip.Create("Wildstone hit tick",data.Length,1,rate,false);
            clip.SetData(data,0); return clip;
        }
        // A low magazine's dry click: 45 ms, hollow and an octave under the hit tick so the two never blur.
        public static AudioClip MakeDry()
        {
            const int rate=22050;
            var data=new float[Mathf.CeilToInt(rate*.045f)];
            float peak=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=i/(float)rate;
                data[i]=(Mathf.Sin(2*Mathf.PI*900*t)*.7f+Mathf.Sin(2*Mathf.PI*2340*t)*.3f)*Mathf.Min(1,t/.001f)*Mathf.Exp(-t*110);
                peak=Mathf.Max(peak,Mathf.Abs(data[i]));
            }
            for(int i=0;i<data.Length;i++) data[i]*=.85f/peak;
            var clip=AudioClip.Create("Wildstone dry click",data.Length,1,rate,false);
            clip.SetData(data,0); return clip;
        }
        static AudioClip MakeCue(int kind)
        {
            const int rate=22050;
            float length=kind==2?.32f:kind==6?.28f:.14f;
            int count=Mathf.CeilToInt(rate*length);
            var data=new float[count];
            uint noise=(uint)(kind+1)*747796405u;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate, x=i/(float)count;
                float frequency=kind==2?90+130*(1-x):kind==4?210+540*x:kind==5?700-260*x:kind==6?170-100*x:340+240*(1-x);
                noise=noise*1664525u+1013904223u;
                float random=((noise>>8)&65535)/32767.5f-1;
                float envelope=(1-x)*(1-x);
                data[i]=(Mathf.Sin(2*Mathf.PI*frequency*t)*.7f+random*(kind==2?.28f:.08f))*envelope;
            }
            var clip=AudioClip.Create("Wildstone cue "+kind,count,1,rate,false);
            clip.SetData(data,0); return clip;
        }
        void OnDestroy() { foreach(var clip in clips) if(clip!=null) Destroy(clip); if(scrapeClip!=null) Destroy(scrapeClip); }
    }
}
