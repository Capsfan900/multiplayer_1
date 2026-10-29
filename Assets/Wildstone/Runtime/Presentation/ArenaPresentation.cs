using System.Collections.Generic;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Match;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    [DefaultExecutionOrder(100),RequireComponent(typeof(PracticeSession))]
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
        FirstPersonBody body;
        int localDiscontinuity;
        readonly List<CombatEvent> localEffects=new List<CombatEvent>(256);
        readonly List<CombatEvent> remoteShots=new List<CombatEvent>(256);
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
            localEffects.Clear();
            remoteShots.Clear();
            lastFeet.Clear();
            effects=new GameObject("Arena effects",typeof(ArenaEffects)).GetComponent<ArenaEffects>();
            effects.transform.SetParent(transform,false);
            effects.Initialize(catalog,session.Match);
            audio=new GameObject("Arena audio",typeof(ArenaAudio)).GetComponent<ArenaAudio>();
            audio.transform.SetParent(transform,false);
            audio.Initialize();
            view=gameObject.AddComponent<WeaponView>();
            view.Initialize(session.Camera,catalog);
            body=new GameObject("Local first-person body",typeof(FirstPersonBody)).GetComponent<FirstPersonBody>();
            body.transform.SetParent(transform,false);
            body.Initialize(catalog,session.LocalPlayer.Team);
            localDiscontinuity=session.LocalPlayer.DiscontinuityTick;
            PresentState();
        }
        void OnSettingsChanged() { view?.SyncProjection(session.Camera); }
        void StopPresentation()
        {
            localEffects.Clear();
            remoteShots.Clear();
            if(view!=null) { view.Teardown(session!=null?session.Camera:null); Destroy(view); view=null; }
            if(body!=null) { Destroy(body.gameObject); body=null; }
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
            for(int i=0;i<session.CombatEvents.Count;i++)
            {
                var e=session.CombatEvents[i];
                if(!history.Accept(e)) continue;
                if(e.Kind==CombatEventKind.Shot && e.Action.Participant!=session.LocalParticipantId && remoteShots.Count<256)
                    remoteShots.Add(e);
                if(e.Kind!=CombatEventKind.Shot && session.LocalPlayer!=null && session.LocalPlayer.Alive && session.LocalPlayer.Connected)
                    view?.Play(e,session.LocalParticipantId);
                lastFeet.TryGetValue(e.Action.Participant,out var departure);
                bool localWeapon=e.Action.Participant==session.LocalParticipantId &&
                    (e.Kind==CombatEventKind.Shot || e.Kind==CombatEventKind.Hit || e.Kind==CombatEventKind.Explosion || e.Kind==CombatEventKind.Ricochet);
                if(localWeapon)
                {
                    // Bound deferred cosmetics even after a long frame; gameplay and audio already consumed the event.
                    if(localEffects.Count<256) localEffects.Add(e);
                }
                else effects.Play(e,match,session.Settings.ReducedMotion,departure,session.CombatEvents);
                var target=match.Find(e.Target);
                var position=(e.Kind==CombatEventKind.Stunned || e.Kind==CombatEventKind.ShieldBroken || e.Kind==CombatEventKind.ShieldActivated) && target!=null?target.Feet:e.Position;
                audio.Play(e,position,session.Settings.EffectsVolume);
            }
            for(int i=0;i<match.Players.Count;i++)
            {
                var player=match.Players[i];
                lastFeet[player.Participant]=player.Feet;
            }
        }
        void LateUpdate()
        {
            if(session.Match!=null && effects!=null)
            {
                PresentState();
                for(int i=0;i<remoteShots.Count;i++)
                    if(players.TryGetValue(remoteShots[i].Action.Participant,out var remote)) remote.Play(remoteShots[i]);
                remoteShots.Clear();
                if(session.LocalPlayer!=null && session.LocalPlayer.Alive && session.LocalPlayer.Connected)
                    for(int i=0;i<localEffects.Count;i++) if(localEffects[i].Kind==CombatEventKind.Shot) view?.Play(localEffects[i],session.LocalParticipantId);
                if(session.Motor!=null && session.LocalPlayer!=null)
                {
                    var motor=session.Motor.CaptureState();
                    if(localDiscontinuity!=session.LocalPlayer.DiscontinuityTick)
                    {
                        localDiscontinuity=session.LocalPlayer.DiscontinuityTick;
                        session.View?.ResetPresentation();
                    }
                    float dt=session.Flow.GameplayInput?Time.unscaledDeltaTime:0;
                    view?.SyncProjection(session.Camera);
                    view?.Present(session.LocalPlayer,catalog,motor,session.Match.Tick,dt,session.Settings.ReducedMotion);
                    body?.Present(session.LocalPlayer,motor,dt,session.Settings.ReducedMotion);
                }
                Vector3? muzzle=view!=null && view.TryGetMuzzle(out var point)?point:(Vector3?)null;
                for(int i=0;i<localEffects.Count;i++)
                    effects.Play(localEffects[i],session.Match,session.Settings.ReducedMotion,Vector3.zero,localEffects,
                        localEffects[i].Kind==CombatEventKind.Shot?muzzle:null);
                localEffects.Clear();
                effects.Present(session.Match,session.Settings.ReducedMotion,session.ZoneCaptor,session.ZoneContested);
            }
        }
        void PresentState()
        {
            var match=session.Match;
            if(match==null) return;
            for(int i=0;i<match.Players.Count;i++)
            {
                var player=match.Players[i];
                if(player.Participant==session.LocalParticipantId || players.ContainsKey(player.Participant)) continue;
                var go=new GameObject("Player visual "+player.Participant,typeof(PlayerVisual));
                go.transform.SetParent(transform,false); go.SetActive(false);
                var visual=go.GetComponent<PlayerVisual>(); visual.Initialize(catalog,player.Team);
                players.Add(player.Participant,visual);
            }
            foreach(var pair in players)
            {
                var player=match.Find(pair.Key);
                if(player!=null && session.Motors.TryGetValue(pair.Key,out ArenaMotor motor))
                {
                    MotorState state=motor.CaptureState();
                    pair.Value.Present(player,state,catalog,session.RenderPose(pair.Key));
                    pair.Value.PresentHandling(player,match.Tick,session.Flow.GameplayInput?Time.unscaledDeltaTime:0,
                        session.Settings.ReducedMotion,match.TickRate);
                }
                else if(pair.Value!=null) pair.Value.gameObject.SetActive(false);
            }
            view?.Present(session.LocalPlayer,catalog);
        }
    }
}
