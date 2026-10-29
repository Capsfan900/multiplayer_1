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
            if(View!=null) { View.ReducedMotion=Settings.ReducedMotion; View.SetBaseFieldOfView(Settings.Fov); }
            Input?.Dispose(); Input=new PracticeInput(Settings); SettingsChanged?.Invoke();
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
        public void PresentNetworkTick(IReadOnlyList<CombatEvent> events = null, int captor = -1, bool contested = false)
        {
            if(!ExternalSimulation || Match==null) return;
            networkEvents=events ?? Array.Empty<CombatEvent>(); networkCaptor=captor; networkContested=contested;
            Stepped?.Invoke();
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
                var avatar=new GameObject(participant==1?"Local practice avatar":"Stationary practice target (fixture)");
                avatar.transform.SetPositionAndRotation(spawn.Position,Quaternion.Euler(0,spawn.Yaw,0));
                var motor=avatar.AddComponent<ArenaMotor>(); motors.Add(participant,motor); freshStates.Add(participant,motor.CaptureState());
                Match.AddPlayer(participant,participant-1,1,1,spawn.Position);
                int owner=participant; world.Bind(owner,motor,()=>Match!=null&&Match.Find(owner).Alive);
                motor.Simulate(new MotorInput { Present=true,Tick=0,Sequence=0,Generation=1,Yaw=spawn.Yaw },MotorTuning.TickDelta,0);
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
            if(active && View!=null && Mouse.current!=null) View.SampleLook(Mouse.current.delta.ReadValue()*Settings.Sensitivity);
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
        void Step(bool active)
        {
            int tick=Match.Tick+1; Input.ConsumeActions(pending,tick,Match.TickRate,LocalPlayer,active); commands.Clear();
            var player=LocalPlayer; Vector3 aim=Quaternion.Euler(View.Pitch,View.Yaw,0)*Vector3.forward;
            foreach(var kind in pending)
            {
                var command=new CombatAction { Participant=1,ConnectionGeneration=player.ConnectionGeneration,LifeGeneration=player.LifeGeneration,AuthorityGeneration=Match.AuthorityGeneration,Tick=tick,Sequence=++actionSequence,Kind=kind,Direction=aim };
                if(kind==CombatActionKind.Pickup)
                {
                    float nearest=6.25f; command.PickupId=-1;
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
                    }
                    else if(evt.Kind==CombatEventKind.Blink) motor.Teleport(evt.Position,View.Yaw);
                }
                foreach(var pair in motors)
                {
                    var p=Match.Find(pair.Key); pair.Value.SetSuppressed(p.Suppressed(tick)||pair.Key!=1||!tickInputActive);
                    var input=pair.Key==1?Input.ConsumeMovement(tick,p.LifeGeneration,View.Yaw,View.Pitch):
                        new MotorInput { Present=true,Tick=tick,Sequence=tick,Generation=p.LifeGeneration,Yaw=pair.Value.CaptureState().Yaw };
                    if(pair.Key==1) input.Sequence=++movementSequence;
                    if(!pair.Value.Simulate(input,MotorTuning.TickDelta,tick)) throw new InvalidOperationException("Practice motor tick was rejected.");
                    var state=pair.Value.CaptureState();
                    Match.SetPose(pair.Key,p.LifeGeneration,state.Position,pair.Key==1?Quaternion.Euler(View.Pitch,View.Yaw,0)*Vector3.forward:p.Aim,state.CapsuleHeight);
                }
                Physics.SyncTransforms();
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
