using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wildstone.Combat;
using Wildstone.Movement;
using Wildstone.UI;

namespace Wildstone.Game
{
    public sealed class PracticeInput : IDisposable
    {
        public const int FireIndex=7, GrenadeIndex=11, AimIndex=15;
        public readonly InputAction[] Actions=new InputAction[LocalSettings.ActionNames.Length];
        readonly List<CombatActionKind> pending=new List<CombatActionKind>();
        readonly MotorInputBuffer movement;
        readonly bool adsToggle;
        MotorButtons pressed;
        bool fireHeld,repeatBlocked,aimHeld,aimToggled,bufferedFire;
        int nextFireTick,nextEmptyTick,lastCombatTick=-1,combatLife,combatConnection;
        WeaponKind combatWeapon;
        static readonly CombatActionKind[] kinds={CombatActionKind.Fire,CombatActionKind.Reload,CombatActionKind.SelectStarter,CombatActionKind.SelectSpecial,CombatActionKind.Grenade,CombatActionKind.Blink,CombatActionKind.Shield,CombatActionKind.Pickup};
        // Held grenade key: presentation may draw the arc preview; release queues one throw (a tap still throws).
        public bool GrenadeHeld { get; private set; }
        // Effective local ADS request after hold/toggle; it rides the movement input's Aim bit.
        public bool Aiming => adsToggle?aimToggled:aimHeld;
        // Increments whenever local input requests an ADS-dropping change (reload, grenade, weapon switch, new life).
        public int AdsDrops { get; private set; }
        // Counts Blink presses so the HUD can tell a refused Blink (charge still held after the round trip) from no press.
        public int BlinkPresses { get; private set; }
        // Rebinding replaces the input object; the online movement sequence must keep rising, or host and client drop
        // every later sample as stale (PLAYTEST-FIX-LATER #1).
        public PracticeInput(LocalSettings settings,PracticeInput previous=null)
        {
            movement=previous?.movement??new MotorInputBuffer();
            adsToggle=settings.AdsToggle;
            for(int i=0;i<Actions.Length;i++)
            {
                int index=i;
                Actions[i]=new InputAction(LocalSettings.ActionNames[i],InputActionType.Button,settings.Bindings[i]);
                Actions[i].performed+=context=>RecordPress(index);
                if(i==GrenadeIndex) Actions[i].canceled+=context=>ReleaseGrenade();
                Actions[i].Enable();
            }
        }
        void RecordPress(int index)
        {
            if(index==FireIndex) repeatBlocked=false;
            // Input updates can complete a tap before the next render sample.
            if(index>=4 && index<=6) pressed|=(MotorButtons)(1<<(index-4));
            else if(index==AimIndex) { if(adsToggle) aimToggled=!aimToggled; }
            else if(index==GrenadeIndex) GrenadeHeld=true;
            else if(index>=FireIndex && pending.Count<32)
            { var kind=kinds[index-FireIndex]; pending.Add(kind); if(kind==CombatActionKind.Blink) BlinkPresses++; }
        }
        void ReleaseGrenade()
        {
            if(!GrenadeHeld) return;
            GrenadeHeld=false;
            if(pending.Count<32) pending.Add(CombatActionKind.Grenade);
        }
        public void Sample(bool active)
        {
            if(!active) { Clear(); return; }
            fireHeld=Actions[FireIndex].IsPressed();
            aimHeld=Actions[AimIndex].IsPressed();
            if(!fireHeld) repeatBlocked=false;
            float Held(int i) => Actions[i].IsPressed()?1:0;
            var buttons=MotorButtons.None;
            if(Held(4)>0) buttons|=MotorButtons.Jump; if(Held(5)>0) buttons|=MotorButtons.Dash; if(Held(6)>0) buttons|=MotorButtons.Slide;
            var move=new Vector2(Held(3)-Held(2),Held(0)-Held(1));
            // Preserve a complete tap or release/repress that occurred inside one rendered frame.
            if(pressed!=MotorButtons.None) { movement.Sample(move,buttons&~pressed); movement.Sample(move,buttons|pressed); }
            movement.Sample(move,buttons);
            pressed=MotorButtons.None;
        }
        public MotorInput ConsumeMovement(int tick,int generation,float yaw,float pitch)
        {
            var input=movement.Consume(tick,generation,yaw,pitch);
            if(Aiming) input.Held|=MotorButtons.Aim;
            return input;
        }
        public void ConsumeActions(List<CombatActionKind> destination) { destination.Clear(); destination.AddRange(pending); pending.Clear(); }
        public void ConsumeActions(List<CombatActionKind> destination,int tick,int rate,CombatState player,bool active)
        {
            if(!active || player==null) { Clear(); destination.Clear(); return; }
            bool changed=lastCombatTick>=0 && (tick<lastCombatTick || player.LifeGeneration!=combatLife || player.ConnectionGeneration!=combatConnection);
            if(changed) { pending.Clear(); nextFireTick=nextEmptyTick=0; repeatBlocked=true; GrenadeHeld=false; bufferedFire=false; }
            var weapon=CombatRules.CurrentWeapon(player);
            if(changed || (lastCombatTick>=0 && weapon!=combatWeapon)) { DropAim(); bufferedFire=false; }
            lastCombatTick=tick; combatLife=player.LifeGeneration; combatConnection=player.ConnectionGeneration; combatWeapon=weapon;
            ConsumeActions(destination);
            for(int i=0;i<destination.Count;i++)
            {
                var kind=destination[i];
                if(kind==CombatActionKind.Reload || kind==CombatActionKind.Grenade || kind==CombatActionKind.SelectStarter || kind==CombatActionKind.SelectSpecial) { DropAim(); break; }
            }
            bool pressedFire=destination.Contains(CombatActionKind.Fire);
            // Any automatic weapon (Line Driver, Laser SMG) repeats while held; host cooldown/ammo checks stay final.
            bool automatic=WeaponCatalog.IsAutomatic(weapon);
            int interval=automatic?WeaponCatalog.Ticks(WeaponCatalog.Get(weapon).Interval,rate):0;
            // A reload that ended at least the fire slack ago has refilled on the host (Expire runs before actions); the slack
            // covers a client request clock running a few ticks ahead of the host stamp.
            int slack=WeaponCatalog.Ticks(WeaponCatalog.FireSlackSeconds,rate);
            int ammo=player.SelectedSpecial?player.SpecialAmmo:player.ReloadUntil>0 && tick>=player.ReloadUntil+slack?WeaponCatalog.StarterAmmo:player.StarterAmmo;
            // An empty trigger asks for what comes next: a reload of the starter clip, or the starter once the special
            // is spent. The host validates both exactly like the keys; the futile shot is not sent.
            if((pressedFire || automatic && fireHeld && !repeatBlocked) && ammo<=0 && weapon!=WeaponKind.None &&
                weapon!=WeaponKind.LaserSword && !player.Suppressed(tick))
            {
                destination.RemoveAll(kind=>kind==CombatActionKind.Fire); pressedFire=false;
                if(tick>=nextEmptyTick)
                {
                    nextEmptyTick=tick+rate/4;
                    if(player.SelectedSpecial) { destination.Add(CombatActionKind.SelectStarter); repeatBlocked=true; DropAim(); }
                    // A started reload (ReloadUntil set until the host refills) never asks for another.
                    else if(player.ReloadUntil==0 && player.Pending==null) { destination.Add(CombatActionKind.Reload); DropAim(); }
                }
            }
            if(pressedFire && automatic) nextFireTick=tick+interval;
            // Repeats keep their own schedule; a frame or two late carries it instead of drifting it (D036). Online the
            // replicated FireReadyTick trails this request clock by the round trip, so it only holds back a cooldown
            // longer than this weapon's (a slower weapon fired before a switch); the host's cooldown stays final.
            if(!pressedFire && automatic && fireHeld && !repeatBlocked && tick>=nextFireTick &&
                tick>=player.FireReadyTick-interval && player.ReloadUntil<=tick && player.Pending==null &&
                ammo>0 && !player.Suppressed(tick))
            {
                destination.Add(CombatActionKind.Fire);
                nextFireTick=(tick-nextFireTick<=slack?nextFireTick:tick)+interval;
            }
            // A semi-automatic press up to FireBufferSeconds before the expected ready tick waits and fires once at
            // ready, instead of reaching the host early and being refused (PLAYTEST-FIX-LATER #8).
            if(!automatic && weapon!=WeaponKind.None)
            {
                int semi=WeaponCatalog.Ticks(WeaponCatalog.Get(weapon).Interval,rate), ready=Math.Max(nextFireTick,player.FireReadyTick-slack);
                if(pressedFire)
                {
                    if(tick<ready && ready-tick<=WeaponCatalog.Ticks(WeaponCatalog.FireBufferSeconds,rate))
                    { destination.RemoveAll(kind=>kind==CombatActionKind.Fire); bufferedFire=true; }
                    else if(tick>=ready) { nextFireTick=tick+semi; bufferedFire=false; }
                }
                else if(bufferedFire && tick>=ready)
                {
                    bufferedFire=false;
                    if((ammo>0 || weapon==WeaponKind.LaserSword) && !player.Suppressed(tick) && player.ReloadUntil<=tick && player.Pending==null)
                    { destination.Add(CombatActionKind.Fire); nextFireTick=(tick-ready<=slack?ready:tick)+semi; }
                }
            }
        }
        void DropAim() { aimToggled=false; AdsDrops++; }
        // Keep the monotonic movement sequence when a local menu clears held input.
        public void Clear()
        {
            movement.Sample(Vector2.zero,MotorButtons.None); movement.Consume(0,1,0,0); pressed=MotorButtons.None; pending.Clear();
            fireHeld=false; repeatBlocked=true; bufferedFire=false; nextFireTick=nextEmptyTick=0; lastCombatTick=-1; aimHeld=false; aimToggled=false; GrenadeHeld=false;
        }
        public void Dispose() { GrenadeHeld=false; foreach(var action in Actions) action?.Dispose(); }
    }
}
