using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    [DefaultExecutionOrder(200)]
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
        CombatState presented;
        int life=-1, connection=-1, discontinuity=-1;
        WeaponKind handlingWeapon;
        float recoil, recoilDuration=.125f, recoilPitch=8, recoilTwist, reload, motionStrength=1;
        public void Initialize(PresentationCatalog catalog,int team)
        {
            if(team<0 || team>=catalog.Humans.Length || catalog.Humans[team]==null) throw new System.ArgumentException("Missing team human model.");
            var model=Instantiate(catalog.Humans[team],transform);
            model.transform.localPosition=Vector3.zero;
            animator=model.GetComponentInChildren<Animator>(true);
            hand=GripAttachment.Find(model.transform,"WeaponHand_R");
            if(hand==null) throw new System.ArgumentException("Human model is missing WeaponHand_R.");
        }
        public void Present(CombatState player,in MotorState motor,PresentationCatalog catalog, Transform renderPose = null)
        {
            aim=player.Aim.sqrMagnitude>.001f?player.Aim.normalized:Quaternion.Euler(0,motor.Yaw,0)*Vector3.forward;
            float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
            transform.SetPositionAndRotation(renderPose!=null?renderPose.position:player.Feet, renderPose!=null?renderPose.rotation:Quaternion.Euler(0,yaw,0));
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
            gameObject.SetActive(true);
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
        public void PresentHandling(CombatState player,int tick,float dt,bool reducedMotion,int tickRate=WeaponCatalog.DefaultTickRate)
        {
            if(life!=player.LifeGeneration || connection!=player.ConnectionGeneration || discontinuity!=player.DiscontinuityTick ||
                handlingWeapon!=currentWeapon || !player.Alive || !player.Connected)
                recoil=0;
            presented=player;
            life=player.LifeGeneration; connection=player.ConnectionGeneration; discontinuity=player.DiscontinuityTick;
            handlingWeapon=currentWeapon;
            motionStrength=reducedMotion?.2f:1;
            recoil=Mathf.MoveTowards(recoil,0,Mathf.Max(0,dt)/recoilDuration);
            reload=player.Alive && player.Connected && currentWeapon==WeaponKind.LineDriver && player.ReloadUntil>tick && tickRate>0?
                Mathf.Sin(Mathf.PI*Mathf.Clamp01((player.ReloadUntil-tick)/(WeaponCatalog.ReloadSeconds*tickRate))):0;
        }
        public void Play(CombatEvent value)
        {
            if(presented==null || !gameObject.activeInHierarchy || !presented.Alive || !presented.Connected ||
                value.Kind!=CombatEventKind.Shot || value.Action.Participant!=presented.Participant || value.Action.LifeGeneration!=life) return;
            // Committed same-life shots can retain a historical connection after host recovery.
            var kind=value.Weapon==WeaponKind.None?currentWeapon:value.Weapon;
            if(kind!=currentWeapon || kind==WeaponKind.Grenade) return;
            recoil=1;
            recoilDuration=kind==WeaponKind.LaserSniper?.38f:kind==WeaponKind.LaserShotgun?.30f:kind==WeaponKind.LaserPistol?.16f:kind==WeaponKind.PulseLobber?.28f:kind==WeaponKind.PushLens?.25f:kind==WeaponKind.ReboundDisc?.20f:.125f;
            recoilPitch=kind==WeaponKind.LaserSniper?17:kind==WeaponKind.LaserShotgun?16:kind==WeaponKind.LaserPistol?9:kind==WeaponKind.PulseLobber?14:kind==WeaponKind.StunGun?4:8;
            recoilTwist=kind==WeaponKind.LaserShotgun?5:kind==WeaponKind.LaserPistol?3:kind==WeaponKind.ReboundDisc?-10:0;
        }
        void OnDisable() { recoil=reload=0; }
        void LateUpdate()
        {
            if(hand!=null && animationName!="Death" && aim.sqrMagnitude>.001f)
                // Rotate the wrist after Animator; its attached weapon retains the authored grip.
                hand.rotation=Quaternion.LookRotation(aim,Vector3.up)*Quaternion.Euler(
                    motionStrength*(-recoil*recoilPitch+reload*18),0,motionStrength*(recoil*recoilTwist-reload*22));
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
