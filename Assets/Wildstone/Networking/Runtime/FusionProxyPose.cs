using System;
using UnityEngine;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    public static class FusionProxyPose
    {
        public static void Apply(ArenaMotor motor, Transform visualRoot, in MotorState latest, in MotorState from,
            in MotorState to, float alpha, bool discontinuity)
        {
            if (visualRoot != null && (visualRoot == motor.transform || !visualRoot.IsChildOf(motor.transform)))
                throw new ArgumentException("Proxy visuals must be a child separate from the collider", nameof(visualRoot));
            motor.RestoreState(latest);
            if (visualRoot == null) return;
            bool snap = discontinuity || from.Generation != to.Generation || latest.Generation != to.Generation || to.Tick < from.Tick;
            visualRoot.SetPositionAndRotation(snap ? latest.Position : Vector3.Lerp(from.Position, to.Position, alpha),
                Quaternion.Euler(0, snap ? latest.Yaw : Mathf.LerpAngle(from.Yaw, to.Yaw, alpha), 0));
        }
    }
}
