using Fusion;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    public static class FusionMotorInput
    {
        public static MotorInput Schedule(bool available, in FusionInput sample, in MotorState state,
            int lastSampleSequence, out int acceptedSampleSequence)
        {
            acceptedSampleSequence = lastSampleSequence;
            if (!available || !sample.Present || sample.Generation != state.Generation || sample.Sequence <= lastSampleSequence ||
                sample.Sequence < 0 || !ArenaMotor.Finite(sample.Move.x) || !ArenaMotor.Finite(sample.Move.y) ||
                sample.Move.x < -1 || sample.Move.x > 1 || sample.Move.y < -1 || sample.Move.y > 1 ||
                !ArenaMotor.Finite(sample.Yaw) || sample.Yaw < -180 || sample.Yaw > 180 ||
                !ArenaMotor.Finite(sample.Pitch) || sample.Pitch < -89 || sample.Pitch > 89 ||
                (((int)sample.Held | (int)sample.Pressed) & ~7) != 0) return default;

            acceptedSampleSequence = sample.Sequence;
            var input = sample.ToMotor();
            // The sample token deduplicates input only; runner scheduling owns motor time and sequence.
            input.Tick = state.Tick + 1;
            input.Sequence = state.LastInputSequence + 1;
            input.Generation = state.Generation;
            return input;
        }
    }
}
