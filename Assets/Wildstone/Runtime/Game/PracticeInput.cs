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
            // Input updates can complete a tap before the next render sample.
            if(index>=4 && index<=6) pressed|=(MotorButtons)(1<<(index-4));
            else if(index>=7 && pending.Count<32) pending.Add(kinds[index-7]);
        }
        public void Sample(bool active)
        {
            if(!active) { Clear(); return; }
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
        // Keep the monotonic movement sequence when a local menu clears held input.
        public void Clear() { movement.Sample(Vector2.zero,MotorButtons.None); movement.Consume(0,1,0,0); pressed=MotorButtons.None; pending.Clear(); }
        public void Dispose() { foreach(var action in Actions) action?.Dispose(); }
    }
}
