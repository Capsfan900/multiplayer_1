// Adapted from vibegame1 2cb6e576; campaign-only laws omitted.
using UnityEngine;

namespace Wildstone.Movement
{
    public static class ForgivenessMath
    {
        public static bool CornerCorrects(float clearOffset, float margin, float verticalSpeed)
        {
            return verticalSpeed > 0f && margin > 0f && clearOffset > 0f && clearOffset <= margin + 1e-4f;
        }
        public static bool LedgeCatches(float feetY, float ledgeTopY, float catchMetres, float verticalSpeed,
                                        float surfaceNormalY, float floorNormalMin, float horizontalSpeed, float minHorizontalSpeed)
        {
            if (catchMetres <= 0f || verticalSpeed >= 0f) return false;
            if (surfaceNormalY < floorNormalMin) return false;
            if (horizontalSpeed < minHorizontalSpeed) return false;
            float gap = ledgeTopY - feetY;
            return gap >= 0f && gap <= catchMetres;
        }
        public static float NudgeStep(float remaining, float metresPerSecond, float dt)
        {
            if (remaining <= 0f || dt <= 0f) return 0f;
            return Mathf.Min(remaining, metresPerSecond * dt);
        }
    }
}

