using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.Presentation;
using Wildstone.UI;

namespace Wildstone.Game
{
    public sealed class PracticeSession : MonoBehaviour
    {
        public const int LocalParticipant=1, TargetParticipant=2;
        public int LocalParticipantId { get; private set; } = LocalParticipant;
        public bool ExternalSimulation { get; private set; }
        public static string MapScenePath(string id) => "Assets/Wildstone/Maps/"+id+".unity";
        public Camera Camera;
        public ArenaCatalog Catalog;
        public readonly MenuFlow Flow=new MenuFlow();
        public LocalSettings Settings { get; private set; }
        public ArenaMatch Match { get; private set; }
        public ArenaMotor Motor => motors.TryGetValue(LocalParticipantId,out var value)?value:null;
        public FirstPersonView View { get; private set; }
        public CombatState LocalPlayer => Match?.Find(LocalParticipantId);
        public IReadOnlyDictionary<int,ArenaMotor> Motors => motors;
        IReadOnlyDictionary<int,Transform> networkRenderPoses;
        IReadOnlyList<CombatEvent> networkEvents = Array.Empty<CombatEvent>();
        int networkCaptor = -1;
        bool networkContested;
        public IReadOnlyList<CombatEvent> CombatEvents => ExternalSimulation ? networkEvents : Match?.Events ?? Array.Empty<CombatEvent>();
        public int ZoneCaptor => ExternalSimulation ? networkCaptor : Match?.Captor ?? -1;
        public bool ZoneContested => ExternalSimulation ? networkContested : Match != null && Match.Contested;
        public Transform RenderPose(int participant) => ExternalSimulation && networkRenderPoses != null &&
            networkRenderPoses.TryGetValue(participant, out var pose) ? pose : null;
        public string MapId { get; private set; }="atrium";
        public string Failure { get; private set; }
        public float LoadProgress { get; private set; }
        public PracticeInput Input { get; private set; }
        // Presentation-only local ADS prediction (zoom, sensitivity); rules use CombatState.AdsProgress.
        public float LocalAdsProgress { get; private set; }
        public WeaponKind LocalWeapon => LocalPlayer!=null?CombatRules.CurrentWeapon(LocalPlayer):WeaponKind.None;
        // Hip vertical FOV derived from the horizontal setting and the current aspect.
        public float HipVerticalFov => LocalSettings.VerticalFov(Settings?.Fov??LocalSettings.DefaultFov,Camera!=null?Camera.aspect:16f/9);
        int adsDrops,adsLife;
        WeaponKind adsWeapon;
        FirstPersonView fovView;
        float fovAspect;
        public event Action SessionStarted, SessionStopped, Stepped, ScreenChanged, SettingsChanged, BackRequested;
        readonly Dictionary<int,ArenaMotor> motors=new Dictionary<int,ArenaMotor>();
        readonly Dictionary<int,MotorState> freshStates=new Dictionary<int,MotorState>();
        readonly List<CombatActionKind> pending=new List<CombatActionKind>();
        readonly List<CombatAction> commands=new List<CombatAction>();
        UnityCombatWorld world;
        Scene mapScene;
        Coroutine loader;
        string requestedMap;
        bool requestedPractice, focused=true;
        int requestGeneration, actionSequence, impulseSequence, movementSequence;
        double accumulator;
        Action simulateMovement;
        bool tickInputActive;
        int drillLeg,drillLegStart,drillLegEnd;
        Vector3 drillHome;
        void Awake()
        {
            simulateMovement=SimulateMovement;
            LightProbes.needsRetetrahedralization+=RefreshProbes;
        }
        void RefreshProbes()
        {
            // Transition-only work: probe data can arrive after the scene operation, including an empty unload.
            LightProbes.Tetrahedralize();
        }

