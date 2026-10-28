using System;
using UnityEngine;

namespace Wildstone.Movement
{
    // Render sampling owns this queue; snapshots own only already-consumed input.
    public sealed class MotorInputBuffer
    {
        Vector2 move;
        MotorButtons held,pressed;
        int sequence;
        public void Sample(Vector2 movement,MotorButtons buttons)
        {
            if(!ArenaMotor.Finite(movement.x) || !ArenaMotor.Finite(movement.y) || ((int)buttons&~7)!=0)
                throw new ArgumentException("Invalid sampled movement");
            move=Vector2.ClampMagnitude(movement,1);
            pressed|=buttons&~held; held=buttons;
        }
        public MotorInput Consume(int tick,int generation,float yaw,float pitch)
        {
            var command=new MotorInput { Present=true,Tick=tick,Generation=generation,Sequence=++sequence,
                Move=move,Held=held,Pressed=pressed,Yaw=yaw,Pitch=pitch };
            pressed=MotorButtons.None;
            return command;
        }
        public void Reset() { move=Vector2.zero; held=pressed=MotorButtons.None; sequence=0; }
    }
}
