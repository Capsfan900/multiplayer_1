using UnityEngine;
using Wildstone.Combat;

namespace Wildstone.Presentation
{
    // Small original synthesized cues; voice count is fixed and never creates sources per event.
    public sealed class ArenaAudio : MonoBehaviour
    {
        const int Voices=16;
        readonly AudioSource[] sources=new AudioSource[Voices];
        readonly AudioClip[] clips=new AudioClip[17];
        int next;
        public void Initialize()
        {
            for(int i=0;i<Voices;i++)
            {
                var go=new GameObject("Cue voice "+i,typeof(AudioSource));
                go.transform.SetParent(transform,false);
                sources[i]=go.GetComponent<AudioSource>();
                sources[i].playOnAwake=false;
                sources[i].spatialBlend=.8f;
                sources[i].rolloffMode=AudioRolloffMode.Linear;
                sources[i].maxDistance=30;
            }
            for(int i=0;i<clips.Length;i++) clips[i]=i<7?MakeCue(i):MakeShot((WeaponKind)(i-6));
        }
        public void Play(in CombatEvent e,Vector3 position,float volume)
        {
            if(volume<=0) return;
            int cue;
            switch(e.Kind)
            {
                case CombatEventKind.Shot: cue=7+Mathf.Clamp((int)e.Weapon-1,0,9); break;
                case CombatEventKind.Hit:
                case CombatEventKind.Ricochet: cue=1; break;
                case CombatEventKind.Explosion: cue=2; break;
                case CombatEventKind.Stunned: cue=5; break;
                case CombatEventKind.Blink:
                case CombatEventKind.ShieldActivated:
                case CombatEventKind.ShieldBroken: cue=4; break;
                case CombatEventKind.Death: cue=6; break;
                default: return;
            }
            var source=sources[next++%Voices];
            source.Stop(); source.transform.position=position;
            source.clip=clips[cue]; source.volume=Mathf.Clamp01(volume)*.26f;
            source.Play();
        }
        static AudioClip MakeShot(WeaponKind weapon)
        {
            const int rate=22050;
            float length=weapon==WeaponKind.LaserSniper?.48f:weapon==WeaponKind.LaserShotgun?.38f:weapon==WeaponKind.LaserPistol?.17f:weapon==WeaponKind.PulseLobber?.34f:weapon==WeaponKind.PushLens?.40f:weapon==WeaponKind.ReboundDisc?.25f:weapon==WeaponKind.LaserSword?.24f:weapon==WeaponKind.Grenade?.18f:.13f;
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
        void OnDestroy() { foreach(var clip in clips) if(clip!=null) Destroy(clip); }
    }
}
