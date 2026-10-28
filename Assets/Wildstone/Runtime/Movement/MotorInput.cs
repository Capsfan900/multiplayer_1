using System;
using UnityEngine;

namespace Wildstone.Movement
{
    [Flags] public enum MotorButtons : byte { None = 0, Jump = 1, Dash = 2, Slide = 4 }

    [Serializable]
    public struct MotorInput
    {
        public bool Present;
        public int Tick, Sequence, Generation;
        public Vector2 Move;
        public float Yaw, Pitch;
        public MotorButtons Held, Pressed;
    }
}
