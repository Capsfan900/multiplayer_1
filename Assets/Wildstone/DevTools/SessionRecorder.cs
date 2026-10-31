using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Match;
using Wildstone.Movement;
using Wildstone.UI;

namespace Wildstone.DevTools
{
    // Development add-on, not part of the game: delete Assets/Wildstone/DevTools to remove it.
    // Play-session flight recorder (docs/SESSION-RECORDER.md). Off unless the player is launched with
    // --wildstone-record [--wildstone-record-dir <dir>]. It only observes PracticeSession; it never feeds input or rules.
    public sealed class SessionRecorder : MonoBehaviour
    {
        public const string Flag="--wildstone-record", DirFlag="--wildstone-record-dir";
        public string Directory { get; private set; }
        SessionLog log;
        PracticeSession session;
        readonly Dictionary<int,MotorState> states=new Dictionary<int,MotorState>();
        readonly List<float> frames=new List<float>(256);
        float startedAt, windowStart;
        int gcCount, marks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,Flag)<0) return;
            int dir=Array.IndexOf(args,DirFlag);
            var path=dir>=0 && dir+1<args.Length?args[dir+1]:
                Path.Combine(Application.persistentDataPath,"Recordings",DateTime.Now.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture));
            var owner=new GameObject("Session recorder"); DontDestroyOnLoad(owner);
            owner.AddComponent<SessionRecorder>().Open(path);
        }

        void Open(string path)
        {
            Directory=path; System.IO.Directory.CreateDirectory(path);
            log=new SessionLog(new StreamWriter(Path.Combine(path,"session.jsonl"),false,new UTF8Encoding(false)));
            startedAt=windowStart=Time.realtimeSinceStartup; gcCount=GC.CollectionCount(0);
            var build=Resources.Load<TextAsset>("WildstoneBuild");
            log.Line("meta",0).Num("version",SessionLog.Version).Str("startedUtc",DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)).Str("build",build!=null?build.text.Trim():null)
                .Str("unity",Application.unityVersion).Str("gpu",SystemInfo.graphicsDeviceName).Str("cpu",SystemInfo.processorType)
                .Num("ramMB",SystemInfo.systemMemorySize).Str("screen",Screen.width+"x"+Screen.height)
                .Num("refreshHz",(float)Screen.currentResolution.refreshRateRatio.value).Str("quality",QualitySettings.names[QualitySettings.GetQualityLevel()])
                .Num("vsync",QualitySettings.vSyncCount).Num("targetFps",Application.targetFrameRate).End();
            Debug.Log("Wildstone session recording to "+path);
        }

        float Now => Time.realtimeSinceStartup-startedAt;

        void Update()
        {
            if(log==null) return;
            if(session==null)
            {
                session=FindFirstObjectByType<PracticeSession>();
                if(session==null) return;
                session.Stepped+=OnStepped; session.ScreenChanged+=OnScreen; session.SettingsChanged+=OnSettings;
                session.SessionStarted+=OnStarted; session.SessionStopped+=OnStopped;
                OnSettings(); OnScreen();
            }
            frames.Add(Time.unscaledDeltaTime*1000);
            if(Keyboard.current?.f8Key.wasPressedThisFrame==true)
            {
                marks++; log.Line("mark",Now).Num("n",marks).Num("tick",session.Match?.Tick??-1).Str("screen",session.Flow.Screen.ToString()).End();
                StartCoroutine(Capture(Path.Combine(Directory,"mark-"+marks+".png")));
            }
            if(Time.realtimeSinceStartup-windowStart>=1) WritePerf();
        }

        // Uses the already-enabled image conversion module (not the screen capture module) and encodes off the main
        // thread, so a mark does not add a frame spike to the data it marks.
        System.Collections.IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            int w=Screen.width, h=Screen.height;
            var texture=new Texture2D(w,h,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,w,h),0,0,false);
            var pixels=texture.GetRawTextureData(); Destroy(texture);
            var format=UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8_UNorm;
            System.Threading.Tasks.Task.Run(()=>{ try { File.WriteAllBytes(path,ImageConversion.EncodeArrayToPNG(pixels,format,(uint)w,(uint)h)); } catch(Exception ex) { Debug.LogWarning("Mark screenshot failed: "+ex.Message); } });
        }

        void WritePerf()
        {
            frames.Sort(); float sum=0, hitches=0, severe=0;
            foreach(var ms in frames) { sum+=ms; if(ms>25) hitches++; if(ms>50) severe++; }
            int gc=GC.CollectionCount(0);
            log.Line("perf",Now).Num("frames",frames.Count).Num("avgMs",frames.Count>0?sum/frames.Count:0)
                .Num("p95Ms",frames.Count>0?frames[(int)((frames.Count-1)*.95f)]:0).Num("maxMs",frames.Count>0?frames[frames.Count-1]:0)
                .Num("over25ms",hitches).Num("over50ms",severe).Num("gc",gc-gcCount).Num("monoMB",GC.GetTotalMemory(false)/1048576f)
                .Bool("playing",session!=null && session.Flow.Screen==MenuScreen.Playing).End();
            log.Flush(); frames.Clear(); gcCount=gc; windowStart=Time.realtimeSinceStartup;
        }

        void OnStepped()
        {
            var match=session.Match; if(match==null) return;
            states.Clear(); foreach(var pair in session.Motors) if(pair.Value!=null) states[pair.Key]=pair.Value.CaptureState();
            log.Tick(Now,match,session.CombatEvents,session.LocalParticipantId,states,session.ZoneCaptor,session.ZoneContested,session.LocalAdsProgress);
        }
        void OnScreen() => log?.Line("screen",Now).Str("screen",session.Flow.Screen.ToString()).End();
        void OnStarted() => log.Line("session",Now).Str("event","start").Str("map",session.MapId).Num("tickRate",session.Match?.TickRate??0)
            .Bool("online",session.ExternalSimulation).Num("local",session.LocalParticipantId).End();
        void OnStopped() => log.Line("session",Now).Str("event","stop").Num("tick",session.Match?.Tick??-1)
            .Str("phase",session.Match?.Phase.ToString()).Num("winner",session.Match?.Winner??-1).End();
        // Raw presses, so the report can compare what was pressed with what the game did. Applying settings replaces
        // PracticeInput (and disposes the old actions), so listen again each time.
        PracticeInput listened;
        void ListenToInput()
        {
            var input=session.Input; if(input==null || input==listened) return;
            listened=input;
            for(int i=0;i<input.Actions.Length;i++)
            {
                string name=LocalSettings.ActionNames[i];
                input.Actions[i].performed+=_=>{ if(log!=null && session.Flow.GameplayInput) log.Line("input",Now).Str("action",name).Num("tick",session.Match?.Tick??-1).End(); };
            }
        }
        void OnSettings()
        {
            ListenToInput();
            var s=session.Settings; if(s==null) return;
            log.Line("settings",Now).Num("sensitivity",s.Sensitivity).Num("fov",s.Fov).Bool("adsToggle",s.AdsToggle)
                .Bool("reducedMotion",s.ReducedMotion).Str("bindings",string.Join(" ",s.Bindings)).End();
        }

        void OnApplicationQuit() => Close("quit");
        void OnDestroy() => Close("destroyed");
        void Close(string reason)
        {
            if(log==null) return;
            WritePerf(); log.Line("end",Now).Str("reason",reason).Num("marks",marks).End(); log.Dispose(); log=null;
            if(session!=null) { session.Stepped-=OnStepped; session.ScreenChanged-=OnScreen; session.SettingsChanged-=OnSettings; session.SessionStarted-=OnStarted; session.SessionStopped-=OnStopped; }
        }
    }

    // Plain JSON-lines writer, separate from Unity hooks so tests can feed it a real ArenaMatch.
    public sealed class SessionLog : IDisposable
    {
        public const int Version=1;
        // 120 Hz ticks sampled every 6th tick = 20 state samples per second; combat and movement events are never sampled.
        public int SampleEvery=6;
        readonly TextWriter writer;
        readonly StringBuilder line=new StringBuilder(512);
        bool first;
        public SessionLog(TextWriter writer) { this.writer=writer; }

        public void Tick(float t,ArenaMatch match,IReadOnlyList<CombatEvent> events,int local,IReadOnlyDictionary<int,MotorState> motors,int captor,bool contested,float localAds)
        {
            int tick=match.Tick;
            foreach(var e in events)
            {
                Line("combat",t).Num("tick",tick).Str("kind",e.Kind.ToString()).Num("actor",e.Action.Participant).Num("seq",e.Action.Sequence).Str("action",e.Action.Kind.ToString())
                    .Num("target",e.Target).Str("weapon",e.Weapon==WeaponKind.None?null:e.Weapon.ToString()).Num("amount",e.Amount)
                    .Bool("head",e.Headshot).Str("reason",e.Reason).Vec("pos",e.Position).Vec("vec",e.Vector).End();
            }
            foreach(var pair in motors)
            {
                var s=pair.Value; if(s.Events==MotorEvents.None) continue;
                Line("move",t).Num("tick",tick).Num("who",pair.Key).Str("events",s.Events.ToString()).Num("speed",Flat(s.Velocity)).Vec("pos",s.Position).End();
            }
            if(tick%SampleEvery!=0) return;
            var p=match.Find(local); if(p==null) return;
            motors.TryGetValue(local,out var m);
            CombatState target=null; foreach(var other in match.Players) if(other.Participant!=local) { target=other; break; }
            Line("sample",t).Num("tick",tick).Vec("pos",m.Position).Vec("vel",m.Velocity).Num("speed",Flat(m.Velocity)).Num("vy",m.Velocity.y)
                .Num("yaw",m.Yaw).Num("pitch",m.Pitch).Bool("grounded",m.Grounded).Bool("sliding",m.Sliding).Bool("wallrun",m.WallRunning)
                .Bool("dashing",m.DashUntil>(m.Tick+1)*(double)MotorTuning.TickDelta).Num("stamina",m.Stamina).Str("held",m.Held.ToString())
                .Bool("alive",p.Alive).Num("hp",p.Health).Num("shield",p.Shield).Str("weapon",CombatRules.CurrentWeapon(p).ToString())
                .Num("ammo",p.SelectedSpecial?p.SpecialAmmo:p.StarterAmmo).Num("grenades",p.GrenadeAmmo).Num("blinks",p.BlinkCharges)
                .Num("shieldCharges",p.ShieldCharges).Bool("reloading",p.ReloadUntil>tick).Bool("stunned",tick<p.StunUntil)
                .Num("ads",p.AdsProgress).Num("adsLocal",localAds).Num("bloom",p.Bloom)
                .Vec("targetPos",target?.Feet??default).Num("targetHp",target?.Health??0).Bool("targetAlive",target?.Alive??false)
                .Vec("zone",match.ZonePosition).Num("captor",captor).Bool("contested",contested)
                .Num("score0",match.ScoreTicks.Count>0?match.Score(0):0).Num("score1",match.ScoreTicks.Count>1?match.Score(1):0).End();
        }
        static float Flat(Vector3 v) => new Vector2(v.x,v.z).magnitude;

        public SessionLog Line(string type,float t) { line.Clear(); line.Append('{'); first=true; return Str("type",type).Num("t",t); }
        public SessionLog Str(string key,string value) { if(value==null) return this; Key(key).Append('"'); Escape(value); line.Append('"'); return this; }
        public SessionLog Num(string key,float value) { Key(key); Append(value); return this; }
        public SessionLog Bool(string key,bool value) { Key(key).Append(value?"true":"false"); return this; }
        public SessionLog Vec(string key,Vector3 v) { Key(key).Append('['); Append(v.x); line.Append(','); Append(v.y); line.Append(','); Append(v.z); line.Append(']'); return this; }
        public void End() { line.Append('}'); writer.WriteLine(line.ToString()); }
        public void Flush() => writer.Flush();
        public void Dispose() => writer.Dispose();
        StringBuilder Key(string key) { if(!first) line.Append(','); first=false; return line.Append('"').Append(key).Append("\":"); }
        void Append(float value)
        {
            if(float.IsNaN(value) || float.IsInfinity(value)) line.Append("null");
            else line.Append(Math.Round(value,4).ToString(CultureInfo.InvariantCulture));
        }
        void Escape(string value)
        {
            foreach(char c in value)
            {
                if(c=='"' || c=='\\') line.Append('\\').Append(c);
                else if(c<' ') line.Append("\\u").Append(((int)c).ToString("x4"));
                else line.Append(c);
            }
        }
    }
}
