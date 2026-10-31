using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    // Shared presentation contract for the D028 soldier (Tools/Assets/soldier_skeleton.py, soldier_clips.py).
    // Parameter names match the controller PresentationBuilder generates; nothing here feeds simulation.
    public static class SoldierRig
    {
        // Nested so the pure helpers below stay usable without the engine (managed tests).
        public static class Param
        {
            public static readonly int MoveX=Animator.StringToHash("MoveX"),
                MoveZ=Animator.StringToHash("MoveZ"),
                Grounded=Animator.StringToHash("Grounded"),
                Sliding=Animator.StringToHash("Sliding"),
                WallRun=Animator.StringToHash("WallRun"),
                WallSide=Animator.StringToHash("WallSide"),
                Dashing=Animator.StringToHash("Dashing"),
                VerticalSpeed=Animator.StringToHash("VerticalSpeed"),
                Dead=Animator.StringToHash("Dead"),
                DeathBack=Animator.StringToHash("DeathBack"),
                Stance=Animator.StringToHash("Stance"),
                Aim=Animator.StringToHash("Aim"),
                Reload=Animator.StringToHash("Reload"),
                Throw=Animator.StringToHash("Throw"),
                Swing=Animator.StringToHash("Swing"),
                Equip=Animator.StringToHash("Equip"),
                Fire=Animator.StringToHash("Fire"),
                HitFront=Animator.StringToHash("HitFront"),
                HitBack=Animator.StringToHash("HitBack"),
                LocomotionRate=Animator.StringToHash("LocomotionRate");
        }
        // Team accents follow the established signal palette; the TeamMark symbol mesh carries the non-colour cue.
        public static readonly Color[] TeamAccents={new Color(.16f,.73f,.78f),new Color(.98f,.92f,.50f),new Color(.40f,.055f,.10f),new Color(.43f,.25f,.60f)};
        public static Color TeamAccent(int team) => TeamAccents[Mathf.Clamp(team,0,TeamAccents.Length-1)];
        // Authored in-place run speeds (m/s, soldier_clips.py RunF/RunB/RunL-R); faster travel speeds the cycle up instead of skating.
        public const float RunCycleSpeed=7.94f,RunBackSpeed=7.832f,RunSideSpeed=8.018f;

        // Cycle rate for a facing-frame velocity: 1 inside the authored run envelope (an ellipse through the four run
        // clips), otherwise how far beyond it, so move/rate lands on the envelope and the blended stride matches travel.
        public static float LocomotionScale(Vector2 move)
        {
            float x=move.x/RunSideSpeed,z=move.y/(move.y>=0?RunCycleSpeed:RunBackSpeed);
            float scale=Mathf.Sqrt(x*x+z*z);
            return ArenaMotor.Finite(scale)?Mathf.Max(1,scale):1;
        }

        // Clip contract with Tools/Assets/soldier_clips.py (SoldierAnimations.fbx); Editor SoldierBuilder imports it.
        public static readonly string[] LocomotionClips={"Idle","WalkF","WalkB","WalkL","WalkR","RunF","RunB","RunL","RunR"};
        public static readonly string[] Clips={"Idle","WalkF","WalkB","WalkL","WalkR","RunF","RunB","RunL","RunR","JumpStart","Fall","Land",
            "Dash","SlideEnter","Slide","WallRunL","WallRunR","DeathF","DeathB","HoldRifle","HoldPistol","HoldSword","AimRifle","AimPistol",
            "Reload","Throw","SwordSwing","Equip","Recoil","HitF","HitB"};
        public static readonly string[] LoopingClips={"Idle","WalkF","WalkB","WalkL","WalkR","RunF","RunB","RunL","RunR","Fall","Slide","WallRunL","WallRunR",
            "HoldRifle","HoldPistol","HoldSword","AimRifle","AimPistol"};

        // 0 rifle (two-handed guns), 1 pistol (one-handed and thrown), 2 sword.
        public static int StanceFor(WeaponKind kind) =>
            kind==WeaponKind.LaserSword?2:kind==WeaponKind.LaserPistol || kind==WeaponKind.StunGun || kind==WeaponKind.Grenade?1:0;

        // Horizontal velocity in the facing frame, metres per second: x right, y forward.
        public static Vector2 LocalMove(in MotorState motor,float yaw)
        {
            // Inverse yaw rotation written out so it stays engine-free.
            float radians=yaw*Mathf.Deg2Rad,cos=Mathf.Cos(radians),sin=Mathf.Sin(radians);
            float x=motor.Velocity.x*cos-motor.Velocity.z*sin,z=motor.Velocity.x*sin+motor.Velocity.z*cos;
            return ArenaMotor.Finite(x) && ArenaMotor.Finite(z)?new Vector2(x,z):Vector2.zero;
        }

        public static void DriveLocomotion(Animator animator,in MotorState motor,float yaw,bool alive,bool deathBack)
        {
            if(animator==null || animator.runtimeAnimatorController==null) return;
            var move=LocalMove(motor,yaw);
            float rate=LocomotionScale(move);
            animator.SetFloat(Param.MoveX,move.x/rate); animator.SetFloat(Param.MoveZ,move.y/rate);
            animator.SetFloat(Param.LocomotionRate,rate);
            animator.SetBool(Param.Grounded,motor.Grounded || motor.WallRunning);
            animator.SetBool(Param.Sliding,motor.Sliding);
            animator.SetBool(Param.WallRun,motor.WallRunning);
            if(motor.WallRunning)
            {
                Vector3 right=Quaternion.Euler(0,yaw,0)*Vector3.right;
                animator.SetFloat(Param.WallSide,Vector3.Dot(-motor.WallNormal,right)>=0?1:-1);
            }
            animator.SetBool(Param.Dashing,(motor.Tick+1)*(double)MotorTuning.TickDelta<motor.DashUntil); // ArenaMotor.Now
            animator.SetFloat(Param.VerticalSpeed,ArenaMotor.Finite(motor.Velocity.y)?motor.Velocity.y:0);
            animator.SetBool(Param.Dead,!alive);
            animator.SetBool(Param.DeathBack,deathBack);
        }

        // Two-bone IK without anatomical axis assumptions: set the elbow angle, aim at the target, swing to the pole.
        public static void SolveTwoBone(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
        {
            if(upper==null || lower==null || end==null) return;
            Vector3 a=upper.position,b=lower.position,c=end.position;
            float lab=Vector3.Distance(a,b),lbc=Vector3.Distance(b,c);
            if(lab<1e-4f || lbc<1e-4f) return;
            float reach=Mathf.Clamp(Vector3.Distance(a,target),Mathf.Abs(lab-lbc)+1e-3f,lab+lbc-1e-4f);
            float wanted=Mathf.Acos(Mathf.Clamp((lab*lab+lbc*lbc-reach*reach)/(2*lab*lbc),-1,1))*Mathf.Rad2Deg;
            Vector3 toUpper=(a-b).normalized,toEnd=(c-b).normalized;
            Vector3 normal=Vector3.Cross(toUpper,toEnd);
            if(normal.sqrMagnitude<1e-8f) normal=Vector3.Cross(toUpper,pole-b);
            if(normal.sqrMagnitude<1e-8f) return;
            Vector3 bent=Quaternion.AngleAxis(wanted,normal.normalized)*toUpper;
            lower.rotation=Quaternion.FromToRotation(toEnd,bent)*lower.rotation;
            upper.rotation=Quaternion.FromToRotation(end.position-a,target-a)*upper.rotation;
            Vector3 axis=(target-a).normalized;
            Vector3 elbow=Vector3.ProjectOnPlane(lower.position-a,axis),wish=Vector3.ProjectOnPlane(pole-a,axis);
            if(elbow.sqrMagnitude>1e-8f && wish.sqrMagnitude>1e-8f) upper.rotation=Quaternion.FromToRotation(elbow,wish)*upper.rotation;
        }
    }

    // Per-digit curl about an axis measured from the rest pose (vibegame1 HandPose, generalised):
    // each joint bends its child toward the palm, so no imported local axis convention is assumed.
    public sealed class FingerCurl
    {
        readonly Transform[] joints;
        readonly Quaternion[] rest;
        readonly Vector3[] axes;
        readonly int[] digit;
        public int Count=>joints.Length;
        FingerCurl(Transform[] joints,Quaternion[] rest,Vector3[] axes,int[] digit) { this.joints=joints; this.rest=rest; this.axes=axes; this.digit=digit; }

        // side "Left"/"Right"; palm points from the hand toward the palm side in the rest pose.
        public static FingerCurl Capture(Transform root,string side,Vector3 palmWorld)
        {
            string[] digits={"Thumb","Index","Middle","Ring","Little"};
            string[] segments={"Proximal","Intermediate","Distal"};
            var joints=new System.Collections.Generic.List<Transform>();
            var rest=new System.Collections.Generic.List<Quaternion>();
            var axes=new System.Collections.Generic.List<Vector3>();
            var index=new System.Collections.Generic.List<int>();
            for(int d=0;d<digits.Length;d++)
                for(int s=0;s<segments.Length;s++)
                {
                    var joint=GripAttachment.Find(root,side+digits[d]+segments[s]);
                    if(joint==null) continue;
                    Vector3 along=joint.childCount>0?joint.GetChild(0).position-joint.position:joint.position-joint.parent.position;
                    Vector3 axis=Vector3.Cross(along,palmWorld);
                    if(axis.sqrMagnitude<1e-10f) continue;
                    joints.Add(joint); rest.Add(joint.localRotation); index.Add(d);
                    axes.Add(joint.InverseTransformDirection(axis.normalized));
                }
            return new FingerCurl(joints.ToArray(),rest.ToArray(),axes.ToArray(),index.ToArray());
        }
        // curl[digit] in 0..1 (thumb, index, middle, ring, little); maxDegrees per joint.
        public void Apply(float[] curl,float maxDegrees=72)
        {
            for(int i=0;i<joints.Length;i++)
            {
                float amount=Mathf.Clamp01(curl[digit[i]]);
                joints[i].localRotation=rest[i]*Quaternion.AngleAxis(amount*maxDegrees*(digit[i]==0?.6f:1),axes[i]);
            }
        }
    }
}
