using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildstone.Presentation;

namespace Wildstone.Tests
{
    // Unity re-encodes the music on import; a loop only stays seamless if the clip keeps the source's exact length.
    [TestFixture,Category("Native")]
    public sealed class MusicImportTests
    {
        const string Folder="Assets/Wildstone/Resources/Audio/Music/";

        [TestCase(MusicMix.MenuTrack)] [TestCase("Level_atrium")] [TestCase("Level_split-gallery")] [TestCase("Level_terrace-steps")]
        public void LoopImportsAsAStreamedClipOfTheSourceLength(string track)
        {
            string path=Folder+track+".ogg";
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            Assert.That(clip,Is.Not.Null,path);
            Assert.That(clip.frequency,Is.EqualTo(44100)); Assert.That(clip.channels,Is.EqualTo(2));
            Assert.That(clip.samples,Is.EqualTo(OggSamples(path)),"Imported length must equal the authored loop length.");
            var settings=((AudioImporter)AssetImporter.GetAtPath(path)).defaultSampleSettings;
            Assert.That(settings.loadType,Is.EqualTo(AudioClipLoadType.Streaming));
            Assert.That(clip,Is.SameAs(Resources.Load<AudioClip>(ArenaMusic.Folder+track)));
        }

        // Sample count of a Vorbis stream = the granule position on its last Ogg page.
        static long OggSamples(string path)
        {
            byte[] data=File.ReadAllBytes(path);
            for(int i=data.Length-27;i>=0;i--)
                if(data[i]=='O' && data[i+1]=='g' && data[i+2]=='g' && data[i+3]=='S') return BitConverter.ToInt64(data,i+6);
            throw new InvalidDataException(path);
        }
    }
}
