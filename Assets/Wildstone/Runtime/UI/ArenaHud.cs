using System;
using UnityEngine;
using UnityEngine.UIElements;
using Wildstone.Combat;
using Wildstone.Game;
using Wildstone.Presentation;

namespace Wildstone.UI
{
    // Combat HUD: team score strip, clock and zone pointer on top; vitals bottom-left; weapon, ammo and items bottom-right.
    // Hit feedback reacts only to host-confirmed events; nothing here changes simulation.
    public sealed class ArenaHud
    {
        public enum Marker { None, Body, Shield, Head, Kill }
        public readonly VisualElement Root=new VisualElement();
        readonly Label time=new Label(),mode=new Label(),zoneArrow=new Label("▲"),zoneText=new Label(),health=new Label(),shieldValue=new Label(),
            weaponName=new Label(),ammo=new Label(),ammoTag=new Label(),prompt=new Label(),hint=new Label(),respawn=new Label(),cue=new Label(),feed=new Label(),banner=new Label();
        readonly Label[] scoreValues=new Label[4];
        readonly VisualElement[] chips=new VisualElement[4],scoreFills=new VisualElement[4];
        readonly VisualElement vitals=new VisualElement(),weaponPanel=new VisualElement(),shieldBar,reloadBar,shieldFill,healthFill,staminaFill,reloadFill;
        readonly VisualElement[] items=new VisualElement[3];
        readonly Label[] itemKeys=new Label[3],itemLabels=new Label[3];
        readonly VisualElement crosshair=new VisualElement(),dot=new VisualElement(),scope=new VisualElement(),scopeRing=new VisualElement(),
            scopeH=new VisualElement(),scopeV=new VisualElement(),vignette=new VisualElement(),damage=new VisualElement();
        readonly VisualElement[] ticks=new VisualElement[4];
        readonly PresentationEventHistory history=new PresentationEventHistory();
        readonly int[] lastHitter=new int[9],lastHitTick=new int[9];
        readonly bool[] lastHitHead=new bool[9];
        readonly string[] feedLines=new string[4];
        readonly float[] feedUntil=new float[4];
        // Assigned by the menu: a 2D source for the head "ding" and kill confirmation.
        public AudioSource Feedback;
        public Marker LastMarker { get; private set; }
        public float CrosshairGap { get; private set; }
        public bool ScopeVisible => scope.style.display.value==DisplayStyle.Flex;
        static AudioClip headClip,killClip,tickClip,dryClip;
        public const float TickScale=.45f; // body/shield ticks sit under the head and kill confirmations
        public const float DryScale=.35f;
        WeaponKind lastWeapon; int lastRounds=-1;
        // The last quarter of a magazine reads in the warning colour and clicks dry on each shot.
        public static bool LowAmmo(WeaponKind weapon,int rounds)
        {
            int capacity=weapon==WeaponKind.None?0:WeaponCatalog.Get(weapon).Ammo;
            return capacity>0 && rounds*4<=capacity;
        }
        float cueUntil,damageUntil,soundedAt=-1,bannerUntil,lastKillAt=float.MinValue;
        // Your kill names the victim under the crosshair; kills within StreakSeconds of the last one count up.
        public const float StreakSeconds=4,BannerSeconds=1.8f;
        public int KillStreak { get; private set; }
        public string BannerText => banner.text;
        Marker sounded;
        public int CuesSounded { get; private set; }
        Vector3 damageFrom;
        int ownTeam=-1;
        // Names match the soldier accent colours (SoldierRig.TeamAccents); the symbol carries identity without colour.
        static readonly string[] Teams={"▲ TEAL","■ GOLD","◆ CRIMSON","● VIOLET"};
        // Interface tints of the accents: crimson and violet are lifted so they read on graphite panels.
        public static readonly Color[] TeamTints={new Color(.16f,.73f,.78f),new Color(.98f,.92f,.50f),new Color(.88f,.26f,.32f),new Color(.66f,.50f,.95f)};
        // Zone seconds that win the round (ArenaMatch.ScoreZone); the score strip fills toward it.
        public const float WinSeconds=120;
        static readonly Color Ivory=new Color(.96f,.94f,.90f,.92f),Warning=new Color(.95f,.42f,.34f),ShieldBlue=new Color(.52f,.80f,.95f);
        public ArenaHud()
        {
            Root.pickingMode=PickingMode.Ignore;
            Fill(Root);
            vignette.pickingMode=PickingMode.Ignore; Fill(vignette); vignette.style.display=DisplayStyle.None; Root.Add(vignette);
            scope.pickingMode=PickingMode.Ignore; Fill(scope); scope.style.display=DisplayStyle.None; Root.Add(scope);
            foreach(var part in new[]{scopeRing,scopeH,scopeV}) { part.pickingMode=PickingMode.Ignore; part.style.position=Position.Absolute; scope.Add(part); }
            scopeRing.style.borderTopColor=scopeRing.style.borderBottomColor=scopeRing.style.borderLeftColor=scopeRing.style.borderRightColor=new Color(.03f,.04f,.035f,1);
            scopeH.style.backgroundColor=scopeV.style.backgroundColor=new Color(.05f,.06f,.05f,.85f);
            var frame=Add(Root,"hud");

            var board=Add(frame,"hud-scoreboard");
            for(int i=0;i<4;i++)
            {
                if(i==2) { var clock=Add(board,"hud-clock"); clock.Add(Style(time,"hud-clock__time")); clock.Add(Style(mode,"hud-clock__mode")); }
                var chip=chips[i]=Add(board,"hud-chip");
                chip.Add(Style(new Label(Teams[i].Substring(0,1)),"hud-chip__symbol"));
                chip.Add(Style(scoreValues[i]=new Label(),"hud-chip__score"));
                Add(chip,"hud-chip__track").style.backgroundColor=TeamTints[i];
                scoreFills[i]=Add(chip,"hud-chip__fill"); scoreFills[i].style.backgroundColor=TeamTints[i];
            }
            var zone=Add(frame,"hud-zone"); zone.Add(Style(zoneArrow,"hud-zone__arrow")); zone.Add(Style(zoneText,"hud-zone__text"));

            // The hint and ability chips stack on top of the vitals panel, clear of the scores and the held weapon.
            var left=Add(frame,"hud-left"); left.Add(Style(hint,"hud-hint")); var itemRow=Add(left,"hud-items");
            for(int i=0;i<3;i++)
            {
                items[i]=Add(itemRow,"hud-item");
                items[i].Add(Style(itemKeys[i]=new Label(),"hud-key")); items[i].Add(Style(itemLabels[i]=new Label(),"hud-item__label"));
            }
            vitals.AddToClassList("hud-vitals"); left.Add(vitals);
            var shieldRow=Add(vitals,"hud-vitals__row"); shieldRow.Add(Style(shieldValue,"hud-shield"));
            shieldBar=Bar(vitals,"hud-bar--shield",out shieldFill);
            var healthRow=Add(vitals,"hud-vitals__row"); healthRow.Add(Style(health,"hud-health")); healthRow.Add(Style(new Label("HEALTH"),"hud-unit"));
            Bar(vitals,"hud-bar--health",out healthFill);
            Bar(vitals,"hud-bar--stamina",out staminaFill);

            weaponPanel.AddToClassList("hud-weapon"); frame.Add(weaponPanel);
            weaponPanel.Add(Style(weaponName,"hud-weapon__name"));
            var ammoRow=Add(weaponPanel,"hud-weapon__row"); ammoRow.Add(Style(ammo,"hud-ammo")); ammoRow.Add(Style(ammoTag,"hud-ammo__tag"));
            reloadBar=Bar(weaponPanel,"hud-bar--reload",out reloadFill);
            frame.Add(Style(prompt,"hud-prompt"));
            frame.Add(Style(respawn,"hud-respawn"));

            feed.name="hud-kill-feed"; feed.style.position=Position.Absolute; feed.style.right=Length.Percent(3); feed.style.top=Length.Percent(15);
            feed.style.fontSize=14; feed.style.unityTextAlign=TextAnchor.UpperRight; feed.style.color=Ivory; Root.Add(feed);
            Center(crosshair); crosshair.name="hud-crosshair"; Root.Add(crosshair);
            // A 2 x 8 ivory core inside a 1 px dark rim (border-box): ivory alone vanished against ivory stone.
            for(int i=0;i<4;i++)
            {
                var tick=ticks[i]=new VisualElement(); tick.style.position=Position.Absolute; tick.style.backgroundColor=Ivory;
                tick.style.width=i<2?4:10; tick.style.height=i<2?10:4; Rim(tick); crosshair.Add(tick);
            }
            dot.style.position=Position.Absolute; dot.style.width=dot.style.height=4; dot.style.left=dot.style.top=-2; dot.style.backgroundColor=Ivory; Rim(dot); crosshair.Add(dot);
            Center(damage); damage.name="hud-damage-direction"; damage.style.display=DisplayStyle.None; Root.Add(damage);
            var wedge=new VisualElement(); wedge.style.position=Position.Absolute; wedge.style.width=56; wedge.style.height=7; wedge.style.left=-28; wedge.style.top=-120;
            wedge.style.backgroundColor=new Color(.86f,.30f,.22f,.85f);
            wedge.style.borderTopLeftRadius=wedge.style.borderTopRightRadius=wedge.style.borderBottomLeftRadius=wedge.style.borderBottomRightRadius=4; damage.Add(wedge);
            // The hitmarker keeps the original hidden cue element; variants change glyph size and color.
            cue.name="hud-hit-cue"; cue.AddToClassList("hit-cue"); cue.text="×"; cue.pickingMode=PickingMode.Ignore;
            cue.style.position=Position.Absolute; cue.style.left=cue.style.top=Length.Percent(50); cue.style.width=cue.style.height=48;
            cue.style.marginLeft=cue.style.marginTop=-24; cue.style.unityTextAlign=TextAnchor.MiddleCenter; cue.style.display=DisplayStyle.None; Root.Add(cue);
            cue.style.unityTextOutlineWidth=1.5f; cue.style.unityTextOutlineColor=RimColor;
            // A graphite pill like the other HUD panels: coloured text alone was muddy on the ivory floor.
            banner.name="hud-kill-banner"; Style(banner,"hud-kill-banner"); banner.style.display=DisplayStyle.None; Root.Add(banner);
        }
        // The streak word takes the kill-mark red; the headshot is spelled out (a ◎ read as the violet team's ●).
        public static string KillBanner(int streak,string victim,bool head)
        {
            string count=streak<=1?"":"<color=#EB5440>"+(streak==2?"DOUBLE":streak==3?"TRIPLE":"×"+streak)+"</color>  ·  ";
            return count+"ELIMINATED  "+victim+(head?"  ·  HEADSHOT":"");
        }
        // The dark edge on reticle elements, the soldier outline's near-black.
        static readonly Color RimColor=new Color(.05f,.055f,.065f,.8f);
        static void Rim(VisualElement element)
        {
            element.style.borderTopWidth=element.style.borderBottomWidth=element.style.borderLeftWidth=element.style.borderRightWidth=1;
            element.style.borderTopColor=element.style.borderBottomColor=element.style.borderLeftColor=element.style.borderRightColor=RimColor;
        }
        static VisualElement Add(VisualElement parent,string style)
        { var element=new VisualElement(); element.AddToClassList(style); element.pickingMode=PickingMode.Ignore; parent.Add(element); return element; }
        static Label Style(Label label,string style) { label.AddToClassList(style); label.pickingMode=PickingMode.Ignore; return label; }
        static VisualElement Bar(VisualElement parent,string variant,out VisualElement fill)
        { var bar=Add(parent,"hud-bar"); bar.AddToClassList(variant); fill=Add(bar,"hud-bar__fill"); return bar; }
        static void SetFill(VisualElement fill,float fraction) => fill.style.width=Length.Percent(100*Mathf.Clamp01(fraction));
        static void Show(VisualElement element,bool shown) => element.style.display=shown?DisplayStyle.Flex:DisplayStyle.None;
        static void Fill(VisualElement element)
        { element.style.position=Position.Absolute; element.style.left=element.style.top=element.style.right=element.style.bottom=0; }
        static void Center(VisualElement element)
        { element.pickingMode=PickingMode.Ignore; element.style.position=Position.Absolute; element.style.left=element.style.top=Length.Percent(50); element.style.width=element.style.height=0; }
        public static string Team(int team) => Teams[Math.Max(0,Math.Min(3,team))];
        public static string WeaponName(WeaponKind kind)
        {
            switch(kind) { case WeaponKind.LineDriver:return "Line Driver"; case WeaponKind.PulseLobber:return "Pulse Lobber"; case WeaponKind.ReboundDisc:return "Rebound Disc"; case WeaponKind.PushLens:return "Push Lens"; case WeaponKind.StunGun:return "Stun Gun"; case WeaponKind.LaserSword:return "Laser Sword"; case WeaponKind.LaserPistol:return "Laser Pistol"; case WeaponKind.LaserShotgun:return "Laser Shotgun"; case WeaponKind.LaserSniper:return "Laser Sniper"; case WeaponKind.LaserSMG:return "Laser SMG"; default:return kind.ToString(); }
        }
        // Cone half-angle to panel units at the camera's current vertical FOV.
        public static float SpreadPixels(float spreadDegrees,float verticalFov,float panelHeight)
        {
            if(!(verticalFov>0 && verticalFov<180) || !(panelHeight>0) || !(spreadDegrees>0)) return 0;
            return Mathf.Tan(spreadDegrees*Mathf.Deg2Rad)/Mathf.Tan(verticalFov*.5f*Mathf.Deg2Rad)*panelHeight*.5f;
        }
        public void Step(PracticeSession session)
        {
            var match=session.Match; int local=session.LocalParticipantId; float now=Time.unscaledTime;
            for(int i=0;i<session.CombatEvents.Count;i++)
            {
                var e=session.CombatEvents[i];
                if(!history.Accept(e)) continue;
                int attacker=e.Action.Participant, target=e.Target;
                if(e.Kind==CombatEventKind.Hit && target>=1 && target<=8)
                {
                    RecordHit(e);
                    if(attacker==local && target!=local) Mark(e.Headshot?Marker.Head:Marker.Body,now);
                    if(target==local && attacker!=local && attacker>0)
                    { var source=match?.Find(attacker); damageFrom=source!=null?source.Eye:e.Position; damageUntil=now+1; }
                }
                else if(e.Kind==CombatEventKind.ShieldBroken && attacker==local && target!=local) Mark(Marker.Shield,now);
                else if(e.Kind==CombatEventKind.Death && target>=1 && target<=8)
                {
                    int killer=Killer(target,e.Tick,out bool head);
                    AddFeed(match,killer,target,killer!=0 && head,now);
                    if(killer==local && target!=local) { Mark(Marker.Kill,now); Confirm(Name(match,target),head,now); }
                }
            }
        }
        void Mark(Marker marker,float now)
        {
            if(now<cueUntil && marker<LastMarker) return;
            LastMarker=marker;
            cueUntil=now+(marker==Marker.Kill?.30f:marker==Marker.Body?.15f:.22f);
            cue.text=marker==Marker.Kill?"✕":"×";
            cue.style.fontSize=marker==Marker.Kill?40:marker==Marker.Head?34:28;
            cue.style.color=marker==Marker.Kill?new Color(.92f,.33f,.25f):marker==Marker.Head?new Color(.98f,.78f,.36f):marker==Marker.Shield?ShieldBlue:Ivory;
            // A shotgun's pellets each report a hit: sound once per frame, unless a stronger marker lands in it.
            if(Feedback==null || now==soundedAt && marker<=sounded) return;
            soundedAt=now; sounded=marker; CuesSounded++;
            if(marker==Marker.Head || marker==Marker.Kill)
            {
                if(marker==Marker.Head) headClip=headClip??ArenaAudio.MakeConfirm(false); else killClip=killClip??ArenaAudio.MakeConfirm(true);
                Feedback.PlayOneShot(marker==Marker.Head?headClip:killClip);
            }
            else Feedback.PlayOneShot(tickClip=tickClip??ArenaAudio.MakeTick(),TickScale);
        }
        // The rules finish a target only at the end of its tick (ArenaMatch.Kill), so later pellets and other shooters'
        // hits that tick land for 0: only a damaging hit names the killer and its headshot.
        void RecordHit(CombatEvent e)
        {
            if(e.Amount<=0 || e.Target<1 || e.Target>8) return;
            lastHitter[e.Target]=e.Action.Participant; lastHitTick[e.Target]=e.Tick; lastHitHead[e.Target]=e.Headshot;
        }
        public int Killer(int target,int tick,out bool head)
        {
            head=target>=1 && target<=8 && lastHitHead[target];
            return target>=1 && target<=8 && lastHitTick[target]==tick?lastHitter[target]:0;
        }
        public void Confirm(string victim,bool head,float now)
        {
            KillStreak=now-lastKillAt<=StreakSeconds?KillStreak+1:1; lastKillAt=now;
            banner.text=KillBanner(KillStreak,victim,head);
            bannerUntil=now+BannerSeconds; // Refresh shows it, so a kill landing after your own death never flashes
        }
        static string Name(Wildstone.Match.ArenaMatch match,int participant)
        { var p=match?.Find(participant); return (p!=null?Teams[Math.Max(0,Math.Min(3,p.Team))].Substring(0,1):"·")+" P"+participant; }
        void AddFeed(Wildstone.Match.ArenaMatch match,int killer,int victim,bool head,float now)
        {
            for(int i=feedLines.Length-1;i>0;i--) { feedLines[i]=feedLines[i-1]; feedUntil[i]=feedUntil[i-1]; }
            feedLines[0]=killer!=0 && killer!=victim?Name(match,killer)+"  ›  "+Name(match,victim)+(head?"  ◎":""):Name(match,victim)+"  eliminated";
            feedUntil[0]=now+5;
        }
        public void Refresh(PracticeSession session)
        {
            var match=session.Match; var p=session.LocalPlayer; if(match==null || p==null || session.Motor==null || session.View==null) return;
            float now=Time.unscaledTime; bool reduced=session.Settings!=null && session.Settings.ReducedMotion;
            if(Feedback!=null && session.Settings!=null) Feedback.volume=Mathf.Clamp01(session.Settings.EffectsVolume)*.5f;
            if(ownTeam!=p.Team)
            {
                ownTeam=p.Team;
                for(int i=0;i<4;i++) chips[i].EnableInClassList("hud-chip--own",i==ownTeam);
                vitals.style.borderLeftColor=TeamTints[Math.Max(0,Math.Min(3,ownTeam))];
            }
            for(int i=0;i<4;i++) { float score=match.Score(i); scoreValues[i].text=$"{score:0}"; SetFill(scoreFills[i],score/WinSeconds); }
            int seconds=Math.Max(0,480-match.ElapsedTicks/match.TickRate);
            time.text=$"{seconds/60}:{seconds%60:00}";
            mode.text=session.ExternalSimulation?"PRIVATE MATCH":"PRACTICE";

            Show(vitals,p.Alive); Show(weaponPanel,p.Alive); Show(items[0].parent,p.Alive); Show(respawn,!p.Alive);
            respawn.text=p.Alive?"":$"RESPAWN  {Math.Max(0,(p.RespawnTick-match.Tick)/(float)match.TickRate):0.0}";
            health.text=$"{Mathf.CeilToInt(p.Health)}";
            health.style.color=p.Health<35?Warning:Ivory;
            SetFill(healthFill,p.Health/WeaponCatalog.Health); healthFill.style.backgroundColor=p.Health<35?Warning:Ivory;
            bool shielded=p.Shield>0;
            Show(shieldValue,shielded); Show(shieldBar,shielded);
            shieldValue.text=$"{Mathf.CeilToInt(p.Shield)} SHIELD"; SetFill(shieldFill,p.Shield/WeaponCatalog.Shield);
            SetFill(staminaFill,session.Motor.CaptureState().Stamina/100);

            var selected=CombatRules.CurrentWeapon(p);
            weaponName.text=WeaponName(selected).ToUpperInvariant();
            bool reloading=p.ReloadUntil>match.Tick;
            int rounds=p.SelectedSpecial?p.SpecialAmmo:p.StarterAmmo;
            ammo.text=selected==WeaponKind.LaserSword?"∞":rounds.ToString();
            bool dry=LowAmmo(selected,rounds);
            ammo.style.color=dry?Warning:new StyleColor(StyleKeyword.Null);
            if(Feedback!=null && p.Alive && dry && selected==lastWeapon && rounds<lastRounds) Feedback.PlayOneShot(dryClip=dryClip??ArenaAudio.MakeDry(),DryScale);
            lastWeapon=selected; lastRounds=rounds;
            ammoTag.text=reloading?"RELOADING":WeaponCatalog.IsAutomatic(selected)?"AUTO":"";
            Show(reloadBar,reloading);
            if(reloading) SetFill(reloadFill,1-(p.ReloadUntil-match.Tick)/(WeaponCatalog.ReloadSeconds*match.TickRate));
            string Key(int i)=>LocalSettings.BindingLabel(session.Settings.Bindings[i]).ToUpperInvariant();
            Item(0,Key(12),"BLINK",p.BlinkCharges); Item(1,Key(13),"SHIELD",p.ShieldCharges); Item(2,Key(11),"GRENADE",p.GrenadeAmmo);

            Vector3 delta=match.ZonePosition-p.Feet; float bearing=Vector3.SignedAngle(Quaternion.Euler(0,session.View.Yaw,0)*Vector3.forward,new Vector3(delta.x,0,delta.z),Vector3.up);
            zoneArrow.style.rotate=new Rotate(new Angle(bearing,AngleUnit.Degree));
            Color zoneColor=session.ZoneContested?Warning:session.ZoneCaptor>=0?TeamTints[Math.Min(3,session.ZoneCaptor)]:Ivory;
            zoneArrow.style.color=zoneColor;
            zoneText.text=(session.ZoneContested?"ZONE CONTESTED":session.ZoneCaptor>=0?"ZONE · "+Team(session.ZoneCaptor):"MOVING ZONE")+$"   {delta.magnitude:0} m";
            hint.text=session.ExternalSimulation?"ESC  MENU":"ESC  MENU  ·  STRAFING TARGET FIXTURE";
            string pick=null;
            foreach(var pickup in match.Pickups) if(pickup.AvailableTick<=match.Tick && (pickup.Position-p.Feet).sqrMagnitude<6.25f)
            { pick=$"{Key(14)}   PICK UP {pickup.Item.Replace('-',' ').ToUpperInvariant()}"; break; }
            prompt.text=pick??""; Show(prompt,p.Alive && pick!=null);
            cue.style.display=now<cueUntil?DisplayStyle.Flex:DisplayStyle.None;
            banner.style.display=p.Alive && now<bannerUntil?DisplayStyle.Flex:DisplayStyle.None;
            banner.style.opacity=reduced?1:Mathf.Clamp01((bannerUntil-now)*3);

            float width=Root.layout.width, height=Root.layout.height, ads=session.LocalAdsProgress;
            float fov=session.Camera!=null?session.Camera.fieldOfView:60;
            // Cone size follows the authoritative ADS/bloom used by the rules; visibility follows the local zoom.
            CrosshairGap=Math.Max(3,SpreadPixels(WeaponCatalog.SpreadDegrees(selected,p.AdsProgress,p.Bloom),fov,height));
            bool scoped=p.Alive && WeaponCatalog.ScopeActive(selected,ads);
            crosshair.style.display=p.Alive && !scoped && WeaponCatalog.HipCrosshairVisible(selected,ads)?DisplayStyle.Flex:DisplayStyle.None;
            ticks[0].style.left=-2; ticks[0].style.top=-CrosshairGap-9; ticks[1].style.left=-2; ticks[1].style.top=CrosshairGap-1;
            ticks[2].style.left=-CrosshairGap-9; ticks[2].style.top=-2; ticks[3].style.left=CrosshairGap-1; ticks[3].style.top=-2;
            scope.style.display=scoped && width>0 && height>0?DisplayStyle.Flex:DisplayStyle.None;
            if(scoped && width>0 && height>0)
            {
                float aperture=.9f*Math.Min(width,height), border=Math.Max(width,height), outer=aperture*.5f+border;
                scopeRing.style.left=width*.5f-outer; scopeRing.style.top=height*.5f-outer; scopeRing.style.width=scopeRing.style.height=outer*2;
                scopeRing.style.borderTopWidth=scopeRing.style.borderBottomWidth=scopeRing.style.borderLeftWidth=scopeRing.style.borderRightWidth=border;
                scopeRing.style.borderTopLeftRadius=scopeRing.style.borderTopRightRadius=scopeRing.style.borderBottomLeftRadius=scopeRing.style.borderBottomRightRadius=outer;
                scopeH.style.left=width*.5f-aperture*.5f; scopeH.style.top=height*.5f-.5f; scopeH.style.width=aperture; scopeH.style.height=1;
                scopeV.style.left=width*.5f-.5f; scopeV.style.top=height*.5f-aperture*.5f; scopeV.style.width=1; scopeV.style.height=aperture;
            }
            bool low=p.Alive && p.Health<35;
            vignette.style.display=low?DisplayStyle.Flex:DisplayStyle.None;
            if(low)
            {
                float alpha=reduced?.26f:.20f+.10f*(.5f+.5f*Mathf.Sin(now*Mathf.PI*2*1.1f));
                var red=new Color(.62f,.12f,.09f,alpha); float edge=Math.Max(12,height*.06f);
                vignette.style.borderTopColor=vignette.style.borderBottomColor=vignette.style.borderLeftColor=vignette.style.borderRightColor=red;
                vignette.style.borderTopWidth=vignette.style.borderBottomWidth=vignette.style.borderLeftWidth=vignette.style.borderRightWidth=edge;
            }
            bool hurt=p.Alive && now<damageUntil;
            damage.style.display=hurt?DisplayStyle.Flex:DisplayStyle.None;
            if(hurt)
            {
                Vector3 from=damageFrom-p.Feet;
                float angle=Vector3.SignedAngle(Quaternion.Euler(0,session.View.Yaw,0)*Vector3.forward,new Vector3(from.x,0,from.z),Vector3.up);
                damage.style.rotate=new Rotate(new Angle(angle,AngleUnit.Degree));
                damage.style.opacity=reduced?1:Mathf.Clamp01((damageUntil-now)*1.5f);
            }
            string lines="";
            for(int i=0;i<feedLines.Length;i++) if(feedLines[i]!=null && now<feedUntil[i]) lines+=(lines.Length>0?"\n":"")+feedLines[i];
            feed.text=lines;
        }
        void Item(int index,string key,string label,int count)
        {
            itemKeys[index].text=key; itemLabels[index].text=$"{label} {count}";
            items[index].EnableInClassList("hud-item--empty",count<=0);
        }
    }
}
