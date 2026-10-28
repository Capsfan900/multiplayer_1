// Adapted from vibegame1 2cb6e576; campaign-only laws omitted.
using UnityEngine;

namespace Wildstone.Movement
{
    public static class TraversalMath
    {
        public static Vector3 SlopeAccel(Vector3 groundNormal, float scale)
        {
            if (scale <= 0f) return Vector3.zero;
            if (groundNormal.sqrMagnitude < 1e-6f) return Vector3.zero;
            Vector3 n = groundNormal.normalized;
            if (n.y >= 0.9999f) return Vector3.zero;                    // flat: nothing to gain
            if (n.y <= 0f) return Vector3.zero;                         // a wall or an overhang is not ground
            Vector3 g = new Vector3(0f, -9.81f, 0f);
            Vector3 alongSlope = g - n * Vector3.Dot(g, n);
            alongSlope.y = 0f;
            return alongSlope * scale;
        }
        public static float SlopeDegrees(Vector3 groundNormal)
        {
            if (groundNormal.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.Angle(groundNormal.normalized, Vector3.up);
        }
        public static bool SustainsDownhillSlide(Vector3 velocity, Vector3 groundNormal, float scale,
                                                float slopeLimit, bool grounded)
        {
            if (!grounded || groundNormal.sqrMagnitude < 1e-6f) return false;
            if (SlopeDegrees(groundNormal) > slopeLimit) return false;
            velocity.y = 0f;
            return Vector3.Dot(velocity, SlopeAccel(groundNormal, scale)) > 0.001f;
        }
        public static Vector3 DrySlideStep(Vector3 velocity, Vector3 groundNormal, float scale, float friction,
                                          float speedCap, float dt, bool downhill)
        {
            if (downhill)
            {
                float ceiling = Mathf.Max(speedCap, velocity.magnitude);
                return Vector3.ClampMagnitude(velocity + SlopeAccel(groundNormal, scale) * dt, ceiling);
            }
            velocity += SlopeAccel(groundNormal, scale) * dt;
            velocity *= Mathf.Max(0f, 1f - friction * dt);
            return velocity;
        }
        public static float DownhillSnapY(Vector3 displacement, Vector3 groundNormal, float contactMargin)
        {
            if (groundNormal.y <= 0.0001f || contactMargin <= 0f) return displacement.y;
            float planeY = -(displacement.x * groundNormal.x + displacement.z * groundNormal.z) / groundNormal.y;
            return planeY < 0f ? Mathf.Min(displacement.y, planeY - contactMargin) : displacement.y;
        }

        public static Vector3 Launch(Vector3 vel, float upSpeed)
        {
            return Launch(vel, upSpeed, float.PositiveInfinity);
        }
        public static Vector3 Launch(Vector3 vel, float upSpeed, float carryCap)
        {
            Vector3 h = new Vector3(vel.x, 0f, vel.z);
            float m = h.magnitude;
            if (carryCap >= 0f && m > carryCap && m > 0.0001f) h *= carryCap / m;
            return new Vector3(h.x, Mathf.Max(0f, upSpeed), h.z);
        }
        public static Vector3 WaterStep(Vector3 rel, Vector3 wish, float floorSpeed, float accel, float dt)
        {
            rel.y = 0f;
            Vector3 w = new Vector3(wish.x, 0f, wish.z);
            float sp = rel.magnitude;
            Vector3 dir;
            if (w.sqrMagnitude > 0.0001f) dir = w.normalized;
            else if (sp > 0.05f) dir = rel / sp;
            else return rel;
            float target = Mathf.Max(floorSpeed, sp);
            return Vector3.MoveTowards(rel, dir * target, Mathf.Max(0f, accel) * dt);
        }
        public static Vector3 WaterVelocity(Vector3 rel, Vector3 flow)
        {
            return new Vector3(rel.x + flow.x, 0f, rel.z + flow.z);
        }
        public static Vector3 Flow(Vector3 direction, float speed)
        {
            Vector3 d = new Vector3(direction.x, 0f, direction.z);
            if (d.sqrMagnitude < 0.0001f || speed <= 0f) return Vector3.zero;
            return d.normalized * speed;
        }
    }
}

