using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Wildstone.Combat;
using Wildstone.Movement;

namespace Wildstone.Presentation
{
    public sealed class WeaponView : MonoBehaviour
    {
        const int ViewLayer=11;
        Camera overlay,worldCamera;
        GameObject weapon;
        WeaponKind currentWeapon;
        Transform socket;
        bool worldHadViewLayer;
        // hands: the moving weapon pivot (the weapon's Grip sits on it). arms: the D028 soldier gauntlets, anchored
        // at the shoulders and solved each frame onto the grip and the weapon's LeftHand marker (vibegame1 ViewmodelArm).
        Transform hands, arms, support;
        readonly Transform[] upper=new Transform[2], lower=new Transform[2], wrist=new Transform[2];
        readonly Vector3[] shoulderRest=new Vector3[2], gripOffset=new Vector3[2];
        readonly Quaternion[] gripRotation=new Quaternion[2];
        readonly FingerCurl[] curls=new FingerCurl[2];
        readonly float[] gripCurl=new float[5], supportCurl=new float[5], released=new float[5];
        int discontinuity=-1;
        Vector3 readyPosition;
        // Arms model offset under the lens: shoulders land at about (+-0.17, -0.24, -0.06) from the eye.
        public static readonly Vector3 ArmsOffset=new Vector3(0,-1.71f,-.05f), DefaultHip=new Vector3(.30f,-.36f,.51f);
        // Without a LeftHand marker the support hand drops out of frame.
        static readonly Vector3 LeftRest=new Vector3(-.24f,-.62f,.18f);
        float phase, recoil, throwTime, landing, slide;
        // Grenade in hand: while the throw key is held the support hand leaves the gun and shows a grenade sized like a
        // real one. The throw itself still starts on the host's Accepted; the raised hand eases down slowly enough to
        // bridge that round trip, and the grenade leaves the hand a third of the way through the swing.
        public bool GrenadeReady { get; set; }
        public const float ThrowSeconds=.45f, GrenadeSize=.08f, GrenadeRelease=.35f;
        // Lens space under the 58-degree viewmodel FOV: about 16% up and 29% across a 16:9 frame, lower left of the crosshair.
        static readonly Vector3 HoldPoint=new Vector3(-.115f,-.08f,.35f), ThrowReach=new Vector3(.06f,.07f,.24f);
        static readonly float[] GrenadeCurl={.55f,.6f,.62f,.64f,.66f};
        Transform heldGrenade;
        float hold, carry, reach;
        int life=-1;
        bool hadGround;
        Transform muzzle, movingPart;
        Vector3 partRest, partAxis;
        Quaternion partRotation;
        readonly LineRenderer[] muzzleElements=new LineRenderer[4];
        Vector3 shotDirection;
        float flashRemaining, flashDuration, recoilDuration=.125f, recoilDistance=.055f, recoilPitch=8, recoilTwist;
        // D041 proposal: a shot eases out to its peak over RecoilRise, then eases back in over recoilDuration. A held SMG
        // shot adds to the kick still in flight instead of snapping it back to one, so a burst settles into a band.
        public const float RecoilRise=.03f;
        float recoilFrom, recoilPeak, recoilAge=99;
        Material shotMaterial;
        bool visible;
        // Emissive Signal parts flare on each shot and hold some heat under sustained fire.
        readonly List<(Renderer renderer,int index,Color emission)> signals=new List<(Renderer,int,Color)>();
        MaterialPropertyBlock glowBlock;
        float heat, glowShown=-1;
        static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");
        const float MaxSignal=1.6f;
        UniversalAdditionalCameraData worldData;
        // D028/D029 framing: an independent viewmodel lens following 35% of the world FOV change (vibegame1 CameraFX),
        // per-weapon hip poses and a sight-aligned ADS pose. Starting values, tuned against packaged captures.
        public const float ViewmodelFieldOfView=58, KickFollow=.35f;
        Transform sight;
        Vector3 hipPosition, adsPosition;
        Quaternion adsRotation=Quaternion.identity;
        float ads, equip, eyeRelief;
        Vector2 sway, swayVelocity, previousLook;
        FirstPersonView aim;
        bool hasLook, scoped;
        // Sway spring: damping 2*sqrt(stiffness) is critical, so the weapon settles without swinging back past the aim.
        const float SwayStiffness=90, SwayDamping=19;
        // Renderer 1 of each Wildstone pipeline omits SSAO (Editor RenderingSetup.OverlayRendererIndex).
        const int OverlayRenderer=1;
        // Dash speed streaks (vibegame1 DashFx pattern): lens-space lines that stream away from the dash direction for
        // .22 s, so a dash reads as distance covered. Cold blue-white at .9 peak, under the 1.05 bloom threshold; they
        // start 15% of the frame height off-axis, clear of the reticle. Sizes are fractions of the overlay frame height.
        public const int StreakCount=10;
        public const float StreakSeconds=.22f, StreakInner=.15f, StreakDepth=.6f;
        readonly LineRenderer[] streaks=new LineRenderer[StreakCount];
        readonly Vector3[] streakStarts=new Vector3[StreakCount];
        readonly Vector3[] streakFlows=new Vector3[StreakCount];
        Material streakMaterial;
        float streakAge=-1;
        double lastDash=double.NaN;