        void Start()
        {
            if(Camera==null) Camera=UnityEngine.Camera.main;
            Catalog=Catalog??Resources.Load<ArenaCatalog>("ArenaCatalog");
            Settings=LocalSettings.Load(); ApplySettings(Settings,false);
            Load("atrium",false);
        }
        public void ApplySettings(LocalSettings value,bool save=true)
        {
            Settings=value.Copy(); Settings.Normalize(); Settings.Apply(Camera); if(save) Settings.Save();
            if(View!=null) { View.ReducedMotion=Settings.ReducedMotion; View.SetBaseFieldOfView(HipVerticalFov); fovView=View; fovAspect=Camera!=null?Camera.aspect:0; }
            var previous=Input; Input=new PracticeInput(Settings,previous); previous?.Dispose(); SettingsChanged?.Invoke();
        }
        public void Show(MenuScreen screen)
        {
            Flow.Show(screen); Input?.Clear(); SetCursor(); ScreenChanged?.Invoke();
        }
        void SetCursor() { bool capture=focused&&Flow.GameplayInput; Cursor.lockState=capture?CursorLockMode.Locked:CursorLockMode.None; Cursor.visible=!capture; }
        public void Load(string id,bool practice=true)
        {
            StopMatch(); requestedMap=id; requestedPractice=practice; requestGeneration=Flow.BeginLoad();
            LoadProgress=0; Failure=null; SetCursor(); ScreenChanged?.Invoke();
            if(loader==null) loader=StartCoroutine(LoadWorker());
        }
        public bool TryRetainNetworkMap(string id)
        {
            if(loader!=null || requestedMap!=null || Failure!=null || MapId!=id || Catalog==null || !mapScene.IsValid() || !mapScene.isLoaded) return false;
            ArenaDefinition expected;
            try { expected=Catalog.Find(id); }
            catch(ArgumentException) { return false; }
            foreach(var root in mapScene.GetRootGameObjects())
            {
                var map=root.GetComponentInChildren<ArenaMap>();
                if(map!=null && map.Definition!=null && map.Definition.Id==id && map.Definition.Revision==expected.Revision)
                    return SceneManager.SetActiveScene(mapScene);
            }
            return false;
        }
        public void CancelLoad() { Flow.CancelLoad(); requestedMap="atrium"; requestedPractice=false; requestGeneration=Flow.Generation; SetCursor(); ScreenChanged?.Invoke(); }
        public void Leave()
        {
            StopMatch(); SetBackdrop(); Show(MenuScreen.Title);
        }
        public void Rematch() => Load(MapId,true);
        public void AttachNetworkMatch(ArenaMatch match,int localParticipant,IReadOnlyDictionary<int,ArenaMotor> avatars)
        {
            if(match==null || match.Find(localParticipant)==null || !avatars.ContainsKey(localParticipant))
                throw new ArgumentException("An admitted local avatar and complete match are required.");
            StopMatch(); ExternalSimulation=true; LocalParticipantId=localParticipant; Match=match;
            RefreshNetworkAvatars(avatars, null);
            View=Motor.gameObject.AddComponent<FirstPersonView>();
            View.Motor=Motor; View.ViewCamera=Camera; View.OwnsLocalView=true; View.ReducedMotion=Settings.ReducedMotion;
            var state=Motor.CaptureState(); View.ResetView(state.Yaw,state.Pitch);
            Input.Clear(); SessionStarted?.Invoke(); Show(MenuScreen.Playing);
        }
        public void RefreshNetworkAvatars(IReadOnlyDictionary<int,ArenaMotor> avatars, IReadOnlyDictionary<int,Transform> renderPoses)
        {
            if(!ExternalSimulation) return;
            motors.Clear();
            foreach(var pair in avatars) if(pair.Value!=null) motors.Add(pair.Key,pair.Value);
            networkRenderPoses=renderPoses;
        }
        // D041: the coordinator raises the local hitscan press here before the host confirms it; presentation plays it once
        // and the host's Shot with the same action identity only corrects the beam. Hits, ammo and markers stay authoritative.
        public event Action<CombatEvent> ShotPredicted;
        public void PredictShot(in CombatEvent shot) { if(ExternalSimulation && Match!=null) ShotPredicted?.Invoke(shot); }
        public void PresentNetworkTick(IReadOnlyList<CombatEvent> events = null, int captor = -1, bool contested = false)
        {
            if(!ExternalSimulation || Match==null) return;
            networkEvents=events ?? Array.Empty<CombatEvent>(); networkCaptor=captor; networkContested=contested;
            Stepped?.Invoke();
        }
        public void ShowNetworkResults(MatchSnapshot snapshot,int localParticipant)
        {
            if(snapshot==null || snapshot.Practice || (snapshot.Phase!=ArenaMatchPhase.Aborted && snapshot.Phase!=ArenaMatchPhase.Finished))
                throw new ArgumentException("A completed online round is required.",nameof(snapshot));
            var summary=new ArenaMatch(Catalog.Find(snapshot.MapId),snapshot.TickRate);
            if(!summary.TryRestore(snapshot) || summary.Find(localParticipant)==null)
                throw new ArgumentException("Invalid online round summary.",nameof(snapshot));
            StopMatch();
            Match=summary; LocalParticipantId=localParticipant; ExternalSimulation=true; MapId=snapshot.MapId;
            Show(MenuScreen.Results);
        }
        public void DetachNetworkMatch() { if(ExternalSimulation) { StopMatch(); Show(MenuScreen.Online); } }
        IEnumerator LoadWorker()
        {
            // Unity scene loads cannot be cancelled; finish and unload stale results before the newest request.
            yield return null;
            while(requestedMap!=null)
            {
                string id=requestedMap; bool practice=requestedPractice; int generation=requestGeneration; requestedMap=null;
                if(mapScene.IsValid() && mapScene.isLoaded) { RestoreBootstrapScene(); var unload=SceneManager.UnloadSceneAsync(mapScene); if(unload!=null) yield return unload; }
                AsyncOperation load=null;
                try
                {
                    if(Catalog==null) throw new InvalidOperationException("Arena catalog is missing.");
                    Catalog.Find(id); load=SceneManager.LoadSceneAsync(MapScenePath(id),LoadSceneMode.Additive);
                    if(load==null) throw new InvalidOperationException("Map load could not start.");
                }
                catch(Exception ex) { Fail(generation,ex); }
                if(load==null) continue;
                float deadline=Time.realtimeSinceStartup+30;
                while(!load.isDone)
                {
                    LoadProgress=load.progress;
                    if(Time.realtimeSinceStartup>deadline && generation==Flow.Generation) { Fail(generation,new TimeoutException("Map loading timed out.")); Flow.InvalidateLoad(); }
                    yield return null;
                }
                mapScene=SceneManager.GetSceneByPath(MapScenePath(id));
                if(generation!=Flow.Generation)
                {
                    if(mapScene.IsValid() && mapScene.isLoaded) { RestoreBootstrapScene(); yield return SceneManager.UnloadSceneAsync(mapScene); }
                    continue;
                }
                try
                {
                    ArenaMap map=null;
                    foreach(var root in mapScene.GetRootGameObjects()) { map=root.GetComponentInChildren<ArenaMap>(); if(map!=null) break; }
                    if(map==null || map.Definition==null || map.Definition.Id!=id) throw new InvalidOperationException("Loaded map definition is missing or mismatched.");
                    if(!SceneManager.SetActiveScene(mapScene)) throw new InvalidOperationException("Loaded map could not become the active environment scene.");
                    MapId=id; SetBackdrop(map.Definition);
                    if(practice) Begin(map.Definition);
                    bool foreground=Flow.ForegroundLoading;
                    Flow.CompleteLoad(generation,practice);
                    if(foreground) { SetCursor(); ScreenChanged?.Invoke(); }
                }
                catch(Exception ex) { StopMatch(); Fail(generation,ex); }
            }
            loader=null;
        }
        void RestoreBootstrapScene()
        {
            var bootstrap=gameObject.scene;
            if(bootstrap.IsValid() && bootstrap.isLoaded && SceneManager.GetActiveScene()!=bootstrap && !SceneManager.SetActiveScene(bootstrap))
                throw new InvalidOperationException("Bootstrap could not become the active scene during map cleanup.");
        }
        void Fail(int generation,Exception ex)
        {
            Debug.LogException(ex,this);
            if(!Flow.FailLoad(generation)) return;
            Failure="This arena could not be opened. Try again or return to the menu."; SetCursor(); ScreenChanged?.Invoke();
        }
        void SetBackdrop(ArenaDefinition definition=null)
        {
            if(Camera==null) return;
            if(definition==null && Catalog!=null) definition=Catalog.Find(MapId);
            if(definition==null) return;
            var center=(definition.Minimum+definition.Maximum)*.5f; center.y=2;
            Camera.transform.SetPositionAndRotation(center+new Vector3(12,10,-16),Quaternion.Euler(23,-35,0));
        }
        void Begin(ArenaDefinition definition)
        {
            ExternalSimulation=false; LocalParticipantId=LocalParticipant;
            Match=new ArenaMatch(definition,120,true); world=new UnityCombatWorld(definition);
            for(int participant=1;participant<=2;participant++)
            {
                var spawn=Array.Find(definition.Spawns,s=>s.Team==participant-1);
                var avatar=new GameObject(participant==1?"Local practice avatar":"Strafing practice target (fixture)");
                avatar.transform.SetPositionAndRotation(spawn.Position,Quaternion.Euler(0,spawn.Yaw,0));
                var motor=avatar.AddComponent<ArenaMotor>(); motors.Add(participant,motor); freshStates.Add(participant,motor.CaptureState());
                Match.AddPlayer(participant,participant-1,1,1,spawn.Position);
                int owner=participant; world.Bind(owner,motor,()=>Match!=null&&Match.Find(owner).Alive);
                motor.Simulate(new MotorInput { Present=true,Tick=0,Sequence=0,Generation=1,Yaw=spawn.Yaw },MotorTuning.TickDelta,0);
                if(participant==TargetParticipant) { drillHome=spawn.Position; drillLeg=drillLegStart=drillLegEnd=0; }
                if(participant==1)
                {
                    View=avatar.AddComponent<FirstPersonView>(); View.Motor=motor; View.ViewCamera=Camera; View.OwnsLocalView=true; View.ReducedMotion=Settings.ReducedMotion; View.ResetView(spawn.Yaw,0);
                }
            }
            Match.Begin(); accumulator=0; actionSequence=impulseSequence=movementSequence=0; Input.Clear(); SessionStarted?.Invoke();
        }
        void StopMatch()
        {
            if(View!=null) View.ResetPresentation();
            if(Match!=null) SessionStopped?.Invoke();
            if(!ExternalSimulation)
                foreach(var motor in motors.Values) if(motor!=null) { motor.gameObject.SetActive(false); Destroy(motor.gameObject); }
            else if(View!=null) { View.OwnsLocalView=false; Destroy(View); }
            motors.Clear(); freshStates.Clear(); networkRenderPoses=null; networkEvents=Array.Empty<CombatEvent>();
            networkCaptor=-1; networkContested=false;
            View=null; Match=null; world=null; accumulator=0; Input?.Clear();
            ExternalSimulation=false; LocalParticipantId=LocalParticipant;
        }
        void Update()
        {
            if(Settings==null) return;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)
            {
                if(Flow.Screen==MenuScreen.Playing) Show(MenuScreen.Pause);
                else if(Flow.Screen==MenuScreen.Pause) Show(MenuScreen.Playing);
                else BackRequested?.Invoke();
            }
            bool active=focused&&Flow.GameplayInput;
            UpdateLocalAds(active);
            if(View!=null && Camera!=null && (View!=fovView || Camera.aspect!=fovAspect))
            { fovView=View; fovAspect=Camera.aspect; View.SetBaseFieldOfView(HipVerticalFov); }
            if(active && View!=null && Mouse.current!=null)
                View.SampleLook(Mouse.current.delta.ReadValue()*Settings.Sensitivity*Settings.LookScale(LocalWeapon,LocalAdsProgress,HipVerticalFov));
            Input.Sample(active);
            if(ExternalSimulation) return;
            if(Match==null || Match.Phase!=ArenaMatchPhase.Running) return;
            accumulator+=Math.Min(Time.unscaledDeltaTime,.25f);
            while(accumulator>=MotorTuning.TickDelta)
            {
                accumulator-=MotorTuning.TickDelta;
                try { Step(active); }
                catch(Exception ex) { Debug.LogException(ex,this); Failure="Practice stopped unexpectedly. Restart the arena to continue."; Show(MenuScreen.Failure); StopMatch(); break; }
                if(Match.Phase!=ArenaMatchPhase.Running) { Show(MenuScreen.Results); break; }
            }
        }
        void UpdateLocalAds(bool active)
        {
            var p=LocalPlayer;
            if(p==null || Input==null) { LocalAdsProgress=0; return; }
            var weapon=CombatRules.CurrentWeapon(p);
            if(weapon!=adsWeapon || p.LifeGeneration!=adsLife || Input.AdsDrops!=adsDrops)
            { LocalAdsProgress=0; adsWeapon=weapon; adsLife=p.LifeGeneration; adsDrops=Input.AdsDrops; }
            bool target=active && Input.Aiming && p.Alive && p.Connected && p.ReloadUntil==0 && WeaponCatalog.CanAim(weapon);
            LocalAdsProgress=CombatRules.AdvanceAds(LocalAdsProgress,target,weapon,Time.unscaledDeltaTime);
        }
        void Step(bool active)
        {
            int tick=Match.Tick+1; Input.ConsumeActions(pending,tick,Match.TickRate,LocalPlayer,active); commands.Clear();
            var player=LocalPlayer; Vector3 aim=Quaternion.Euler(View.Pitch,View.Yaw,0)*Vector3.forward;
            foreach(var kind in pending)
            {
                var command=new CombatAction { Participant=1,ConnectionGeneration=player.ConnectionGeneration,LifeGeneration=player.LifeGeneration,AuthorityGeneration=Match.AuthorityGeneration,Tick=tick,Sequence=++actionSequence,Kind=kind,Direction=aim };
                if(kind==CombatActionKind.Pickup)
                {
                    float nearest=WeaponCatalog.PickupReach*WeaponCatalog.PickupReach; command.PickupId=-1;
                    for(int i=0;i<Match.Pickups.Count;i++)
                    {
                        var item=Match.Pickups[i];
                        float distance=(item.Position-player.Feet).sqrMagnitude;
                        if(item.AvailableTick<=tick && distance<nearest) { nearest=distance; command.PickupId=item.Id; command.PickupGeneration=item.Generation; }
                    }
                }
                commands.Add(command);
            }
            tickInputActive=active;
            if(!Match.Step(tick,commands,world,true,simulateMovement)) throw new InvalidOperationException("Practice match tick was rejected.");
            for(int i=0;i<Match.Events.Count;i++)
            {
                var evt=Match.Events[i];
                if(!motors.TryGetValue(evt.Target,out var motor)) continue;
                if(evt.Kind==CombatEventKind.Impulse) motor.ApplyImpulse(evt.Vector,++impulseSequence,tick);
                if(evt.Kind==CombatEventKind.Death) motor.SetSuppressed(true);
            }
            Stepped?.Invoke();
        }
        void SimulateMovement()
        {
                int tick=Match.Tick;
                for(int i=0;i<Match.Events.Count;i++)
                {
                    var evt=Match.Events[i];
                    if(!motors.TryGetValue(evt.Target,out var motor)) continue;
                    if(evt.Kind==CombatEventKind.Respawn)
                    {
                        var state=freshStates[evt.Target]; state.Position=evt.Position; state.Generation=evt.TargetLife; state.Tick=tick-1;
                        motor.RestoreState(state); if(evt.Target==1) View.ResetView(state.Yaw,0);
                        if(evt.Target==TargetParticipant) drillHome=evt.Position;
                    }
                    else if(evt.Kind==CombatEventKind.Blink) motor.Teleport(evt.Position,View.Yaw);
                }
                foreach(var pair in motors)
                {
                    var p=Match.Find(pair.Key); pair.Value.SetSuppressed(p.Suppressed(tick)||!tickInputActive);
                    var input=pair.Key==1?Input.ConsumeMovement(tick,p.LifeGeneration,View.Yaw,View.Pitch):TargetInput(tick,p,pair.Value.CaptureState());
                    if(pair.Key==1) input.Sequence=++movementSequence;
                    bool aiming=pair.Key==1 && tickInputActive && input.Aiming;
                    if(!pair.Value.Simulate(input.ForMotor(),MotorTuning.TickDelta,tick)) throw new InvalidOperationException("Practice motor tick was rejected.");
                    var state=pair.Value.CaptureState();
                    Match.SetPose(pair.Key,p.LifeGeneration,state.Position,Quaternion.Euler(pair.Key==1?View.Pitch:0,pair.Key==1?View.Yaw:state.Yaw,0)*Vector3.forward,state.CapsuleHeight,aiming);
                }
                Physics.SyncTransforms();
        }
        MotorInput TargetInput(int tick,CombatState target,MotorState state)
        {
            if(tick>=drillLegEnd) { drillLeg++; drillLegStart=tick; drillLegEnd=tick+DrillLegTicks(drillLeg); }
            var input=Drill(drillLeg,tick-drillLegStart,state.Position,drillHome,LocalPlayer.Feet);
            input.Tick=input.Sequence=tick; input.Generation=target.LifeGeneration;
            return input;
        }
        // Practice target drill (a fixture, not a bot: it never attacks). It faces the player and strafes in uneven legs of
        // 0.5-1.3 s at 0.7 input, pausing every fourth, jumping into every third and dashing into every fifth, and is pulled back once
        // it strays 3 m from its spawn, so tracking, spread and head shots can be felt against movement.
        public static int DrillLegTicks(int leg) => 60+(int)((uint)leg*2654435761u>>24)*96/255;
        public static MotorInput Drill(int leg,int legTick,Vector3 position,Vector3 home,Vector3 player)
        {
            Vector3 look=player-position; look.y=0;
            float yaw=look.sqrMagnitude>.01f?Mathf.Atan2(look.x,look.z)*Mathf.Rad2Deg:0;
            var move=leg%4==3?Vector2.zero:new Vector2(leg%2==0?.7f:-.7f,0);
            Vector3 back=Quaternion.Euler(0,-yaw,0)*(home-position); back.y=0;
            if(back.magnitude>3) move+=new Vector2(back.x,back.z).normalized*Mathf.Clamp01((back.magnitude-3)/2)*1.5f;
            var held=legTick<30 && leg%3==1?MotorButtons.Jump:MotorButtons.None;
            var pressed=legTick==0?held|(leg%5==2?MotorButtons.Dash:MotorButtons.None):MotorButtons.None;
            return new MotorInput { Present=true,Move=Vector2.ClampMagnitude(move,1),Yaw=yaw,Held=held|pressed,Pressed=pressed };
        }
        void OnApplicationFocus(bool value)
        {
            focused=value; Input?.Clear(); if(!value) { Flow.LoseFocus(); ScreenChanged?.Invoke(); } SetCursor();
        }
        void OnDestroy()
        {
            LightProbes.needsRetetrahedralization-=RefreshProbes;
            StopMatch(); Input?.Dispose(); Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
    }
}
