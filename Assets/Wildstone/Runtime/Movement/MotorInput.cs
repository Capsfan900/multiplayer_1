using System;
using UnityEngine;

namespace Wildstone.Movement
{
    // Aim (ADS) rides the same replicated input for combat only; ForMotor strips it before ArenaMotor.
    [Flags] public enum MotorButtons : byte { None = 0, Jump = 1, Dash = 2, Slide = 4, Aim = 8 }

    [Serializable]
    public struct MotorInput
    {
        public bool Present;
        public int Tick, Sequence, Generation;
        public Vector2 Move;
        public float Yaw, Pitch;
        public MotorButtons Held, Pressed;
        public const MotorButtons MovementButtons = MotorButtons.Jump | MotorButtons.Dash | MotorButtons.Slide;
        public bool Aiming => (Held & MotorButtons.Aim) != 0;
        public MotorInput ForMotor() { var copy = this; copy.Held &= MovementButtons; copy.Pressed &= MovementButtons; return copy; }
    }
}
