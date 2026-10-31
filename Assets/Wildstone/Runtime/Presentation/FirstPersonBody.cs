using System;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    // D028 local body (vibegame1 PlayerBody pattern): the soldier's animated legs and waist are visible, everything
    // from the upper chest up is collapsed out of the lens, and a shadow-only full body keeps the player's shadow whole.
    public sealed class FirstPersonBody : MonoBehaviour
    {
        // Nothing visible above this height over the feet (standing eye 1.65).
        public const float VisibleCeiling=1.36f;
        // Hips sit this far behind the lens.
        public const float StandingHips=-.30f;
        GameObject visible, shadow;
        Animator visibleAnimator, shadowAnimator;
        Transform collapsed;
        Renderer[] shown;
        Vector3 lastPosition;
        float slide,slideVelocity;
        int life=-1,generation=-1,discontinuityTick=-1;
        bool wasAlive;

        public void Initialize(PresentationCatalog catalog,int team)
        {
            if(catalog==null || team<0 || team>=catalog.Humans.Length || catalog.Humans[team]==null)
                throw new ArgumentException("Missing local team human model.");
            visible=Spawn(catalog.Humans[team],"Local body",out visibleAnimator);
            shadow=Spawn(catalog.Humans[team],"Local body shadow",out shadowAnimator);
            collapsed=visibleAnimator!=null && visibleAnimator.isHuman?visibleAnimator.GetBoneTransform(HumanBodyBones.UpperChest)??visibleAnimator.GetBoneTransform(HumanBodyBones.Chest):null;
            if(collapsed==null) throw new ArgumentException("Local body has no humanoid chest to collapse.");
            collapsed.localScale=Vector3.zero;
            visible.transform.localPosition=new Vector3(0,0,StandingHips);
            shown=Array.FindAll(visible.GetComponentsInChildren<Renderer>(true),renderer=>renderer.enabled);
            foreach(var renderer in visible.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                if(renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen=true;
            }
            foreach(var renderer in shadow.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }
        GameObject Spawn(GameObject prefab,string name,out Animator animator)
        {
            var model=Instantiate(prefab,transform);
            model.name=name; model.transform.localPosition=Vector3.zero;
            foreach(var collider in model.GetComponentsInChildren<Collider>(true)) { collider.enabled=false; Destroy(collider); }
            animator=model.GetComponentInChildren<Animator>(true);
            // Driven manually every frame; never culled, or the shadow copy and a glance down would show the bind pose.
            if(animator!=null) { animator.enabled=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
            // Full detail: the camera is always inside this body.
            foreach(var group in model.GetComponentsInChildren<LODGroup>(true)) { group.ForceLOD(0); }
            return model;
        }

        public void Present(CombatState player,in MotorState motor,float dt,bool reducedMotion)
        {
            if(visible==null || player==null) return;
            bool alive=player.Alive && player.Connected;
            visible.SetActive(alive); shadow.SetActive(alive);
            if(!alive) { wasAlive=false; return; }
            if(!ArenaMotor.Finite(dt) || dt<0) dt=0;
            bool reset=!wasAlive || life!=player.LifeGeneration || generation!=motor.Generation || discontinuityTick!=player.DiscontinuityTick ||
                (motor.Position-lastPosition).sqrMagnitude>Mathf.Pow(4+motor.Velocity.magnitude*dt*2,2);
            if(reset)
            {
                slide=slideVelocity=0;
                visibleAnimator?.Rebind(); shadowAnimator?.Rebind();
                collapsed.localScale=Vector3.zero;
            }
            wasAlive=true; life=player.LifeGeneration; generation=motor.Generation; lastPosition=motor.Position;
            discontinuityTick=player.DiscontinuityTick;
            Vector3 horizontal=new Vector3(motor.Velocity.x,0,motor.Velocity.z);
            float yaw=motor.Yaw;
            // The boots-forward slide turns toward travel; otherwise the body follows the local look yaw.
            if(motor.Sliding && horizontal.magnitude>.5f) yaw=Mathf.Atan2(horizontal.x,horizontal.z)*Mathf.Rad2Deg;
            transform.SetPositionAndRotation(motor.Position,Quaternion.Euler(0,yaw,0));
            Spring(motor.Sliding?1:0,dt,reducedMotion?1:.6f);
            foreach(var animator in new[] { visibleAnimator,shadowAnimator })
            {
                if(animator==null || animator.runtimeAnimatorController==null) continue;
                SoldierRig.DriveLocomotion(animator,motor,yaw,true,false);
                animator.SetInteger(SoldierRig.Param.Stance,SoldierRig.StanceFor(player.SelectedSpecial && player.SpecialWeapon!=WeaponKind.None?player.SpecialWeapon:WeaponKind.LineDriver));
                animator.Update(dt);
            }
            // Players saw their own slide legs as folded shins, cut knees and a boot pile in the view: the 0.75 m slide lens
            // sits among the legs, so the visible body hides from the first slide frame until the slide has settled and
            // the lens is back up. The shadow copy stays (SlideHidesTheVisibleBodyAndKeepsItsShadow).
            bool clear=!motor.Sliding && slide<=.05f;
            foreach(var renderer in shown) renderer.enabled=clear;
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
