using System;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    public sealed class FirstPersonBody : MonoBehaviour
    {
        GameObject model;
        readonly Transform[] bones=new Transform[6];
        readonly Quaternion[] rest=new Quaternion[6];
        Vector3 modelRest,lastPosition;
        float phase,slide,slideVelocity;
        int life=-1,generation=-1,discontinuityTick=-1;
        bool wasAlive;

        public void Initialize(PresentationCatalog catalog,int team)
        {
            if(catalog==null || team<0 || team>=catalog.Humans.Length || catalog.Humans[team]==null)
                throw new ArgumentException("Missing local team human model.");
            model=Instantiate(catalog.Humans[team],transform);
            model.transform.localPosition=Vector3.zero;
            modelRest=model.transform.localPosition;
            foreach(var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled=false;
                Destroy(collider);
            }
            foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                string name=renderer.name;
                renderer.enabled=name=="ThighL" || name=="ThighR" || name=="ShinL" || name=="ShinR" || name=="FootL" || name=="FootR";
                if(renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen=true;
            }
            string[] names={"thigh.L","shin.L","foot.L","thigh.R","shin.R","foot.R"};
            for(int i=0;i<bones.Length;i++)
            {
                bones[i]=GripAttachment.Find(model.transform,names[i]);
                if(bones[i]==null) throw new ArgumentException("Local body is missing "+names[i]+".");
                rest[i]=Quaternion.Inverse(transform.rotation)*bones[i].rotation;
            }
        }

        public void Present(CombatState player,in MotorState motor,float dt,bool reducedMotion)
        {
            if(model==null || player==null) return;
            bool alive=player.Alive && player.Connected;
            model.SetActive(alive);
            if(!alive) { wasAlive=false; return; }
            if(!ArenaMotor.Finite(dt) || dt<0) dt=0;
            bool reset=!wasAlive || life!=player.LifeGeneration || generation!=motor.Generation || discontinuityTick!=player.DiscontinuityTick ||
                (motor.Position-lastPosition).sqrMagnitude>Mathf.Pow(4+motor.Velocity.magnitude*dt*2,2);
            if(reset) { phase=slide=slideVelocity=0; }
            wasAlive=true; life=player.LifeGeneration; generation=motor.Generation; lastPosition=motor.Position;
            discontinuityTick=player.DiscontinuityTick;
            Vector3 horizontal=new Vector3(motor.Velocity.x,0,motor.Velocity.z);
            float speed=horizontal.magnitude;
            float yaw=motor.Yaw;
            if(motor.Sliding && speed>.5f) yaw=Mathf.Atan2(horizontal.x,horizontal.z)*Mathf.Rad2Deg;
            transform.SetPositionAndRotation(motor.Position,Quaternion.Euler(0,yaw,0));
            Spring(motor.Sliding?1:0,dt,reducedMotion?1:.6f);
            float blend=reducedMotion?Mathf.Clamp01(slide):slide;
            float gait=(!motor.Sliding && (motor.Grounded || motor.WallRunning))?Mathf.Clamp01(speed/11):0;
            phase+=speed*1.3f*dt;
            float bob=reducedMotion?0:.02f*gait*Mathf.Abs(Mathf.Cos(phase));
            float hipL=14,hipR=14,kneeL=40,kneeR=40;
            if(motor.Grounded || motor.WallRunning)
            {
                hipR=Mathf.Sin(phase)*30*gait; hipL=-hipR;
                kneeR=Mathf.Max(0,Mathf.Cos(phase))*42*gait;
                kneeL=Mathf.Max(0,-Mathf.Cos(phase))*42*gait;
            }
            hipL=Mathf.LerpUnclamped(hipL,82,blend); kneeL=Mathf.LerpUnclamped(kneeL,12,blend);
            hipR=Mathf.LerpUnclamped(hipR,88,blend); kneeR=Mathf.LerpUnclamped(kneeR,8,blend);
            float sink=.30f*Mathf.Clamp01(blend);
            // Keep boots above the floor through the throw, before the bent legs have gained their full height.
            if(motor.Grounded || motor.Sliding)
                sink=Mathf.Min(sink,Mathf.Max(0,Mathf.Min(HipClearance(hipL,kneeL),HipClearance(hipR,kneeR))-bob));
            // Hips stay under the lens so thighs, calves and boots occupy separate screen angles.
            model.transform.localPosition=modelRest+new Vector3(0,-sink-bob,-.30f+.30f*Mathf.Clamp01(blend));
            PoseLeg(0,hipL,kneeL);
            PoseLeg(3,hipR,kneeR);
        }

        static float HipClearance(float hip,float knee) => .84f-.36f*Mathf.Cos(hip*Mathf.Deg2Rad)-.38f*Mathf.Cos((hip-knee)*Mathf.Deg2Rad)-.10f;

        void PoseLeg(int index,float hip,float knee)
        {
            // Rotate in body space; imported Blender bone local axes are not Unity anatomical axes.
            bones[index].rotation=transform.rotation*Quaternion.Euler(-hip,0,0)*rest[index];
            bones[index+1].rotation=transform.rotation*Quaternion.Euler(-hip+knee,0,0)*rest[index+1];
            bones[index+2].rotation=transform.rotation*rest[index+2];
        }

        void Spring(float target,float dt,float damping)
        {
            float omega=2*Mathf.PI*6,offset=slide-target;
            if(damping>=1)
            {
                float c=slideVelocity+omega*offset,decay=Mathf.Exp(-omega*dt);
                slide=target+(offset+c*dt)*decay;
                slideVelocity=(slideVelocity-omega*c*dt)*decay;
            }
            else
            {
                float damp=omega*damping,frequency=omega*Mathf.Sqrt(1-damping*damping);
                float c=(slideVelocity+damp*offset)/frequency,angle=frequency*dt,decay=Mathf.Exp(-damp*dt);
                float cosine=Mathf.Cos(angle),sine=Mathf.Sin(angle);
                slide=target+decay*(offset*cosine+c*sine);
                slideVelocity=decay*((c*frequency-damp*offset)*cosine-(offset*frequency+damp*c)*sine);
            }
            slide=Mathf.Clamp(slide,0,1.15f);
        }
    }
}
