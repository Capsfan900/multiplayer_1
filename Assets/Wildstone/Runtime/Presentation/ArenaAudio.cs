using UnityEngine;
using Wildstone.Combat;

namespace Wildstone.Presentation
{
    // Small original synthesized cues; voice count is fixed and never creates sources per event.
    public sealed class ArenaAudio : MonoBehaviour
    {
        const int Voices=16;
        readonly AudioSource[] sources=new AudioSource[Voices];
        readonly AudioClip[] clips=new AudioClip[7];
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
            for(int i=0;i<clips.Length;i++) clips[i]=MakeCue(i);
        }
        public void Play(in CombatEvent e,Vector3 position,float volume)
        {
            if(volume<=0) return;
            int cue;
            switch(e.Kind)
            {
                case CombatEventKind.Shot: cue=e.Weapon==WeaponKind.LaserSword?3:e.Weapon==WeaponKind.PushLens?4:0; break;
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
