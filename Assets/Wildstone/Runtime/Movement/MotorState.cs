using System;
using UnityEngine;

namespace Wildstone.Movement
{
    [Serializable]
    public struct MotorState
    {
        public int Tick, Generation, LastInputSequence, LastFreshInputTick;
        public Vector3 Position, Velocity;
        public float Yaw, Pitch, CapsuleHeight;
        public Vector3 CapsuleCenter, GroundNormal;
        public bool Grounded, GroundStale;
        public int GroundContactId;
        public Vector2 LastMove;
        public MotorButtons Held;
        public bool Suppressed;
        public int SuppressedUntil;
        public double LastGroundedAt, JumpPressedAt;
        public bool DashRequested, DashFromGround, AirDashUsed;
        public double DashUntil, DashReadyAt, DashStartedAt;
        public Vector3 DashDirection;
        public float DashSpeed;
        public bool Sliding;
        public double SlideEndsAt, SlideReadyAt, LastSlideEndedAt;
        public int SlideChain, WallJumpsUsed, WallRunsUsed;
        public Vector3 LastWallNormal, WallEntryNormal, WallNormal, WallDirection, WallLastNormal, WallLastDirection;
        public bool HasLastWall, WallRunning, WallPerfectWindow;
        public float WallElapsed, WallLostTime;
        public double WallReadyAt, WallLeftAt;
        public int WallEndReason, WallContactId;
        public double WaterUntil, LaunchedAt;
        public int WaterContactId, LastLaunchId, LastLaunchActionId, LastImpulseId, LastImpulseTick;
        public Vector3 WaterFlow;
        public float Stamina, StaminaDelay;
        public int EventSequence, EventTick;
        public MotorEvents Events;
    }

    [Flags] public enum MotorEvents { None = 0, Jump = 1, Dash = 2, Slide = 4, Land = 8, WallStart = 16, WallEnd = 32, WallJump = 64, Perfect = 128, Launch = 256 }
}
