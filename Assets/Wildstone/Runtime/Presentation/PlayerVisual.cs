using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    public sealed class PlayerVisual : MonoBehaviour
    {
        Animator animator;
        Transform hand;
        GameObject weapon;
        WeaponKind currentWeapon;
        string animationName;
        Vector3 aim=Vector3.forward;
        bool wasAlive=true;
        float deathUntil;
        public void Initialize(PresentationCatalog catalog,int team)
        {
            if(team<0 || team>=catalog.Humans.Length || catalog.Humans[team]==null) throw new System.ArgumentException("Missing team human model.");
            var model=Instantiate(catalog.Humans[team],transform);
            model.transform.localPosition=Vector3.zero;
            animator=model.GetComponentInChildren<Animator>(true);
            hand=GripAttachment.Find(model.transform,"WeaponHand_R");
            if(hand==null) throw new System.ArgumentException("Human model is missing WeaponHand_R.");
        }
        public void Present(CombatState player,in MotorState motor,PresentationCatalog catalog)
        {
            aim=player.Aim.sqrMagnitude>.001f?player.Aim.normalized:Quaternion.Euler(0,motor.Yaw,0)*Vector3.forward;
            float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
            transform.SetPositionAndRotation(player.Feet,Quaternion.Euler(0,yaw,0));
            if(!player.Alive)
            {
                if(wasAlive)
                {
                    deathUntil=Time.realtimeSinceStartup+1.25f;
                    animator?.Play("Death",0,0);
                    animationName="Death";
                }
                wasAlive=false;
                gameObject.SetActive(player.Connected && Time.realtimeSinceStartup<deathUntil);
                return;
            }
            if(!wasAlive) animationName=null;
            wasAlive=true;
            gameObject.SetActive(player.Connected);
            if(!gameObject.activeSelf) return;
            string next=SelectAnimation(player,motor);
            if(next!=animationName && animator!=null) { animator.CrossFade(next,.12f); animationName=next; }
            WeaponKind selected=player.SelectedSpecial && player.SpecialWeapon!=WeaponKind.None?player.SpecialWeapon:WeaponKind.LineDriver;
            if(selected==currentWeapon) return;
            if(weapon!=null) Destroy(weapon);
            currentWeapon=selected;
            var prefab=catalog.Weapon(selected);
            if(prefab==null) return;
            weapon=Instantiate(prefab,hand);
            GripAttachment.Align(weapon.transform,hand);
        }
        void LateUpdate()
        {
            if(hand!=null && animationName!="Death" && aim.sqrMagnitude>.001f)
                hand.rotation=Quaternion.LookRotation(aim,Vector3.up);
        }
        public static string SelectAnimation(CombatState player,in MotorState motor)
        {
            if(!player.Alive) return "Death";
            if(!motor.Grounded) return "Jump";
            if(motor.Sliding) return "Slide";
            var motion=new Vector3(motor.Velocity.x,0,motor.Velocity.z);
            if(motion.sqrMagnitude>1)
            {
                var right=Vector3.Cross(Vector3.up,player.Aim.normalized);
                if(Mathf.Abs(Vector3.Dot(motion,right))>Mathf.Abs(Vector3.Dot(motion,player.Aim.normalized))*.8f) return "Strafe";
                return "Run";
            }
            return "Aim";
        }
    }
}
