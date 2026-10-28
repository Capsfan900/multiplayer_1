using System.Collections.Generic;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Match;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    [RequireComponent(typeof(PracticeSession))]
    public sealed class ArenaPresentation : MonoBehaviour
    {
        PracticeSession session;
        PresentationCatalog catalog;
        readonly Dictionary<int,PlayerVisual> players=new Dictionary<int,PlayerVisual>();
        readonly Dictionary<int,Vector3> lastFeet=new Dictionary<int,Vector3>();
        PresentationEventHistory history=new PresentationEventHistory();
        ArenaEffects effects;
        ArenaAudio audio;
        WeaponView view;
        void Awake()
        {
            session=GetComponent<PracticeSession>();
            catalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            if(catalog==null) throw new System.InvalidOperationException("Wildstone presentation catalog is missing.");
        }
        void OnEnable()
        {
            if(session==null) session=GetComponent<PracticeSession>();
            session.SessionStarted+=StartPresentation;
            session.SessionStopped+=StopPresentation;
            session.Stepped+=OnStep;
            session.SettingsChanged+=OnSettingsChanged;
        }
        void OnDisable()
        {
            session.SessionStarted-=StartPresentation;
            session.SessionStopped-=StopPresentation;
            session.Stepped-=OnStep;
            session.SettingsChanged-=OnSettingsChanged;
            StopPresentation();
        }
        void StartPresentation()
        {
            StopPresentation();
            history=new PresentationEventHistory();
            lastFeet.Clear();
            for(int i=0;i<session.Match.Players.Count;i++)
            {
                var player=session.Match.Players[i];
                lastFeet[player.Participant]=player.Feet;
                if(player.Participant==PracticeSession.LocalParticipant) continue;
                var go=new GameObject("Player visual "+player.Participant,typeof(PlayerVisual));
                go.transform.SetParent(transform,false);
                var visual=go.GetComponent<PlayerVisual>();
                visual.Initialize(catalog,player.Team);
                players.Add(player.Participant,visual);
            }
            effects=new GameObject("Arena effects",typeof(ArenaEffects)).GetComponent<ArenaEffects>();
            effects.transform.SetParent(transform,false);
            effects.Initialize(catalog,session.Match);
            audio=new GameObject("Arena audio",typeof(ArenaAudio)).GetComponent<ArenaAudio>();
            audio.transform.SetParent(transform,false);
            audio.Initialize();
            view=gameObject.AddComponent<WeaponView>();
            view.Initialize(session.Camera,catalog);
            PresentState();
        }
        void OnSettingsChanged() { view?.SyncProjection(session.Camera); }
        void StopPresentation()
        {
            if(view!=null) { view.Teardown(session!=null?session.Camera:null); Destroy(view); view=null; }
            if(effects!=null) { Destroy(effects.gameObject); effects=null; }
            if(audio!=null) { Destroy(audio.gameObject); audio=null; }
            foreach(var player in players.Values) if(player!=null) Destroy(player.gameObject);
            players.Clear();
            lastFeet.Clear();
        }
        void OnStep()
        {
            var match=session.Match;
            if(match==null || effects==null) return;
            for(int i=0;i<match.Events.Count;i++)
            {
                var e=match.Events[i];
                if(!history.Accept(e)) continue;
                lastFeet.TryGetValue(e.Action.Participant,out var departure);
                effects.Play(e,match,session.Settings.ReducedMotion,departure);
                var target=match.Find(e.Target);
                var position=(e.Kind==CombatEventKind.Stunned || e.Kind==CombatEventKind.ShieldBroken || e.Kind==CombatEventKind.ShieldActivated) && target!=null?target.Feet:e.Position;
                audio.Play(e,position,session.Settings.EffectsVolume);
            }
            for(int i=0;i<match.Players.Count;i++)
            {
                var player=match.Players[i];
                lastFeet[player.Participant]=player.Feet;
            }
            PresentState();
        }
        void LateUpdate()
        {
            if(session.Match!=null && effects!=null) effects.Present(session.Match,session.Settings.ReducedMotion);
        }
        void PresentState()
        {
            var match=session.Match;
            if(match==null) return;
            foreach(var pair in players)
            {
                var player=match.Find(pair.Key);
                if(player!=null && session.Motors.TryGetValue(pair.Key,out ArenaMotor motor))
                {
                    MotorState state=motor.CaptureState();
                    pair.Value.Present(player,state,catalog);
                }
            }
            view?.Present(session.LocalPlayer,catalog);
        }
    }
}
