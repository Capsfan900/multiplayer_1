using Fusion;
using UnityEngine;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    public struct FusionMotorState : INetworkStruct
    {
        public int Tick, Generation, LastInputSequence, LastFreshInputTick;
        public Vector3 Position, Velocity;
        public float Yaw, Pitch, CapsuleHeight;
        public Vector3 CapsuleCenter, GroundNormal;
        public NetworkBool Grounded, GroundStale;
        public int GroundContactId;
        public Vector2 LastMove;
        public MotorButtons Held;
        public NetworkBool Suppressed;
        public int SuppressedUntil;
        public double LastGroundedAt, JumpPressedAt;
        public NetworkBool DashRequested, DashFromGround, AirDashUsed;
        public double DashUntil, DashReadyAt, DashStartedAt;
        public Vector3 DashDirection;
        public float DashSpeed;
        public NetworkBool Sliding;
        public double SlideEndsAt, SlideReadyAt, LastSlideEndedAt;
        public int SlideChain, WallJumpsUsed, WallRunsUsed;
        public Vector3 LastWallNormal, WallEntryNormal, WallNormal, WallDirection, WallLastNormal, WallLastDirection;
        public NetworkBool HasLastWall, WallRunning, WallPerfectWindow;
        public float WallElapsed, WallLostTime;
        public double WallReadyAt, WallLeftAt;
        public int WallEndReason, WallContactId;
        public double WaterUntil, LaunchedAt;
        public int WaterContactId, LastLaunchId, LastLaunchActionId, LastImpulseId, LastImpulseTick;
        public Vector3 WaterFlow;
        public float Stamina, StaminaDelay;
        public int EventSequence, EventTick;
        public MotorEvents Events;

        public static FusionMotorState FromMotor(in MotorState state) => new FusionMotorState
        {
            Tick = state.Tick,
            Generation = state.Generation,
            LastInputSequence = state.LastInputSequence,
            LastFreshInputTick = state.LastFreshInputTick,
            Position = state.Position,
            Velocity = state.Velocity,
            Yaw = state.Yaw,
            Pitch = state.Pitch,
            CapsuleHeight = state.CapsuleHeight,
            CapsuleCenter = state.CapsuleCenter,
            GroundNormal = state.GroundNormal,
            Grounded = state.Grounded,
            GroundStale = state.GroundStale,
            GroundContactId = state.GroundContactId,
            LastMove = state.LastMove,
            Held = state.Held,
            Suppressed = state.Suppressed,
            SuppressedUntil = state.SuppressedUntil,
            LastGroundedAt = state.LastGroundedAt,
            JumpPressedAt = state.JumpPressedAt,
            DashRequested = state.DashRequested,
            DashFromGround = state.DashFromGround,
            AirDashUsed = state.AirDashUsed,
            DashUntil = state.DashUntil,
            DashReadyAt = state.DashReadyAt,
            DashStartedAt = state.DashStartedAt,
            DashDirection = state.DashDirection,
            DashSpeed = state.DashSpeed,
            Sliding = state.Sliding,
            SlideEndsAt = state.SlideEndsAt,
            SlideReadyAt = state.SlideReadyAt,
            LastSlideEndedAt = state.LastSlideEndedAt,
            SlideChain = state.SlideChain,
            WallJumpsUsed = state.WallJumpsUsed,
            WallRunsUsed = state.WallRunsUsed,
            LastWallNormal = state.LastWallNormal,
            WallEntryNormal = state.WallEntryNormal,
            WallNormal = state.WallNormal,
            WallDirection = state.WallDirection,
            WallLastNormal = state.WallLastNormal,
            WallLastDirection = state.WallLastDirection,
            HasLastWall = state.HasLastWall,
            WallRunning = state.WallRunning,
            WallPerfectWindow = state.WallPerfectWindow,
            WallElapsed = state.WallElapsed,
            WallLostTime = state.WallLostTime,
            WallReadyAt = state.WallReadyAt,
            WallLeftAt = state.WallLeftAt,
            WallEndReason = state.WallEndReason,
            WallContactId = state.WallContactId,
            WaterUntil = state.WaterUntil,
            LaunchedAt = state.LaunchedAt,
            WaterContactId = state.WaterContactId,
            LastLaunchId = state.LastLaunchId,
            LastLaunchActionId = state.LastLaunchActionId,
            LastImpulseId = state.LastImpulseId,
            LastImpulseTick = state.LastImpulseTick,
            WaterFlow = state.WaterFlow,
            Stamina = state.Stamina,
            StaminaDelay = state.StaminaDelay,
            EventSequence = state.EventSequence,
            EventTick = state.EventTick,
            Events = state.Events,
        };

        public MotorState ToMotor() => new MotorState
        {
            Tick = Tick,
            Generation = Generation,
            LastInputSequence = LastInputSequence,
            LastFreshInputTick = LastFreshInputTick,
            Position = Position,
            Velocity = Velocity,
            Yaw = Yaw,
            Pitch = Pitch,
            CapsuleHeight = CapsuleHeight,
            CapsuleCenter = CapsuleCenter,
            GroundNormal = GroundNormal,
            Grounded = Grounded,
            GroundStale = GroundStale,
            GroundContactId = GroundContactId,
            LastMove = LastMove,
            Held = Held,
            Suppressed = Suppressed,
            SuppressedUntil = SuppressedUntil,
            LastGroundedAt = LastGroundedAt,
            JumpPressedAt = JumpPressedAt,
            DashRequested = DashRequested,
            DashFromGround = DashFromGround,
            AirDashUsed = AirDashUsed,
            DashUntil = DashUntil,
            DashReadyAt = DashReadyAt,
            DashStartedAt = DashStartedAt,
            DashDirection = DashDirection,
            DashSpeed = DashSpeed,
            Sliding = Sliding,
            SlideEndsAt = SlideEndsAt,
            SlideReadyAt = SlideReadyAt,
            LastSlideEndedAt = LastSlideEndedAt,
            SlideChain = SlideChain,
            WallJumpsUsed = WallJumpsUsed,
            WallRunsUsed = WallRunsUsed,
            LastWallNormal = LastWallNormal,
            WallEntryNormal = WallEntryNormal,
            WallNormal = WallNormal,
            WallDirection = WallDirection,
            WallLastNormal = WallLastNormal,
            WallLastDirection = WallLastDirection,
            HasLastWall = HasLastWall,
            WallRunning = WallRunning,
            WallPerfectWindow = WallPerfectWindow,
            WallElapsed = WallElapsed,
            WallLostTime = WallLostTime,
            WallReadyAt = WallReadyAt,
            WallLeftAt = WallLeftAt,
            WallEndReason = WallEndReason,
            WallContactId = WallContactId,
            WaterUntil = WaterUntil,
            LaunchedAt = LaunchedAt,
            WaterContactId = WaterContactId,
            LastLaunchId = LastLaunchId,
            LastLaunchActionId = LastLaunchActionId,
            LastImpulseId = LastImpulseId,
            LastImpulseTick = LastImpulseTick,
            WaterFlow = WaterFlow,
            Stamina = Stamina,
            StaminaDelay = StaminaDelay,
            EventSequence = EventSequence,
            EventTick = EventTick,
            Events = Events,
        };
    }
}
