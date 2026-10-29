using NUnit.Framework;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    [TestFixture,Category("Native")]
    public sealed class ArenaWeaponAudioTests
    {
        [Test] public void TenWeaponsHaveDistinctBoundedOriginalCuesAndReuseSixteenVoices()
        {
            var root=new GameObject("Audio fixture",typeof(ArenaAudio));
            try
            {
                var audio=root.GetComponent<ArenaAudio>(); audio.Initialize();
                var sources=root.GetComponentsInChildren<AudioSource>();
                Assert.That(sources.Length,Is.EqualTo(16));
                var clips=new System.Collections.Generic.HashSet<AudioClip>();
                double previousFingerprint=0;
                for(int i=1;i<=10;i++)
                {
                    var shot=new CombatEvent(CombatEventKind.Shot,new CombatAction { Participant=1 },10,weapon:(WeaponKind)i);
                    audio.Play(shot,Vector3.zero,.5f);
                    var clip=sources[i-1].clip;
                    Assert.That(clips.Add(clip),Is.True);
                    Assert.That(clip.length,Is.InRange(.1f,.49f));
                    var samples=new float[clip.samples]; Assert.That(clip.GetData(samples,0),Is.True);
                    float peak=0; double energy=0,fingerprint=0;
                    for(int j=0;j<samples.Length;j++)
                    {
                        peak=Mathf.Max(peak,Mathf.Abs(samples[j])); energy+=samples[j]*samples[j];
                        if(j<128) fingerprint+=samples[j]*(j+1);
                    }
                    Assert.That(peak,Is.InRange(.84f,.86f));
                    Assert.That(energy/samples.Length,Is.GreaterThan(.005));
                    if(i>1) Assert.That(System.Math.Abs(fingerprint-previousFingerprint),Is.GreaterThan(.001));
                    previousFingerprint=fingerprint;
                }
                for(int i=0;i<50;i++) audio.Play(new CombatEvent(CombatEventKind.Shot,default,10,weapon:WeaponKind.LineDriver),Vector3.zero,.5f);
                Assert.That(root.GetComponentsInChildren<AudioSource>().Length,Is.EqualTo(16));
                var before=sources[0].clip;
                audio.Play(new CombatEvent(CombatEventKind.Rejected,default,10),Vector3.zero,.5f);
                Assert.That(sources[0].clip,Is.SameAs(before));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
