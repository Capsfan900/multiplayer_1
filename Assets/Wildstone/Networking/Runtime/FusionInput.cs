using Fusion;
using UnityEngine;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    public struct FusionInput : INetworkInput
    {
        public NetworkBool Present;
        public int Tick, Sequence, Generation;
        public Vector2 Move;
        public float Yaw, Pitch;
        public MotorButtons Held, Pressed;

        public static FusionInput FromMotor(in MotorInput state) => new FusionInput
        {
            Present = state.Present,
            Tick = state.Tick,
            Sequence = state.Sequence,
            Generation = state.Generation,
            Move = state.Move,
            Yaw = state.Yaw,
            Pitch = state.Pitch,
            Held = state.Held,
            Pressed = state.Pressed,
        };

        public MotorInput ToMotor() => new MotorInput
        {
            Present = Present,
            Tick = Tick,
            Sequence = Sequence,
            Generation = Generation,
            Move = Move,
            Yaw = Yaw,
            Pitch = Pitch,
            Held = Held,
            Pressed = Pressed,
        };
    }
}
