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
        float rollKick,kickRemaining;

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
        void LateUpdate()
        {
            if(!OwnsLocalView || Motor==null || ViewCamera==null) return;
            var state=Motor.CaptureState();
            Present(state);
            float roll=0;
            if(state.WallRunning)
            {
                Vector3 right=Quaternion.Euler(0,Yaw,0)*Vector3.right;
                roll=(Vector3.Dot(-state.WallNormal,right)>=0?1:-1)*MotorTuning.Source.wallRunCameraRoll;
            }
            roll+=rollKick*(kickRemaining/.28f);
            if(ReducedMotion) roll=0;
            kickRemaining=Mathf.Max(0,kickRemaining-Time.unscaledDeltaTime);
            ViewCamera.transform.SetPositionAndRotation(state.Position+Vector3.up*(state.CapsuleHeight-.15f),Quaternion.Euler(Pitch,Yaw,roll));
        }
    }
}
