// Adapted from vibegame1 2cb6e576 FirstPersonMotor.cs:2200-2504.
using UnityEngine;
namespace Wildstone.Movement
{
    public enum WallRunReject
    {
        None = 0,
        TooSlow,          // total horizontal speed after the approach-angle gate
        FallingTooFast,   // a plummet does not catch a wall
        WrongApproach,    // running INTO the face rather than along it
        LookingAway,      // no intent: the player is not looking down the run
        NoTangent,        // degenerate - velocity is entirely into or out of the wall
        CannotAct,        // staggered / cannot move
        Grounded,         // a wall run starts in the air
        OverBudget,       // maxWallRuns spent since the last landing
        Cooldown,         // wallRunCooldown since the last run ended has not elapsed
        NoWallInReach,    // the scan found no runnable face
        Sliding,          // a slide could not end (ceiling) so cannot yield to the wall
        NoStamina,        // every other gate passed; could not afford wallRunEntryCost
        StickAway         // the stick is pressed away from the face: the player is leaving, not entering
    }
    public enum WallRunEnd
    {
        None = 0,
        Expired,      // the loan ran out (wallRunMaxDuration)
        Decayed,      // bled below wallRunMinSustainSpeed
        LostWall,     // the face went away, or turned a corner too sharply
        Landed,
        Jumped,
        Cancelled,    // dash, teleport, stagger
        Exhausted     // stamina hit zero on the wall
    }
    public static class WallRunMath
    {
        public const float SubStep = 0.002f;
        public struct Params
        {
            public float gravity;              // negative, the motor's own
            public float minEntrySpeed;        // total horizontal m/s
            public float maxEntryFallSpeed;    // m/s, positive magnitude
            public float maxApproachCos;       // |dot(velDirFlat, wallNormal)| ceiling
            public float minLookAlongCos;      // dot(lookFlat, runDir) floor
            public float maxDuration;          // s
            public float gravityStartScale;    // x gravity at t=0
            public float gravityEndScale;      // x gravity at t=maxDuration
            public float entryUpSpeed;         // vy floor applied once, on entry
            public float speedDecay;           // 1/s, exponential
            public float minSustainSpeed;      // m/s, below which the run drops you
            public float accel;                // m/s^2 top-up while holding forward
            public float topSpeed;             // ceiling the forward top-up accelerates toward
            public float maxSpeed;             // hard ceiling on tangential speed (dashSpeed)
            public float exitUpSpeed;
            public float exitPushSpeed;        // along the wall normal
            public float exitTangentBoost;     // along the run direction
        }
        public static bool RunDirection(Vector3 vel, Vector3 wallNormal, out Vector3 runDir)
        {
            Vector3 n = new Vector3(wallNormal.x, 0f, wallNormal.z);
            float nl = n.magnitude;
            if (nl < 1e-4f) { runDir = Vector3.zero; return false; }
            n /= nl;

            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            Vector3 t = flat - n * Vector3.Dot(flat, n);
            if (t.sqrMagnitude < 1e-6f) { runDir = Vector3.zero; return false; }
            runDir = t.normalized;
            return true;
        }
        public static bool CanEnter(Vector3 vel, Vector3 wallNormal, Vector3 lookFlat, Params pr,
                                    out Vector3 runDir, out WallRunReject why)
        {
            runDir = Vector3.zero;
            why = WallRunReject.None;

            if (vel.y < -Mathf.Abs(pr.maxEntryFallSpeed)) { why = WallRunReject.FallingTooFast; return false; }

            Vector3 n = new Vector3(wallNormal.x, 0f, wallNormal.z);
            if (n.sqrMagnitude < 1e-6f) { why = WallRunReject.NoTangent; return false; }
            n.Normalize();

            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            float flatSpeed = flat.magnitude;
            if (flatSpeed < 1e-4f) { why = WallRunReject.TooSlow; return false; }
            float approach = Mathf.Abs(Vector3.Dot(flat / flatSpeed, n));
            if (approach > pr.maxApproachCos) { why = WallRunReject.WrongApproach; return false; }

            if (!RunDirection(vel, n, out runDir)) { why = WallRunReject.NoTangent; return false; }
            if (flatSpeed < pr.minEntrySpeed) { why = WallRunReject.TooSlow; return false; }

            Vector3 lf = new Vector3(lookFlat.x, 0f, lookFlat.z);
            if (lf.sqrMagnitude > 1e-6f)
            {
                lf.Normalize();
                if (Vector3.Dot(lf, runDir) < pr.minLookAlongCos) { why = WallRunReject.LookingAway; return false; }
            }
            return true;
        }
        public static Vector3 Enter(Vector3 vel, Vector3 runDir, Params pr)
        {
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            float along = flat.magnitude;
            if (along > pr.maxSpeed) along = pr.maxSpeed;
            return new Vector3(runDir.x * along, Mathf.Max(vel.y, pr.entryUpSpeed), runDir.z * along);
        }
        public static float GravityScale(float elapsed, Params pr)
        {
            if (pr.maxDuration <= 0f) return pr.gravityEndScale;
            float t = Mathf.Clamp01(elapsed / pr.maxDuration);
            return Mathf.Lerp(pr.gravityStartScale, pr.gravityEndScale, t * t);
        }
        public static Vector3 Advance(Vector3 vel, Vector3 runDir, float elapsed, bool holdingForward,
                                      Params pr, float dt, out Vector3 displacement, out float consumed)
        {
            displacement = Vector3.zero;
            consumed = 0f;
            if (dt <= 0f) return vel;

            float left = Mathf.Max(0f, pr.maxDuration - elapsed);
            float budget = Mathf.Min(dt, left);
            if (budget <= 0f) return vel;

            int steps = Mathf.Max(1, Mathf.CeilToInt(budget / SubStep));
            float h = budget / steps;

            for (int i = 0; i < steps; i++)
            {
                float t = elapsed + i * h;

                vel.y += pr.gravity * GravityScale(t, pr) * h;
                float along = vel.x * runDir.x + vel.z * runDir.z;
                along *= Mathf.Exp(-pr.speedDecay * h);
                if (holdingForward && along < pr.topSpeed)
                    along = Mathf.Min(pr.topSpeed, along + pr.accel * h);
                if (along > pr.maxSpeed) along = pr.maxSpeed;

                vel.x = runDir.x * along;
                vel.z = runDir.z * along;

                displacement += vel * h;
            }

            consumed = budget;
            return vel;
        }
        public static bool IsSameWall(Vector3 a, Vector3 b, float cosLimit)
        {
            return Vector3.Dot(a, b) > cosLimit;
        }
        public static bool ShouldEnd(float elapsed, float tangentialSpeed, Params pr, out WallRunEnd why)
        {
            if (elapsed >= pr.maxDuration) { why = WallRunEnd.Expired; return true; }
            if (tangentialSpeed < pr.minSustainSpeed) { why = WallRunEnd.Decayed; return true; }
            why = WallRunEnd.None;
            return false;
        }
        public static float NaturalReleaseIn(float elapsed, float tangentialSpeed, bool holdingForward, Params pr)
        {
            float untilClock = Mathf.Max(0f, pr.maxDuration - elapsed);
            if (holdingForward || pr.speedDecay <= 0f) return untilClock;
            if (tangentialSpeed <= pr.minSustainSpeed) return 0f;

            float untilDecay = Mathf.Log(tangentialSpeed / pr.minSustainSpeed) / pr.speedDecay;
            return Mathf.Min(untilClock, Mathf.Max(0f, untilDecay));
        }
        public static Vector3 Exit(Vector3 vel, Vector3 wallNormal, Vector3 runDir, Params pr, float speedClamp)
        {
            Vector3 n = new Vector3(wallNormal.x, 0f, wallNormal.z);
            if (n.sqrMagnitude > 1e-6f) n.Normalize(); else n = Vector3.zero;

            float along = vel.x * runDir.x + vel.z * runDir.z;
            if (along < 0f) along = 0f;
            along += pr.exitTangentBoost;

            float hx = runDir.x * along + n.x * pr.exitPushSpeed;
            float hz = runDir.z * along + n.z * pr.exitPushSpeed;

            float sp = Mathf.Sqrt(hx * hx + hz * hz);
            if (speedClamp > 0f && sp > speedClamp) { float k = speedClamp / sp; hx *= k; hz *= k; }

            return new Vector3(hx, pr.exitUpSpeed, hz);
        }
        public static Vector3 PerfectExit(Vector3 vel, Vector3 wallNormal, Vector3 runDir, Params pr, float speedClamp)
        {
            pr.exitTangentBoost *= 1.5f;
            return Exit(vel, wallNormal, runDir, pr, speedClamp);
        }


    }
}

