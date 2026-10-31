using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Wildstone.Game;
using Wildstone.Match;
using Wildstone.Presentation;

namespace Wildstone.UI
{
    [RequireComponent(typeof(UIDocument),typeof(PracticeSession))]
    public sealed class ArenaMenu : MonoBehaviour
    {
        PracticeSession session;
        UIDocument document;
        VisualElement root,card;
        ArenaHud hud;
        public ArenaHud Hud => hud;
        ArenaMatch hudMatch;
        MenuScreen renderedScreen;
        Label loading;
        LocalSettings draft;
        MenuScreen settingsReturn=MenuScreen.Title;
        InputActionRebindingExtensions.RebindingOperation rebind;
        string selectedMap="atrium";
        OnlineMenuBridge online;
        bool showingRecovery;
        int suppressBackFrame=-1;
        MenuBackdrop backdrop;
        ArenaMusic music;
        VisualElement curtain;
        static Texture2D ramp;
        static AudioClip click;
        static AudioClip[] clicks;
        void OnEnable()
        {
            session=GetComponent<PracticeSession>(); document=GetComponent<UIDocument>();
            // Runtime-added so the saved Bootstrap scene needs no rebuild; both degrade to nothing without content.
            backdrop=GetComponent<MenuBackdrop>(); if(backdrop==null) backdrop=gameObject.AddComponent<MenuBackdrop>();
            music=GetComponent<ArenaMusic>(); if(music==null) music=gameObject.AddComponent<ArenaMusic>();
            root=document.rootVisualElement; root.AddToClassList("arena-root");
            online=GetComponent<OnlineMenuBridge>(); if(online!=null) online.Changed+=OnlineChanged;
            var sheet=Resources.Load<StyleSheet>("ArenaMenu"); if(sheet!=null) root.styleSheets.Add(sheet);
            session.ScreenChanged+=Render; session.SettingsChanged+=SettingsChanged; session.Stepped+=OnStep;
            session.BackRequested+=Back; Render();
        }
        void OnDisable()
        {
            if(online!=null) online.Changed-=OnlineChanged;
            StopRebind(); session.ScreenChanged-=Render; session.SettingsChanged-=SettingsChanged; session.Stepped-=OnStep;
            session.BackRequested-=Back;
        }
        void OnlineChanged()
        {
            if(online.Recovering)
            {
                showingRecovery=true;
                if(session.Flow.Screen!=MenuScreen.Online) { session.Show(MenuScreen.Online); return; }
            }
            else if(showingRecovery)
            {
                showingRecovery=false;
                if(online.ActiveSession && !online.SessionLost && session.ExternalSimulation && session.Match!=null) { session.Show(MenuScreen.Playing); return; }
            }
            if(session.Flow.Screen==MenuScreen.Online) Render();
        }
        void OnStep() { hud?.Step(session); }
        AudioSource feedback;
        // One non-spatial voice for HUD hit confirmations; positional combat cues stay in ArenaAudio.
        AudioSource FeedbackSource()
        {
            if(feedback!=null) return feedback;
            var voice=new GameObject("HUD feedback audio",typeof(AudioSource)); voice.transform.SetParent(transform,false);
            feedback=voice.GetComponent<AudioSource>(); feedback.playOnAwake=false; feedback.spatialBlend=0; return feedback;
        }
        void SettingsChanged()
        {
            document.panelSettings.scale=session.Settings.UiScale;
            root.EnableInClassList("reduced-motion",session.Settings.ReducedMotion);
        }
        void Back()
        {
            if(rebind!=null || Time.frameCount<=suppressBackFrame) return;
            if(session.Flow.Screen==MenuScreen.Settings) session.Show(settingsReturn);
            else if(session.Flow.Screen==MenuScreen.Loading) CancelArenaLoad();
            else if(session.Flow.Screen==MenuScreen.Online) { online?.Exit(); session.Show(MenuScreen.Title); }
            else if(session.Flow.Screen==MenuScreen.Practice || session.Flow.Screen==MenuScreen.Failure || session.Flow.Screen==MenuScreen.Results)
                LeaveArena();
        }
        void LeaveArena() { if(online?.ActiveSession==true) online.Exit(); session.Leave(); }
        void CancelArenaLoad() { if(online?.ActiveSession==true) online.Exit(); session.CancelLoad(); }
        void Update()
        {
            hud?.Refresh(session);
            if(loading!=null) loading.text=$"Opening arena  {Mathf.RoundToInt(session.LoadProgress*100)}%";
        }
        void Heading(string eyebrow,string title,string description=null)
        {
            var small=new Label(eyebrow); small.AddToClassList("eyebrow"); card.Add(small);
            var main=new Label(title); main.AddToClassList("title"); card.Add(main);
            // The display face has no team symbols; titles that carry one keep the default face.
            if(title.IndexOfAny(TeamSymbols)<0) main.AddToClassList("display");
            if(description!=null) { var copy=new Label(description); copy.AddToClassList("description"); card.Add(copy); }
        }
        static readonly char[] TeamSymbols={'▲','■','◆','●'};
        // User-chosen game name (D038); two 64 px lines fit the 520 px column and keep the title menu unscrolled at 720p.
        public const string GameTitle="BETTER BEAN\nBATTLES";
        // vibegame1 title language: left-hung display title over an offset accent copy, a slab rule with a fading tail.
        void Hero()
        {
            var hero=new VisualElement(); hero.AddToClassList("hero"); card.Add(hero);
            var echo=new Label(GameTitle); echo.AddToClassList("hero-echo"); echo.pickingMode=PickingMode.Ignore; hero.Add(echo);
            var title=new Label(GameTitle); title.AddToClassList("hero-title"); hero.Add(title);
            var rule=new VisualElement(); rule.AddToClassList("hero-rule"); card.Add(rule);
            var slab=new VisualElement(); slab.AddToClassList("hero-rule__slab"); rule.Add(slab);
            var tail=new VisualElement(); tail.AddToClassList("hero-rule__tail"); tail.style.backgroundImage=new StyleBackground(Ramp()); rule.Add(tail);
            var sub=new Label("MAKE YOUR MOVE"); sub.AddToClassList("hero-subtitle"); card.Add(sub);
            var copy=new Label("Clean lines. Fast movement. Hold the moving zone."); copy.AddToClassList("description"); card.Add(copy);
        }
        Button Button(string text,Action clicked,bool primary=false)
        {
            var button=new Button(()=>{Click();clicked();}){text=text,name="menu-"+text}; button.AddToClassList(primary?"primary":"secondary"); card.Add(button); return button;
        }
        // vibegame1 UiSprites.RampH, generated at runtime: white, alpha from MenuMotion.RampAlpha; USS tints it.
        static Texture2D Ramp()
        {
            if(ramp!=null) return ramp;
            ramp=new Texture2D(256,1,TextureFormat.RGBA32,false){name="Wildstone menu ramp",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.DontSave};
            var pixels=new Color32[256];
            for(int x=0;x<pixels.Length;x++) pixels[x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(255*MenuMotion.RampAlpha(x/255f)));
            ramp.SetPixels32(pixels); ramp.Apply(false,true); return ramp;
        }
        // UI click on activation: the CC0 Kenney click bank shared with vibegame1, else a soft synthesized tick.
        void Click()
        {
            if(session==null || session.Settings==null || session.Settings.EffectsVolume<=0) return;
            clicks=clicks??Resources.LoadAll<AudioClip>(ArenaAudio.Folder+"Click");
            if(clicks.Length>0) { FeedbackSource().PlayOneShot(clicks[UnityEngine.Random.Range(0,clicks.Length)],session.Settings.EffectsVolume*.6f); return; }
            if(click==null)
            {
                const int rate=22050; var data=new float[rate*45/1000];
                for(int i=0;i<data.Length;i++) { float t=i/(float)rate; data[i]=(Mathf.Sin(2*Mathf.PI*1100*t)*.7f+Mathf.Sin(2*Mathf.PI*2200*t)*.3f)*Mathf.Exp(-t*110)*.6f; }
                click=AudioClip.Create("Wildstone menu click",data.Length,1,rate,false); click.SetData(data,0);
            }
            FeedbackSource().PlayOneShot(click,session.Settings.EffectsVolume*.35f);
        }
        // CloudDive port: the column whips out, the camera pushes in and the curtain closes before the arena loads.
        void Dive(Action then)
        {
            var leaving=card;
            if(leaving==null || leaving.panel==null) { then(); return; }
            leaving.SetEnabled(false); leaving.AddToClassList("menu-card--exit");
            curtain?.AddToClassList("menu-curtain--dive"); curtain?.AddToClassList("menu-curtain--closed");
            backdrop?.Dive();
            float seconds=session.Settings!=null && session.Settings.ReducedMotion?MenuMotion.StillSeconds:MenuMotion.DiveSeconds;
            // A re-render (Escape, recovery) replaces the card, which cancels this hand-over.
            leaving.schedule.Execute(()=>{ if(card==leaving) then(); }).ExecuteLater((long)(seconds*1000));
        }
        // One persistent ivory curtain under the menu: closed while an arena loads, opening into the next screen.
        void Curtain(MenuScreen screen)
        {
            if(curtain==null) { curtain=new VisualElement{name="menu-curtain",pickingMode=PickingMode.Ignore}; curtain.AddToClassList("menu-curtain"); }
            if(curtain.parent!=root) root.Insert(0,curtain);
            curtain.RemoveFromClassList("menu-curtain--dive");
            curtain.EnableInClassList("menu-curtain--closed",screen==MenuScreen.Loading);
        }
        void OpenSettings() { settingsReturn=session.Flow.Screen; draft=session.Settings.Copy(); session.Show(MenuScreen.Settings); }
        void Render()
        {
            if(online?.Recovering==true && session.Flow.Screen!=MenuScreen.Online)
            { showingRecovery=true; session.Show(MenuScreen.Online); return; }
            var screen=session.Flow.Screen;
            bool entering=screen!=renderedScreen;
            string focusKey=screen==renderedScreen?FocusKey(root.panel?.focusController.focusedElement as VisualElement,card):null;
            renderedScreen=screen;
            StopRebind(); loading=null;
            // Keep the curtain attached so its fade survives re-renders; everything else is rebuilt.
            for(int i=root.childCount-1;i>=0;i--) if(root[i]!=curtain) root.RemoveAt(i);
            card=null; Curtain(screen);
            if(session.Match!=null)
            {
                if(hud==null || !ReferenceEquals(hudMatch,session.Match)) { hud=new ArenaHud { Feedback=FeedbackSource() }; hudMatch=session.Match; }
                root.Add(hud.Root);
                hud.Root.style.display=screen==MenuScreen.Playing?DisplayStyle.Flex:DisplayStyle.None;
            }
            else { hud=null; hudMatch=null; }
            if(screen==MenuScreen.Playing) return;
            // Over the live backdrop only a left ramp dims the world; over a match a light flat dim is added.
            var shade=new VisualElement(); shade.AddToClassList("menu-shade"); shade.EnableInClassList("menu-shade--over-match",session.Match!=null); root.Add(shade);
            var scrim=new VisualElement{pickingMode=PickingMode.Ignore}; scrim.AddToClassList("menu-scrim"); scrim.style.backgroundImage=new StyleBackground(Ramp()); shade.Add(scrim);
            var scroll=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden}; scroll.AddToClassList("menu-scroll"); shade.Add(scroll);
            card=new VisualElement(); card.AddToClassList("menu-card"); scroll.Add(card);
            if(entering)
            {
                // Screen change: the column slides in from the margin (interruptible; reduced motion keeps only the fade).
                var entered=card; entered.AddToClassList("menu-card--enter");
                entered.RegisterCallbackOnce<GeometryChangedEvent>(_=>entered.RemoveFromClassList("menu-card--enter"));
            }
            switch(screen)
            {
                case MenuScreen.Title:
                    Hero();
                    Button("Play online",()=>session.Show(MenuScreen.Online),true);
                    Button("Practice",()=>session.Show(MenuScreen.Practice)); Button("Settings",OpenSettings); Button("Quit",Application.Quit);
                    Note(online?.Available==true?"Private matches · Offline Practice ready":"Offline Practice ready"); break;
                case MenuScreen.Online: Online(); break;
                case MenuScreen.Practice:
                    Heading("OFFLINE PRACTICE","Choose your arena","Explore movement, collect weapons and hold the zone. Blink pickups teleport you 6 m along your aim onto open floor. One strafing target fixture; no online players.");
                    if(session.Catalog!=null) foreach(var map in session.Catalog.Maps)
                    { string id=map.Id; Button(map.DisplayName,()=>Dive(()=>{selectedMap=id;session.Load(id);}),true); }
                    else Note("Arena content is unavailable. Return and retry loading.");
                    Button("Back",()=>session.Show(MenuScreen.Title)); break;
                case MenuScreen.Loading:
                    Heading("ONE MOMENT","Entering Better Bean Battles"); loading=new Label(); card.Add(loading); Button("Cancel",CancelArenaLoad); break;
                case MenuScreen.Pause:
                    Heading(session.ExternalSimulation?"ONLINE MATCH":"OFFLINE PRACTICE","Take a breath.","The arena keeps moving while this menu is open.");
                    Button("Resume",()=>session.Show(MenuScreen.Playing),true); Button("Settings & controls",OpenSettings);
                    if(!session.ExternalSimulation) Button("Restart arena",session.Rematch);
                    Button(session.ExternalSimulation?"Leave match":"Leave practice",LeaveArena); break;
                case MenuScreen.Settings: SettingsPanel(); break;
                case MenuScreen.Results:
                    var match=session.Match;
                    Heading(session.ExternalSimulation?"MATCH RESULTS":"PRACTICE RESULTS",match?.Phase==ArenaMatchPhase.Aborted?"Round ended":match?.Winner<0?"Draw":ArenaHud.Team(match?.Winner??0)+" wins");
                    if(match!=null) for(int i=0;i<4;i++) Note(ArenaHud.Team(i)+$"   {match.Score(i):0.0} seconds");
                    if(!session.ExternalSimulation)
                    { Button("Play again",session.Rematch,true); Button("Choose arena",()=>{session.Leave();session.Show(MenuScreen.Practice);}); }
                    // Online rematch belongs to the host of a live session; an aborted round has already closed it.
                    else if(online?.ActiveSession==true && match?.Phase==ArenaMatchPhase.Finished)
                    { if(online.Host) Button("Rematch",online.Rematch,true); else Note("Waiting for the host to start a rematch."); }
                    Button(online?.ActiveSession==true?"Leave match":"Back to title",LeaveArena); break;
                case MenuScreen.Failure:
                    Heading("LET’S TRY AGAIN","Arena unavailable",session.Failure);
                    if(online?.ActiveSession!=true) Button("Retry",()=>session.Load(selectedMap),true);
                    Button("Back to title",LeaveArena); break;
            }
            var focus=FocusTarget(card,screen,focusKey);
            focus?.schedule.Execute(()=>focus.Focus());
        }
        static string FocusKey(VisualElement focused,VisualElement boundary)
        {
            for(var element=focused;element!=null && element!=boundary;element=element.parent)
                if(element.name!=null && element.name.StartsWith("menu-",StringComparison.Ordinal)) return element.name;
            return null;
        }
        static VisualElement FocusTarget(VisualElement card,MenuScreen screen,string previous)
        {
            return (previous==null?null:card.Q<VisualElement>(previous))??
                (screen==MenuScreen.Settings?card.Q<Slider>():null)??(VisualElement)card.Q<Button>();
        }
        void Note(string text) { var label=new Label(text); label.AddToClassList("note"); card.Add(label); }
        void Online()
        {
            Heading("PLAY TOGETHER",online?.Recovering==true?"Recovering match":online?.InLobby==true?"Private lobby":"Private matches");
            Note(online?.Status??"Online setup is unavailable. Practice is ready.");
            if(online?.Available==true && !online.Busy)
            {
                if(online.Recovering)
                {
                    Button("Leave match",online.Exit);
                }
                else if(online.SessionLost)
                {
                    if(online.CanReconnect) Button("Reconnect to previous match",online.RetryConnection,true);
                    Button("Leave previous match",online.Exit);
                }
                else if(online.InLobby)
                {
                    Note("ROOM  "+online.Room); Button("Copy room code",()=>GUIUtility.systemCopyBuffer=online.Room);
                    Note(online.Roster);
                    for(int i=0;i<4;i++) { int team=i; Button("Join "+ArenaHud.Team(i),()=>online.SelectTeam(team)); }
                    Button(online.Ready?"Not ready":"Ready",online.ToggleReady,true).name="menu-ready";
                    if(online.Host) Button("Start match",online.StartGame);
                    Note("Choose two or more teams. Everyone must be ready before the host starts.");
                    Button("Leave lobby",online.Exit);
                }
                else
                {
                    if(session.Catalog!=null)
                    {
                        var names=new List<string>(); var ids=new List<string>();
                        foreach(var map in session.Catalog.Maps) { names.Add(map.DisplayName); ids.Add(map.Id); }
                        if(ids.Count>0)
                        {
                            int index=Math.Max(0,ids.IndexOf(online.Map));
                            var maps=new DropdownField("Arena (must match host)",names,index){name="menu-map"}; maps.AddToClassList("setting");
                            maps.RegisterValueChangedCallback(evt=>online.Map=ids[maps.index]); card.Add(maps);
                        }
                    }
                    Button("Host a match",online.HostRoom,true);
                    var code=Field("Join code",12); var feedback=new Label(); card.Add(feedback);
                    Button("Join",()=>{if(MenuFlow.ValidJoinCode(code.value))online.Connect(code.value.Trim().ToUpperInvariant());else feedback.text="Enter a code with 4–12 letters or numbers.";});
                }
            }
            if(online?.Busy==true) Button("Cancel",online.Exit);
            Button("Practice",()=>{online?.Exit();session.Show(MenuScreen.Practice);});
            Button("Back",()=>{online?.Exit();session.Show(MenuScreen.Title);});
        }
        TextField Field(string label,int maximum)
        {
            var field=new TextField(label){maxLength=maximum,name="menu-"+label}; field.AddToClassList("setting"); card.Add(field); return field;
        }
        void Slider(string label,float low,float high,float current,Action<float> change)
        {
            var field=new Slider(label,low,high){value=current,showInputField=true,name="menu-"+label}; field.AddToClassList("setting"); field.RegisterValueChangedCallback(evt=>change(evt.newValue)); card.Add(field);
        }
        void Toggle(string label,bool current,Action<bool> change)
        {
            var field=new Toggle(label){value=current,name="menu-"+label}; field.AddToClassList("setting"); field.RegisterValueChangedCallback(evt=>change(evt.newValue)); card.Add(field);
        }
        void SettingsPanel()
        {
            draft=draft??session.Settings.Copy();
            Heading("YOUR SPACE","Settings","Changes apply when you choose Apply. Escape cancels.");
            Slider("Mouse sensitivity",.01f,1,draft.Sensitivity,x=>draft.Sensitivity=x);
            Slider("Field of view (horizontal)",LocalSettings.MinimumFov,LocalSettings.MaximumFov,draft.Fov,x=>draft.Fov=x);
            Slider("Aim sensitivity (ADS)",.2f,2,draft.AdsSensitivity,x=>draft.AdsSensitivity=x); Toggle("Toggle aim (ADS)",draft.AdsToggle,x=>draft.AdsToggle=x);
            Slider("Master volume",0,1,draft.MasterVolume,x=>draft.MasterVolume=x);
            if(music!=null && music.HasMusic) Slider("Music volume",0,1,draft.MusicVolume,x=>draft.MusicVolume=x);
            Slider("Effects volume",0,1,draft.EffectsVolume,x=>draft.EffectsVolume=x);
            var resolutions=new List<string>{"1280 × 720","1600 × 900","1920 × 1080","2560 × 1440","3440 × 1440","3840 × 2160"};
            string current=$"{draft.Width} × {draft.Height}"; if(!resolutions.Contains(current)) resolutions.Add(current);
            var resolution=new DropdownField("Resolution",resolutions,resolutions.IndexOf(current)){name="menu-resolution"}; resolution.AddToClassList("setting");
            resolution.RegisterValueChangedCallback(evt=>{var parts=evt.newValue.Split('×');draft.Width=int.Parse(parts[0]);draft.Height=int.Parse(parts[1]);}); card.Add(resolution);
            Toggle("Fullscreen",draft.Fullscreen,x=>draft.Fullscreen=x); Toggle("VSync",draft.VSync,x=>draft.VSync=x);
            Slider("Frame limit",30,360,draft.FpsCap,x=>draft.FpsCap=(int)x);
            var qualityNames=new List<string>(QualitySettings.names); draft.Normalize(qualityNames.Count);
            if(qualityNames.Count>0)
            {
                var quality=new DropdownField("Quality",qualityNames,draft.Quality){name="menu-quality"}; quality.AddToClassList("setting");
                quality.RegisterValueChangedCallback(evt=>draft.Quality=quality.index); card.Add(quality);
            }
            Slider("UI scale",.8f,1.4f,draft.UiScale,x=>draft.UiScale=x); Toggle("Reduce motion",draft.ReducedMotion,x=>draft.ReducedMotion=x);
            Note("CONTROLS · Select a binding, then press a key or mouse button. Escape cancels rebinding.");
            for(int i=0;i<LocalSettings.ActionNames.Length;i++)
            {
                int index=i; Button binding=null;
                binding=Button(LocalSettings.ActionNames[i]+"   "+LocalSettings.BindingLabel(draft.Bindings[i]),()=>Rebind(index,binding));
            }
            Button("Apply",()=>{StopRebind();session.ApplySettings(draft);session.Show(settingsReturn);},true);
            Button("Cancel",()=>session.Show(settingsReturn)); Button("Restore defaults",()=>{draft=new LocalSettings();Render();});
        }
        void Rebind(int index,Button button)
        {
            StopRebind(); var action=session.Input.Actions[index]; action.Disable(); button.text="Press a key or mouse button…";
            rebind=action.PerformInteractiveRebinding().WithControlsHavingToMatchPath("<Keyboard>/*").WithControlsHavingToMatchPath("<Mouse>/*")
                .WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough("<Keyboard>/escape").OnCancel(op=>{suppressBackFrame=Time.frameCount+1;FinishRebind(index,button);})
                .OnComplete(op=>{var path=action.bindings[0].effectivePath;if(LocalSettings.ValidBinding(path))draft.Bindings[index]=path;FinishRebind(index,button);});
            rebind.Start();
        }
        void FinishRebind(int index,Button button)
        {
            var action=session.Input.Actions[index]; action.RemoveAllBindingOverrides(); action.Enable();
            rebind?.Dispose(); rebind=null; button.text=LocalSettings.ActionNames[index]+"   "+LocalSettings.BindingLabel(draft.Bindings[index]);
        }
        void StopRebind() { if(rebind!=null) { rebind.Cancel(); rebind?.Dispose(); rebind=null; } }
    }
}
