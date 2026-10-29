using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Wildstone.Game;
using Wildstone.Match;

namespace Wildstone.UI
{
    [RequireComponent(typeof(UIDocument),typeof(PracticeSession))]
    public sealed class ArenaMenu : MonoBehaviour
    {
        PracticeSession session;
        UIDocument document;
        VisualElement root,card;
        ArenaHud hud;
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
        void OnEnable()
        {
            session=GetComponent<PracticeSession>(); document=GetComponent<UIDocument>();
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
            if(description!=null) { var copy=new Label(description); copy.AddToClassList("description"); card.Add(copy); }
        }
        Button Button(string text,Action clicked,bool primary=false)
        {
            var button=new Button(clicked){text=text,name="menu-"+text}; button.AddToClassList(primary?"primary":"secondary"); card.Add(button); return button;
        }
        void OpenSettings() { settingsReturn=session.Flow.Screen; draft=session.Settings.Copy(); session.Show(MenuScreen.Settings); }
        void Render()
        {
            if(online?.Recovering==true && session.Flow.Screen!=MenuScreen.Online)
            { showingRecovery=true; session.Show(MenuScreen.Online); return; }
            var screen=session.Flow.Screen;
            string focusKey=screen==renderedScreen?FocusKey(root.panel?.focusController.focusedElement as VisualElement,card):null;
            renderedScreen=screen;
            StopRebind(); root.Clear(); loading=null;
            if(session.Match!=null)
            {
                if(hud==null || !ReferenceEquals(hudMatch,session.Match)) { hud=new ArenaHud(); hudMatch=session.Match; }
                root.Add(hud.Root);
                hud.Root.style.display=screen==MenuScreen.Playing?DisplayStyle.Flex:DisplayStyle.None;
            }
            else { hud=null; hudMatch=null; }
            if(screen==MenuScreen.Playing) return;
            var shade=new VisualElement(); shade.AddToClassList("menu-shade"); root.Add(shade);
            var scroll=new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("menu-scroll"); shade.Add(scroll);
            card=new VisualElement(); card.AddToClassList("menu-card"); scroll.Add(card);
            switch(screen)
            {
                case MenuScreen.Title:
                    Heading("WILDSTONE / ARENA","Make your move.","Clean lines. Fast movement. Hold the moving zone.");
                    Button("Play online",()=>session.Show(MenuScreen.Online),true);
                    Button("Practice",()=>session.Show(MenuScreen.Practice)); Button("Settings",OpenSettings); Button("Quit",Application.Quit);
                    Note(online?.Available==true?"Private matches · Offline Practice ready":"Offline Practice ready"); break;
                case MenuScreen.Online: Online(); break;
                case MenuScreen.Practice:
                    Heading("OFFLINE PRACTICE","Choose your arena","Explore movement, collect weapons and hold the zone. One stationary target fixture; no online players.");
                    if(session.Catalog!=null) foreach(var map in session.Catalog.Maps)
                    { string id=map.Id; Button(map.DisplayName,()=>{selectedMap=id;session.Load(id);},true); }
                    else Note("Arena content is unavailable. Return and retry loading.");
                    Button("Back",()=>session.Show(MenuScreen.Title)); break;
                case MenuScreen.Loading:
                    Heading("ONE MOMENT","Entering Wildstone"); loading=new Label(); card.Add(loading); Button("Cancel",CancelArenaLoad); break;
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
                    Button("Back to title",LeaveArena); break;
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
            Slider("Mouse sensitivity",.01f,1,draft.Sensitivity,x=>draft.Sensitivity=x); Slider("Field of view",60,110,draft.Fov,x=>draft.Fov=x);
            Slider("Master volume",0,1,draft.MasterVolume,x=>draft.MasterVolume=x); Slider("Effects volume",0,1,draft.EffectsVolume,x=>draft.EffectsVolume=x);
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
