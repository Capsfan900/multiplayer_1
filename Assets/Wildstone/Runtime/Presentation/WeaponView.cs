using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    public sealed class WeaponView : MonoBehaviour
    {
        const int ViewLayer=11;
        Camera overlay;
        GameObject weapon;
        WeaponKind currentWeapon;
        Transform socket;
        bool worldHadViewLayer;
        Transform hands, rightHand, leftHand, support;
        readonly Transform[] sleeves=new Transform[2];
        readonly Vector3[] shoulders=new Vector3[2], restReach=new Vector3[2], cuffs=new Vector3[2];
        readonly Transform[] fingers=new Transform[8];
        readonly Quaternion[] fingerRest=new Quaternion[8];
        Vector3 sourceRight, sourceUp, sourceForward, sleeveUp, handForward;
        int discontinuity=-1;
        Vector3 readyPosition, leftRest;
        Quaternion leftRotation;
        float phase, recoil, throwTime, landing, slide;
        int life=-1;
        bool hadGround;
        Transform muzzle, movingPart;
        Vector3 partRest, partAxis;
        Quaternion partRotation;
        readonly LineRenderer[] muzzleElements=new LineRenderer[4];
        Vector3 shotDirection;
        float flashRemaining, flashDuration, recoilDuration=.125f, recoilDistance=.055f, recoilPitch=8, recoilTwist;
        Material shotMaterial;
        bool visible;

        public void Initialize(Camera world,PresentationCatalog catalog)
        {
            if(world==null || catalog.Hands==null) throw new System.ArgumentException("First-person camera or hands missing.");
            var holder=new GameObject("Viewmodel",typeof(Camera));
            holder.transform.SetParent(world.transform,false);
            overlay=holder.GetComponent<Camera>();
            overlay.CopyFrom(world);
            overlay.cullingMask=1<<ViewLayer;
            overlay.clearFlags=CameraClearFlags.Depth;
            overlay.depth=world.depth+1;
            overlay.nearClipPlane=.01f;
            overlay.farClipPlane=4;
            var worldData=world.GetUniversalAdditionalCameraData();
            var overlayData=overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType=CameraRenderType.Overlay;
            overlayData.renderPostProcessing=false;
            worldHadViewLayer=(world.cullingMask&(1<<ViewLayer))!=0;
            world.cullingMask&=~(1<<ViewLayer);
            worldData.cameraStack.Add(overlay);
            var handsObject=Instantiate(catalog.Hands,holder.transform);
            hands=handsObject.transform;
            hands.localPosition=new Vector3(.22f,-.24f,.48f);
            hands.localRotation=Quaternion.identity;
            SetLayer(hands,ViewLayer);
            var sourceR=GripAttachment.Find(hands,"Grip_R");
            var sourceL=GripAttachment.Find(hands,"Grip_L");
            var sourcePalm=GripAttachment.Find(hands,"PalmR");
            sourceRight=(sourceR.position-sourceL.position)/.20f;
            sourceForward=(sourceR.position-sourcePalm.position)/.08f;
            sourceUp=(sourceR.parent.position+sourceRight*.10f-sourceForward*.07f-sourceR.position)/.11f;
            sleeveUp=hands.InverseTransformDirection(sourceUp); handForward=hands.InverseTransformDirection(sourceForward);
            rightHand=RigHand("R",0);
            leftHand=RigHand("L",1);
            leftRest=leftHand.localPosition; leftRotation=leftHand.localRotation;
            readyPosition=hands.localPosition;
            socket=GripAttachment.Find(hands,"Grip_R");
            if(socket==null) throw new System.ArgumentException("First-person hands have no Grip_R socket.");
            for(int i=0;i<muzzleElements.Length;i++)
            {
                var element=new GameObject("Local muzzle "+i,typeof(LineRenderer));
                element.transform.SetParent(holder.transform,false); element.layer=ViewLayer;
                var line=element.GetComponent<LineRenderer>();
                line.useWorldSpace=true; line.positionCount=i==1?17:3;
                line.numCapVertices=2; line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows=false; line.enabled=false;
                muzzleElements[i]=line;
            }
        }
        public void SyncProjection(Camera world)
        {
            if(world==null || overlay==null) return;
            overlay.fieldOfView=world.fieldOfView;
            overlay.aspect=world.aspect;
            overlay.orthographic=world.orthographic;
            overlay.orthographicSize=world.orthographicSize;
        }
        public void Present(CombatState player,PresentationCatalog catalog)
        {
            if(overlay==null || player==null) return;
            visible=player.Alive && player.Connected;
            overlay.enabled=visible;
            WeaponKind selected=player.SelectedSpecial && player.SpecialWeapon!=WeaponKind.None?player.SpecialWeapon:WeaponKind.LineDriver;
            if(selected==currentWeapon) return;
            ResetMotion();
            support=null; muzzle=null; movingPart=null;
            if(weapon!=null) Destroy(weapon);
            currentWeapon=selected;
            var prefab=catalog.Weapon(selected);
            if(prefab==null) return;
            weapon=Instantiate(prefab,socket);
            if(selected==WeaponKind.LaserPistol || selected==WeaponKind.LaserShotgun || selected==WeaponKind.LaserSniper)
                weapon.transform.localScale*=.7f;
            GripAttachment.Align(weapon.transform,socket);
            SetLayer(weapon.transform,ViewLayer);
            support=GripAttachment.Find(weapon.transform,"LeftHand");
            muzzle=GripAttachment.Find(weapon.transform,"Muzzle");
            string part=selected==WeaponKind.LineDriver?"SplitRail":selected==WeaponKind.PulseLobber?"OrbChamber":
                selected==WeaponKind.ReboundDisc?"DiscSignal":selected==WeaponKind.PushLens?"InnerField":
                selected==WeaponKind.StunGun?"WarningBar":selected==WeaponKind.LaserSword?"BladeCore":
                selected==WeaponKind.LaserPistol?"PistolSlide":selected==WeaponKind.LaserShotgun?"PumpForeend":selected==WeaponKind.LaserSniper?"PrecisionBarrel":null;
            var geometry=part==null?null:GripAttachment.Find(weapon.transform,part);
            if(geometry!=null)
            {
                var pivot=new GameObject("Weapon mechanism").transform;
                pivot.SetParent(weapon.transform,false);
                var renderer=geometry.GetComponent<Renderer>();
                pivot.position=renderer!=null?renderer.bounds.center:geometry.position;
                geometry.SetParent(pivot,true);
                movingPart=pivot; partRest=pivot.localPosition; partRotation=pivot.localRotation;
                partAxis=pivot.InverseTransformDirection(hands.TransformDirection(selected==WeaponKind.ReboundDisc?sleeveUp:handForward)).normalized;
            }
            shotMaterial=MaterialFor(catalog,selected);
        }
        // Geometry stays original; only the wrist and sleeve hierarchy is articulated.
        Transform RigHand(string side,int index)
        {
            var grip=GripAttachment.Find(hands,"Grip_"+side);
            var wrist=new GameObject("Wrist_"+side).transform;
            wrist.SetParent(hands,false); wrist.position=grip.position; wrist.rotation=grip.rotation;
            var palm=GripAttachment.Find(hands,"Palm"+side);
            if(palm!=null) palm.SetParent(wrist,true);
            for(int i=0;i<4;i++)
            {
                var finger=GripAttachment.Find(hands,"Finger"+side+i);
                if(finger==null) continue;
                var pivot=new GameObject("Knuckle_"+side+i).transform;
                pivot.SetParent(wrist,false);
                pivot.position=SourcePoint(grip,side,new Vector3((side=="R"?1:-1)*(.055f+i*.028f),-.10f,-.11f));
                pivot.rotation=Quaternion.LookRotation(sourceForward,sourceUp);
                finger.SetParent(pivot,true);
                fingers[index*4+i]=pivot; fingerRest[index*4+i]=pivot.localRotation;
            }
            grip.SetParent(wrist,true);
            var sleeve=GripAttachment.Find(hands,"Sleeve"+side);
            float sign=side=="R"?1:-1;
            shoulders[index]=hands.InverseTransformPoint(SourcePoint(grip,side,new Vector3(sign*.20f,-.17f,-.38f)));
            Vector3 cuff=SourcePoint(grip,side,new Vector3(sign*.12f,-.12f,-.18f));
            cuffs[index]=wrist.InverseTransformPoint(cuff);
            var arm=new GameObject("Forearm_"+side).transform;
            arm.SetParent(hands,false); arm.localPosition=shoulders[index];
            restReach[index]=hands.InverseTransformPoint(cuff)-shoulders[index];
            arm.localRotation=Quaternion.LookRotation(restReach[index],hands.InverseTransformDirection(sourceUp));
            if(sleeve!=null) sleeve.SetParent(arm,true);
            sleeves[index]=arm;

            return wrist;
        }
        Vector3 SourcePoint(Transform grip,string side,Vector3 point)
        {
            return grip.position+sourceRight*(point.x-(side=="R"?.10f:-.10f))+sourceUp*(point.y+.11f)+sourceForward*(point.z+.07f);
        }
        void CloseFingers(int hand,float closed)
        {
            for(int i=hand*4;i<hand*4+4;i++)
                if(fingers[i]!=null) fingers[i].localRotation=fingerRest[i]*Quaternion.Euler(65*closed,0,0);
        }
        public void Play(CombatEvent value,int localParticipantId)
        {
            if(value.Action.Participant!=localParticipantId) return;
            if(value.Kind==CombatEventKind.Shot)
            {
                var kind=value.Weapon==WeaponKind.None?currentWeapon:value.Weapon;
                if(kind!=WeaponKind.Grenade)
                {
                    recoil=1;
                    recoilDuration=kind==WeaponKind.LaserSniper?.38f:kind==WeaponKind.LaserShotgun?.30f:kind==WeaponKind.LaserPistol?.16f:kind==WeaponKind.PulseLobber?.28f:kind==WeaponKind.PushLens?.25f:kind==WeaponKind.ReboundDisc?.20f:kind==WeaponKind.LaserSword?.18f:.125f;
                    recoilDistance=kind==WeaponKind.LaserSniper?.13f:kind==WeaponKind.LaserShotgun?.12f:kind==WeaponKind.LaserPistol?.06f:kind==WeaponKind.PulseLobber?.10f:kind==WeaponKind.PushLens?.085f:kind==WeaponKind.StunGun?.035f:.055f;
                    recoilPitch=kind==WeaponKind.LaserSniper?17:kind==WeaponKind.LaserShotgun?16:kind==WeaponKind.LaserPistol?9:kind==WeaponKind.PulseLobber?14:kind==WeaponKind.StunGun?4:8;
                    recoilTwist=kind==WeaponKind.LaserShotgun?5:kind==WeaponKind.LaserPistol?3:kind==WeaponKind.ReboundDisc?-10:kind==WeaponKind.LaserSword?12:0;
                }
                if(kind==currentWeapon && kind!=WeaponKind.LaserSword && kind!=WeaponKind.Grenade)
                {
                    flashDuration=kind==WeaponKind.LaserSniper?.11f:kind==WeaponKind.LaserShotgun?.16f:kind==WeaponKind.PulseLobber?.18f:kind==WeaponKind.PushLens?.20f:.10f;
                    flashRemaining=flashDuration;
                    shotDirection=value.Vector.sqrMagnitude>.001f?value.Vector.normalized:overlay.transform.forward;
                }
            }
            if(value.Kind==CombatEventKind.Accepted && value.Action.Kind==CombatActionKind.Grenade) throwTime=.45f;
        }
        public void ResetMotion()
        {
            phase=recoil=throwTime=landing=slide=previousVertical=flashRemaining=0; hadGround=false;
            for(int i=0;i<muzzleElements.Length;i++) if(muzzleElements[i]!=null) muzzleElements[i].enabled=false;
            if(movingPart!=null) { movingPart.localPosition=partRest; movingPart.localRotation=partRotation; movingPart.localScale=Vector3.one; }
            if(hands!=null)
            {
                hands.localPosition=readyPosition; hands.localRotation=Quaternion.identity;
                leftHand.localPosition=leftRest; leftHand.localRotation=leftRotation;
                if(support!=null) { leftHand.position=support.position; leftHand.rotation=support.rotation; }
                CloseFingers(0,1); CloseFingers(1,1);
                SolveSleeve(0,rightHand); SolveSleeve(1,leftHand);
            }
        }
        public void Present(CombatState player,PresentationCatalog catalog,in MotorState motor,int tick,float dt,bool reducedMotion)
        {
            Present(player,catalog);
            if(hands==null || player==null) return;
            if(life!=player.LifeGeneration || discontinuity!=player.DiscontinuityTick || !player.Alive || !player.Connected)
            { ResetMotion(); life=player.LifeGeneration; discontinuity=player.DiscontinuityTick; }
            if(!player.Alive || !player.Connected || dt<=0) return;

            float strength=reducedMotion?.2f:1;
            var velocity=Quaternion.Inverse(Quaternion.Euler(0,motor.Yaw,0))*motor.Velocity;
            float speed=new Vector2(velocity.x,velocity.z).magnitude;
            phase+=speed*dt*1.5f;
            if(motor.Grounded && !hadGround) landing=Mathf.Clamp01(-previousVertical/16);
            hadGround=motor.Grounded; previousVertical=motor.Velocity.y;
            recoil=Mathf.MoveTowards(recoil,0,dt/recoilDuration);
            flashRemaining=Mathf.Max(0,flashRemaining-dt);
            landing=Mathf.MoveTowards(landing,0,dt*5);
            throwTime=Mathf.Max(0,throwTime-dt);
            slide=Mathf.MoveTowards(slide,motor.Sliding?1:0,dt*8);
            float bob=!reducedMotion && motor.Grounded && !motor.Sliding?Mathf.Min(speed/8,1):0;
            float reload=player.ReloadUntil>tick?Mathf.Sin(Mathf.PI*Mathf.Clamp01((player.ReloadUntil-tick)/(WeaponCatalog.ReloadSeconds*WeaponCatalog.DefaultTickRate))):0;
            float attack=0;
            if(player.Pending!=null && player.Pending.Weapon==WeaponKind.LaserSword)
            {
                var pending=player.Pending;
                float duration=Mathf.Max(1,pending.EndTick-pending.Action.Tick);
                attack=Mathf.Sin(Mathf.PI*Mathf.Clamp01((tick-pending.Action.Tick)/duration));
            }
            hands.localPosition=readyPosition+strength*new Vector3(Mathf.Sin(phase)*.009f*bob-attack*.14f,-slide*.085f-landing*.04f-reload*.06f+Mathf.Cos(phase*2)*.007f*bob,-recoil*recoilDistance-slide*.055f);
            hands.localRotation=Quaternion.Euler(strength*(-recoil*recoilPitch+slide*17+reload*18+Mathf.Clamp(-velocity.y,-5,5)),strength*(attack*-32-Mathf.Clamp(velocity.x,-8,8)*.5f),strength*(-slide*15-reload*22+recoil*recoilTwist));
            leftHand.localPosition=leftRest; leftHand.localRotation=leftRotation;
            if(support!=null) { leftHand.position=support.position; leftHand.rotation=support.rotation; }
            // Reload and throw deliberately release the supporting hand, then recover contact.
            float release=Mathf.Max(reload,Mathf.Sin(Mathf.PI*throwTime/.45f));
            leftHand.localPosition+=new Vector3(-.06f,-.10f,-.08f)*release*strength;
            leftHand.localRotation*=Quaternion.Euler(-35*release*strength,0,-20*release*strength);
            CloseFingers(0,1); CloseFingers(1,1-release);
            float charge=0;
            if(currentWeapon==WeaponKind.PushLens && player.Pending!=null && player.Pending.Weapon==WeaponKind.PushLens && tick<player.Pending.ResolveTick)
                charge=Mathf.Clamp01((tick-player.Pending.Action.Tick)/(float)Mathf.Max(1,player.Pending.ResolveTick-player.Pending.Action.Tick));
            PresentWeaponMechanism(charge,strength,reducedMotion);
            PresentMuzzle(charge,reducedMotion);
            SolveSleeve(0,rightHand); SolveSleeve(1,leftHand);
        }
        public bool TryGetMuzzle(out Vector3 position)
        {
            position=muzzle!=null?muzzle.position:Vector3.zero;
            return visible && muzzle!=null;
        }
        static Material MaterialFor(PresentationCatalog catalog,WeaponKind kind)
        {
            switch(kind)
            {
                case WeaponKind.LaserShotgun:
                case WeaponKind.PulseLobber: return catalog.Pulse;
                case WeaponKind.ReboundDisc: return catalog.Disc;
                case WeaponKind.LaserSniper:
                case WeaponKind.PushLens: return catalog.Push;
                case WeaponKind.StunGun: return catalog.Stun;
                default: return catalog.Beam;
            }
        }
        void PresentWeaponMechanism(float charge,float strength,bool reducedMotion)
        {
            if(movingPart==null) return;
            float spin=currentWeapon==WeaponKind.ReboundDisc?recoil*150:currentWeapon==WeaponKind.PushLens?charge*90:0;
            movingPart.localPosition=partRest+partAxis*(-recoil*.025f*strength);
            movingPart.localRotation=partRotation*Quaternion.AngleAxis(reducedMotion?0:spin,partAxis);
            float pulse=currentWeapon==WeaponKind.PulseLobber?recoil*.12f:currentWeapon==WeaponKind.PushLens?charge*.08f:0;
            movingPart.localScale=Vector3.one*(1+pulse*strength);
        }
        void PresentMuzzle(float charge,bool reducedMotion)
        {
            bool active=visible && muzzle!=null && (flashRemaining>0 || charge>0);
            for(int i=0;i<muzzleElements.Length;i++) muzzleElements[i].enabled=active && (!reducedMotion || i==0 || (i==1 && charge>0));
            if(!active) return;
            Vector3 origin=muzzle.position;
            Vector3 forward=flashRemaining>0?shotDirection:overlay.transform.forward;
            Vector3 right=Vector3.Cross(overlay.transform.up,forward).normalized,up=Vector3.Cross(forward,right);
            float progress=flashRemaining>0?1-flashRemaining/flashDuration:0;
            float radius=charge>0?Mathf.Lerp(.15f,.055f,charge):Mathf.Lerp(.035f,currentWeapon==WeaponKind.PushLens?.22f:currentWeapon==WeaponKind.LaserShotgun?.16f:.09f,progress);
            float size=reducedMotion?.5f:1;
            for(int i=0;i<muzzleElements.Length;i++)
            {
                var line=muzzleElements[i]; line.sharedMaterial=shotMaterial;
                line.startWidth=line.endWidth=(i==0?.022f:.009f)*size;
            }
            var core=muzzleElements[0];
            float length=(currentWeapon==WeaponKind.LaserSniper?.35f:currentWeapon==WeaponKind.LaserShotgun?.16f:currentWeapon==WeaponKind.LaserPistol?.13f:currentWeapon==WeaponKind.LineDriver?.22f:currentWeapon==WeaponKind.PulseLobber?.12f:.08f)*size;
            core.SetPosition(0,origin); core.SetPosition(1,origin+forward*length*.5f); core.SetPosition(2,origin+forward*length);
            var ring=muzzleElements[1];
            for(int i=0;i<17;i++)
            {
                float angle=i*Mathf.PI/8;
                float flatten=currentWeapon==WeaponKind.ReboundDisc?.25f:1;
                ring.SetPosition(i,origin+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle)*flatten)*radius*size);
            }
            for(int i=2;i<4;i++)
            {
                float sign=i==2?-1:1;
                var arc=muzzleElements[i];
                arc.SetPosition(0,origin+right*.025f*sign);
                arc.SetPosition(1,origin+forward*length*.65f+right*radius*sign+up*.025f);
                arc.SetPosition(2,origin+forward*length+right*.035f*sign);
            }
        }
        float previousVertical;
        void SolveSleeve(int index,Transform wrist)
        {
            Vector3 reach=hands.InverseTransformPoint(wrist.TransformPoint(cuffs[index]))-shoulders[index];
            sleeves[index].localRotation=Quaternion.LookRotation(reach,sleeveUp);
            sleeves[index].localScale=new Vector3(1,1,reach.magnitude/restReach[index].magnitude);
        }
        public void Teardown(Camera world)
        {
            if(overlay==null) return;
            if(world!=null)
            {
                world.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
                world.cullingMask=(world.cullingMask&~(1<<ViewLayer))|(worldHadViewLayer?(1<<ViewLayer):0);
            }
            Destroy(overlay.gameObject);
            overlay=null; weapon=null; socket=null; hands=null; support=null; muzzle=null; movingPart=null; visible=false; currentWeapon=WeaponKind.None; life=-1; ResetMotion();
        }
        static void SetLayer(Transform root,int layer)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=layer;
        }
    }
}