        public void Initialize(Camera world,PresentationCatalog catalog,int team=0)
        {
            if(world==null || catalog.Hands==null) throw new System.ArgumentException("First-person camera or hands missing.");
            var holder=new GameObject("Viewmodel",typeof(Camera));
            holder.transform.SetParent(world.transform,false);
            overlay=holder.GetComponent<Camera>();
            worldCamera=world;
            overlay.CopyFrom(world);
            overlay.cullingMask=1<<ViewLayer;
            overlay.clearFlags=CameraClearFlags.Depth;
            overlay.depth=world.depth+1;
            overlay.nearClipPlane=.01f;
            overlay.farClipPlane=4;
            worldData=world.GetUniversalAdditionalCameraData();
            var overlayData=overlay.GetUniversalAdditionalCameraData();
            overlayData.renderType=CameraRenderType.Overlay;
            // The last active camera grades the whole stack once: the overlay while it draws, else the world.
            overlayData.renderPostProcessing=true;
            overlayData.antialiasing=worldData.antialiasing;
            overlayData.antialiasingQuality=worldData.antialiasingQuality;
            overlayData.renderShadows=false;
            overlayData.requiresDepthOption=CameraOverrideOption.Off;
            overlayData.SetRenderer(OverlayRenderer);
            worldHadViewLayer=(world.cullingMask&(1<<ViewLayer))!=0;
            world.cullingMask&=~(1<<ViewLayer);
            worldData.cameraStack.Add(overlay);
            arms=Instantiate(catalog.Hands,holder.transform).transform;
            arms.localPosition=ArmsOffset; arms.localRotation=Quaternion.identity;
            SetLayer(arms,ViewLayer);
            TintArms(team);
            string[] sides={"Right","Left"};
            for(int i=0;i<2;i++)
            {
                upper[i]=GripAttachment.Find(arms,sides[i]+"UpperArm"); lower[i]=GripAttachment.Find(arms,sides[i]+"LowerArm");
                wrist[i]=GripAttachment.Find(arms,sides[i]+"Hand");
                var grip=GripAttachment.Find(arms,i==0?"Grip_R":"Grip_L");
                if(upper[i]==null || lower[i]==null || wrist[i]==null || grip==null)
                    throw new System.ArgumentException("First-person arms are missing the "+sides[i]+" arm chain or grip socket.");
                shoulderRest[i]=upper[i].localPosition;
                gripRotation[i]=Quaternion.Inverse(wrist[i].rotation)*grip.rotation;
                gripOffset[i]=Quaternion.Inverse(wrist[i].rotation)*(grip.position-wrist[i].position);
                // Bind palms face down (soldier_skeleton.py), so curl axes are measured against the model's down.
                curls[i]=FingerCurl.Capture(arms,sides[i],arms.TransformDirection(Vector3.down));
                if(i==1) HoldGrenade(catalog,grip);
            }
            hands=new GameObject("Weapon pivot").transform;
            hands.SetParent(holder.transform,false);
            readyPosition=DefaultHip; hands.localPosition=readyPosition;
            socket=hands;
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
            streakMaterial=new Material(catalog.Beam) { name="Dash streaks" };
            streakMaterial.SetColor("_BaseColor",new Color(.63f,.72f,.9f,1));
            streakMaterial.SetVector("_NoiseParams",new Vector4(0,1,1,0));
            for(int i=0;i<StreakCount;i++)
            {
                var element=new GameObject("Dash streak "+i,typeof(LineRenderer));
                element.transform.SetParent(holder.transform,false); element.layer=ViewLayer;
                var line=element.GetComponent<LineRenderer>();
                line.useWorldSpace=false; line.positionCount=2; line.sharedMaterial=streakMaterial;
                line.widthCurve=AnimationCurve.Linear(0,1,1,.12f);
                line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows=false; line.enabled=false;
                streaks[i]=line;
            }
        }
        public void SyncProjection(Camera world,FirstPersonView firstPerson=null)
        {
            aim=firstPerson; // sway reads this aim, never the punched camera transform
            if(world==null || overlay==null) return;
            float change=firstPerson!=null && firstPerson.BaseFieldOfView>0?world.fieldOfView-firstPerson.BaseFieldOfView:0;
            overlay.fieldOfView=Mathf.Clamp(ViewmodelFieldOfView+change*KickFollow,20,100);
            overlay.aspect=world.aspect;
            overlay.orthographic=world.orthographic;
            overlay.orthographicSize=world.orthographicSize;
        }
        public void Present(CombatState player,PresentationCatalog catalog)
        {
            if(overlay==null || player==null) return;
            WeaponKind selected=player.SelectedSpecial && player.SpecialWeapon!=WeaponKind.None?player.SpecialWeapon:WeaponKind.LineDriver;
            // A full scope replaces the viewmodel with the HUD scope overlay.
            scoped=WeaponCatalog.ScopeActive(selected,Ads(player));
            visible=player.Alive && player.Connected && !scoped;
            overlay.enabled=visible;
            worldData.renderPostProcessing=!visible;
            if(selected==currentWeapon) return;
            ResetMotion();
            support=null; muzzle=null; movingPart=null;
            if(weapon!=null) Destroy(weapon);
            currentWeapon=selected;
            var prefab=catalog.Weapon(selected);
            if(prefab==null) return;
            weapon=Instantiate(prefab,socket);
            HipPose(selected,out hipPosition,out float scale,out eyeRelief);
            weapon.transform.localScale*=scale;
            GripAttachment.Align(weapon.transform,socket);
            hands.localPosition=hipPosition; hands.localRotation=Quaternion.identity;
            sight=GripAttachment.Find(weapon.transform,"Sight");
            SolveAds();
            equip=1;
            SetLayer(weapon.transform,ViewLayer);
            signals.Clear(); glowShown=-1;
            foreach(var renderer in weapon.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                    if(materials[i]!=null && materials[i].name.StartsWith("Signal",System.StringComparison.Ordinal) && materials[i].IsKeywordEnabled("_EMISSION"))
                        signals.Add((renderer,i,materials[i].GetColor(EmissionColor)));
            }
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
                partAxis=pivot.InverseTransformDirection(hands.TransformDirection(selected==WeaponKind.ReboundDisc?Vector3.up:Vector3.forward)).normalized;
            }
            shotMaterial=MaterialFor(catalog,selected);
            GripCurl(selected,gripCurl,supportCurl);
            PoseArms(0);
        }
        // Hip pose per weapon: the grip position in lens space. Kit models are authored about twice life size for
        // third-person readability, so the viewmodel draws them at 0.6 and places the sight about 37% below centre
        // (ViewmodelCapture measures grip/sight/muzzle). Relief is the sight's ADS distance from the eye.
        static void HipPose(WeaponKind kind,out Vector3 position,out float scale,out float relief)
        {
            relief=.16f; scale=.6f;
            switch(kind)
            {
                case WeaponKind.LineDriver: position=new Vector3(.24f,-.36f,.50f); break;
                case WeaponKind.LaserSMG: position=new Vector3(.23f,-.33f,.47f); relief=.15f; break;
                case WeaponKind.LaserPistol: position=new Vector3(.22f,-.28f,.42f); relief=.24f; break;
                case WeaponKind.LaserShotgun: position=new Vector3(.27f,-.38f,.50f); break; // twin barrels: lower so they clear the centre of view
                case WeaponKind.LaserSniper: position=new Vector3(.25f,-.35f,.56f); relief=.10f; break;
                case WeaponKind.LaserSword: position=DefaultHip; scale=.7f; break;
                case WeaponKind.PulseLobber: position=DefaultHip+new Vector3(.04f,-.05f,.05f); scale=.48f; break; // the launcher body is bulky; keep it off the centre of view
                default: position=DefaultHip; break;
            }
        }
        // The ADS pose moves the hands so the weapon's Sight socket sits on the lens axis, bore forward.
        void SolveAds()
        {
            adsPosition=hipPosition; adsRotation=Quaternion.identity;
            if(sight==null || overlay==null) return;
            var holder=overlay.transform;
            adsRotation=Quaternion.FromToRotation(holder.InverseTransformDirection(sight.forward),Vector3.forward);
            Vector3 relative=holder.InverseTransformPoint(sight.position)-hipPosition;
            adsPosition=new Vector3(0,0,eyeRelief)-adsRotation*relative;
        }
        // Per-digit grip (thumb, index, middle, ring, little): the trigger finger rests straighter on guns.
        static void GripCurl(WeaponKind kind,float[] grip,float[] support)
        {
            bool fist=kind==WeaponKind.LaserSword || kind==WeaponKind.Grenade;
            float[] main=fist?new[]{.8f,.95f,.95f,.95f,.95f}:new[]{.65f,.4f,.9f,.92f,.95f};
            float[] cup={.5f,.7f,.75f,.8f,.85f};
            System.Array.Copy(main,grip,5); System.Array.Copy(cup,support,5);
        }
        // Kit models are authored large; the held grenade is scaled to GrenadeSize and centred on the left grip.
        void HoldGrenade(PresentationCatalog catalog,Transform grip)
        {
            var prefab=catalog.Weapon(WeaponKind.Grenade);
            if(prefab==null) return;
            heldGrenade=Instantiate(prefab,grip).transform;
            // Its Grip/Muzzle markers must not answer lookups meant for the held weapon.
            foreach(var part in heldGrenade.GetComponentsInChildren<Transform>(true)) part.name="Held grenade "+part.name;
            heldGrenade.name="Held grenade";
            SetLayer(heldGrenade,ViewLayer);
            var renderers=heldGrenade.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) { Destroy(heldGrenade.gameObject); heldGrenade=null; return; }
            // Measured unrotated: a rotated bounding box overstates the size.
            heldGrenade.SetPositionAndRotation(grip.position,Quaternion.identity);
            var bounds=Measure(renderers);
            float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            if(size>1e-4f) heldGrenade.localScale*=GrenadeSize/size;
            var centre=Measure(renderers).center-heldGrenade.position;
            heldGrenade.localRotation=Quaternion.identity;
            heldGrenade.position=grip.position-heldGrenade.rotation*centre;
            heldGrenade.gameObject.SetActive(false);
        }
        static Bounds Measure(Renderer[] renderers)
        {
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        void TintArms(int team)
        {
            var block=new MaterialPropertyBlock();
            var accent=SoldierRig.TeamAccent(team);
            foreach(var renderer in arms.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                // Solved bones leave the bind bounds, so never cull the arms against stale bounds.
                if(renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen=true;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(materials[i]==null) continue;
                    bool glow=materials[i].name.StartsWith("SoldierGlow",System.StringComparison.Ordinal);
                    if(!glow && !materials[i].name.StartsWith("SoldierAccent",System.StringComparison.Ordinal)) continue;
                    block.Clear(); block.SetColor("_BaseColor",accent);
                    if(glow) block.SetColor("_EmissionColor",accent*2.2f);
                    renderer.SetPropertyBlock(block,i);
                }
            }
        }
        // Right hand on the grip, left on the LeftHand marker; release lifts the support hand for reload/throw.
        void PoseArms(float release)
        {
            if(arms==null || overlay==null) return;
            PoseArm(0,socket.position,socket.rotation);
            var lens=overlay.transform;
            Vector3 position=support!=null?support.position:lens.TransformPoint(LeftRest);
            Quaternion rotation=support!=null?support.rotation:lens.rotation*Quaternion.Euler(20,25,0);
            position+=lens.TransformDirection(new Vector3(-.06f,-.10f,-.08f)*release*(1-carry));
            rotation*=Quaternion.Euler(-35*release*(1-carry),0,-20*release*(1-carry));
            if(carry>0)
            {
                position=Vector3.Lerp(position,lens.TransformPoint(HoldPoint+ThrowReach*reach),carry);
                rotation=Quaternion.Slerp(rotation,lens.rotation*Quaternion.Euler(-10-40*reach,25,-35),carry);
            }
            PoseArm(1,position,rotation);
            curls[0].Apply(gripCurl);
            bool holding=heldGrenade!=null && heldGrenade.gameObject.activeSelf;
            for(int i=0;i<5;i++) released[i]=Mathf.Lerp(Mathf.Lerp(supportCurl[i],.15f,release),GrenadeCurl[i],holding?carry:0);
            curls[1].Apply(released);
        }
        void PoseArm(int index,Vector3 gripPosition,Quaternion gripWorld)
        {
            var rotation=gripWorld*Quaternion.Inverse(gripRotation[index]);
            var target=gripPosition-rotation*gripOffset[index];
            // The shoulder slides toward an out-of-reach grip instead of tearing the hand off the weapon.
            upper[index].localPosition=shoulderRest[index];
            float reach=Vector3.Distance(upper[index].position,lower[index].position)+Vector3.Distance(lower[index].position,wrist[index].position);
            Vector3 toTarget=target-upper[index].position;
            float excess=toTarget.magnitude-reach*.97f;
            if(excess>0) upper[index].position+=toTarget.normalized*excess;
            var pole=upper[index].position+overlay.transform.TransformDirection(new Vector3(index==0?.35f:-.35f,-.6f,-.1f));
            SoldierRig.SolveTwoBone(upper[index],lower[index],wrist[index],target,pole);
            wrist[index].rotation=rotation;
        }
        public void Play(CombatEvent value,int localParticipantId)
        {
            if(value.Action.Participant!=localParticipantId) return;
            if(value.Kind==CombatEventKind.Shot)
            {
                var kind=value.Weapon==WeaponKind.None?currentWeapon:value.Weapon;
                if(kind!=WeaponKind.Grenade)
                {
                    recoilFrom=recoil; recoilPeak=kind==WeaponKind.LaserSMG?Mathf.Min(1,recoil+.5f):1; recoilAge=0;
                    recoilDuration=kind==WeaponKind.LaserSniper?.38f:kind==WeaponKind.LaserShotgun?.30f:kind==WeaponKind.LaserPistol?.16f:kind==WeaponKind.PulseLobber?.28f:kind==WeaponKind.PushLens?.25f:kind==WeaponKind.ReboundDisc?.20f:kind==WeaponKind.LaserSword?.18f:.125f;
                    recoilDistance=kind==WeaponKind.LaserSniper?.13f:kind==WeaponKind.LaserShotgun?.12f:kind==WeaponKind.LaserPistol?.06f:kind==WeaponKind.PulseLobber?.10f:kind==WeaponKind.PushLens?.085f:kind==WeaponKind.StunGun?.035f:.055f;
                    recoilPitch=kind==WeaponKind.LaserSniper?9:kind==WeaponKind.LaserShotgun?10:kind==WeaponKind.LaserPistol?6:kind==WeaponKind.PulseLobber?14:kind==WeaponKind.StunGun?4:kind==WeaponKind.LaserSMG?3:kind==WeaponKind.LineDriver?4:8;
                    recoilTwist=kind==WeaponKind.LaserShotgun?5:kind==WeaponKind.LaserPistol?3:kind==WeaponKind.ReboundDisc?-10:kind==WeaponKind.LaserSword?12:0;
                }
                if(kind==currentWeapon && kind!=WeaponKind.LaserSword && kind!=WeaponKind.Grenade)
                {
                    flashDuration=kind==WeaponKind.LaserSniper?.11f:kind==WeaponKind.LaserShotgun?.16f:kind==WeaponKind.PulseLobber?.18f:kind==WeaponKind.PushLens?.20f:.10f;
                    flashRemaining=flashDuration; flashSpin+=2.4f;
                    heat=Mathf.Min(1,heat+(kind==WeaponKind.LaserSniper || kind==WeaponKind.LaserShotgun || kind==WeaponKind.PulseLobber?.4f:kind==WeaponKind.LaserPistol?.2f:.1f));
                    shotDirection=value.Vector.sqrMagnitude>.001f?value.Vector.normalized:overlay.transform.forward;
                }
            }
            if(value.Kind==CombatEventKind.Accepted && value.Action.Kind==CombatActionKind.Grenade) throwTime=ThrowSeconds;
        }
        // Degrees of cosmetic camera punch per local shot (FirstPersonView.AddRecoil); heavy shots kick harder.
        public WeaponKind CurrentWeapon=>currentWeapon;
        public static float ViewPunch(WeaponKind kind) => kind==WeaponKind.LaserSniper?1.2f:kind==WeaponKind.LaserShotgun?1f:kind==WeaponKind.PulseLobber?.8f:
            kind==WeaponKind.PushLens?.7f:kind==WeaponKind.LaserPistol?.5f:kind==WeaponKind.ReboundDisc?.4f:kind==WeaponKind.LaserSMG?.25f:
            kind==WeaponKind.LaserSword || kind==WeaponKind.Grenade?0:.35f;
        public static float RecoilCurve(float age,float from,float peak,float duration)
        {
            if(age<RecoilRise) { float u=age/RecoilRise; return Mathf.Lerp(from,peak,1-(1-u)*(1-u)); }
            float t=Mathf.Clamp01((age-RecoilRise)/duration); return peak*(1-t*t);
        }
        public void ResetMotion()
        {
            phase=recoil=recoilPeak=throwTime=landing=slide=previousVertical=flashRemaining=ads=equip=heat=hold=carry=reach=0; recoilAge=99; hadGround=false; hasLook=false;
            if(heldGrenade!=null) heldGrenade.gameObject.SetActive(false);
            sway=swayVelocity=Vector2.zero;
            StopStreaks(); lastDash=double.NaN;
            for(int i=0;i<muzzleElements.Length;i++) if(muzzleElements[i]!=null) muzzleElements[i].enabled=false;
            if(movingPart!=null) { movingPart.localPosition=partRest; movingPart.localRotation=partRotation; movingPart.localScale=Vector3.one; }
            if(hands!=null)
            {
                hands.localPosition=currentWeapon==WeaponKind.None?readyPosition:hipPosition; hands.localRotation=Quaternion.identity;
                PoseArms(0);
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
            recoilAge+=dt; recoil=RecoilCurve(recoilAge,recoilFrom,recoilPeak,recoilDuration);
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
            // ADS follows the authoritative progress; sway/bob/tuck keep 15% of their amplitude on the sights.
            ads=Mathf.SmoothStep(0,1,WeaponCatalog.CanAim(currentWeapon)?Mathf.Clamp01(Ads(player)):0);
            equip=Mathf.MoveTowards(equip,0,dt/.3f);
            float settle=strength*(1-.85f*ads);
            UpdateSway(dt,reducedMotion);
            Vector3 motion=new Vector3(Mathf.Sin(phase)*.009f*bob-attack*.14f+sway.x*.012f,-slide*.085f-landing*.04f-reload*.06f+Mathf.Cos(phase*2)*.007f*bob+sway.y*.012f-equip*equip*.14f,-slide*.055f);
            hands.localPosition=Vector3.Lerp(hipPosition,adsPosition,ads)+motion*settle+new Vector3(0,0,-recoil*recoilDistance*strength*(1-.5f*ads));
            hands.localRotation=Quaternion.Slerp(Quaternion.identity,adsRotation,ads)*Quaternion.Euler(
                strength*(-recoil*recoilPitch*(1-.6f*ads))+settle*(slide*17+reload*18+Mathf.Clamp(-velocity.y,-5,5)+equip*equip*28-sway.y*2.2f),
                settle*(attack*-32-Mathf.Clamp(velocity.x,-8,8)*.5f+sway.x*2.2f),
                strength*recoil*recoilTwist+settle*(-slide*15-reload*22-sway.x*1.5f));
            // Reload and throw deliberately release the supporting hand, then recover contact.
            float release=Mathf.Max(reload,Mathf.Sin(Mathf.PI*throwTime/ThrowSeconds));
            hold=Mathf.MoveTowards(hold,GrenadeReady?1:0,dt/(GrenadeReady?.12f:.4f));
            float swing=throwTime>0?1-throwTime/ThrowSeconds:0;
            carry=throwTime>0?1-swing:hold;
            reach=Mathf.SmoothStep(0,1,swing)*strength;
            if(heldGrenade!=null) heldGrenade.gameObject.SetActive(throwTime>0?swing<GrenadeRelease:hold>.01f);
            float charge=0;
            if(currentWeapon==WeaponKind.PushLens && player.Pending!=null && player.Pending.Weapon==WeaponKind.PushLens && tick<player.Pending.ResolveTick)
                charge=Mathf.Clamp01((tick-player.Pending.Action.Tick)/(float)Mathf.Max(1,player.Pending.ResolveTick-player.Pending.Action.Tick));
            PresentWeaponMechanism(charge,strength,reducedMotion);
            PresentMuzzle(charge,reducedMotion);
            heat=Mathf.MoveTowards(heat,0,dt*.5f);
            PresentSignals(1+recoil*1.2f+heat*.8f+charge*.6f);
            PoseArms(release*strength);
            PresentStreaks(motor,dt,reducedMotion);
        }
        void PresentStreaks(in MotorState motor,float dt,bool reducedMotion)
        {
            bool dashed=!double.IsNaN(lastDash) && motor.DashStartedAt>0 && motor.DashStartedAt!=lastDash;
            lastDash=motor.DashStartedAt;
            float frame=2*StreakDepth*Mathf.Tan(overlay.fieldOfView*.5f*Mathf.Deg2Rad);
            if(dashed && !reducedMotion)
            {
                // The world streams opposite the travel: radially for a forward dash, sideways for a strafe.
                var travel=overlay.transform.InverseTransformDirection(motor.DashDirection);
                travel=travel.sqrMagnitude>1e-6f?travel.normalized:Vector3.forward;
                for(int i=0;i<StreakCount;i++)
                {
                    float step=2*Mathf.PI/StreakCount, angle=i*step+(Random.value-.5f)*step*.8f;
                    var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                    var flow=radial*Mathf.Abs(travel.z)+new Vector3(-travel.x,-travel.y,0)*new Vector2(travel.x,travel.y).magnitude;
                    streakFlows[i]=flow.sqrMagnitude>1e-6f?flow.normalized:radial;
                    streakStarts[i]=radial*frame*(StreakInner+Random.value*.2f)+Vector3.forward*StreakDepth;
                    streaks[i].widthMultiplier=.0084f*frame; streaks[i].enabled=true;
                }
                streakAge=0;
            }
            if(streakAge<0) return;
            float t=streakAge/StreakSeconds;
            streakAge+=dt;
            if(t>=1 || reducedMotion) { StopStreaks(); return; }
            float alpha=(1-t)*(1-t)*.85f;
            var head=new Color(1,1,1,alpha); var tail=new Color(1,1,1,alpha*.15f);
            for(int i=0;i<StreakCount;i++)
            {
                var start=streakStarts[i]+streakFlows[i]*frame*.1f*t;
                streaks[i].SetPosition(0,start); streaks[i].SetPosition(1,start+streakFlows[i]*frame*.26f);
                streaks[i].startColor=head; streaks[i].endColor=tail;
            }
        }
        void StopStreaks()
        {
            streakAge=-1;
            foreach(var line in streaks) if(line!=null) line.enabled=false;
        }
        void PresentSignals(float glow)
        {
            if(Mathf.Abs(glow-glowShown)<.01f) return;
            glowShown=glow;
            glowBlock??=new MaterialPropertyBlock();
            foreach(var (renderer,index,emission) in signals)
            {
                if(renderer==null) continue;
                // Capped so the brightest channel never clips: a flared amber stays amber instead of tonemapping to yellow.
                float scale=Mathf.Min(glow,Mathf.Max(1,MaxSignal/Mathf.Max(.001f,emission.maxColorComponent)));
                renderer.GetPropertyBlock(glowBlock,index); glowBlock.SetColor(EmissionColor,emission*scale); renderer.SetPropertyBlock(glowBlock,index);
            }
        }
        // Mouse-look inertia (vibegame1 ViewmodelMotion): the weapon lags the turn on a critically damped spring. It follows
        // the aim (FirstPersonView.Yaw/Pitch), not the camera, so shot punch, shake, landing nod and roll never feed it.
        void UpdateSway(float dt,bool reducedMotion)
        {
            Vector2 turn=Vector2.zero, look=aim!=null?new Vector2(aim.Yaw,aim.Pitch):Vector2.zero;
            if(aim!=null && hasLook && dt>0) turn=new Vector2(Mathf.DeltaAngle(previousLook.x,look.x),look.y-previousLook.y)/dt;
            previousLook=look; hasLook=aim!=null;
            Vector2 target=reducedMotion?Vector2.zero:Vector2.ClampMagnitude(-turn*.004f,1);
            swayVelocity+=((target-sway)*SwayStiffness-swayVelocity*SwayDamping)*dt;
            sway+=swayVelocity*dt;
            sway=Vector2.ClampMagnitude(sway,1.2f);
        }
        public bool Scoped=>scoped;
        // Owner-predicted ADS progress; negative falls back to the replicated CombatState value.
        public float LocalAds { get; set; }=-1;
        float Ads(CombatState player) => LocalAds>=0?LocalAds:player.AdsProgress;
        // World effects (beams, flashes) are drawn by the wider world camera, so the socket is moved to where the viewmodel
        // camera shows it: same screen point, same depth. Without this a beam starts short of the barrel, toward the centre.
        public bool TryGetMuzzle(out Vector3 position)
        {
            position=muzzle!=null?muzzle.position:Vector3.zero;
            if(muzzle!=null && worldCamera!=null) position=worldCamera.ViewportToWorldPoint(overlay.WorldToViewportPoint(position));
            return visible && muzzle!=null;
        }
        static Material MaterialFor(PresentationCatalog catalog,WeaponKind kind)
        {
            switch(kind)
            {
                case WeaponKind.LaserShotgun:
                case WeaponKind.PulseLobber: return catalog.Pulse;
                case WeaponKind.ReboundDisc: return catalog.Disc;
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
        static readonly AnimationCurve Even=AnimationCurve.Constant(0,1,1),
            Spike=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,1),new Keyframe(1,0)),Tongue=AnimationCurve.Linear(0,1,1,.15f);
        float flashSpin;
        // A shot is a hot tongue out of the barrel inside a six-point star flare that blooms and shrinks; each shot turns
        // the star. The Push Lens charge keeps its closing ring and converging arcs.
        void PresentMuzzle(float charge,bool reducedMotion)
        {
            bool active=visible && muzzle!=null && (flashRemaining>0 || charge>0);
            for(int i=0;i<muzzleElements.Length;i++) muzzleElements[i].enabled=active && (!reducedMotion || i==0 || (i==1 && charge>0));
            if(!active) return;
            Vector3 origin=muzzle.position;
            Vector3 forward=flashRemaining>0?shotDirection:overlay.transform.forward;
            Vector3 right=Vector3.Cross(overlay.transform.up,forward).normalized,up=Vector3.Cross(forward,right);
            float progress=flashRemaining>0?1-flashRemaining/flashDuration:0;
            float size=reducedMotion?.5f:1;
            bool charging=charge>0;
            for(int i=0;i<muzzleElements.Length;i++)
            {
                var line=muzzleElements[i]; line.sharedMaterial=shotMaterial;
                line.widthMultiplier=(i==0?.026f:charging?.009f:.02f)*size;
                line.widthCurve=i==0?Tongue:charging?Even:Spike;
            }
            muzzleElements[1].positionCount=charging?17:3;
            var core=muzzleElements[0];
            float length=(currentWeapon==WeaponKind.LaserSniper?.35f:currentWeapon==WeaponKind.LaserShotgun?.16f:currentWeapon==WeaponKind.LaserPistol?.13f:currentWeapon==WeaponKind.LineDriver?.22f:currentWeapon==WeaponKind.PulseLobber?.12f:.08f)*size;
            core.SetPosition(0,origin); core.SetPosition(1,origin+forward*length*.5f); core.SetPosition(2,origin+forward*length);
            if(charging)
            {
                float radius=Mathf.Lerp(.15f,.055f,charge);
                for(int i=0;i<17;i++)
                {
                    float angle=i*Mathf.PI/8;
                    muzzleElements[1].SetPosition(i,origin+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius*size);
                }
                for(int i=2;i<4;i++)
                {
                    float sign=i==2?-1:1;
                    var arc=muzzleElements[i];
                    arc.SetPosition(0,origin+right*.025f*sign);
                    arc.SetPosition(1,origin+forward*length*.65f+right*radius*sign+up*.025f);
                    arc.SetPosition(2,origin+forward*length+right*.035f*sign);
                }
                return;
            }
            float flare=currentWeapon==WeaponKind.PushLens?.16f:currentWeapon==WeaponKind.PulseLobber?.13f:currentWeapon==WeaponKind.LaserShotgun?.12f:
                currentWeapon==WeaponKind.LaserSniper?.11f:currentWeapon==WeaponKind.LineDriver?.075f:currentWeapon==WeaponKind.LaserSMG?.065f:currentWeapon==WeaponKind.LaserPistol?.05f:.08f;
            float bloom=progress<.25f?Mathf.Lerp(.6f,1,progress/.25f):(1-progress)/.75f;
            Vector3 center=origin+forward*flare*.3f;
            for(int i=1;i<4;i++)
            {
                float angle=flashSpin+(i-1)*Mathf.PI/3;
                Vector3 spoke=(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*flare*bloom*size*(i==1?1.25f:1);
                var line=muzzleElements[i];
                line.SetPosition(0,center-spoke); line.SetPosition(1,center); line.SetPosition(2,center+spoke);
            }
        }
        float previousVertical;
        public void Teardown(Camera world)
        {
            if(overlay==null) return;
            if(world!=null)
            {
                world.GetUniversalAdditionalCameraData().cameraStack.Remove(overlay);
                world.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                world.cullingMask=(world.cullingMask&~(1<<ViewLayer))|(worldHadViewLayer?(1<<ViewLayer):0);
            }
            Destroy(overlay.gameObject);
            if(streakMaterial!=null) Destroy(streakMaterial);
            overlay=null; worldData=null; sight=null; weapon=null; socket=null; hands=null; arms=null; support=null; muzzle=null; movingPart=null; visible=false; currentWeapon=WeaponKind.None; life=-1; ResetMotion();
            heldGrenade=null;
        }
        // Overlay-only content: its own layer, and no shadows, since a viewmodel shadow lands in the world at the wrong scale.
        static void SetLayer(Transform root,int layer)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=layer;
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
