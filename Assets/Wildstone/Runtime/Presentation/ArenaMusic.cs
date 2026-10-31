using System;
using System.Collections.Generic;
using UnityEngine;
using Wildstone.Game;

namespace Wildstone.Presentation
{
    // Two-voice track state, kept engine-free so managed tests can step it.
    public sealed class MusicMix
    {
        public const string MenuTrack="MenuTheme";
        public const float FadeSeconds=1.5f, DuckGain=.35f, DuckSeconds=.4f;
        public readonly string[] Tracks=new string[2];
        public readonly float[] Gains=new float[2];
        public int Active { get; private set; }
        public float Duck { get; private set; }=1;

        // Menus share one theme; an attached match plays its map's loop until Results.
        public static string TrackFor(MenuScreen screen,bool match,string mapId) =>
            match && screen!=MenuScreen.Results && !string.IsNullOrEmpty(mapId)?"Level_"+mapId:MenuTrack;
        // Pause and settings over a live match lower the level instead of switching tracks.
        public static bool Ducked(MenuScreen screen,bool match) => match && screen!=MenuScreen.Playing && screen!=MenuScreen.Results;

        // Returns the voice that must start the track from silence, or -1 when nothing restarts.
        // ponytail: a third change inside one fade cuts the oldest voice; add a third voice if that is audible.
        public int Select(string track)
        {
            if(track==Tracks[Active]) return -1;
            Active=1-Active;
            if(track==Tracks[Active]) return -1; // reversing mid-fade resumes the fading voice
            Tracks[Active]=track; Gains[Active]=0; return Active;
        }
        public void Step(float dt,bool ducked)
        {
            dt=float.IsNaN(dt) || dt<0?0:dt;
            for(int i=0;i<2;i++) Gains[i]=Mathf.MoveTowards(Gains[i],i==Active?1:0,dt/FadeSeconds);
            Duck=Mathf.MoveTowards(Duck,ducked?DuckGain:1,dt/DuckSeconds);
        }
        // Equal-power curve: the crossfade midpoint keeps loudness instead of dipping.
        public float Volume(int voice,float music) => Mathf.Clamp01(music)*Duck*Mathf.Sin(Gains[voice]*Mathf.PI*.5f);
    }

    // Looping menu/map music from Resources/Audio/Music. Missing clips leave that track silent; nothing waits on audio.
    [RequireComponent(typeof(PracticeSession))]
    public sealed class ArenaMusic : MonoBehaviour
    {
        public const string Folder="Audio/Music/";
        readonly AudioSource[] voices=new AudioSource[2];
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        public readonly MusicMix Mix=new MusicMix();
        public AudioSource ActiveVoice => voices[Mix.Active];
        PracticeSession session;
        MenuScreen lastScreen;
        bool lastMatch, primed;
        string lastMap;
        void Awake()
        {
            session=GetComponent<PracticeSession>();
            for(int i=0;i<voices.Length;i++)
            {
                var voice=new GameObject("Music voice "+i,typeof(AudioSource)).GetComponent<AudioSource>();
                voice.transform.SetParent(transform,false);
                voice.playOnAwake=false; voice.loop=true; voice.spatialBlend=0; voice.priority=0; voice.volume=0;
                voice.bypassReverbZones=true; voices[i]=voice;
            }
        }
        public AudioClip Clip(string track)
        {
            if(!clips.TryGetValue(track,out var clip)) clips[track]=clip=Resources.Load<AudioClip>(Folder+track);
            return clip;
        }
        // The Settings music slider is shown only when at least one track exists (UI spec).
        public bool HasMusic
        {
            get
            {
                if(Clip(MusicMix.MenuTrack)!=null) return true;
                if(session!=null && session.Catalog!=null)
                    foreach(var map in session.Catalog.Maps) if(Clip(MusicMix.TrackFor(MenuScreen.Playing,true,map.Id))!=null) return true;
                return false;
            }
        }
        void Update()
        {
            if(session==null || session.Settings==null) return;
            var screen=session.Flow.Screen; bool match=session.Match!=null; string map=session.MapId;
            if(!primed || screen!=lastScreen || match!=lastMatch || map!=lastMap)
            {
                primed=true; lastScreen=screen; lastMatch=match; lastMap=map;
                string track=MusicMix.TrackFor(screen,match,map);
                int start=Mix.Select(track);
                if(start>=0)
                {
                    var voice=voices[start]; voice.Stop(); voice.clip=Clip(track);
                    if(voice.clip!=null) voice.Play();
                }
            }
            Mix.Step(Time.unscaledDeltaTime,MusicMix.Ducked(screen,match));
            for(int i=0;i<voices.Length;i++)
            {
                voices[i].volume=Mix.Volume(i,session.Settings.MusicVolume);
                if(i!=Mix.Active && Mix.Gains[i]<=0 && voices[i].isPlaying) voices[i].Stop();
            }
        }
    }
}
