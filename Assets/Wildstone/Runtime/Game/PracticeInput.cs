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
        public readonly InputAction[] Actions=new InputAction[LocalSettings.ActionNames.Length];
        readonly List<CombatActionKind> pending=new List<CombatActionKind>();
        readonly MotorInputBuffer movement=new MotorInputBuffer();
        MotorButtons pressed;
        bool fireHeld,repeatBlocked;
        int nextFireTick,lastCombatTick=-1,combatLife,combatConnection;
        static readonly CombatActionKind[] kinds={CombatActionKind.Fire,CombatActionKind.Reload,CombatActionKind.SelectStarter,CombatActionKind.SelectSpecial,CombatActionKind.Grenade,CombatActionKind.Blink,CombatActionKind.Shield,CombatActionKind.Pickup};
        public PracticeInput(LocalSettings settings)
        {
            for(int i=0;i<Actions.Length;i++)
            {
                int index=i;
                Actions[i]=new InputAction(LocalSettings.ActionNames[i],InputActionType.Button,settings.Bindings[i]);
                Actions[i].performed+=context=>RecordPress(index);
                Actions[i].Enable();
            }
        }
        void RecordPress(int index)
        {
            if(index==7) repeatBlocked=false;
            // Input updates can complete a tap before the next render sample.
            if(index>=4 && index<=6) pressed|=(MotorButtons)(1<<(index-4));
            else if(index>=7 && pending.Count<32) pending.Add(kinds[index-7]);
        }
        public void Sample(bool active)
        {
            if(!active) { Clear(); return; }
            fireHeld=Actions[7].IsPressed();
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
        public MotorInput ConsumeMovement(int tick,int generation,float yaw,float pitch) => movement.Consume(tick,generation,yaw,pitch);
        public void ConsumeActions(List<CombatActionKind> destination) { destination.Clear(); destination.AddRange(pending); pending.Clear(); }
        public void ConsumeActions(List<CombatActionKind> destination,int tick,int rate,CombatState player,bool active)
        {
            if(!active || player==null) { Clear(); destination.Clear(); return; }
            bool changed=lastCombatTick>=0 && (tick<lastCombatTick || player.LifeGeneration!=combatLife || player.ConnectionGeneration!=combatConnection);
            if(changed) { pending.Clear(); nextFireTick=0; repeatBlocked=true; }
            lastCombatTick=tick; combatLife=player.LifeGeneration; combatConnection=player.ConnectionGeneration;
            ConsumeActions(destination);
            bool pressedFire=destination.Contains(CombatActionKind.Fire);
            bool starter=!player.SelectedSpecial;
            int interval=WeaponCatalog.Ticks(WeaponCatalog.Get(WeaponKind.LineDriver).Interval,rate);
            if(pressedFire && starter) nextFireTick=tick+interval;
            if(!pressedFire && starter && fireHeld && !repeatBlocked && tick>=nextFireTick &&
                tick>=player.FireReadyTick && player.ReloadUntil<=tick && player.Pending==null &&
                player.StarterAmmo>0 && !player.Suppressed(tick))
            {
                destination.Add(CombatActionKind.Fire);
                nextFireTick=tick+interval;
            }
        }
        // Keep the monotonic movement sequence when a local menu clears held input.
        public void Clear() { movement.Sample(Vector2.zero,MotorButtons.None); movement.Consume(0,1,0,0); pressed=MotorButtons.None; pending.Clear(); fireHeld=false; repeatBlocked=true; nextFireTick=0; lastCombatTick=-1; }
        public void Dispose() { foreach(var action in Actions) action?.Dispose(); }
    }
}
