// Adapted from vibegame1 2cb6e576; campaign-only laws omitted.
using UnityEngine;

namespace Wildstone.Movement
{
    public enum PerfectKind { None = 0, WallJump = 1, DashJump = 2 }
    public static class PerfectMath
    {
        public static bool WallJumpHybridIsPerfect(float secondsToRelease, float secondsSinceRelease, float window)
        {
            if (window <= 0f) return false;
            bool before = secondsToRelease >= 0f && secondsToRelease <= window;
            bool after = secondsSinceRelease >= 0f && secondsSinceRelease <= window;
            return before || after;
        }
        public static bool DashJumpIsPerfect(float sinceDash, float minDelay, float window)
        {
            if (window <= 0f || sinceDash < 0f) return false;
            return sinceDash >= minDelay && sinceDash <= minDelay + window;
        }
        public static float Refund(float current, float max, float amount)
        {
            if (amount <= 0f) return current;
            return Mathf.Min(max, current + amount);
        }
    }
}

