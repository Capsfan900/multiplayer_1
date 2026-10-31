using System.Collections.Generic;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    [DefaultExecutionOrder(200)]
    public sealed class PlayerVisual : MonoBehaviour
    {
        Animator animator;
        Transform hand, model;
        Transform spine, chest, upperChest, neck, head, leftUpper, leftLower, leftHand, rightHand;
        Transform support, glint, muzzle;
        GameObject weapon;
        WeaponKind currentWeapon;
        Vector3 aim=Vector3.forward;
        bool wasAlive=true, deathBack, reloading;
        float deathUntil, ads;
        CombatState presented;
        int life=-1, connection=-1, discontinuity=-1;
        WeaponKind handlingWeapon;
        float recoil, recoilDuration=.125f, recoilPitch=8, recoilTwist, reload, motionStrength=1;
        static readonly float[] PitchShare={.25f,.25f,.2f,.15f,.15f};
        // D028 derez (Tron-style elimination): a short death beat, then the frozen body dissolves top-down into
        // team-coloured voxels (Wildstone/Derez) that burst outward and finish in a flash. Presentation only.
        public const float DerezDelay=.18f, DerezSeconds=.7f, DerezBand=.35f, DerezCell=.045f;
        static readonly Dictionary<Material,Material> DerezMaterials=new Dictionary<Material,Material>();
        static readonly int Progress=Shader.PropertyToID("_Progress"),Bottom=Shader.PropertyToID("_Bottom"),Height=Shader.PropertyToID("_Height"),EdgeColor=Shader.PropertyToID("_EdgeColor");
        Material derezTemplate;
        Renderer[] derezRenderers;
        Material[][] savedMaterials;
        MaterialPropertyBlock derezBlock;
        Vector3[] derezPoints;
        float[] derezKeys;
        Vector3 derezPush;
        Color accent;
        float derezStart, derezProgress, derezBottom, derezHeight;
        bool frozen, popped;
        // Confirmed-hit flash: the whole body lights white for a moment (brighter on a headshot).
        public const float HitFlashSeconds=.12f;
        static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");
        Renderer[] flashRenderers;
        MaterialPropertyBlock flashBlock;
        float hitFlash, flashPeak;
        public float HitFlash => hitFlash;
        public ArenaVfx Vfx { get; set; }
        public bool Derezzing => frozen;
        // Remote soldiers sit on the Enemy layer, which RenderingSetup's dark "Soldier outline" pass draws; the
        // dissolving body leaves it so the hull cannot outline voxels that are already gone.
        public const int OutlineLayer=7;
        public float DerezProgress => derezProgress;
        public void Initialize(PresentationCatalog catalog,int team)
        {
            if(team<0 || team>=catalog.Humans.Length || catalog.Humans[team]==null) throw new System.ArgumentException("Missing team human model.");
            model=Instantiate(catalog.Humans[team],transform).transform;
            accent=SoldierRig.TeamAccent(team); derezTemplate=catalog.Derez;
            model.localPosition=Vector3.zero;
            animator=model.GetComponentInChildren<Animator>(true);
            flashRenderers=model.GetComponentsInChildren<Renderer>(true);
            SetOutline(true);
            // Stepped by PresentHandling with the gameplay clock, so menus and pauses freeze the pose. Always animated:
            // culling skipped the bones of a body not yet seen (first frame, every respawn), which drew one T-pose frame
            // and left an off-screen shooter's muzzle, where its beam starts, stale.
            if(animator!=null) { animator.enabled=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
            hand=GripAttachment.Find(model,"WeaponHand_R");
            if(hand==null) throw new System.ArgumentException("Human model is missing WeaponHand_R.");
            spine=Bone(HumanBodyBones.Spine); chest=Bone(HumanBodyBones.Chest); upperChest=Bone(HumanBodyBones.UpperChest);
            neck=Bone(HumanBodyBones.Neck); head=Bone(HumanBodyBones.Head);
            leftUpper=Bone(HumanBodyBones.LeftUpperArm); leftLower=Bone(HumanBodyBones.LeftLowerArm); leftHand=Bone(HumanBodyBones.LeftHand);
            rightHand=Bone(HumanBodyBones.RightHand);
            if(catalog.VfxGlow!=null)
            {
                // Sniper scope glint: an opponent aiming down the scope is visible from the front.
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name="Scope glint"; quad.transform.SetParent(transform,false);
                var renderer=quad.GetComponent<MeshRenderer>(); renderer.sharedMaterial=catalog.VfxGlow;
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
                glint=quad.transform; quad.SetActive(false);
            }
        }
        Transform Bone(HumanBodyBones bone) => animator!=null && animator.isHuman?animator.GetBoneTransform(bone):null;
        public void Present(CombatState player,in MotorState motor,PresentationCatalog catalog, Transform renderPose = null)
        {
            aim=player.Aim.sqrMagnitude>.001f?player.Aim.normalized:Quaternion.Euler(0,motor.Yaw,0)*Vector3.forward;
            float yaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
            transform.SetPositionAndRotation(renderPose!=null?renderPose.position:player.Feet, renderPose!=null?renderPose.rotation:Quaternion.Euler(0,yaw,0));
            SoldierRig.DriveLocomotion(animator,motor,transform.eulerAngles.y,player.Alive,deathBack);
            if(!player.Alive)
            {
                float now=Time.unscaledTime;
                if(wasAlive) { deathUntil=now+DerezDelay+DerezSeconds+.1f; derezStart=now+DerezDelay; derezProgress=0; popped=false; }
                wasAlive=false;
                bool show=player.Connected && now<deathUntil;
                gameObject.SetActive(show);
                if(show) Derez(now);
                return;
            }
            if(!wasAlive) { deathBack=false; RestoreBody(); animator?.Rebind(); }
            wasAlive=true;
            gameObject.SetActive(true);
            WeaponKind selected=player.SelectedSpecial && player.SpecialWeapon!=WeaponKind.None?player.SpecialWeapon:WeaponKind.LineDriver;
            ads=WeaponCatalog.CanAim(selected)?Mathf.Clamp01(player.AdsProgress):0;
            if(animator!=null && animator.runtimeAnimatorController!=null)
            {
                animator.SetInteger(SoldierRig.Param.Stance,SoldierRig.StanceFor(selected));
                animator.SetFloat(SoldierRig.Param.Aim,ads);
            }
            if(selected==currentWeapon) return;
            if(weapon!=null) Destroy(weapon);
            currentWeapon=selected; support=null; muzzle=null;
            var prefab=catalog.Weapon(selected);
            if(prefab==null) return;
            weapon=Instantiate(prefab,hand);
            GripAttachment.Align(weapon.transform,hand);
            muzzle=GripAttachment.Find(weapon.transform,"Muzzle");
            if(SoldierRig.StanceFor(selected)==0) support=GripAttachment.Find(weapon.transform,"LeftHand");
            if(animator!=null && animator.runtimeAnimatorController!=null) animator.SetTrigger(SoldierRig.Param.Equip);
        }
        // Remote beams leave the rendered barrel instead of the authoritative eye (presentation only).
        public bool TryGetMuzzle(out Vector3 position)
        {
            position=muzzle!=null?muzzle.position:Vector3.zero;
            return muzzle!=null && wasAlive && gameObject.activeInHierarchy;
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
            if(hitFlash>0) { hitFlash=Mathf.MoveTowards(hitFlash,0,Mathf.Max(0,dt)/HitFlashSeconds); PresentFlash(); }
            reloading=player.Alive && player.Connected && currentWeapon==WeaponKind.LineDriver && player.ReloadUntil>tick && tickRate>0;
            reload=reloading?Mathf.Sin(Mathf.PI*Mathf.Clamp01((player.ReloadUntil-tick)/(WeaponCatalog.ReloadSeconds*tickRate))):0;
            if(animator!=null && animator.runtimeAnimatorController!=null && gameObject.activeInHierarchy && !frozen)
            {
                animator.SetBool(SoldierRig.Param.Reload,reloading);
                // Update(0) still re-evaluates, so LateUpdate's spine pitch and IK never accumulate while paused.
                animator.Update(Mathf.Max(0,dt));
            }
        }
        public void Play(CombatEvent value)
        {
            if(presented==null || !gameObject.activeInHierarchy || !presented.Alive || !presented.Connected ||
                value.Kind!=CombatEventKind.Shot || value.Action.Participant!=presented.Participant || value.Action.LifeGeneration!=life) return;
            // Committed same-life shots can retain a historical connection after host recovery.
            var kind=value.Weapon==WeaponKind.None?currentWeapon:value.Weapon;
            if(kind==WeaponKind.Grenade && animator!=null && animator.runtimeAnimatorController!=null) animator.SetTrigger(SoldierRig.Param.Throw);
            if(kind!=currentWeapon || kind==WeaponKind.Grenade) return;
            recoil=1;
            recoilDuration=kind==WeaponKind.LaserSniper?.38f:kind==WeaponKind.LaserShotgun?.30f:kind==WeaponKind.LaserPistol?.16f:kind==WeaponKind.PulseLobber?.28f:kind==WeaponKind.PushLens?.25f:kind==WeaponKind.ReboundDisc?.20f:kind==WeaponKind.LaserSMG?.09f:.125f;
            recoilPitch=kind==WeaponKind.LaserSniper?17:kind==WeaponKind.LaserShotgun?16:kind==WeaponKind.LaserPistol?9:kind==WeaponKind.PulseLobber?14:kind==WeaponKind.StunGun?4:kind==WeaponKind.LaserSMG?5:8;
            recoilTwist=kind==WeaponKind.LaserShotgun?5:kind==WeaponKind.LaserPistol?3:kind==WeaponKind.ReboundDisc?-10:0;
            if(animator!=null && animator.runtimeAnimatorController!=null)
                animator.SetTrigger(kind==WeaponKind.LaserSword?SoldierRig.Param.Swing:SoldierRig.Param.Fire);
        }
        // Confirmed damage on this player: a short additive flinch, and the fall direction if it was lethal.
        public void React(in CombatEvent value)
        {
            if(!gameObject.activeInHierarchy) return;
            if(value.Kind==CombatEventKind.Hit && !frozen)
            {
                // Reduced motion keeps a dimmer flash so rapid fire never strobes.
                flashPeak=(value.Headshot?2.2f:1.4f)*(motionStrength<1?.35f:1);
                hitFlash=1; PresentFlash();
            }
            if(animator==null || animator.runtimeAnimatorController==null) return;
            bool front=Vector3.Dot(value.Position-transform.position,transform.forward)>=0;
            deathBack=front;
            // Voxels blow away from the impact side.
            var away=transform.position-value.Position; away.y=0;
            derezPush=away.sqrMagnitude>1e-4f?away.normalized:Vector3.zero;
            animator.SetTrigger(front?SoldierRig.Param.HitFront:SoldierRig.Param.HitBack);
        }
        // A renderer-wide block, so the derez block replaces it outright; cleared when the flash ends.
        void PresentFlash()
        {
            if(flashRenderers==null || frozen) return;
            flashBlock??=new MaterialPropertyBlock();
            flashBlock.Clear();
            if(hitFlash>0) flashBlock.SetColor(EmissionColor,Color.white*flashPeak*hitFlash);
            foreach(var renderer in flashRenderers)
                if(renderer!=null) renderer.SetPropertyBlock(hitFlash>0?flashBlock:null);
        }
        void Derez(float now)
        {
            if(now<derezStart) return;
            if(!frozen) BeginDerez();
            float previous=derezProgress;
            derezProgress=Mathf.Clamp01((now-derezStart)/DerezSeconds);
            derezBlock.SetFloat(Progress,derezProgress);
            foreach(var renderer in derezRenderers) if(renderer!=null) renderer.SetPropertyBlock(derezBlock);
            bool reduced=motionStrength<1;
            if(Vfx!=null)
            {
                var center=transform.position+Vector3.up*derezHeight*.55f;
                for(int i=0;i<derezPoints.Length;i++)
                {
                    // A voxel leaves when the shader sweep reaches it (clipped once key > 1 - progress).
                    float leave=1-derezKeys[i];
                    if(leave<previous || leave>=derezProgress) continue;
                    float r1=Mathf.Repeat(i*.618f,1),r2=Mathf.Repeat(i*.377f+.2f,1),r3=Mathf.Repeat(i*.731f+.5f,1);
                    var outward=derezPoints[i]-center; outward.y*=.35f; outward=outward.sqrMagnitude>1e-4f?outward.normalized:Vector3.up;
                    // Later voxels leave faster, so the dissolve ends as an explosion.
                    float burst=(1+2.5f*derezProgress*derezProgress)*(reduced?.45f:1);
                    var velocity=(outward*(1.2f+2f*r1)+Vector3.up*(.5f+1.3f*r2)+derezPush*(.8f+1.8f*r3))*burst;
                    var color=r3<.18f?Color.Lerp(accent,Color.white,.6f)*2.4f:accent*2.2f;
                    Vfx.Pixel(derezPoints[i],velocity,.035f+.03f*r2,.5f+.5f*r1,color,i*37f);
                }
                if(derezProgress>=1 && !popped)
                {
                    Vfx.Flash(center,accent*2.4f,reduced?.8f:1.6f,.12f);
                    Vfx.Lamp(center,accent,2.5f,4,.15f);
                    if(!reduced) Vfx.SparkBurst(center,Vector3.up,accent*1.6f,14,5,1.8f);
                    Vfx.Wave(transform.position,Vector3.up,1.4f,accent*.8f,.35f);
                }
            }
            if(derezProgress>=1 && !popped) { popped=true; model.gameObject.SetActive(false); }
        }
        void BeginDerez()
        {
            frozen=true; SetOutline(false);
            derezBlock??=new MaterialPropertyBlock();
            derezBottom=transform.position.y; derezHeight=1.8f;
            derezRenderers=model.GetComponentsInChildren<Renderer>(false);
            savedMaterials=new Material[derezRenderers.Length][];
            SkinnedMeshRenderer sample=null;
            for(int i=0;i<derezRenderers.Length;i++)
            {
                var renderer=derezRenderers[i];
                savedMaterials[i]=renderer.sharedMaterials;
                derezHeight=Mathf.Max(derezHeight,renderer.bounds.max.y-derezBottom);
                if(renderer is SkinnedMeshRenderer skin && skin.sharedMesh!=null && (sample==null || skin.sharedMesh.vertexCount<sample.sharedMesh.vertexCount)) sample=skin;
                if(derezTemplate==null) continue;
                var swapped=new Material[savedMaterials[i].Length];
                for(int m=0;m<swapped.Length;m++) swapped[m]=DerezMaterial(savedMaterials[i][m]);
                renderer.sharedMaterials=swapped;
            }
            derezHeight=Mathf.Min(derezHeight,2.4f);
            derezBlock.Clear();
            derezBlock.SetFloat(Bottom,derezBottom); derezBlock.SetFloat(Height,derezHeight);
            derezBlock.SetColor(EdgeColor,accent*2.6f);
            // The cheapest LOD is enough to place the voxels on the frozen pose.
            derezPoints=System.Array.Empty<Vector3>(); derezKeys=System.Array.Empty<float>();
            if(sample==null || Vfx==null) return;
            var baked=new Mesh();
            try
            {
                sample.BakeMesh(baked,true);
                var vertices=baked.vertices;
                int count=Mathf.Min(vertices.Length,motionStrength<1?90:280);
                if(count==0) return;
                var space=Matrix4x4.TRS(sample.transform.position,sample.transform.rotation,Vector3.one);
                derezPoints=new Vector3[count]; derezKeys=new float[count];
                for(int i=0;i<count;i++)
                {
                    var point=space.MultiplyPoint3x4(vertices[i*vertices.Length/count]);
                    derezPoints[i]=point;
                    float height=Mathf.Clamp01((Mathf.Floor(point.y/DerezCell)*DerezCell-derezBottom)/derezHeight);
                    derezKeys[i]=height*(1-DerezBand)+Mathf.Repeat(i*.4142f,1)*DerezBand;
                }
            }
            finally { Destroy(baked); }
        }
        Material DerezMaterial(Material source)
        {
            if(source==null) return derezTemplate;
            if(DerezMaterials.TryGetValue(source,out var cached) && cached!=null) return cached;
            var material=new Material(derezTemplate) { name=source.name+" (derez)" };
            if(source.HasProperty("_BaseColor")) material.SetColor("_BaseColor",source.GetColor("_BaseColor"));
            DerezMaterials[source]=material;
            return material;
        }
        void RestoreBody()
        {
            if(model!=null) model.gameObject.SetActive(true);
            if(derezRenderers!=null)
                for(int i=0;i<derezRenderers.Length;i++)
                    if(derezRenderers[i]!=null) { derezRenderers[i].sharedMaterials=savedMaterials[i]; derezRenderers[i].SetPropertyBlock(null); }
            derezRenderers=null; savedMaterials=null; frozen=popped=false; derezProgress=0;
            SetOutline(true);
        }
        void SetOutline(bool on)
        {
            foreach(var renderer in flashRenderers) if(renderer!=null) renderer.gameObject.layer=on?OutlineLayer:0;
        }
        void OnDisable() { recoil=reload=0; if(glint!=null) glint.gameObject.SetActive(false); }
        void LateUpdate()
        {
            if(!wasAlive || aim.sqrMagnitude<=.001f) return;
            // Aim pitch spreads over spine, chest and neck so the upper body follows the replicated aim.
            float pitch=-Mathf.Asin(Mathf.Clamp(aim.y,-1,1))*Mathf.Rad2Deg;
            Vector3 right=transform.right;
            var chain=new[] { spine,chest,upperChest,neck,head };
            for(int i=0;i<chain.Length;i++)
                if(chain[i]!=null) chain[i].rotation=Quaternion.AngleAxis(pitch*PitchShare[i],right)*chain[i].rotation;
            // Turn the wrist after Animator so the socket, and the weapon gripped in it, follows the replicated aim.
            var held=Quaternion.LookRotation(aim,Vector3.up)*Quaternion.Euler(
                motionStrength*(-recoil*recoilPitch+reload*18),0,motionStrength*(recoil*recoilTwist-reload*22));
            if(rightHand!=null && hand.parent==rightHand) rightHand.rotation=held*Quaternion.Inverse(hand.localRotation);
            else hand.rotation=held;
            if(support!=null && !reloading)
                SoldierRig.SolveTwoBone(leftUpper,leftLower,leftHand,support.position,leftLower!=null?leftLower.position-transform.up*.3f-transform.right*.2f:support.position);
            if(glint!=null)
            {
                bool show=currentWeapon==WeaponKind.LaserSniper && ads>.5f && weapon!=null && Camera.main!=null;
                glint.gameObject.SetActive(show);
                if(show)
                {
                    var lens=GripAttachment.Find(weapon.transform,"ScopeLens");
                    var cam=Camera.main.transform;
                    glint.position=(lens!=null?lens.position:hand.position)+aim*.05f;
                    glint.rotation=Quaternion.LookRotation(glint.position-cam.position);
                    float facing=Mathf.Clamp01(Vector3.Dot(aim,(cam.position-glint.position).normalized));
                    glint.localScale=Vector3.one*(.15f+.9f*facing*facing*ads)*Mathf.Clamp(Vector3.Distance(cam.position,glint.position)/20,1,4);
                }
            }
        }
    }
}
