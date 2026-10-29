using System;
using UnityEngine;
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
        float rollKick,kickRemaining,slideRoll,slideFov,baseFieldOfView;
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
            }
            if(fresh!=MotorEvents.None) LocalEvents?.Invoke(fresh);
        }
        public void SetBaseFieldOfView(float value)
        {
            if(!ArenaMotor.Finite(value) || value<=0 || value>=180) return;
            baseFieldOfView=value; hasBaseFieldOfView=true;
            if(ViewCamera!=null) ViewCamera.fieldOfView=baseFieldOfView+(ReducedMotion?0:slideFov);
        }
        public void ResetPresentation()
        {
            rollKick=kickRemaining=slideRoll=slideFov=0;
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
            float roll=0;
            if(state.WallRunning)
            {
                Vector3 right=Quaternion.Euler(0,Yaw,0)*Vector3.right;
                roll=(Vector3.Dot(-state.WallNormal,right)>=0?1:-1)*MotorTuning.Source.wallRunCameraRoll;
            }
            roll+=rollKick*(kickRemaining/.28f)+slideRoll;
            if(ReducedMotion) { roll=0; slideRoll=slideFov=0; }
            kickRemaining=Mathf.Max(0,kickRemaining-dt);
            ViewCamera.fieldOfView=baseFieldOfView+slideFov;
            ViewCamera.transform.SetPositionAndRotation(state.Position+Vector3.up*(state.CapsuleHeight-.15f),Quaternion.Euler(Pitch,Yaw,roll));
        }
    }
}
