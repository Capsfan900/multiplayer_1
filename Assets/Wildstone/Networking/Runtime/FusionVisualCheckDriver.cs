#if WILDSTONE_SMOKE
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Wildstone.Game;
using Wildstone.Combat;
using Wildstone.Match;
using Wildstone.Maps;
using Wildstone.Movement;
using Wildstone.Presentation;
using Wildstone.UI;

namespace Wildstone.Networking
{
    // Diagnostic player only: capture actual rendering without desktop input or saved settings.
    [DefaultExecutionOrder(200)]
    public sealed class FusionVisualCheckDriver : MonoBehaviour
    {
        sealed class Step
        {
            public string Name;
            public Action Enter;
            public Func<bool> Ready;
            public Action BeforeCapture;
            public float SettleSeconds=1.5f;
        }
        [Serializable] sealed class Capture
        {
            public string file,screen,map,mode;
            public int width,height,screenWidth,screenHeight;
            public float cameraAspect;
            public float uiScale;
            public int simulatedFrame,simulationTick;
            public float simulatedSeconds,simulationDelta,cameraFov;
            public double wallSeconds;
            public bool sliding,reloading;
            public string stage;
            public Vector3 velocity,cameraPosition,handsPosition;
            public Quaternion handsRotation;
        }
        [Serializable] sealed class Result
        {
            public bool passed;
            public string error="",graphicsDevice="",mode="static-screen-captures";
            public List<Capture> captures=new List<Capture>();
        }
        readonly List<Step> steps=new List<Step>();
        readonly Result result=new Result();
        PracticeSession session;
        UIDocument document;
        RenderTexture target;
        readonly RenderPipeline.StandardRequest renderRequest=new RenderPipeline.StandardRequest();
        string directory,pending;
        int index;
        double began,entered,readyAt=-1;
        bool initialized,started,finished,motionCheck,weaponCheck,sequenceCheck,capturePrepared;
        int sequenceFrame=-1;
        const float SequenceDelta=1f/60;
        string sequenceStage;
        double preparedAt;
        FirstPersonBody fixtureBody;
        WeaponView fixtureWeapon;
        FirstPersonView fixtureView;
        PresentationCatalog fixtureCatalog;
        CombatState fixturePlayer;
        MotorState fixtureState;
        ArenaEffects fixtureEffects;
        ArenaMatch fixtureEffectMatch;
        readonly List<CombatEvent> fixtureEvents=new List<CombatEvent>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BeginRequested()
        {
            var args=Environment.GetCommandLineArgs(); int argument=Array.IndexOf(args,"--wildstone-visual-output");
            if(argument<0) return;
            try
            {
                if(argument+1>=args.Length || !Path.IsPathRooted(args[argument+1])) throw new ArgumentException("An absolute capture directory is required.");
                string output=Path.GetFullPath(args[argument+1]);
                string artifacts=Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."))+Path.DirectorySeparatorChar;
                if(!output.StartsWith(artifacts,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Captures must stay under this build's Artifacts directory.");
                Directory.CreateDirectory(output);
                var owner=new GameObject("Wildstone visual check"); DontDestroyOnLoad(owner);
                var driver=owner.AddComponent<FusionVisualCheckDriver>(); driver.directory=output; driver.began=Time.realtimeSinceStartupAsDouble;
                driver.motionCheck=Array.IndexOf(args,"--wildstone-motion-check")>=0;
                driver.weaponCheck=Array.IndexOf(args,"--wildstone-weapon-check")>=0;
                driver.sequenceCheck=Array.IndexOf(args,"--wildstone-sequence-check")>=0;
                if((driver.motionCheck?1:0)+(driver.weaponCheck?1:0)+(driver.sequenceCheck?1:0)>1) throw new ArgumentException("Choose one diagnostic mode.");
                if(driver.sequenceCheck) driver.result.mode="continuous-presentation-samples-not-gameplay-or-framerate-proof";
                if(driver.motionCheck) driver.result.mode="visual-pose-fixtures-not-gameplay-proof";
                if(driver.weaponCheck) driver.result.mode="weapon-effect-fixtures-not-gameplay-proof";
            }
            catch(Exception exception) { Debug.LogException(exception); Application.Quit(2); }
        }
        void Update()
        {
            if(finished || directory==null) return;
            try
            {
                double now=Time.realtimeSinceStartupAsDouble;
                if(now-began>180) throw new TimeoutException("Visual capture exceeded its bounded deadline.");
                if(!initialized)
                {
                    if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) throw new InvalidOperationException("Visual checks require graphics; omit -nographics.");
                    session=FindFirstObjectByType<PracticeSession>();
                    if(session==null || session.Settings==null || session.Flow.ForegroundLoading || session.MapId!="atrium") return;
                    document=session.GetComponent<UIDocument>();
                    session.SendMessage("OnApplicationFocus",false);
                    result.graphicsDevice=SystemInfo.graphicsDeviceType.ToString();
                    ConfigureSteps(); initialized=true;
                }
                UnityEngine.Cursor.lockState=CursorLockMode.None;
                if(index>=steps.Count) { Finish(true,""); return; }
                var step=steps[index];
                if(!started) { step.Enter(); entered=now; readyAt=-1; started=true; capturePrepared=false; }
                if(now-entered>35) throw new TimeoutException("Visual step did not complete: "+step.Name+
                    $"; screen={session.Flow.Screen}; loading={session.Flow.ForegroundLoading}; map={session.MapId}; match={session.Match!=null}"+
                    $"; size={Screen.width}x{Screen.height}; fixture={fixtureView!=null}; sessionEnabled={session.enabled}"+
                    $"; target={(target!=null?target.width+"x"+target.height:"none")}; aspect={session.Camera.aspect:F4}; capturePrepared={capturePrepared}; readyAt={readyAt:F3}; preparedAt={preparedAt:F3}; now={now:F3}; pending={pending??"none"}; simulatedFrame={sequenceFrame}");
                if(pending!=null)
                {
                    if(!File.Exists(pending) || new FileInfo(pending).Length<100) return;
                    var capture=new Capture {file=pending,screen=session.Flow.Screen.ToString(),map=session.MapId,
                        width=target.width,height=target.height,screenWidth=Screen.width,screenHeight=Screen.height,cameraAspect=session.Camera.aspect,uiScale=session.Settings.UiScale,mode=result.mode};
                    if(sequenceCheck) RecordSequence(capture,now);
                    result.captures.Add(capture);
                    File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(result,true));
                    pending=null; index++; started=false; return;
                }
                if(!step.Ready()) { readyAt=-1; return; }
                if(readyAt<0) readyAt=now;
                if(now-readyAt<step.SettleSeconds) return;
                if(step.BeforeCapture!=null)
                {
                    if(!capturePrepared) { step.BeforeCapture(); capturePrepared=true; preparedAt=now; return; }
                    if(now-preparedAt<.04) return;
                }
                pending=Path.Combine(directory,step.Name+".png");
                var previous=RenderTexture.active;
                var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                try
                {
                    RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
                    pixels.Apply(); File.WriteAllBytes(pending,pixels.EncodeToPNG());
                }
                finally { RenderTexture.active=previous; Destroy(pixels); }
            }
            catch(Exception exception) { Debug.LogException(exception); Finish(false,exception.GetType().Name+": "+exception.Message); }
        }
        void ConfigureSteps()
        {
            if(sequenceCheck) { ConfigureSequenceSteps(); return; }
            if(weaponCheck) { ConfigureWeaponSteps(); return; }
            if(motionCheck) { ConfigureMotionSteps(); return; }
            Add("title-720",()=>{ApplySize(1280,720,1);session.Show(MenuScreen.Title);},()=>At(MenuScreen.Title,1280,720));
            Add("settings-720-top",()=>session.Show(MenuScreen.Settings),()=>At(MenuScreen.Settings,1280,720));
            Add("settings-720-controls",()=>{var scroll=document.rootVisualElement.Q<ScrollView>();scroll.verticalScroller.value=scroll.verticalScroller.highValue;},()=>At(MenuScreen.Settings,1280,720));
            Add("online-720",()=>session.Show(MenuScreen.Online),()=>At(MenuScreen.Online,1280,720));
            Add("practice-720",()=>session.Show(MenuScreen.Practice),()=>At(MenuScreen.Practice,1280,720));
            foreach(string id in new[]{"atrium","split-gallery","terrace-steps"})
            {
                string map=id;
                Add(map+"-720",()=>{ApplySize(1280,720,1);session.Load(map);},()=>Playing(map,1280,720));
                Add(map+"-720-large-ui",()=>ApplySize(1280,720,1.4f),()=>Playing(map,1280,720));
                Add(map+"-1080",()=>ApplySize(1920,1080,1),()=>Playing(map,1920,1080));
            }
            Add("pause-1080",()=>session.Show(MenuScreen.Pause),()=>At(MenuScreen.Pause,1920,1080));
            Add("title-ultrawide",()=>{session.Leave();ApplySize(1920,810,1);},()=>At(MenuScreen.Title,1920,810));
        }
        void ConfigureSequenceSteps()
        {
            // These are fixed simulation samples, deliberately held while PNG readback completes.
            // Adjacent samples at transition boundaries preserve intervening presentation history.
            foreach(int frame in new[]{0,12,24,30,31,32,42,54,60,61,62,72,84,90,91,92,96,102,108,114,120,121,122,132,144,156,168,180,192,198,204,216})
            {
                int sample=frame;
                Add("sequence-"+sample.ToString("D3"),()=>
                {
                    if(fixtureView==null) { ApplySize(1280,720,1); session.Load("atrium"); }
                },()=>
                {
                    if(!Playing("atrium",1280,720)) return false;
                    if(fixtureView==null) { CreateFixture(); Pose(35,0,0,false,true,90); }
                    return true;
                });
                var step=steps[steps.Count-1]; step.SettleSeconds=0;
                step.BeforeCapture=()=>AdvanceSequence(sample);
            }
        }
        void AdvanceSequence(int sample)
        {
            for(int frame=sequenceFrame+1;frame<=sample;frame++)
            {
                sequenceStage=frame<30?"run":frame<60?"slide":frame<90?"slide-return":frame<120?"fire-and-settle":frame<120+Mathf.CeilToInt(WeaponCatalog.ReloadSeconds/SequenceDelta)?"reload":"reload-return";
                fixtureState.Sliding=frame>=30 && frame<60;
                fixtureState.CapsuleHeight=fixtureState.Sliding?.9f:1.8f;
                fixtureState.Velocity=frame<60?Vector3.forward*(fixtureState.Sliding?16:8):Vector3.zero;
                fixtureState.LastMove=frame<60?Vector2.up:Vector2.zero;
                int tick=100+Mathf.RoundToInt(frame*SequenceDelta*WeaponCatalog.DefaultTickRate);
                if(frame==90) fixtureWeapon.Play(new CombatEvent(CombatEventKind.Shot,
                    new CombatAction {Participant=fixturePlayer.Participant},tick),fixturePlayer.Participant);
                if(frame==120) fixturePlayer.ReloadUntil=tick+Mathf.RoundToInt(WeaponCatalog.ReloadSeconds*WeaponCatalog.DefaultTickRate);
                fixtureView.RenderFrame(fixtureState,SequenceDelta);
                fixtureBody.Present(fixturePlayer,fixtureState,SequenceDelta,false);
                fixtureWeapon.Present(fixturePlayer,fixtureCatalog,fixtureState,tick,SequenceDelta,false);
            }
            sequenceFrame=sample;
            fixtureWeapon.SyncProjection(session.Camera);
        }
        void RecordSequence(Capture capture,double now)
        {
            var overlay=session.Camera.transform.Find("Viewmodel");
            if(overlay==null || overlay.childCount==0) throw new InvalidOperationException("Missing sequence hands.");
            var hands=overlay.GetChild(0);
            capture.simulatedFrame=sequenceFrame; capture.simulationTick=100+Mathf.RoundToInt(sequenceFrame*SequenceDelta*WeaponCatalog.DefaultTickRate);
            capture.simulationDelta=SequenceDelta; capture.simulatedSeconds=(sequenceFrame+1)*SequenceDelta;
            capture.wallSeconds=now-began; capture.stage=sequenceStage;
            capture.sliding=fixtureState.Sliding; capture.reloading=fixturePlayer.ReloadUntil>capture.simulationTick;
            capture.velocity=fixtureState.Velocity; capture.cameraFov=session.Camera.fieldOfView;
            capture.cameraPosition=session.Camera.transform.position;
            capture.handsPosition=hands.localPosition; capture.handsRotation=hands.localRotation;
        }
        void ConfigureMotionSteps()
        {
            Add("fixture-standing-forward",()=>{ApplySize(1280,720,1);session.Load("atrium");},()=>
            {
                if(!Playing("atrium",1280,720)) return false;
                if(fixtureView==null) { CreateFixture(); Pose(0,0,0,false,true,90); }
                return true;
            });
            AddPose("fixture-standing-down",()=>Pose(65,0,0,false,true,90));
            AddPose("fixture-walk-phase-a",()=>Pose(55,8,.08f,false,true,90));
            AddPose("fixture-walk-phase-b",()=>Pose(55,8,.23f,false,true,90));
            AddPose("fixture-air-down",()=>Pose(55,6,.15f,false,false,90));
            AddPose("fixture-slide-entry",()=>Pose(0,16,.05f,true,true,90));
            AddPose("fixture-slide-sustain",()=>Pose(0,16,.3f,true,true,90));
            AddPose("fixture-slide-down",()=>Pose(55,16,.3f,true,true,90));
            AddPose("fixture-slide-fov60",()=>Pose(0,16,.3f,true,true,60));
            AddPose("fixture-slide-exit",()=>
            {
                Pose(45,16,.3f,true,true,90);
                fixtureState.Sliding=false; fixtureState.CapsuleHeight=1.8f; AdvanceFixture(.1f);
            });
            AddPose("fixture-fire",()=>
            {
                Pose(0,0,0,false,true,90);
                fixtureWeapon.Play(new CombatEvent(CombatEventKind.Shot,new CombatAction {Participant=fixturePlayer.Participant},100),fixturePlayer.Participant);
                AdvanceFixture(.025f);
            });
            AddPose("fixture-reload",()=>
            {
                Pose(0,0,0,false,true,90);
                fixturePlayer.ReloadUntil=100+Mathf.RoundToInt(WeaponCatalog.ReloadSeconds*WeaponCatalog.DefaultTickRate*.5f);
                AdvanceFixture(.05f);
            });
            AddPose("fixture-special-sword",()=>
            {
                Pose(0,0,0,false,true,90);
                fixturePlayer.SelectedSpecial=true; fixturePlayer.SpecialWeapon=WeaponKind.LaserSword;
                fixturePlayer.Pending=new PendingAttack {Weapon=WeaponKind.LaserSword,Action=new CombatAction {Tick=90},EndTick=110};
                AdvanceFixture(.05f);
            });
        }
        void AddPose(string name,Action pose) => Add(name,pose,()=>fixtureView!=null);
        void ConfigureWeaponSteps()
        {
            foreach(var weapon in new[]{WeaponKind.LineDriver,WeaponKind.PulseLobber,WeaponKind.ReboundDisc,WeaponKind.PushLens,WeaponKind.StunGun,WeaponKind.LaserSword,WeaponKind.Grenade,WeaponKind.LaserPistol,WeaponKind.LaserShotgun,WeaponKind.LaserSniper})
                for(int background=0;background<2;background++)
                {
                    var kind=weapon; int facing=background;
                    AddWeapon("weapon-"+kind.ToString().ToLowerInvariant()+"-"+facing,kind,facing,CombatEventKind.Shot);
                }
            AddWeapon("weapon-plasma-impact",WeaponKind.PulseLobber,0,CombatEventKind.Explosion);
            AddWeapon("weapon-disc-ricochet",WeaponKind.ReboundDisc,0,CombatEventKind.Ricochet);
            AddWeapon("weapon-stun-impact",WeaponKind.StunGun,0,CombatEventKind.Hit);
            AddWeapon("weapon-grenade-blast",WeaponKind.Grenade,0,CombatEventKind.Explosion);
            AddWeapon("weapon-lens-charge",WeaponKind.PushLens,0,CombatEventKind.Accepted);
            AddWeapon("weapon-overlapping-lasers",WeaponKind.LineDriver,0,CombatEventKind.Hit);
        }
        void AddWeapon(string name,WeaponKind kind,int facing,CombatEventKind effect)
        {
            Add(name,()=>
            {
                if(fixtureView==null) { ApplySize(1280,720,1); session.Load("atrium"); }
            },()=>
            {
                if(!Playing("atrium",1280,720)) return false;
                if(fixtureView==null) CreateFixture();
                return true;
            });
            steps[steps.Count-1].BeforeCapture=()=>WeaponFixture(kind,facing,effect);
        }
        void WeaponFixture(WeaponKind kind,int facing,CombatEventKind effect)
        {
            if(fixtureEffects!=null) DestroyImmediate(fixtureEffects.gameObject);
            fixtureState.Position=new Vector3(0,.02f,-12);
            fixtureState.Yaw=facing==0?0:180;
            Pose(0,0,0,false,true,90);
            fixturePlayer.SelectedSpecial=kind!=WeaponKind.LineDriver && kind!=WeaponKind.Grenade;
            fixturePlayer.SpecialWeapon=fixturePlayer.SelectedSpecial?kind:WeaponKind.None;
            session.LocalPlayer.SelectedSpecial=fixturePlayer.SelectedSpecial;
            session.LocalPlayer.SpecialWeapon=fixturePlayer.SpecialWeapon;
            session.LocalPlayer.SpecialAmmo=fixturePlayer.SelectedSpecial?WeaponCatalog.Get(kind).Ammo:0;
            AdvanceFixture(.01f);
            var definition=new ArenaDefinition { Id="atrium",Spawns=Array.Empty<ArenaSpawn>(),Pickups=Array.Empty<ArenaPickup>(),
                ZoneRoute=new[]{Vector3.zero,Vector3.forward*8,Vector3.right*8} };
            fixtureEffectMatch=new ArenaMatch(definition,practice:true);
            fixtureEffectMatch.AddPlayer(1,0,1,1,fixtureState.Position); fixtureEffectMatch.Begin();
            var shooter=fixtureEffectMatch.Find(1); shooter.Aim=session.Camera.transform.forward;
            shooter.SelectedSpecial=fixturePlayer.SelectedSpecial; shooter.SpecialWeapon=fixturePlayer.SpecialWeapon;
            shooter.SpecialAmmo=fixturePlayer.SelectedSpecial?WeaponCatalog.Get(kind).Ammo:0; shooter.GrenadeAmmo=2;
            var action=new CombatAction {Participant=1,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1,Sequence=1,Tick=1,
                Kind=kind==WeaponKind.Grenade?CombatActionKind.Grenade:CombatActionKind.Fire,Direction=shooter.Aim};
            fixtureEffectMatch.Step(1,new[]{action},new EmptyCombatWorld());
            fixtureEvents.Clear(); fixtureEvents.AddRange(fixtureEffectMatch.Events);
            fixtureEffects=new GameObject("Diagnostic weapon effects",typeof(ArenaEffects)).GetComponent<ArenaEffects>();
            fixtureEffects.transform.SetParent(transform,false); fixtureEffects.Initialize(fixtureCatalog,fixtureEffectMatch);
            if(effect==CombatEventKind.Accepted || kind==WeaponKind.LaserSword)
            {
                fixturePlayer.Pending=new PendingAttack {Weapon=kind,Action=new CombatAction {Tick=80},ResolveTick=effect==CombatEventKind.Accepted?110:95,EndTick=115};
            }
            // Advance real projectile rules through an explicitly empty fixture world, not the live map.
            var empty=new EmptyCombatWorld();
            for(int tick=2;tick<=24;tick++)
            {
                fixtureEffectMatch.Step(tick,Array.Empty<CombatAction>(),empty);
                fixtureEvents.AddRange(fixtureEffectMatch.Events);
                fixtureEffects.Present(fixtureEffectMatch,false);
            }
            foreach(var shot in fixtureEvents)
            {
                if(effect!=CombatEventKind.Accepted) fixtureWeapon.Play(shot,fixturePlayer.Participant);
            }
            AdvanceFixture(.025f);
            Vector3? muzzle=fixtureWeapon.TryGetMuzzle(out var point)?point:(Vector3?)null;
            if(effect!=CombatEventKind.Accepted)
                foreach(var shot in fixtureEvents) fixtureEffects.Play(shot,fixtureEffectMatch,false,shooter.Feet,fixtureEvents,muzzle);
            if(effect!=CombatEventKind.Shot && effect!=CombatEventKind.Accepted)
            {
                Vector3 impact=shooter.Eye+shooter.Aim*4;
                var cue=new CombatEvent(effect,action,20,position:impact,vector:-shooter.Aim,
                    amount:effect==CombatEventKind.Explosion?WeaponCatalog.Get(kind).Radius:1,weapon:kind);
                fixtureEffects.Play(cue,fixtureEffectMatch,false,shooter.Feet,fixtureEvents);
                if(kind==WeaponKind.LineDriver)
                    for(int i=0;i<8;i++)
                    {
                        var spread=Quaternion.AngleAxis((i-3.5f)*3,Vector3.up)*shooter.Aim;
                        var other=new CombatAction {Participant=i+1,LifeGeneration=1,ConnectionGeneration=1,AuthorityGeneration=1,Sequence=2};
                        var beam=new CombatEvent(CombatEventKind.Shot,other,20,position:shooter.Eye+shooter.Aim*3+session.Camera.transform.right*(i-3.5f)*.7f,
                            vector:spread,weapon:WeaponKind.LineDriver);
                        fixtureEffects.Play(beam,fixtureEffectMatch,false,shooter.Feet);
                    }
            }
        }
        void CreateFixture()
        {
            fixtureState=session.Motor.CaptureState(); fixturePlayer=session.LocalPlayer.Copy();
            session.enabled=false;
            var presentation=session.GetComponent<ArenaPresentation>();
            if(presentation!=null) presentation.enabled=false;
            session.View.ResetPresentation(); session.View.enabled=false;
            fixtureCatalog=Resources.Load<PresentationCatalog>("WildstonePresentation");
            fixtureView=gameObject.AddComponent<FirstPersonView>();
            fixtureView.ViewCamera=session.Camera; fixtureView.OwnsLocalView=true; fixtureView.enabled=false;
            fixtureWeapon=gameObject.AddComponent<WeaponView>(); fixtureWeapon.Initialize(session.Camera,fixtureCatalog);
            fixtureBody=new GameObject("Diagnostic pose fixture body",typeof(FirstPersonBody)).GetComponent<FirstPersonBody>();
            fixtureBody.transform.SetParent(transform,false); fixtureBody.Initialize(fixtureCatalog,fixturePlayer.Team);
        }
        void Pose(float pitch,float speed,float elapsed,bool sliding,bool grounded,float fov)
        {
            fixtureState.Generation++;
            fixtureState.Events=MotorEvents.None; fixtureState.EventTick=0;
            fixtureState.Sliding=sliding; fixtureState.Grounded=grounded; fixtureState.CapsuleHeight=sliding?.9f:1.8f;
            fixtureState.Velocity=Quaternion.Euler(0,fixtureState.Yaw,0)*new Vector3(0,grounded?0:3,speed);
            fixtureState.LastMove=new Vector2(sliding?.5f:0,1);
            fixturePlayer.SelectedSpecial=false; fixturePlayer.ReloadUntil=0; fixturePlayer.Pending=null;
            fixtureView.ResetView(fixtureState.Yaw,pitch); fixtureView.SetBaseFieldOfView(fov); fixtureWeapon.ResetMotion();
            AdvanceFixture(elapsed);
        }
        void AdvanceFixture(float elapsed)
        {
            // Deterministic samples are held for capture; real render time never advances these synthetic poses.
            int samples=Mathf.Max(1,Mathf.CeilToInt(elapsed*120)); float dt=elapsed/samples;
            for(int i=0;i<samples;i++)
            {
                fixtureView.RenderFrame(fixtureState,dt);
                fixtureBody.Present(fixturePlayer,fixtureState,dt,false);
                fixtureWeapon.Present(fixturePlayer,fixtureCatalog,fixtureState,100,dt,false);
            }
            fixtureWeapon.SyncProjection(session.Camera);
        }
        void ReleaseFixture()
        {
            if(fixtureEffects!=null) { Destroy(fixtureEffects.gameObject); fixtureEffects=null; }
            if(fixtureWeapon!=null) { fixtureWeapon.Teardown(session!=null?session.Camera:null); Destroy(fixtureWeapon); fixtureWeapon=null; }
            if(fixtureView!=null) { fixtureView.ResetPresentation(); Destroy(fixtureView); fixtureView=null; }
            if(fixtureBody!=null) { Destroy(fixtureBody.gameObject); fixtureBody=null; }
        }
        void LateUpdate()
        {
            if(!finished && target!=null && session?.Camera!=null)
                RenderPipeline.SubmitRenderRequest(session.Camera,renderRequest);
        }
        void Add(string name,Action enter,Func<bool> ready) => steps.Add(new Step {Name=name,Enter=enter,Ready=ready});
        bool At(MenuScreen screen,int width,int height) => session.Flow.Screen==screen &&
            target!=null && target.IsCreated() && target.width==width && target.height==height &&
            session.Camera!=null && session.Camera.targetTexture==target && renderRequest.destination==target &&
            document!=null && document.panelSettings!=null && document.panelSettings.targetTexture==target &&
            Mathf.Abs(session.Camera.aspect-width/(float)height)<.0001f;
        bool Playing(string map,int width,int height)
        {
            if(session.Failure!=null) throw new InvalidOperationException(session.Failure);
            if(session.Flow.ForegroundLoading || session.MapId!=map || session.Match==null) return false;
            if(session.Flow.Screen==MenuScreen.Pause) session.Show(MenuScreen.Playing);
            return At(MenuScreen.Playing,width,height);
        }
        void ApplySize(int width,int height,float scale)
        {
            var settings=session.Settings.Copy(); settings.Width=width; settings.Height=height; settings.Fullscreen=false;
            settings.UiScale=scale; settings.FpsCap=60; settings.VSync=false; session.ApplySettings(settings,false);
            // The diagnostic draws offscreen: an OS-clamped hidden window must not define projection.
            session.Camera.aspect=width/(float)height;
            if(target!=null && target.width==width && target.height==height) return;
            ReleaseTarget();
            target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            target.Create(); session.Camera.targetTexture=target;
            renderRequest.destination=target;
            document.panelSettings.targetTexture=target; document.panelSettings.clearColor=false;
        }
        void ReleaseTarget()
        {
            if(target==null) return;
            foreach(var camera in FindObjectsByType<Camera>()) if(camera.targetTexture==target) camera.targetTexture=null;
            if(document!=null && document.panelSettings!=null) document.panelSettings.targetTexture=null;
            target.Release(); Destroy(target); target=null; renderRequest.destination=null;
        }
        void OnDestroy() { ReleaseFixture(); ReleaseTarget(); }
        void Finish(bool passed,string error)
        {
            finished=true; result.passed=passed; result.error=error;
            ReleaseFixture();
            File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(result,true));
            Application.Quit(passed?0:1);
        }
    }
}
#endif
