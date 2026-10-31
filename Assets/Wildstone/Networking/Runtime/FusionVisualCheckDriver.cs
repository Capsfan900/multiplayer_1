#if WILDSTONE_SMOKE
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
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
            public string stage,music,sfx;
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
        bool initialized,started,finished,motionCheck,weaponCheck,sequenceCheck,capturePrepared,perfCheck,musicCheck,firefightCheck;
        int sequenceFrame=-1;
        const float SequenceDelta=1f/60;
        string sequenceStage;
        double preparedAt;
        FirstPersonBody fixtureBody;
        PlayerVisual fixtureTarget;
        GameObject fixtureWallRun;
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
                driver.perfCheck=Array.IndexOf(args,"--wildstone-perf-check")>=0;
                driver.musicCheck=Array.IndexOf(args,"--wildstone-music-check")>=0;
                driver.firefightCheck=Array.IndexOf(args,"--wildstone-firefight-check")>=0;
                if((driver.motionCheck?1:0)+(driver.weaponCheck?1:0)+(driver.sequenceCheck?1:0)+(driver.perfCheck?1:0)+(driver.musicCheck?1:0)+(driver.firefightCheck?1:0)>1) throw new ArgumentException("Choose one diagnostic mode.");
                if(driver.firefightCheck) driver.result.mode="firefight-frame-sequences-not-gameplay-proof";
                if(driver.musicCheck) driver.result.mode="music-streaming-check-listener-muted";
                if(driver.perfCheck) driver.result.mode="frame-time-sample-not-acceptance";
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
                if(perfCheck) { RunPerf(now); return; }
                if(firefightCheck) { RunFirefight(now); return; }
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
                    if(!sequenceCheck && !weaponCheck && !motionCheck) RecordMusic(capture);
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
            if(musicCheck) { ConfigureMusicSteps(); return; }
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
                    if(fixtureView==null) { CreateFixture(); Pose(35,0,0,false,true,FixtureFov); }
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
                // A dash mid-run: sample 12 shows its lens kick and streaks one frame in, sample 24 their end.
                if(frame==11)
                {
                    fixtureState.DashStartedAt=frame*SequenceDelta; fixtureState.DashDirection=Vector3.forward;
                    fixtureState.Events=MotorEvents.Dash; fixtureState.EventTick=tick;
                }
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
            // The animated pivot, not child 0: the static soldier arms rig is created first.
            var hands=session.Camera.transform.Find("Viewmodel/Weapon pivot");
            if(hands==null) throw new InvalidOperationException("Missing sequence hands.");
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
                if(fixtureView==null) { CreateFixture(); Pose(0,0,0,false,true,FixtureFov); }
                return true;
            });
            AddPose("fixture-standing-down",()=>Pose(65,0,0,false,true,FixtureFov));
            AddPose("fixture-walk-phase-a",()=>Pose(55,8,.08f,false,true,FixtureFov));
            AddPose("fixture-walk-phase-b",()=>Pose(55,8,.23f,false,true,FixtureFov));
            AddPose("fixture-air-down",()=>Pose(55,6,.15f,false,false,FixtureFov));
            AddPose("fixture-slide-entry",()=>Pose(0,16,.05f,true,true,FixtureFov));
            AddPose("fixture-slide-sustain",()=>Pose(0,16,.3f,true,true,FixtureFov));
            AddPose("fixture-slide-down",()=>Pose(55,16,.3f,true,true,FixtureFov));
            AddPose("fixture-slide-fov60",()=>Pose(0,16,.3f,true,true,60));
            AddPose("fixture-slide-exit",()=>
            {
                Pose(45,16,.3f,true,true,FixtureFov);
                fixtureState.Sliding=false; fixtureState.CapsuleHeight=1.8f; AdvanceFixture(.1f);
            });
            AddPose("fixture-fire",()=>
            {
                Pose(0,0,0,false,true,FixtureFov);
                fixtureWeapon.Play(new CombatEvent(CombatEventKind.Shot,new CombatAction {Participant=fixturePlayer.Participant},100),fixturePlayer.Participant);
                AdvanceFixture(.025f);
            });
            AddPose("fixture-reload",()=>
            {
                Pose(0,0,0,false,true,FixtureFov);
                fixturePlayer.ReloadUntil=100+Mathf.RoundToInt(WeaponCatalog.ReloadSeconds*WeaponCatalog.DefaultTickRate*.5f);
                AdvanceFixture(.05f);
            });
            AddPose("fixture-special-sword",()=>
            {
                Pose(0,0,0,false,true,FixtureFov);
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
            AddWeapon("weapon-grenade-aim",WeaponKind.Grenade,0,CombatEventKind.Accepted);
            AddWeapon("weapon-lens-charge",WeaponKind.PushLens,0,CombatEventKind.Accepted);
            AddWeapon("weapon-overlapping-lasers",WeaponKind.LineDriver,0,CombatEventKind.Hit);
            AddWeapon("weapon-remote-hit",WeaponKind.LineDriver,0,CombatEventKind.Shot);
            steps[steps.Count-1].BeforeCapture=()=>{ WeaponFixture(WeaponKind.LineDriver,0,CombatEventKind.Shot); RemoteHitFixture(); };
            AddWeapon("weapon-pickups",WeaponKind.LineDriver,0,CombatEventKind.Shot);
            steps[steps.Count-1].BeforeCapture=PickupFixture;
            AddWeapon("weapon-status-shield",WeaponKind.LineDriver,0,CombatEventKind.Shot);
            steps[steps.Count-1].BeforeCapture=()=>StatusFixture(true);
            AddWeapon("weapon-status-stun",WeaponKind.LineDriver,0,CombatEventKind.Shot);
            steps[steps.Count-1].BeforeCapture=()=>StatusFixture(false);
            // The shield breaks on the presented frame: the shell's flare, ring, sparks and flash at their start.
            AddWeapon("weapon-status-shield-break",WeaponKind.LineDriver,0,CombatEventKind.Shot);
            steps[steps.Count-1].BeforeCapture=()=>
            {
                StatusFixture(true);
                fixtureEffectMatch.Find(2).Shield=0;
                fixtureEffects.Play(new CombatEvent(CombatEventKind.ShieldBroken,new CombatAction { Participant=1,LifeGeneration=1,Sequence=1 },fixtureEffectMatch.Tick,target:2),
                    fixtureEffectMatch,false,Vector3.zero);
                fixtureEffects.Present(fixtureEffectMatch,false);
            };
            AddWeapon("weapon-remote-wallrun",WeaponKind.LineDriver,0,CombatEventKind.Shot);
            steps[steps.Count-1].BeforeCapture=WallRunFixture;
            // Near the end, because it leaves the starter low: five rounds read in the warning colour (ArenaHud.LowAmmo).
            AddWeapon("weapon-low-ammo",WeaponKind.LineDriver,0,CombatEventKind.Shot);
            steps[steps.Count-1].BeforeCapture=()=>{ WeaponFixture(WeaponKind.LineDriver,0,CombatEventKind.Shot); session.LocalPlayer.StarterAmmo=5; };
            // Last, because the banner outlasts a step: a quick second kill names the victim and the headshot.
            AddWeapon("weapon-kill-banner",WeaponKind.LineDriver,0,CombatEventKind.Shot);
            steps[steps.Count-1].BeforeCapture=()=>
            {
                WeaponFixture(WeaponKind.LineDriver,0,CombatEventKind.Shot);
                var hud=FindAnyObjectByType<ArenaMenu>()?.Hud;
                if(hud==null) throw new InvalidOperationException("The kill banner capture needs the playing HUD.");
                hud.Confirm("◆ P3",false,Time.unscaledTime-1); hud.Confirm("● P4",true,Time.unscaledTime);
            };
        }
        // A remote soldier 6 m ahead with an active shield, or stunned (with its stun immunity): the status reads opponents see.
        void StatusFixture(bool shield)
        {
            WeaponFixture(WeaponKind.LineDriver,0,CombatEventKind.Accepted);
            fixturePlayer.Pending=null;
            DestroyImmediate(fixtureEffects.gameObject);
            var definition=new ArenaDefinition { Id="atrium",Spawns=Array.Empty<ArenaSpawn>(),Pickups=Array.Empty<ArenaPickup>(),
                ZoneRoute=new[]{Vector3.zero,Vector3.forward*8,Vector3.right*8} };
            var feet=fixtureState.Position+Vector3.forward*6;
            fixtureEffectMatch=new ArenaMatch(definition,practice:true);
            fixtureEffectMatch.AddPlayer(1,0,1,1,fixtureState.Position); fixtureEffectMatch.AddPlayer(2,1,1,1,feet); fixtureEffectMatch.Begin();
            var target=fixtureEffectMatch.Find(2);
            if(shield) { target.Shield=WeaponCatalog.Shield; target.ShieldUntil=fixtureEffectMatch.Tick+WeaponCatalog.Ticks(WeaponCatalog.ShieldSeconds,fixtureEffectMatch.TickRate); }
            else { target.StunUntil=fixtureEffectMatch.Tick+120; target.ImmunityUntil=fixtureEffectMatch.Tick+120; }
            fixtureEffects=new GameObject("Diagnostic status effects",typeof(ArenaEffects)).GetComponent<ArenaEffects>();
            fixtureEffects.transform.SetParent(transform,false); fixtureEffects.Initialize(fixtureCatalog,fixtureEffectMatch);
            fixtureEffects.Present(fixtureEffectMatch,false);
            if(fixtureTarget==null)
            {
                fixtureTarget=new GameObject("Diagnostic status target",typeof(PlayerVisual)).GetComponent<PlayerVisual>();
                fixtureTarget.transform.SetParent(transform,false); fixtureTarget.Initialize(fixtureCatalog,1); FreshSkinning(fixtureTarget);
            }
            var state=new CombatState {Participant=2,Team=1,LifeGeneration=1,Alive=true,Connected=true,Feet=feet,Aim=Vector3.back};
            var motor=new MotorState {Grounded=true,Yaw=180,CapsuleHeight=1.8f,Position=feet};
            fixtureTarget.Present(state,in motor,fixtureCatalog);
            fixtureTarget.PresentHandling(state,100,1,false); // a full second lets the previous step's hit flash finish
        }
        // Two remote soldiers wall-running toward the lens mid-stride, 0.4 m (the capsule radius) off a wall on their
        // outside: WallRunL on the right of the frame, WallRunR on the left, so the lean reads across the screen.
        void WallRunFixture()
        {
            WeaponFixture(WeaponKind.LineDriver,0,CombatEventKind.Accepted);
            fixturePlayer.Pending=null;
            if(fixtureTarget!=null) fixtureTarget.gameObject.SetActive(false);
            fixtureWallRun=new GameObject("Diagnostic wall run");
            fixtureWallRun.transform.SetParent(transform,false);
            var stone=Array.Find(FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None),r=>r.sharedMaterial!=null && r.sharedMaterial.name=="Stone-Charcoal");
            for(int side=-1;side<=1;side+=2)
            {
                var feet=fixtureState.Position+new Vector3(side*1.3f,.9f,6);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                DestroyImmediate(wall.GetComponent<Collider>());
                wall.transform.SetParent(fixtureWallRun.transform,false);
                wall.transform.position=feet+new Vector3(side*.55f,.6f,4); wall.transform.localScale=new Vector3(.3f,3.4f,12);
                if(stone!=null) wall.GetComponent<MeshRenderer>().sharedMaterial=stone.sharedMaterial;
                var runner=new GameObject("Diagnostic wall runner",typeof(PlayerVisual)).GetComponent<PlayerVisual>();
                runner.transform.SetParent(fixtureWallRun.transform,false); runner.Initialize(fixtureCatalog,1); FreshSkinning(runner);
                var state=new CombatState {Participant=2,Team=1,LifeGeneration=1,Alive=true,Connected=true,Feet=feet,Aim=Vector3.back};
                var motor=new MotorState {WallRunning=true,WallNormal=Vector3.left*side,Velocity=Vector3.back*9,Yaw=180,CapsuleHeight=1.8f,Position=feet};
                for(int i=0;i<12;i++) { runner.Present(state,in motor,fixtureCatalog); runner.PresentHandling(state,100,.05f,false); }
            }
        }
        // Six pickup pads on the court ahead; the shotgun was taken 6 s ago, so its pad shows the refill arc.
        void PickupFixture()
        {
            // Accepted plays no shot; clear the charge it would otherwise leave pending on the Line Driver.
            WeaponFixture(WeaponKind.LineDriver,0,CombatEventKind.Accepted);
            fixturePlayer.Pending=null;
            if(fixtureTarget!=null) fixtureTarget.gameObject.SetActive(false);
            DestroyImmediate(fixtureEffects.gameObject);
            ArenaPickup Pad(string item,float x,float z) => new ArenaPickup {Item=item,Position=new Vector3(x,.45f,z)};
            var definition=new ArenaDefinition { Id="atrium",Spawns=Array.Empty<ArenaSpawn>(),
                Pickups=new[]{Pad("laser-pistol",-3.5f,-6),Pad("laser-shotgun",0,-4),Pad("laser-sniper",3.5f,-6),Pad("shield",-1.6f,-8.5f),Pad("pulse-lobber",1.8f,-8.5f),Pad("health",-2.5f,-2)},
                ZoneRoute=new[]{Vector3.zero,Vector3.forward*8,Vector3.right*8} };
            fixtureEffectMatch=new ArenaMatch(definition,practice:true);
            fixtureEffectMatch.AddPlayer(1,0,1,1,fixtureState.Position); fixtureEffectMatch.Begin();
            fixtureEffectMatch.Pickups[1].AvailableTick=fixtureEffectMatch.Tick+WeaponCatalog.Ticks(WeaponCatalog.PickupSeconds-6,fixtureEffectMatch.TickRate);
            fixtureEffects=new GameObject("Diagnostic pickup effects",typeof(ArenaEffects)).GetComponent<ArenaEffects>();
            fixtureEffects.transform.SetParent(transform,false); fixtureEffects.Initialize(fixtureCatalog,fixtureEffectMatch);
            fixtureEffects.Present(fixtureEffectMatch,false);
        }
        // A remote soldier 7 m down the beam, caught at the peak of its confirmed-hit flash (decay waits for PresentHandling).
        void RemoteHitFixture()
        {
            if(fixtureTarget==null)
            {
                fixtureTarget=new GameObject("Diagnostic hit target",typeof(PlayerVisual)).GetComponent<PlayerVisual>();
                fixtureTarget.transform.SetParent(transform,false); fixtureTarget.Initialize(fixtureCatalog,1); FreshSkinning(fixtureTarget);
            }
            var feet=fixtureState.Position+Vector3.forward*7;
            var state=new CombatState {Participant=2,Team=1,LifeGeneration=1,Alive=true,Connected=true,Feet=feet,Aim=Vector3.back};
            var motor=new MotorState {Grounded=true,Yaw=180,CapsuleHeight=1.8f,Position=feet};
            fixtureTarget.Present(state,in motor,fixtureCatalog);
            fixtureTarget.PresentHandling(state,100,.016f,false);
            var hit=new CombatAction {Participant=fixturePlayer.Participant,LifeGeneration=1,Sequence=1};
            fixtureTarget.React(new CombatEvent(CombatEventKind.Hit,hit,2,position:feet+Vector3.up*1.2f,amount:10));
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
        void WeaponFixture(WeaponKind kind,int facing,CombatEventKind effect,Vector3? hitAt=null,Vector3? blast=null)
        {
            if(fixtureEffects!=null) DestroyImmediate(fixtureEffects.gameObject);
            if(fixtureWallRun!=null) DestroyImmediate(fixtureWallRun);
            fixtureState.Position=new Vector3(0,.02f,-12);
            fixtureState.Yaw=facing==0?0:180;
            Pose(0,0,0,false,true,FixtureFov);
            fixturePlayer.SelectedSpecial=kind!=WeaponKind.LineDriver && kind!=WeaponKind.Grenade;
            fixturePlayer.SpecialWeapon=fixturePlayer.SelectedSpecial?kind:WeaponKind.None;
            session.LocalPlayer.SelectedSpecial=fixturePlayer.SelectedSpecial;
            session.LocalPlayer.SpecialWeapon=fixturePlayer.SpecialWeapon;
            session.LocalPlayer.SpecialAmmo=fixturePlayer.SelectedSpecial?WeaponCatalog.Get(kind).Ammo:0;
            // The grenade aim frame holds the throw key: grenade in the support hand and the arc, nothing thrown.
            bool aim=kind==WeaponKind.Grenade && effect==CombatEventKind.Accepted;
            fixtureWeapon.GrenadeReady=aim;
            AdvanceFixture(.4f); // let the 0.3 s equip raise finish so the shot frames the settled viewmodel
            var definition=new ArenaDefinition { Id="atrium",Spawns=Array.Empty<ArenaSpawn>(),Pickups=Array.Empty<ArenaPickup>(),
                ZoneRoute=new[]{Vector3.zero,Vector3.forward*8,Vector3.right*8} };
            fixtureEffectMatch=new ArenaMatch(definition,practice:true);
            fixtureEffectMatch.AddPlayer(1,0,1,1,fixtureState.Position); fixtureEffectMatch.Begin();
            var shooter=fixtureEffectMatch.Find(1); shooter.Aim=session.Camera.transform.forward;
            shooter.SelectedSpecial=fixturePlayer.SelectedSpecial; shooter.SpecialWeapon=fixturePlayer.SpecialWeapon;
            shooter.SpecialAmmo=fixturePlayer.SelectedSpecial?WeaponCatalog.Get(kind).Ammo:0; shooter.GrenadeAmmo=2;
            var action=new CombatAction {Participant=1,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1,Sequence=1,Tick=1,
                Kind=kind==WeaponKind.Grenade?CombatActionKind.Grenade:CombatActionKind.Fire,Direction=shooter.Aim};
            fixtureEffectMatch.Step(1,aim?Array.Empty<CombatAction>():new[]{action},new EmptyCombatWorld());
            fixtureEvents.Clear(); if(blast==null) fixtureEvents.AddRange(fixtureEffectMatch.Events);
            // The empty fixture world never reports hits; a firefight adds the confirmed hit so the beam ends on the target.
            if(hitAt is Vector3 hitPoint)
                foreach(var shot in fixtureEffectMatch.Events)
                    if(shot.Kind==CombatEventKind.Shot) fixtureEvents.Add(new CombatEvent(CombatEventKind.Hit,shot.Action,shot.Tick,2,1,position:hitPoint,amount:10,weapon:kind,headshot:true));
            fixtureEffects=new GameObject("Diagnostic weapon effects",typeof(ArenaEffects)).GetComponent<ArenaEffects>();
            fixtureEffects.transform.SetParent(transform,false); fixtureEffects.Initialize(fixtureCatalog,fixtureEffectMatch);
            if(aim) fixtureEffects.PresentGrenadeAim(true,session.Camera.transform.position,session.Camera.transform.forward,1,fixtureEffectMatch.TickRate);
            if(effect==CombatEventKind.Accepted || kind==WeaponKind.LaserSword)
            {
                fixturePlayer.Pending=new PendingAttack {Weapon=kind,Action=new CombatAction {Tick=80},ResolveTick=effect==CombatEventKind.Accepted?110:95,EndTick=115};
            }
            // Advance real projectile rules through an explicitly empty fixture world, not the live map.
            var empty=new EmptyCombatWorld();
            for(int tick=2;blast==null && tick<=24;tick++)
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
                Vector3 impact=blast??shooter.Eye+shooter.Aim*4;
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
            FreshSkinning(session.Camera); FreshSkinning(fixtureBody);
        }
        // Captures render from LateUpdate, before the frame's own skinning pass; without this a skinned mesh shows the
        // previous frame's bones (a fixture reset or weapon change left the support hand where the last weapon had it).
        static void FreshSkinning(Component root)
        {
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.forceMatrixRecalculationPerRender=true;
        }
        // Fixtures frame the world at the game's default view (100 degrees horizontal at 16:9), not the retired 90 degree vertical.
        static readonly float FixtureFov=LocalSettings.VerticalFov(LocalSettings.DefaultFov,16f/9);
        void Pose(float pitch,float speed,float elapsed,bool sliding,bool grounded,float fov)
        {
            fixtureState.Generation++;
            fixtureState.Events=MotorEvents.None; fixtureState.EventTick=0;
            fixtureState.Sliding=sliding; fixtureState.Grounded=grounded; fixtureState.CapsuleHeight=sliding?.9f:1.8f;
            fixtureState.Velocity=Quaternion.Euler(0,fixtureState.Yaw,0)*new Vector3(0,grounded?0:3,speed);
            fixtureState.LastMove=new Vector2(sliding?.5f:0,1);
            fixturePlayer.SelectedSpecial=false; fixturePlayer.ReloadUntil=0; fixturePlayer.Pending=null;
            fixtureView.ResetView(fixtureState.Yaw,pitch); fixtureView.SetBaseFieldOfView(fov); fixtureWeapon.GrenadeReady=false; fixtureWeapon.ResetMotion();
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
            if(fixtureTarget!=null) { Destroy(fixtureTarget.gameObject); fixtureTarget=null; }
        }
        void LateUpdate()
        {
            if(!finished && !perfCheck && target!=null && session?.Camera!=null && ScenesSettled())
            {
                RenderPipeline.SubmitRenderRequest(session.Camera,renderRequest);
                if(firefightCheck && firefightFrame>=0 && firefightFrame<=Samples[Samples.Length-1])
                {
                    if(Array.IndexOf(Samples,firefightFrame)>=0)
                    {
                        // GPU-side copy: no readback or encode inside the sequence, so every sample is one exact 1/60 s step.
                        var copy=new RenderTexture(target.descriptor); copy.Create(); Graphics.CopyTexture(target,copy);
                        firefightCopies.Add((copy,firefightFrame,Time.unscaledTime-firefightShot,Time.unscaledDeltaTime));
                    }
                    firefightFrame++;
                }
            }
        }
        // Firefight sequences: a real shot from each main hitscan weapon lands a headshot on a remote soldier strafing 12 m
        // ahead (beam, sparks, burn, hit flash, view punch, viewmodel flare). Time.captureDeltaTime holds fast frames to
        // 1/60 s, but the unscaled effect clock still advances by real time on a slow frame (the shot-setup frame), so
        // each sample records its true age. PNG encoding waits until the sequence ends. A last sequence shows the other
        // side: a remote sniper rail from the right flank that passes just beside the lens. Then a grenade and a plasma orb
        // detonate on the court 8 m ahead beside the soldier, sampled for most of a second. The fixture world has no floor,
        // so their flight is skipped and each blast is placed where the host would report it.
        static readonly WeaponKind[] FirefightWeapons={WeaponKind.LineDriver,WeaponKind.LaserSMG,WeaponKind.LaserShotgun,WeaponKind.LaserSniper,WeaponKind.LaserSword};
        static readonly WeaponKind[] BlastWeapons={WeaponKind.Grenade,WeaponKind.PulseLobber};
        static readonly int[] FirefightSamples={0,1,2,4,6,9,14,20},BlastSamples={0,2,4,8,14,22,34,50};
        static readonly int[] RangeDistances={15,30,45},RangeSamples={0};
        bool FirefightRange => firefightWeapon>FirefightWeapons.Length+BlastWeapons.Length;
        int[] Samples => FirefightRange?RangeSamples:firefightWeapon>FirefightWeapons.Length?BlastSamples:FirefightSamples;
        readonly List<(RenderTexture texture,int frame,float age,float delta)> firefightCopies=new List<(RenderTexture,int,float,float)>();
        int firefightWeapon=-1,firefightFrame=-1;
        bool firefightWarmed;
        float firefightShot;
        double firefightReadyAt=-1;
        Vector3 firefightTargetFeet;
        void RunFirefight(double now)
        {
            if(firefightWeapon<0)
            {
                if(target==null) { ApplySize(1280,720,1); session.Load("atrium"); return; }
                if(!Playing("atrium",1280,720)) return;
                if(fixtureView==null) CreateFixture();
                if(firefightReadyAt<0) { firefightReadyAt=now; Time.captureDeltaTime=1f/60; } // on before the first shot: it takes effect a frame late
                // One unrecorded shot first: the first use of the effect materials can stall the frame after a shot.
                if(now-firefightReadyAt>=.5 && !firefightWarmed) { firefightWarmed=true; WeaponFixture(WeaponKind.LineDriver,0,CombatEventKind.Shot,new Vector3(0,1.67f,-.3f)); }
                if(now-firefightReadyAt<1.5) return; // let streaming and probes settle, as the weapon steps do
                firefightWeapon=0;
            }
            // Particles age on unscaled time: firing right after the previous sequence's PNG encoding (a long frame) would
            // age a new blast by that whole stall before its first sample, killing its fireball and flash.
            if(firefightFrame<0) { if(Time.unscaledDeltaTime<.05f) FireFirefightShot(); return; }
            if(firefightFrame>Samples[Samples.Length-1])
            {
                SaveFirefight();
                firefightFrame=-1;
                if(++firefightWeapon>FirefightWeapons.Length+BlastWeapons.Length+RangeDistances.Length) Finish(true,"");
                return;
            }
            AdvanceFixture(1f/60);
            if(firefightWeapon<=FirefightWeapons.Length) firefightTargetFeet+=Vector3.right*5f/60;
            PresentFirefightTarget(1f/60);
        }
        void FireFirefightShot()
        {
            if(firefightWeapon==FirefightWeapons.Length) { IncomingRail(); firefightShot=Time.unscaledTime; firefightFrame=0; return; }
            if(fixtureTarget==null)
            {
                fixtureTarget=new GameObject("Diagnostic firefight target",typeof(PlayerVisual)).GetComponent<PlayerVisual>();
                fixtureTarget.transform.SetParent(transform,false); fixtureTarget.Initialize(fixtureCatalog,1); FreshSkinning(fixtureTarget);
            }
            if(FirefightRange)
            {
                int distance=RangeDistances[firefightWeapon-FirefightWeapons.Length-BlastWeapons.Length-1];
                WeaponFixture(WeaponKind.LineDriver,0,CombatEventKind.Accepted);
                fixturePlayer.Pending=null;
                fixtureState.Position=new Vector3(0,.02f,-distance*.5f);
                AdvanceFixture(.2f);
                firefightTargetFeet=new Vector3(0,.02f,distance*.5f);
                PresentFirefightTarget(.2f);
                firefightShot=Time.unscaledTime; firefightFrame=0; return;
            }
            if(firefightWeapon>FirefightWeapons.Length) { FireBlast(BlastWeapons[firefightWeapon-FirefightWeapons.Length-1]); firefightShot=Time.unscaledTime; firefightFrame=0; return; }
            var kind=FirefightWeapons[firefightWeapon];
            firefightTargetFeet=new Vector3(0,.02f,0);
            if(kind==WeaponKind.LaserSword)
                firefightTargetFeet.z=-12+WeaponCatalog.Get(kind).Range*.6f;
            PresentFirefightTarget(0);
            var hit=firefightTargetFeet+new Vector3(0,1.65f,-.3f); // eye height on the aim line, at the target's face
            WeaponFixture(kind,0,CombatEventKind.Shot,hit);
            fixtureTarget.Vfx=fixtureEffects.Vfx;
            fixtureTarget.React(new CombatEvent(CombatEventKind.Hit,new CombatAction {Participant=1,LifeGeneration=1,Sequence=1},1,2,1,position:hit,amount:10,weapon:kind,headshot:true));
            fixtureView.AddRecoil(WeaponView.ViewPunch(kind));
            firefightShot=Time.unscaledTime; firefightFrame=0;
        }
        void FireBlast(WeaponKind kind)
        {
            var blast=new Vector3(0,.3f,-4);
            firefightTargetFeet=new Vector3(-2,.02f,-4);
            PresentFirefightTarget(0);
            WeaponFixture(kind,0,CombatEventKind.Explosion,blast:blast);
            fixtureTarget.Vfx=fixtureEffects.Vfx;
            fixtureTarget.React(new CombatEvent(CombatEventKind.Hit,new CombatAction {Participant=1,LifeGeneration=1,Sequence=1},1,2,1,
                position:firefightTargetFeet+Vector3.up*1.2f,amount:30,weapon:kind));
        }
        // Shooter 28 degrees right and above, up to 30 m out but short of any wall, so the rail has a clear line to the lens.
        void IncomingRail()
        {
            var view=session.Camera.transform;
            Vector3 eye=view.position,toward=Quaternion.AngleAxis(-6,view.right)*Quaternion.AngleAxis(28,Vector3.up)*view.forward;
            float distance=Physics.Raycast(eye,toward,out var wall,30,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)?wall.distance-.5f:30;
            Vector3 origin=eye+toward*distance,aim=eye-view.right*.7f-view.up*.15f;
            var action=new CombatAction {Participant=2,ConnectionGeneration=1,LifeGeneration=1,AuthorityGeneration=1,Sequence=9};
            fixtureEffects.Play(new CombatEvent(CombatEventKind.Shot,action,40,position:origin,vector:(aim-origin).normalized,weapon:WeaponKind.LaserSniper),
                fixtureEffectMatch,false,origin);
        }
        void PresentFirefightTarget(float dt)
        {
            var state=new CombatState {Participant=2,Team=1,LifeGeneration=1,Alive=true,Connected=true,Feet=firefightTargetFeet,Aim=Vector3.back};
            var motor=new MotorState {Grounded=true,Yaw=180,CapsuleHeight=1.8f,Position=firefightTargetFeet,Velocity=Vector3.right*5};
            fixtureTarget.Present(state,in motor,fixtureCatalog);
            fixtureTarget.PresentHandling(state,100,dt,false);
        }
        void SaveFirefight()
        {
            string weapon=FirefightRange?"range-"+RangeDistances[firefightWeapon-FirefightWeapons.Length-BlastWeapons.Length-1]+"m":firefightWeapon<FirefightWeapons.Length?FirefightWeapons[firefightWeapon].ToString().ToLowerInvariant():
                firefightWeapon==FirefightWeapons.Length?"incoming-sniper":BlastWeapons[firefightWeapon-FirefightWeapons.Length-1].ToString().ToLowerInvariant()+"-blast";
            var previous=RenderTexture.active; var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                foreach(var sample in firefightCopies)
                {
                    RenderTexture.active=sample.texture; pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply();
                    string file=Path.Combine(directory,"firefight-"+weapon+(FirefightRange?"":"-"+sample.frame.ToString("D2"))+".png");
                    File.WriteAllBytes(file,pixels.EncodeToPNG());
                    result.captures.Add(new Capture {file=file,screen=session.Flow.Screen.ToString(),map=session.MapId,mode=result.mode,
                        width=target.width,height=target.height,screenWidth=Screen.width,screenHeight=Screen.height,cameraAspect=session.Camera.aspect,
                        uiScale=session.Settings.UiScale,stage=weapon,simulatedFrame=sample.frame,simulatedSeconds=sample.age,simulationDelta=sample.delta});
                }
                File.WriteAllText(Path.Combine(directory,"result.json"),JsonUtility.ToJson(result,true));
            }
            finally
            {
                RenderTexture.active=previous; Destroy(pixels);
                foreach(var sample in firefightCopies) { sample.texture.Release(); Destroy(sample.texture); }
                firefightCopies.Clear();
            }
        }
        // A forced render while a map scene loads or unloads can cull reflection probes whose components are already
        // gone (URP ReflectionProbeManager NRE). Captures never wait on those frames, so skip them.
        static bool ScenesSettled()
        {
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isLoaded) return false;
            return true;
        }
        void Add(string name,Action enter,Func<bool> ready) => steps.Add(new Step {Name=name,Enter=enter,Ready=ready});
        // The packaged player must be streaming the screen's own loop (D030): menus share the theme, matches play their map.
        // Batch-mode players run without audio, so only the windowed music check can assert playback.
        void ConfigureMusicSteps()
        {
            AudioListener.volume=0; // real streaming and mixing, silent speakers
            Add("music-title",()=>{ApplySize(1280,720,1);session.Show(MenuScreen.Title);},()=>At(MenuScreen.Title,1280,720) && MusicAudible());
            foreach(string id in new[]{"atrium","split-gallery","terrace-steps"})
            {
                string map=id;
                Add("music-"+map,()=>session.Load(map),()=>Playing(map,1280,720) && MusicAudible());
            }
            // The recorded weapon banks (SOURCE-REUSE 2026-10-30) must ship, decode and reach the mixer.
            Add("sfx-recorded",FireRecordedCue,()=>Playing("terrace-steps",1280,720) && MusicAudible() && RecordedCueAudible());
        }
        string sfxHeard,tailHeard;
        // A Line Driver shot, then a sniper shot whose LaserCellNova tail is the second recorded bank that must ship.
        void FireRecordedCue()
        {
            var audio=session.GetComponentInChildren<ArenaAudio>(); var at=session.Camera.transform.position;
            audio.Play(new CombatEvent(CombatEventKind.Shot,default,session.Match.Tick,weapon:WeaponKind.LineDriver),at,1);
            audio.Play(new CombatEvent(CombatEventKind.Shot,default,session.Match.Tick,weapon:WeaponKind.LaserSniper),at,1);
        }
        bool RecordedCueAudible()
        {
            if(sfxHeard!=null && tailHeard!=null) return true;
            var audio=session.GetComponentInChildren<ArenaAudio>(); var shots=audio.Bank("LaserFire"); var tails=audio.Bank("LaserCellNova"); bool playing=false;
            foreach(var voice in audio.GetComponentsInChildren<AudioSource>())
            {
                bool shot=Array.IndexOf(shots,voice.clip)>=0, tail=Array.IndexOf(tails,voice.clip)>=0;
                if(!voice.isPlaying || !shot && !tail) continue;
                playing=true; float rms=OutputRms(voice);
                if(rms<=1e-4f) continue;
                string heard=voice.clip.name+" at pitch "+voice.pitch.ToString("F2")+", volume "+voice.volume.ToString("F2")+", output RMS "+rms.ToString("F4");
                if(shot && sfxHeard==null) sfxHeard=heard;
                if(tail && tailHeard==null) tailHeard=heard;
            }
            if(!playing) FireRecordedCue(); // the clips ended before the mixer reported them
            return sfxHeard!=null && tailHeard!=null;
        }
        string ExpectedTrack => MusicMix.TrackFor(session.Flow.Screen,session.Match!=null,session.MapId);
        bool MusicAudible()
        {
            var voice=session.GetComponent<ArenaMusic>().ActiveVoice;
            return voice.clip!=null && voice.clip.name==ExpectedTrack && voice.isPlaying && voice.time>.5f && voice.volume>0 && OutputRms(voice)>1e-4f;
        }
        static float OutputRms(AudioSource voice)
        {
            var block=new float[1024]; voice.GetOutputData(block,0);
            double sum=0; foreach(float sample in block) sum+=sample*sample;
            return (float)Math.Sqrt(sum/block.Length);
        }
        void RecordMusic(Capture capture)
        {
            if(Application.isBatchMode) { capture.music="not checked: batch-mode players run without audio"; return; }
            var voice=session.GetComponent<ArenaMusic>().ActiveVoice;
            capture.music=(voice.clip!=null?voice.clip.name:"none")+(voice.isPlaying?" playing at ":" stopped at ")+voice.time.ToString("F2")+
                " s, volume "+voice.volume.ToString("F2")+", output RMS "+OutputRms(voice).ToString("F4");
            if(!MusicAudible()) throw new InvalidOperationException("Music is "+capture.music+"; expected "+ExpectedTrack+" streaming audibly.");
            if(steps[index].Name=="sfx-recorded")
            {
                var wind=session.GetComponentInChildren<ArenaAudio>().transform.Find("Speed wind")?.GetComponent<AudioSource>();
                capture.sfx=sfxHeard+"; "+tailHeard+"; wind "+(wind!=null && wind.clip!=null && wind.loop?wind.clip.name+" looping":"missing");
            }
        }
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
        // Frame-time sample for the D028 budget: the normal render loop (no render requests) into a 1920x1080 target, so
        // a smaller desktop cannot shrink it; Atrium, the local fixture plus seven animated soldiers, continuous beams and
        // a blast every 1.2 s. High (level 5) then Low (level 1), 3 s warm-up and 8 s measured each, one PNG per tier.
        // Same-PC evidence only, not a performance acceptance.
        // GC figures come from the development player's "GC Allocated In Frame" counter over the measured frames.
        [Serializable] sealed class PerfTier { public string quality; public int level,frames,gen0Collections; public float meanMs,p50Ms,p95Ms,p99Ms,maxMs; public long gcAllocMeanBytes,gcAllocMaxBytes; }
        [Serializable] sealed class PerfResult { public string mode="frame-time-sample-not-acceptance",graphicsDevice="",gpu="",resolution=""; public List<PerfTier> tiers=new List<PerfTier>(); }
        static readonly int[] PerfLevels={5,1};
        readonly List<float> perfFrames=new List<float>();
        readonly List<PlayerVisual> perfSoldiers=new List<PlayerVisual>();
        readonly List<CombatState> perfStates=new List<CombatState>();
        PerfResult perf;
        int perfTier, perfSequence, perfCollections=-1;
        ProfilerRecorder perfGc;
        long perfGcSum, perfGcMax;
        double perfPhase;
        float perfBeam, perfBlast, perfClock;
        void RunPerf(double now)
        {
            if(perf==null)
            {
                perf=new PerfResult {graphicsDevice=SystemInfo.graphicsDeviceType.ToString(),gpu=SystemInfo.graphicsDeviceName};
                ApplySize(1920,1080,1);
                var settings=session.Settings.Copy(); settings.MasterVolume=0; session.ApplySettings(settings,false);
                session.Load("atrium"); return;
            }
            if(fixtureView==null)
            {
                if(session.Failure!=null) throw new InvalidOperationException(session.Failure);
                if(session.Flow.ForegroundLoading || session.MapId!="atrium" || session.Match==null) return;
                if(session.Flow.Screen==MenuScreen.Pause) session.Show(MenuScreen.Playing);
                CreateFixture(); CreatePerfScene(); StartPerfTier(now); return;
            }
            float dt=Time.unscaledDeltaTime;
            AdvancePerf(dt);
            double elapsed=now-perfPhase;
            if(elapsed>3)
            {
                if(perfCollections<0) perfCollections=GC.CollectionCount(0);
                perfFrames.Add(dt*1000);
                long allocated=perfGc.Valid?perfGc.LastValue:0; perfGcSum+=allocated; perfGcMax=Math.Max(perfGcMax,allocated);
            }
            if(elapsed<11) return;
            perfFrames.Sort();
            float Percentile(float p) => perfFrames[Mathf.Clamp(Mathf.CeilToInt(p*perfFrames.Count)-1,0,perfFrames.Count-1)];
            float sum=0; foreach(var frame in perfFrames) sum+=frame;
            SavePerfFrame("perf-"+PerfLevels[perfTier]+".png");
            perf.tiers.Add(new PerfTier {quality=QualitySettings.names[PerfLevels[perfTier]],level=PerfLevels[perfTier],frames=perfFrames.Count,
                meanMs=sum/Mathf.Max(1,perfFrames.Count),p50Ms=Percentile(.5f),p95Ms=Percentile(.95f),p99Ms=Percentile(.99f),maxMs=perfFrames[perfFrames.Count-1],
                gcAllocMeanBytes=perfGcSum/Math.Max(1,perfFrames.Count),gcAllocMaxBytes=perfGcMax,gen0Collections=GC.CollectionCount(0)-perfCollections});
            if(++perfTier<PerfLevels.Length) { StartPerfTier(now); return; }
            perfGc.Dispose();
            perf.resolution=target.width+"x"+target.height;
            File.WriteAllText(Path.Combine(directory,"perf.json"),JsonUtility.ToJson(perf,true));
            Finish(true,"");
        }
        void SavePerfFrame(string name)
        {
            var previous=RenderTexture.active; var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try { RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply(); File.WriteAllBytes(Path.Combine(directory,name),pixels.EncodeToPNG()); }
            finally { RenderTexture.active=previous; Destroy(pixels); }
        }
        void StartPerfTier(double now)
        {
            QualitySettings.SetQualityLevel(PerfLevels[perfTier],true);
            // Quality levels carry vSyncCount 1; the sample is uncapped.
            QualitySettings.vSyncCount=0; Application.targetFrameRate=-1;
            perfFrames.Clear(); perfPhase=now;
            if(!perfGc.Valid) perfGc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame");
            perfGcSum=perfGcMax=0; perfCollections=-1;
        }
        void CreatePerfScene()
        {
            fixtureState.Position=new Vector3(0,.02f,-12); fixtureState.Yaw=0;
            Pose(0,0,0,false,true,FixtureFov);
            var definition=new ArenaDefinition { Id="atrium",Spawns=Array.Empty<ArenaSpawn>(),Pickups=Array.Empty<ArenaPickup>(),
                ZoneRoute=new[]{Vector3.zero,Vector3.forward*8,Vector3.right*8} };
            fixtureEffectMatch=new ArenaMatch(definition,practice:true);
            fixtureEffectMatch.AddPlayer(1,0,1,1,fixtureState.Position);
            for(int i=0;i<7;i++) fixtureEffectMatch.AddPlayer(i+2,(i+1)%4,1,1,Vector3.zero);
            fixtureEffectMatch.Begin();
            fixtureEffects=new GameObject("Diagnostic perf effects",typeof(ArenaEffects)).GetComponent<ArenaEffects>();
            fixtureEffects.transform.SetParent(transform,false); fixtureEffects.Initialize(fixtureCatalog,fixtureEffectMatch);
            for(int i=0;i<7;i++)
            {
                var visual=new GameObject("Perf soldier "+i,typeof(PlayerVisual)).GetComponent<PlayerVisual>();
                visual.transform.SetParent(transform,false); visual.Initialize(fixtureCatalog,(i+1)%4); visual.Vfx=fixtureEffects.Vfx;
                var feet=new Vector3((i-3)*2.6f,.02f,-4+(i%3)*5);
                perfSoldiers.Add(visual);
                perfStates.Add(new CombatState {Participant=i+2,Team=(i+1)%4,LifeGeneration=1,Feet=feet,Aim=(fixtureState.Position-feet).normalized,
                    SelectedSpecial=i%2==1,SpecialWeapon=i%2==1?WeaponKind.LaserSniper:WeaponKind.None});
            }
        }
        void AdvancePerf(float dt)
        {
            perfClock+=dt; perfBeam+=dt; perfBlast+=dt;
            AdvanceFixture(dt);
            int tick=100+Mathf.RoundToInt(perfClock*WeaponCatalog.DefaultTickRate);
            for(int i=0;i<perfSoldiers.Count;i++)
            {
                // Strafing in place keeps the locomotion blend and skinning busy.
                var state=perfStates[i]; float side=Mathf.Sin(perfClock*1.3f+i);
                var motor=new MotorState {Grounded=true,Velocity=new Vector3(side*7,0,0),Yaw=180,CapsuleHeight=1.8f,Position=state.Feet};
                perfSoldiers[i].Present(state,in motor,fixtureCatalog);
                perfSoldiers[i].PresentHandling(state,tick,dt,false);
            }
            if(perfBeam>=.1f)
            {
                perfBeam=0;
                foreach(var state in perfStates)
                {
                    var action=new CombatAction {Participant=state.Participant,LifeGeneration=1,ConnectionGeneration=1,AuthorityGeneration=1,Sequence=++perfSequence};
                    var origin=state.Feet+Vector3.up*1.5f;
                    var direction=(session.Camera.transform.position+UnityEngine.Random.insideUnitSphere*2-origin).normalized;
                    fixtureEffects.Play(new CombatEvent(CombatEventKind.Shot,action,tick,position:origin,vector:direction,weapon:WeaponKind.LineDriver),fixtureEffectMatch,false,state.Feet);
                }
            }
            if(perfBlast>=1.2f)
            {
                perfBlast=0;
                var action=new CombatAction {Participant=2,LifeGeneration=1,ConnectionGeneration=1,AuthorityGeneration=1,Sequence=++perfSequence};
                var impact=new Vector3(UnityEngine.Random.Range(-6f,6f),.3f,UnityEngine.Random.Range(2f,10f));
                fixtureEffects.Play(new CombatEvent(CombatEventKind.Explosion,action,tick,position:impact,vector:Vector3.up,
                    amount:WeaponCatalog.Get(WeaponKind.Grenade).Radius,weapon:WeaponKind.Grenade),fixtureEffectMatch,false,impact);
            }
            fixtureEffects.Present(fixtureEffectMatch,false);
        }
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
