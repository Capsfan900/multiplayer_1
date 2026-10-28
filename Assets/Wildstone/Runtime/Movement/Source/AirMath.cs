// Adapted from vibegame1 2cb6e576 FirstPersonMotor.cs.
using UnityEngine;
namespace Wildstone.Movement
{
    public static class AirMath
    {
        public static float FallGravity(float gravity, float vy, bool grounded, float fallMultiplier)
        {
            return (!grounded && vy < 0f) ? gravity * fallMultiplier : gravity;
        }
        public static Vector3 AirSteer(Vector3 hv, Vector3 wish, float degPerSec, float dt)
        {
            if (degPerSec <= 0f || wish.sqrMagnitude < 0.0001f) return hv;
            float sp = hv.magnitude;
            if (sp < 0.5f) return hv;
            Vector3 w = new Vector3(wish.x, 0f, wish.z);
            if (w.sqrMagnitude < 0.0001f) return hv;
            w.Normalize();
            if (Vector3.Dot(hv, w) < -1e-4f) return hv;   // a stick at 90 deg is a strafe, not a brake
            return Vector3.RotateTowards(hv, w * sp, degPerSec * Mathf.Deg2Rad * dt, 0f);
        }
        public static float LandingSpeedFactor(float fallSpeed, float soft, float hard, float loss)
        {
            if (hard <= soft || loss <= 0f) return 1f;
            float t = Mathf.Clamp01((fallSpeed - soft) / (hard - soft));
            return 1f - Mathf.Clamp01(loss) * t;
        }
        public static Vector3 DecayExcess(Vector3 hv, float cap, float k, float dt)
        {
            float sp = hv.magnitude;
            if (sp <= cap || sp < 1e-5f) return hv;
            float target = cap + (sp - cap) * Mathf.Exp(-k * dt);
            return hv * (target / sp);
        }
        public static Vector3 AirStrafe(Vector3 v, Vector3 wish, float wishSpeed, float accel, float dt)
        {
            if (wish.sqrMagnitude < 0.0001f) return v;
            Vector3 dir = wish.normalized;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            if (flat.sqrMagnitude > 1e-6f)
            {
                Vector3 along = flat.normalized;
                if (Vector3.Dot(dir, along) < 0f)
                {
                    Vector3 across = dir - along * Vector3.Dot(dir, along);
                    if (across.sqrMagnitude < 1e-6f) return v;
                    dir = across.normalized;
                }
            }
            float current = Vector3.Dot(v, dir);
            float add = wishSpeed - current;
            if (add <= 0f) return v;
            float a = Mathf.Min(accel * dt, add);
            return v + dir * a;
        }
        public static Vector3 AirAccelerate(Vector3 v, Vector3 wish, float maxSpeed, float accel, float dt)
        {
            if (wish.sqrMagnitude < 0.0001f) return v;
            Vector3 dir = wish.normalized;
            float current = Vector3.Dot(v, dir);
            float add = maxSpeed - current;
            if (add <= 0f) return v;
            float a = Mathf.Min(accel * dt, add);
            return v + dir * a;
        }
    }
}

