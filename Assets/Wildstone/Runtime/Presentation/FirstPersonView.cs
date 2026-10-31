using System;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    public sealed class MotorFeedbackHistory
    {
        const int Capacity=512;
        readonly int[] ticks=new int[Capacity];
        readonly MotorEvents[] seen=new MotorEvents[Capacity];
        int generation=-1,latestTick=-1;

        // Tick/kind identities survive rollback of event ordinals; expired replays stay suppressed.
        public MotorEvents Consume(int life,int tick,MotorEvents events)
        {
            if(life<1 || life<generation || tick<0) return MotorEvents.None;
            if(life!=generation)
            {
                generation=life; latestTick=-1;
                Array.Clear(ticks,0,Capacity); Array.Clear(seen,0,Capacity);
            }
            latestTick=Math.Max(latestTick,tick);
            if(tick<latestTick-Capacity+1) return MotorEvents.None;
            int slot=tick%Capacity;
            if(ticks[slot]!=tick) { ticks[slot]=tick; seen[slot]=MotorEvents.None; }
            MotorEvents fresh=events&~seen[slot];
            seen[slot]|=events;
            return fresh;
        }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class FirstPersonView : MonoBehaviour
    {
        public ArenaMotor Motor;
        public Camera ViewCamera;
        public bool OwnsLocalView;
        public bool ReducedMotion;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public event Action<MotorEvents> LocalEvents;
        readonly MotorFeedbackHistory feedbackHistory=new MotorFeedbackHistory();
        float rollKick,kickRemaining,slideRoll,slideFov,baseFieldOfView,adsProgress,shake,punch;
        // Movement lens (vibegame1 PlayerFeedback/CameraFX pattern, scaled from its 95° to this ~68° vertical lens):
        // decaying FOV impulses on dash/jump/land/launch, a wall-run hold and a landing nod. Cosmetic like the shake:
        // the eye stays the authoritative one (vibegame1's positional landing dip would move it off the shot origin).
        float fovKick,wallFov,nod,airSpeed;
        bool wasSliding;
        public const float DashKick=4,JumpKick=2,WallJumpKick=3,PerfectKick=2,LaunchKick=4,LandKick=-2.5f,SlideEndKick=-2,WallRunHold=3;
        public const float HardLanding=22,LandNod=2.5f,SlideNod=1.2f,DashRoll=1.4f;
        WeaponKind adsWeapon;
        public float BaseFieldOfView=>baseFieldOfView;
        // Cosmetic movement FOV (slide/wall-run holds and kicks) before ADS zoom, which fades it out.
        public float KickFieldOfView=>(slideFov+wallFov+fovKick)*(1-adsProgress);
        public float AdsProgress=>adsProgress;
        bool hasBaseFieldOfView,hasPresentationState;
        int presentationGeneration;
        Vector3 lastPosition;

        // Called once per rendered input sample even when gameplay is suppressed.
        public void SampleLook(Vector2 degrees)
        {
            if(!OwnsLocalView || !ArenaMotor.Finite(degrees.x) || !ArenaMotor.Finite(degrees.y)) return;
            Yaw=Mathf.Repeat(Yaw+degrees.x+180,360)-180;
            Pitch=Mathf.Clamp(Pitch-degrees.y,-89,89);
        }
        public void ResetView(float yaw,float pitch)
        {
            if(!ArenaMotor.Finite(yaw) || !ArenaMotor.Finite(pitch)) return;
            Yaw=Mathf.Repeat(yaw+180,360)-180; Pitch=Mathf.Clamp(pitch,-89,89);
            if(!hasBaseFieldOfView && ViewCamera!=null) SetBaseFieldOfView(ViewCamera.fieldOfView);
            ResetPresentation();
        }
        public void Present(in MotorState state)
        {
            if(!OwnsLocalView) return;
            MotorEvents fresh=feedbackHistory.Consume(state.Generation,state.EventTick,state.Events);
            if((fresh&MotorEvents.WallJump)!=0)
            {
                Vector3 right=Quaternion.Euler(0,Yaw,0)*Vector3.right;
                rollKick=(Vector3.Dot(-state.LastWallNormal,right)>=0?-1:1)*MotorTuning.Source.wallRunExitRollKick;
                kickRemaining=.28f;
                Kick(WallJumpKick);
            }
            if((fresh&MotorEvents.Dash)!=0)
            {
                Kick(DashKick);
                // A strafe dash banks the view into its travel, through the wall-jump roll kick.
                float side=Vector3.Dot(state.DashDirection,Quaternion.Euler(0,Yaw,0)*Vector3.right);
                if(Mathf.Abs(side)>.3f) { rollKick=-Mathf.Sign(side)*DashRoll; kickRemaining=.28f; }
            }
            if((fresh&MotorEvents.Slide)!=0) nod+=SlideNod; // the nose dips as the slide commits
            if((fresh&MotorEvents.Perfect)!=0) Kick(PerfectKick); // a perfect wall jump's stamina refund, otherwise unseen
            if((fresh&MotorEvents.Launch)!=0) Kick(LaunchKick);
            if((fresh&MotorEvents.Jump)!=0) Kick(JumpKick);
            if((fresh&MotorEvents.Land)!=0)
            {
                // Scaled by the fall; stepping off a kerb does not thud.
                float k=Mathf.Clamp01(Mathf.Max(airSpeed,-state.Velocity.y)/HardLanding);
                if(k>=.08f && !ReducedMotion) { nod+=LandNod*k; Kick(LandKick*k); }
            }
            if(fresh!=MotorEvents.None) LocalEvents?.Invoke(fresh);
        }
        // ADS zoom is presentation of the authoritative AdsProgress; aim still reads Yaw/Pitch only.
        public void SetAds(WeaponKind weapon,float progress)
        {
            adsWeapon=weapon; adsProgress=ArenaMotor.Finite(progress)?Mathf.Clamp01(progress):0;
        }
        // Distance-scaled blast shake. Cosmetic rotation only, suppressed by reduced motion.
        public void AddShake(float amount)
        {
            if(ReducedMotion || !ArenaMotor.Finite(amount) || amount<=0) return;
            shake=Mathf.Min(1.5f,shake+amount);
        }
        // View punch on the local player's own shots: the camera tips up and recovers in about .15 s. Cosmetic like
        // the shake: shots read Yaw/Pitch, never the camera transform.
        public void AddRecoil(float degrees)
        {
            if(ReducedMotion || !ArenaMotor.Finite(degrees) || degrees<=0) return;
            punch=Mathf.Min(3,punch+degrees);
        }
        public void SetBaseFieldOfView(float value)
        {
            if(!ArenaMotor.Finite(value) || value<=0 || value>=180) return;
            baseFieldOfView=value; hasBaseFieldOfView=true;
            if(ViewCamera!=null) ViewCamera.fieldOfView=baseFieldOfView+(ReducedMotion?0:slideFov);
        }
        void Kick(float degrees) { if(!ReducedMotion) fovKick=Mathf.Clamp(fovKick+degrees,-10,10); }
        public void ResetPresentation()
        {
            rollKick=kickRemaining=slideRoll=slideFov=shake=punch=fovKick=wallFov=nod=airSpeed=0; wasSliding=false;
            hasPresentationState=false;
            if(ViewCamera!=null)
            {
                if(hasBaseFieldOfView) ViewCamera.fieldOfView=baseFieldOfView;
                ViewCamera.transform.rotation=Quaternion.Euler(Pitch,Yaw,0);
            }
        }
        void LateUpdate()
        {
            if(!OwnsLocalView || Motor==null || ViewCamera==null) return;
            RenderFrame(Motor.CaptureState(),Time.unscaledDeltaTime);
        }
        public void RenderFrame(in MotorState state,float deltaTime)
        {
            if(!OwnsLocalView || ViewCamera==null) return;
            if(!hasBaseFieldOfView) SetBaseFieldOfView(ViewCamera.fieldOfView);
            bool discontinuity=hasPresentationState && (state.Generation!=presentationGeneration ||
                (state.Position-lastPosition).sqrMagnitude>25);
            if(discontinuity) ResetPresentation();
            hasPresentationState=true; presentationGeneration=state.Generation; lastPosition=state.Position;
            Present(state);
            float dt=ArenaMotor.Finite(deltaTime)?Mathf.Max(0,deltaTime):0;
            float blend=1-Mathf.Exp(-12*dt);
            slideRoll=Mathf.Lerp(slideRoll,state.Sliding?-Mathf.Clamp(state.LastMove.x,-1,1)*2.5f:0,blend);
            slideFov=Mathf.Lerp(slideFov,state.Sliding?6:0,blend);
            wallFov=Mathf.Lerp(wallFov,state.WallRunning?WallRunHold:0,blend);
            if(wasSliding && !state.Sliding) Kick(SlideEndKick); // the world closing up as the slide's speed goes
            wasSliding=state.Sliding;
            airSpeed=state.Grounded?0:Mathf.Max(0,-state.Velocity.y);
            float settle=Mathf.Exp(-11*dt);
            fovKick*=settle; nod*=settle;
            float roll=0;
            if(state.WallRunning)
            {
                Vector3 right=Quaternion.Euler(0,Yaw,0)*Vector3.right;
                roll=(Vector3.Dot(-state.WallNormal,right)>=0?1:-1)*MotorTuning.Source.wallRunCameraRoll;
            }
            roll+=rollKick*(kickRemaining/.28f)+slideRoll;
            if(ReducedMotion) { roll=0; slideRoll=slideFov=shake=punch=fovKick=wallFov=nod=0; }
            kickRemaining=Mathf.Max(0,kickRemaining-dt);
            shake=Mathf.MoveTowards(shake,0,dt*2.2f);
            punch*=Mathf.Exp(-18*dt);
            float t=Time.unscaledTime*22, jolt=shake*shake*1.6f;
            Vector3 tremor=new Vector3(Mathf.PerlinNoise(t,.3f)-.5f,Mathf.PerlinNoise(.7f,t)-.5f,Mathf.PerlinNoise(t,5.1f)-.5f)*jolt;
            ViewCamera.fieldOfView=WeaponCatalog.AdsVerticalFov(baseFieldOfView+KickFieldOfView,adsWeapon,adsProgress);
            ViewCamera.transform.SetPositionAndRotation(state.Position+Vector3.up*(state.CapsuleHeight-.15f),Quaternion.Euler(Pitch-punch+nod+tremor.x,Yaw+tremor.y,roll+tremor.z));
        }
    }
}
