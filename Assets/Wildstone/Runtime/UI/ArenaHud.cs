using System;
using UnityEngine;
using UnityEngine.UIElements;
using Wildstone.Combat;
using Wildstone.Game;

namespace Wildstone.UI
{
    public sealed class ArenaHud
    {
        public readonly VisualElement Root=new VisualElement();
        readonly Label scores=new Label(),status=new Label(),vitals=new Label(),weapon=new Label(),items=new Label(),zone=new Label(),prompt=new Label(),cue=new Label();
        float cueUntil;
        static readonly string[] Teams={"▲ IVORY","■ ASH","◆ SAGE","● CLAY"};
        public ArenaHud()
        {
            Root.AddToClassList("hud"); Root.pickingMode=PickingMode.Ignore;
            var top=new VisualElement(); top.AddToClassList("hud-top"); top.Add(scores); top.Add(status); Root.Add(top);
            var center=new Label("·"); center.AddToClassList("crosshair"); Root.Add(center);
            cue.name="hud-hit-cue"; cue.AddToClassList("hit-cue"); cue.style.display=DisplayStyle.None; Root.Add(cue);
            var bottom=new VisualElement(); bottom.AddToClassList("hud-bottom");
            bottom.Add(vitals); bottom.Add(weapon); bottom.Add(items); bottom.Add(zone); bottom.Add(prompt); Root.Add(bottom);
        }
        public static string Team(int team) => Teams[Math.Max(0,Math.Min(3,team))];
        public static string WeaponName(WeaponKind kind)
        {
            switch(kind) { case WeaponKind.LineDriver:return "Line Driver"; case WeaponKind.PulseLobber:return "Pulse Lobber"; case WeaponKind.ReboundDisc:return "Rebound Disc"; case WeaponKind.PushLens:return "Push Lens"; case WeaponKind.StunGun:return "Stun Gun"; case WeaponKind.LaserSword:return "Laser Sword"; default:return kind.ToString(); }
        }
        public void Step(PracticeSession session)
        {
            for(int i=0;i<session.CombatEvents.Count;i++)
            {
                var evt=session.CombatEvents[i];
                if(evt.Kind==CombatEventKind.Hit && (evt.Target==session.LocalParticipantId || evt.Action.Participant==session.LocalParticipantId))
                { cue.text=evt.Target==session.LocalParticipantId?"DAMAGE":"×"; cueUntil=Time.unscaledTime+.16f; }
            }
        }
        public void Refresh(PracticeSession session)
        {
            var match=session.Match; var p=session.LocalPlayer; if(match==null || p==null || session.Motor==null || session.View==null) return;
            scores.text=$"▲ {match.Score(0):0}    ■ {match.Score(1):0}    ◆ {match.Score(2):0}    ● {match.Score(3):0}";
            int seconds=Math.Max(0,480-match.ElapsedTicks/match.TickRate);
            status.text=$"{(session.ExternalSimulation?"PRIVATE MATCH":"OFFLINE PRACTICE")} · {seconds/60:00}:{seconds%60:00}";
            vitals.text=p.Alive?$"{p.Health:0} HEALTH   {p.Shield:0} SHIELD   {session.Motor.CaptureState().Stamina:0} STAMINA":$"RESPAWN IN {Math.Max(0,(p.RespawnTick-match.Tick)/(float)match.TickRate):0.0}s";
            var selected=p.SelectedSpecial?p.SpecialWeapon:WeaponKind.LineDriver;
            weapon.text=WeaponName(selected)+"   "+(selected==WeaponKind.LaserSword?"∞":(p.SelectedSpecial?p.SpecialAmmo:p.StarterAmmo).ToString())+(p.ReloadUntil>match.Tick?"  RELOADING":selected==WeaponKind.LineDriver?"  AUTO":"");
            string Key(int i)=>LocalSettings.BindingLabel(session.Settings.Bindings[i]);
            items.text=$"{Key(12)}  Blink {p.BlinkCharges}     {Key(13)}  Shield {p.ShieldCharges}     {Key(11)}  Grenade {p.GrenadeAmmo}";
            Vector3 delta=match.ZonePosition-p.Feet; float bearing=Vector3.SignedAngle(Quaternion.Euler(0,session.View.Yaw,0)*Vector3.forward,new Vector3(delta.x,0,delta.z),Vector3.up);
            string direction=Math.Abs(bearing)<30?"AHEAD":Math.Abs(bearing)>150?"BEHIND":bearing>0?"RIGHT":"LEFT";
            zone.text=(session.ZoneContested?"ZONE CONTESTED":session.ZoneCaptor>=0?"ZONE · "+Team(session.ZoneCaptor):"MOVING ZONE")+$"  {delta.magnitude:0}m · {direction}";
            prompt.text=session.ExternalSimulation?"Esc  Menu":"Esc  Menu · Stationary target fixture";
            foreach(var pickup in match.Pickups) if(pickup.AvailableTick<=match.Tick && (pickup.Position-p.Feet).sqrMagnitude<6.25f)
            { prompt.text=$"{Key(14)}  Pick up {pickup.Item.Replace('-',' ')}"; break; }
            cue.style.display=Time.unscaledTime<cueUntil?DisplayStyle.Flex:DisplayStyle.None;
        }
    }
}
