using System;
using UnityEngine;

namespace Wildstone.Movement
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ArenaMotor : MonoBehaviour
    {
        public ILaunchOrbWorld LaunchOrbWorld { get; set; }
        readonly MotorTuning t = MotorTuning.Source;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        CharacterController cc;
        MotorState s;
        Vector3 groundNormalThisMove;
        int groundIdThisMove;
        double Now => (s.Tick + 1) * (double)MotorTuning.TickDelta;
        bool Dashing => Now < s.DashUntil;
        Vector3 Forward => Quaternion.Euler(0,s.Yaw,0) * Vector3.forward;
        Vector3 Right => Quaternion.Euler(0,s.Yaw,0) * Vector3.right;
        public MotorState CaptureState() => s;

        void Awake()
        {
            cc=GetComponent<CharacterController>();
            bool wasEnabled=cc.enabled;
            cc.enabled=false;
            gameObject.layer=MotorTuning.PlayerLayer;
            cc.height=t.height; cc.center=t.center; cc.radius=t.radius; cc.slopeLimit=t.slopeLimit;
            cc.stepOffset=t.stepOffset; cc.skinWidth=t.skinWidth; cc.minMoveDistance=t.minMoveDistance;
            cc.excludeLayers=1<<MotorTuning.PlayerLayer;
            // Recreate the native query shape after changing the capsule's startup geometry.
            cc.enabled=wasEnabled;
            s=InitialState(transform.position,transform.eulerAngles.y);
        }

        public static MotorState InitialState(Vector3 position,float yaw)
        {
            var tuning=MotorTuning.Source;
            return new MotorState { Tick=-1, Generation=1, LastInputSequence=-1, LastFreshInputTick=-1000,
                Position=position, Yaw=yaw, CapsuleHeight=tuning.height, CapsuleCenter=tuning.center,
                GroundNormal=Vector3.up, GroundStale=true, Stamina=tuning.max,
                LastGroundedAt=-99, JumpPressedAt=-99, LastSlideEndedAt=-99, WallLeftAt=-99,
                LaunchedAt=-99, WaterUntil=-99, DashStartedAt=-99, LastImpulseTick=-1 };
        }

        public void RestoreState(in MotorState saved)
        {
            if(!ValidSnapshot(saved))
                throw new ArgumentException("Invalid authoritative motor snapshot",nameof(saved));
            // A restore preserves the simulation contact answer; the next Move replaces CC's hidden contact cache.
            cc.enabled=false;
            transform.SetPositionAndRotation(saved.Position,Quaternion.Euler(0,saved.Yaw,0));
            cc.height=saved.CapsuleHeight; cc.center=saved.CapsuleCenter;
            cc.enabled=true;
            s=saved;
            groundNormalThisMove=Vector3.zero; groundIdThisMove=0;
        }

        public static bool ValidSnapshot(in MotorState saved) =>
            Finite(saved.Position) && Finite(saved.Velocity) && Finite(saved.Yaw) && Finite(saved.Pitch) &&
            Finite(saved.CapsuleHeight) && saved.CapsuleHeight>=MotorTuning.Source.radius*2 && saved.CapsuleHeight<=MotorTuning.Source.height &&
            Finite(saved.CapsuleCenter) && ValidTraversalState(saved) && saved.Generation>=1 && saved.Tick>=-1 && saved.SuppressedUntil>=0;

        // liftTick>0 is the motor tick a stun ends, so a predicting client unfreezes on time, not one round trip late.
        public void SetSuppressed(bool suppressed,int liftTick=0) { s.Suppressed=suppressed; s.SuppressedUntil=suppressed?Mathf.Max(0,liftTick):0; }

        public void Teleport(Vector3 position,float yaw)
        {
            if(!Finite(position) || !Finite(yaw)) throw new ArgumentException("Invalid teleport pose");
            s.Position=position; s.Yaw=Mathf.Repeat(yaw+180,360)-180; s.Velocity=Vector3.zero;
            s.DashUntil=Now; s.DashRequested=false; s.JumpPressedAt=-99; s.LastGroundedAt=-99;
            s.Grounded=false; s.GroundStale=true; s.GroundNormal=Vector3.up; s.GroundContactId=0;
            EndWall(WallRunEnd.Cancelled); s.WallLeftAt=-99; s.WallPerfectWindow=false; s.WallContactId=0;
            s.WaterUntil=-99; s.WaterContactId=0; s.WaterFlow=Vector3.zero; s.LaunchedAt=-99;
            // Blink preserves spent charges and stamina; respawn is a new authoritative life snapshot.
            RestoreState(s);
        }

        static bool ValidTraversalState(in MotorState v)
        {
            return Finite(v.GroundNormal) && Finite(v.LastMove.x) && Finite(v.LastMove.y)
                && Finite(v.LastWallNormal) && Finite(v.WallEntryNormal) && Finite(v.WallNormal)
                && Finite(v.WallDirection) && Finite(v.WallLastNormal) && Finite(v.WallLastDirection)
                && Finite(v.WaterFlow) && Finite(v.DashDirection) && Finite(v.DashSpeed)
                && Finite(v.WallElapsed) && Finite(v.WallLostTime) && Finite(v.Stamina) && v.Stamina>=0 && v.Stamina<=100
                && Finite(v.StaminaDelay) && Finite(v.LastGroundedAt) && Finite(v.JumpPressedAt)
                && Finite(v.DashUntil) && Finite(v.DashReadyAt) && Finite(v.DashStartedAt)
                && Finite(v.SlideEndsAt) && Finite(v.SlideReadyAt) && Finite(v.LastSlideEndedAt)
                && Finite(v.WallReadyAt) && Finite(v.WallLeftAt) && Finite(v.WaterUntil) && Finite(v.LaunchedAt);
        }

        public bool Simulate(in MotorInput input, float deltaTime, int tick)
        {
            if(deltaTime!=MotorTuning.TickDelta || tick!=s.Tick+1 || tick<0) return false;
            if(input.Present && (!ValidInput(input,tick) || input.Sequence<=s.LastInputSequence)) return false;
            s.Tick=tick; s.Events=MotorEvents.None; s.EventTick=tick;
            if(s.Suppressed && s.SuppressedUntil>0 && tick>=s.SuppressedUntil) { s.Suppressed=false; s.SuppressedUntil=0; }
            MotorButtons pressed=MotorButtons.None;
            if(input.Present)
            {
                // Presses are sampled edges; resumed held input must not manufacture one after a timeout.
                pressed=input.Pressed;
                s.Held=input.Held; s.LastMove=Vector2.ClampMagnitude(input.Move,1);
                s.Yaw=input.Yaw; s.Pitch=input.Pitch;
                s.LastInputSequence=input.Sequence; s.LastFreshInputTick=tick;
            }
            else if((tick-s.LastFreshInputTick)*(double)MotorTuning.TickDelta>.1)
            { s.LastMove=Vector2.zero; s.Held=MotorButtons.None; }
            transform.rotation=Quaternion.Euler(0,s.Yaw,0);
            // Explicit order replaces the source's unspecified ordering of two independent Update scripts.
            if(s.StaminaDelay>0) s.StaminaDelay-=deltaTime;
            else s.Stamina=Mathf.Min(t.max,s.Stamina+(s.Grounded?t.regenPerSecondGrounded:t.regenPerSecondAirborne)*deltaTime);

            if(s.Grounded && !s.GroundStale && s.Velocity.y<=0)
            {
                s.LastGroundedAt=Now; s.AirDashUsed=false; s.WallJumpsUsed=0; s.WallRunsUsed=0; s.HasLastWall=false;
            }
            else if(s.GroundStale || s.Velocity.y>0) s.Grounded=false;

            QueryTraversal(s.Position,Vector3.zero);
            bool inWater=Now<s.WaterUntil;
            if(!inWater) { s.WaterContactId=0; s.WaterFlow=Vector3.zero; }
            Vector2 m=s.Suppressed?Vector2.zero:s.LastMove;
            Vector3 wish=Right*m.x+Forward*m.y;
            if(!s.Suppressed)
            {
                if((pressed&MotorButtons.Jump)!=0) s.JumpPressedAt=Now;
                if((pressed&MotorButtons.Dash)!=0) s.DashRequested=true;
                if((pressed&MotorButtons.Slide)!=0) TrySlide();
            }
            else { s.JumpPressedAt=-99; s.DashRequested=false; }

            if(!s.WallRunning) TryWallRun(m);
            if(s.WallRunning && s.Suppressed) EndWall(WallRunEnd.Cancelled);
            UpdateWallPerfect(Vector3.Dot(wish,s.WallDirection)>.1f);
            float dt=deltaTime;
            if(s.WallRunning)
            {
                if(!s.Suppressed && Now-s.JumpPressedAt<=t.jumpBuffer && TryWallJump()) { }
                else if(!s.Suppressed && s.DashRequested && Now>=s.DashReadyAt && !s.AirDashUsed && CanSpend(t.dashCost))
                    EndWall(WallRunEnd.Cancelled);
                else
                {
                    dt-=AdvanceWall(dt,wish,m);
                    if(dt<=.00001f) { s.DashRequested=false; return true; }
                }
            }

            Vector3 hv=Flat(s.Velocity);
            if(Dashing)
            {
                hv=s.DashDirection*s.DashSpeed; s.Velocity.y=0;
                if(!s.Suppressed && s.DashFromGround && Now-s.JumpPressedAt<=t.jumpBuffer)
                {
                    bool perfect=PerfectMath.DashJumpIsPerfect((float)(Now-s.DashStartedAt),t.perfectDashJumpMinDelay,t.perfectDashJumpWindow);
                    Jump(); s.DashUntil=Now;
                    if(perfect) Refund(t.perfectDashJumpRefund);
                }
            }
            else
            {
                bool slideOnGround=s.Sliding && (s.Grounded || Now-s.LastGroundedAt<=t.coyoteTime);
                if(slideOnGround && inWater)
                {
                    hv=TraversalMath.WaterVelocity(TraversalMath.WaterStep(hv-s.WaterFlow,wish,t.groundSpeed*t.waterSpeedScale,t.waterAccel,dt),s.WaterFlow);
                    s.SlideEndsAt=Now+t.slideMaxDuration;
                    if(s.Grounded && s.Velocity.y<0) s.Velocity.y=-2;
                }
                else if(slideOnGround)
                {
                    if(wish.sqrMagnitude>.001f) hv=Vector3.MoveTowards(hv,wish.normalized*hv.magnitude,t.slideSteerAccel*dt);
                    bool downhill=TraversalMath.SustainsDownhillSlide(hv,s.GroundNormal,t.slideSlopeAccel,t.slopeLimit,s.Grounded && s.Velocity.y<=0);
                    hv=TraversalMath.DrySlideStep(hv,s.GroundNormal,t.slideSlopeAccel,t.slideFriction,t.slideMaxSpeed,dt,downhill);
                    if(downhill) s.SlideEndsAt=Now+t.slideMaxDuration;
                    if(!downhill && (hv.magnitude<=t.slideEndSpeed || Now>=s.SlideEndsAt) && !EndSlide())
                    {
                        Vector3 d=wish.sqrMagnitude>.001f?wish.normalized:(hv.sqrMagnitude>.0001f?hv.normalized:Forward);
                        hv=d*Mathf.Max(hv.magnitude,t.slideEndSpeed); s.SlideEndsAt=Now+.2;
                    }
                    if(s.Grounded && s.Velocity.y<0) s.Velocity.y=-2;
                }
                else if(s.Grounded)
                {
                    if(wish.sqrMagnitude<.001f) hv*=Mathf.Max(0,1-t.groundFriction*dt);
                    else
                    {
                        float target=Mathf.Max(t.groundSpeed,Mathf.Min(hv.magnitude,t.dashSpeed));
                        hv=Vector3.MoveTowards(hv,wish*target,t.groundAccel*dt);
                        hv=AirMath.DecayExcess(hv,t.groundSpeed,t.groundOverspeedDecay,dt);
                    }
                    if(s.Velocity.y<0) s.Velocity.y=-2;
                }
                else
                {
                    if(s.Sliding && Now-s.LastGroundedAt>t.coyoteTime) EndSlide();
                    bool floating=Now-s.LaunchedAt<=t.launchFloatSeconds;
                    if(!floating && Mathf.Abs(m.y)<.2f && Mathf.Abs(m.x)>.5f)
                        hv=AirMath.AirStrafe(hv,wish,t.airStrafeWishSpeed,t.airAccel,dt);
                    else
                    {
                        hv=AirMath.AirSteer(hv,wish,t.airSteerDegPerSec*(floating?t.launchSteerBoost:1),dt);
                        hv=AirMath.AirAccelerate(hv,wish,t.groundSpeed,t.airAccel,dt);
                    }
                    hv=AirMath.DecayExcess(hv,t.groundSpeed,t.airCarryDecay,dt);
                    hv=AirMath.DecayExcess(hv,t.airSoftCap,t.airDrag,dt);
                }
                float gravity=Now-s.LaunchedAt<=t.launchFloatSeconds?t.gravity*t.launchGravityScale:t.gravity;
                s.Velocity.y+=AirMath.FallGravity(gravity,s.Velocity.y,s.Grounded,t.fallGravityMultiplier)*dt;
                if(!s.Grounded && s.Velocity.y>0 && (s.Held&MotorButtons.Jump)==0)
                    s.Velocity.y+=t.gravity*t.jumpCutGravityMultiplier*dt;
                if(!s.Suppressed && Now-s.JumpPressedAt<=t.jumpBuffer)
                {
                    if(Now-s.LastGroundedAt<=t.coyoteTime) Jump();
                    else { s.Velocity.x=hv.x; s.Velocity.z=hv.z; if(TryWallJump()) hv=Flat(s.Velocity); }
                }
                if(!s.Suppressed && s.DashRequested && Now>=s.DashReadyAt && (s.Grounded || !s.AirDashUsed) && Spend(t.dashCost))
                {
                    EndSlide(); s.DashDirection=wish.sqrMagnitude>.01f?wish.normalized:Forward;
                    s.DashUntil=Now+t.dashDuration; s.DashReadyAt=Now+t.dashCooldown;
                    s.DashFromGround=s.Grounded || Now-s.LastGroundedAt<=t.coyoteTime;
                    if(!s.Grounded) s.AirDashUsed=true;
                    s.DashSpeed=t.dashSpeed; s.DashStartedAt=Now;
                    hv=s.DashDirection*s.DashSpeed; s.Velocity.y=0; Emit(MotorEvents.Dash);
                }
            }
            s.DashRequested=false;
            if(!Dashing) hv=Vector3.ClampMagnitude(hv,t.maxHorizontalSpeed);
            s.Velocity.x=hv.x; s.Velocity.z=hv.z;
            Vector3 disp=s.Velocity*dt;
            if(s.Grounded && s.Velocity.y<=0 && disp.y> -t.groundSnapDistance) disp.y=-t.groundSnapDistance;
            if(s.Sliding && !Dashing && !inWater && s.Velocity.y<=0 &&
                TraversalMath.SustainsDownhillSlide(hv,s.GroundNormal,t.slideSlopeAccel,t.slopeLimit,s.Grounded))
                disp.y=TraversalMath.DownhillSnapY(disp,s.GroundNormal,t.groundSnapDistance);
            LedgeCatch(ref disp,dt);
            Move(disp,true);
            return true;
        }

        bool ValidInput(in MotorInput input,int tick) => input.Tick==tick && input.Sequence>=0 && input.Generation==s.Generation
            && Finite(input.Move.x) && Finite(input.Move.y) && Mathf.Abs(input.Move.x)<=1 && Mathf.Abs(input.Move.y)<=1
            && Finite(input.Yaw) && input.Yaw>=-180 && input.Yaw<=180 && Finite(input.Pitch) && Mathf.Abs(input.Pitch)<=89
            && (((int)input.Held | (int)input.Pressed)&~7)==0;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x,0,v.z);
        bool CanSpend(float amount) => s.Stamina>=amount-.001f;
        bool Spend(float amount)
        {
            if(!CanSpend(amount)) return false;
            s.Stamina=Mathf.Max(0,s.Stamina-amount); s.StaminaDelay=t.regenDelay; return true;
        }
        void Refund(float amount) { s.Stamina=PerfectMath.Refund(s.Stamina,t.max,amount); Emit(MotorEvents.Perfect); }
        void Emit(MotorEvents events) { s.Events|=events; s.EventSequence++; s.EventTick=s.Tick; }
        void Jump()
        {
            EndSlide(); s.Velocity.y=Mathf.Sqrt(2*-t.gravity*t.jumpHeight);
            s.JumpPressedAt=-99; s.LastGroundedAt=-99; s.Grounded=false; Emit(MotorEvents.Jump);
        }
        bool TrySlide()
        {
            if(s.Sliding || Now<s.SlideReadyAt || (!s.Grounded && Now-s.LastGroundedAt>t.coyoteTime)) return false;
            Vector3 hv=Flat(s.Velocity); float speed=hv.magnitude;
            if(speed<t.slideMinEntrySpeed || !Spend(t.slideCost)) return false;
            float fade=Mathf.Clamp01((t.slideMaxSpeed-speed)/Mathf.Max(.01f,t.slideMaxSpeed-t.groundSpeed));
            s.SlideChain=Now-s.LastSlideEndedAt<=t.slideChainWindow?s.SlideChain+1:0;
            float boost=t.slideBoost*fade*Mathf.Pow(Mathf.Clamp01(t.slideChainFalloff),s.SlideChain);
            float entry=Mathf.Max(speed,Mathf.Min(Mathf.Max(speed,t.groundSpeed)+boost,t.slideMaxSpeed));
            hv*=entry/speed; s.Velocity.x=hv.x; s.Velocity.z=hv.z;
            s.Sliding=true; s.SlideEndsAt=Now+t.slideMaxDuration; SetHeight(t.slideHeight);
            // Keep the authoritative prior ground answer while reseating the resized native capsule.
            cc.Move(Vector3.down*Mathf.Max(.02f,cc.skinWidth*2)); s.Position=transform.position;
            Emit(MotorEvents.Slide); return true;
        }
        bool EndSlide()
        {
            if(!s.Sliding || CeilingBlocked()) return false;
            s.Sliding=false; SetHeight(t.height); s.SlideReadyAt=Now+t.slideCooldown; s.LastSlideEndedAt=Now; return true;
        }
        void SetHeight(float height)
        {
            s.CapsuleHeight=height; s.CapsuleCenter=t.center-Vector3.up*((t.height-height)*.5f);
            cc.height=height; cc.center=s.CapsuleCenter;
        }
        bool CeilingBlocked()
        {
            float gap=t.height-cc.height;
            return gap>.001f && Physics.SphereCast(s.Position+Vector3.up*(cc.height-cc.radius*.95f),cc.radius*.95f,
                Vector3.up,out _,gap+.05f,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore);
        }

        WallRunMath.Params WallSettings => new WallRunMath.Params {
            gravity=t.gravity,minEntrySpeed=t.wallRunMinEntrySpeed,maxEntryFallSpeed=t.wallRunMaxEntryFallSpeed,
            maxApproachCos=t.wallRunMaxApproachCos,minLookAlongCos=t.wallRunMinLookAlongCos,maxDuration=t.wallRunMaxDuration,
            gravityStartScale=t.wallRunGravityStartScale,gravityEndScale=t.wallRunGravityEndScale,entryUpSpeed=t.wallRunEntryUpSpeed,
            speedDecay=t.wallRunSpeedDecay,minSustainSpeed=t.wallRunMinSustainSpeed,accel=t.wallRunAccel,topSpeed=t.wallRunTopSpeed,
            maxSpeed=t.dashSpeed,exitUpSpeed=t.wallRunExitUpSpeed,exitPushSpeed=t.wallRunExitPushSpeed,exitTangentBoost=t.wallRunExitTangentBoost };
        float WallSide(Vector3 normal) => Vector3.Dot(-normal,Right)>=0?1:-1;
        bool Away(Vector2 move,Vector3 normal) => move.x*WallSide(normal)<-t.wallRunReleaseCos;
        bool TryWallRun(Vector2 move)
        {
            if(s.Suppressed || s.Grounded || (s.Velocity.y<=0 && Now-s.LastGroundedAt<=t.coyoteTime)
                || s.WallRunsUsed>=t.maxWallRuns || Now<s.WallReadyAt || s.Velocity.y< -t.wallRunMaxEntryFallSpeed
                || Flat(s.Velocity).sqrMagnitude<t.wallRunMinEntrySpeed*t.wallRunMinEntrySpeed) return false;
            if(!FindWall(true,false,out var normal,out int id) || Away(move,normal)
                || !WallRunMath.CanEnter(s.Velocity,normal,Forward,WallSettings,out var direction,out _)
                || (s.Sliding && CeilingBlocked())) return false;
            if(!Spend(t.wallRunEntryCost)) { s.WallReadyAt=Now+t.wallRunCooldown; return false; }
            EndSlide(); s.Velocity=WallRunMath.Enter(s.Velocity,direction,WallSettings);
            s.WallLostTime=0; s.WallRunning=true; s.WallElapsed=0; s.WallNormal=normal; s.WallEntryNormal=normal;
            s.WallDirection=direction; s.WallPerfectWindow=false; s.WallRunsUsed++;
            s.LastWallNormal=normal; s.HasLastWall=true; s.WallContactId=id; Emit(MotorEvents.WallStart); return true;
        }
        void EndWall(WallRunEnd why)
        {
            if(!s.WallRunning) return;
            s.WallRunning=false; s.WallEndReason=(int)why; s.WallReadyAt=Now+t.wallRunCooldown; s.WallLostTime=0;
            bool natural=why==WallRunEnd.Expired || why==WallRunEnd.Decayed || why==WallRunEnd.LostWall || why==WallRunEnd.Exhausted;
            s.WallLeftAt=natural?Now:-99; s.WallPerfectWindow=natural;
            s.WallLastNormal=s.WallNormal; s.WallLastDirection=s.WallDirection; Emit(MotorEvents.WallEnd);
        }
        bool TryWallJump()
        {
            if(s.Suppressed) return false;
            bool grace=!s.Grounded && Now-s.LastGroundedAt>t.coyoteTime && s.HasLastWall && Now-s.WallLeftAt<=t.wallRunExitGrace;
            if(s.WallRunning || grace)
            {
                Vector3 normal=s.WallRunning?s.WallNormal:s.WallLastNormal;
                Vector3 direction=s.WallRunning?s.WallDirection:s.WallLastDirection;
                if(direction.sqrMagnitude<.5f) return false;
                bool perfect=s.WallRunning?s.WallPerfectWindow:PerfectMath.WallJumpHybridIsPerfect(-1,(float)(Now-s.WallLeftAt),t.perfectWallJumpWindow);
                s.Velocity=perfect?WallRunMath.PerfectExit(s.Velocity,normal,direction,WallSettings,t.dashSpeed):
                    WallRunMath.Exit(s.Velocity,normal,direction,WallSettings,t.dashSpeed);
                s.LastWallNormal=normal; s.HasLastWall=true; EndWall(WallRunEnd.Jumped);
                s.WallLeftAt=-99; s.WallPerfectWindow=false; s.JumpPressedAt=-99;
                Emit(MotorEvents.WallJump); if(perfect) Refund(t.perfectWallJumpRefund); return true;
            }
            if(s.Grounded || Now-s.LastGroundedAt<=t.coyoteTime || s.WallJumpsUsed>=t.maxWallJumps
                || !FindWall(false,false,out var n,out int id) || !Spend(t.wallJumpCost)) return false;
            Vector3 h=Flat(s.Velocity); float into=Vector3.Dot(h,n); if(into<0) h-=n*into;
            h=Vector3.ClampMagnitude(h+n*t.wallJumpPushSpeed,t.dashSpeed);
            s.Velocity=new Vector3(h.x,t.wallJumpUpSpeed,h.z); s.LastWallNormal=n; s.HasLastWall=true;
            s.WallContactId=id; s.WallJumpsUsed++; s.JumpPressedAt=-99; EndSlide(); Emit(MotorEvents.WallJump); return true;
        }
        void UpdateWallPerfect(bool holdingForward)
        {
            if(!s.WallRunning)
            {
                s.WallPerfectWindow=s.WallLeftAt>=0 && PerfectMath.WallJumpHybridIsPerfect(-1,(float)(Now-s.WallLeftAt),t.perfectWallJumpWindow);
                return;
            }
            float seconds=WallRunMath.NaturalReleaseIn(s.WallElapsed,Mathf.Abs(Vector3.Dot(Flat(s.Velocity),s.WallDirection)),holdingForward,WallSettings);
            seconds=Mathf.Min(seconds,s.Stamina/t.wallRunDrainPerSecond);
            s.WallPerfectWindow=PerfectMath.WallJumpHybridIsPerfect(seconds,-1,t.perfectWallJumpWindow);
        }
        float AdvanceWall(float dt,Vector3 wish,Vector2 move)
        {
            if(Away(move,s.WallNormal)) { EndWall(WallRunEnd.Cancelled); return 0; }
            if(!FindWall(true,true,out var n,out int id))
            {
                s.WallLostTime+=dt;
                if(s.WallLostTime>t.wallRunLostGrace) { EndWall(WallRunEnd.LostWall); return 0; }
                n=s.WallNormal;
            }
            else { s.WallLostTime=0; s.WallContactId=id; }
            s.WallNormal=n;
            if(!WallRunMath.RunDirection(s.Velocity,n,out var direction) && !WallRunMath.RunDirection(s.WallDirection,n,out direction))
            { EndWall(WallRunEnd.LostWall); return 0; }
            s.WallDirection=direction;
            s.Velocity=WallRunMath.Advance(s.Velocity,direction,s.WallElapsed,Vector3.Dot(wish,direction)>.1f,
                WallSettings,dt,out var disp,out float used);
            s.WallElapsed+=used;
            if(s.WallLostTime<=0) disp-=n*(t.wallRunStickSpeed*used);
            Move(disp,false);
            if(s.Grounded) { s.LastGroundedAt=Now; EndWall(WallRunEnd.Landed); return used; }
            s.Stamina=Mathf.Max(0,s.Stamina-t.wallRunDrainPerSecond*used); s.StaminaDelay=t.regenDelay;
            if(s.Stamina<=0) EndWall(WallRunEnd.Exhausted);
            else if(WallRunMath.ShouldEnd(s.WallElapsed,Mathf.Abs(Vector3.Dot(Flat(s.Velocity),direction)),WallSettings,out var why)) EndWall(why);
            return used;
        }

        bool FindWall(bool run,bool sustaining,out Vector3 normal,out int contactId)
        {
            normal=Vector3.zero; contactId=0;
            Vector3 pref=Flat(s.Velocity); if(pref.sqrMagnitude<.01f) pref=Forward; pref.Normalize();
            Vector3 side=new Vector3(pref.z,0,-pref.x); float best=float.MaxValue;
            int count=sustaining?1:(run?2:8);
            for(int i=0;i<count;i++)
            {
                Vector3 dir=sustaining?-s.WallNormal:(run?(i==0?side:-side):Quaternion.Euler(0,i*45,0)*pref);
                int found=Physics.SphereCastNonAlloc(s.Position+cc.center,Mathf.Max(.05f,cc.radius*.9f),dir,hits,t.wallCheckDistance,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore);
                if(found==hits.Length) throw new InvalidOperationException("Wall query capacity exceeded");
                for(int j=0;j<found;j++)
                {
                    Vector3 raw=hits[j].normal; if(Mathf.Abs(raw.y)>.4f) continue;
                    Vector3 n=Flat(raw).normalized; if(n.sqrMagnitude<.5f) continue;
                    if(sustaining?!WallRunMath.IsSameWall(n,s.WallEntryNormal,t.sameWallCosineLimit):
                        s.HasLastWall && WallRunMath.IsSameWall(n,s.LastWallNormal,t.sameWallCosineLimit)) continue;
                    float score=run?hits[j].distance:Vector3.Dot(n,pref);
                    int id=SurfaceId(hits[j].collider);
                    if(score<best || (score==best && id<contactId)) { best=score; normal=n; contactId=id; }
                }
            }
            return normal.sqrMagnitude>.5f;
        }

        void Move(Vector3 displacement,bool forgiveness)
        {
            Vector3 before=s.Position;
            bool wasGrounded=s.Grounded;
            groundNormalThisMove=Vector3.zero; groundIdThisMove=0;
            var flags=cc.Move(displacement);
            s.Position=transform.position; s.GroundStale=false;
            s.Grounded=(flags&CollisionFlags.Below)!=0 && s.Velocity.y<=0;
            s.GroundNormal=groundNormalThisMove.sqrMagnitude>.000001f?groundNormalThisMove.normalized:
                (s.Grounded?s.GroundNormal:Vector3.up);
            s.GroundContactId=s.Grounded?groundIdThisMove:0;
            if(s.Grounded && !wasGrounded)
            {
                float keep=AirMath.LandingSpeedFactor(Mathf.Abs(s.Velocity.y),t.landingSoftSpeed,t.landingHardSpeed,t.landingSpeedLoss);
                s.Velocity.x*=keep; s.Velocity.z*=keep; Emit(MotorEvents.Land);
            }
            if((flags&CollisionFlags.Above)!=0 && s.Velocity.y>0)
            {
                bool corrected=false;
                if(forgiveness)
                {
                    float r=cc.radius*.95f;
                    Vector3 p0=s.Position+cc.center+Vector3.up*(cc.height*.5f-r+.06f);
                    Vector3 p1=s.Position+cc.center-Vector3.up*(cc.height*.5f-r-.06f);
                    for(int i=0;i<4 && !corrected;i++)
                    {
                        Vector3 dir=i==0?Right:i==1?-Right:i==2?Forward:-Forward;
                        Vector3 offset=dir*t.cornerCorrectionMetres;
                        if(!Physics.CheckCapsule(p0+offset,p1+offset,r,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore))
                        {
                            Vector3 prior=transform.position; cc.Move(offset); s.Position=transform.position;
                            corrected=ForgivenessMath.CornerCorrects(Vector3.Distance(prior,s.Position),t.cornerCorrectionMetres,s.Velocity.y);
                        }
                    }
                }
                if(!corrected) s.Velocity.y=0;
            }
            QueryTraversal(before,s.Position-before);
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(hit.normal.y>.2f && hit.normal.y>groundNormalThisMove.y)
            { groundNormalThisMove=hit.normal; groundIdThisMove=SurfaceId(hit.collider); }
        }
        void LedgeCatch(ref Vector3 displacement,float dt)
        {
            if(s.Grounded || s.Velocity.y>=0 || s.Sliding || s.WallRunning || Dashing) return;
            Vector3 h=Flat(s.Velocity); float speed=h.magnitude; if(speed<t.ledgeCatchMinSpeed) return;
            Vector3 origin=s.Position+h/speed*(cc.radius+.12f)+Vector3.up*(t.ledgeCatchMetres+.05f);
            if(Physics.Raycast(origin,Vector3.down,out var hit,t.ledgeCatchMetres+.1f,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)
                && ForgivenessMath.LedgeCatches(s.Position.y,hit.point.y,t.ledgeCatchMetres,s.Velocity.y,hit.normal.y,.7f,speed,t.ledgeCatchMinSpeed))
                displacement.y=Mathf.Max(displacement.y,0)+ForgivenessMath.NudgeStep(hit.point.y-s.Position.y+.02f,t.ledgeCatchLiftSpeed,dt);
        }

        public bool ApplyLaunch(int contactId,int actionId,float upSpeed)
        {
            if(contactId<=0 || actionId<=0 || (contactId==s.LastLaunchId && actionId<=s.LastLaunchActionId)
                || !Finite(upSpeed) || upSpeed<0 || upSpeed>t.maxHorizontalSpeed) return false;
            s.LastLaunchId=contactId; s.LastLaunchActionId=actionId;
            bool wasDashing=Dashing;
            s.AirDashUsed=false;
            if(wasDashing)
            {
                s.DashReadyAt=Now;
                s.DashUntil=Now; Vector3 h=Vector3.ClampMagnitude(Flat(s.Velocity),t.launchCarryCap);
                s.Velocity.x=h.x; s.Velocity.z=h.z;
            }
            else
            {
                s.DashUntil=Now; s.WallJumpsUsed=0; s.WallRunsUsed=0; s.HasLastWall=false;
                s.JumpPressedAt=-99; EndSlide(); EndWall(WallRunEnd.Cancelled);
                s.Velocity=TraversalMath.Launch(s.Velocity,upSpeed,t.launchCarryCap);
                s.Grounded=false; s.LastGroundedAt=-99;
            }
            s.LaunchedAt=Now; Emit(MotorEvents.Launch); return true;
        }
        public bool ApplyImpulse(Vector3 impulse,int actionId,int tick)
        {
            if(!Finite(impulse) || impulse.sqrMagnitude>10000 || actionId<=s.LastImpulseId || tick!=s.Tick) return false;
            s.Velocity+=impulse; s.LastImpulseId=actionId; s.LastImpulseTick=tick;
            if(impulse.y>0) { s.Grounded=false; s.LastGroundedAt=-99; }
            return true;
        }
        static int SurfaceId(Collider collider)
        {
            // shortcut: unlabelled static geometry has ID 0; map validation must reject it before network replay.
            var surface=collider.GetComponent<MotorSurface>();
            return surface!=null?surface.Id:0;
        }
        void QueryTraversal(Vector3 from,Vector3 displacement)
        {
            Vector3 center=from+s.CapsuleCenter;
            float span=Mathf.Max(0,s.CapsuleHeight*.5f-cc.radius);
            Vector3 a=center+Vector3.up*span,b=center-Vector3.up*span;
            int count=Physics.OverlapCapsuleNonAlloc(a,b,cc.radius,overlaps,1<<8,QueryTriggerInteraction.Collide);
            if(count==overlaps.Length) throw new InvalidOperationException("Traversal overlap capacity exceeded");
            // Fixed IDs, not callback order, decide overlapping traversal contacts.
            for(int i=0;i<count;i++) for(int j=i+1;j<count;j++)
                if(SurfaceId(overlaps[j])<SurfaceId(overlaps[i])) { var swap=overlaps[i]; overlaps[i]=overlaps[j]; overlaps[j]=swap; }
            for(int i=0;i<count;i++) Touch(overlaps[i]);
            if(displacement.sqrMagnitude<.000001f) return;
            count=Physics.CapsuleCastNonAlloc(a,b,cc.radius,displacement.normalized,hits,displacement.magnitude,1<<8,QueryTriggerInteraction.Collide);
            if(count==hits.Length) throw new InvalidOperationException("Traversal sweep capacity exceeded");
            for(int i=0;i<count;i++) for(int j=i+1;j<count;j++)
                if(hits[j].distance<hits[i].distance || (hits[j].distance==hits[i].distance && SurfaceId(hits[j].collider)<SurfaceId(hits[i].collider)))
                { var swap=hits[i]; hits[i]=hits[j]; hits[j]=swap; }
            for(int i=0;i<count;i++) Touch(hits[i].collider);
        }
        void Touch(Collider contact)
        {
            var water=contact.GetComponent<MotorWater>();
            if(water!=null)
            { s.WaterContactId=water.Id; s.WaterFlow=TraversalMath.Flow(water.FlowDirection,water.FlowSpeed); s.WaterUntil=Now+t.waterGrace; }
            var orb=contact.GetComponent<MotorLaunchOrb>();
            if(orb!=null)
            {
                int action;
                bool consumed=LaunchOrbWorld==null?orb.TryConsume(s.Tick,out action):LaunchOrbWorld.TryConsume(orb,out action);
                if(consumed) ApplyLaunch(orb.Id,action,MotorLaunchOrb.LaunchSpeed);
            }
        }
    }
}
