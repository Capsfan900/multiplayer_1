using System;
using System.Collections.Generic;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Match;

namespace Wildstone.Presentation
{
    // Every cue reads committed state. No effect object has a collider or changes simulation.
    public sealed class ArenaEffects : MonoBehaviour
    {
        const int BurstCapacity=96, ProjectileCapacity=WeaponCatalog.MaximumProjectiles, PickupCapacity=32;
        readonly LineRenderer[] bursts=new LineRenderer[BurstCapacity];
        readonly float[] burstEnds=new float[BurstCapacity];
        readonly float[] burstStarts=new float[BurstCapacity],burstWidths=new float[BurstCapacity],burstRadii=new float[BurstCapacity];
        readonly Vector3[] burstCenters=new Vector3[BurstCapacity],burstNormals=new Vector3[BurstCapacity];
        readonly bool[] expanding=new bool[BurstCapacity];
        readonly PresentationActionKey[] contextIds=new PresentationActionKey[512];
        readonly WeaponKind[] contextWeapons=new WeaponKind[512];
        readonly Vector3[] contextDirections=new Vector3[512];
        int contextIndex;
        readonly MeshRenderer[] projectileRenderers=new MeshRenderer[ProjectileCapacity];
        readonly Transform[] projectileTransforms=new Transform[ProjectileCapacity];
        readonly LineRenderer[] trails=new LineRenderer[ProjectileCapacity];
        readonly PresentationActionKey[] trailIds=new PresentationActionKey[ProjectileCapacity];
        readonly Vector3[] trailEnds=new Vector3[ProjectileCapacity];
        readonly Vector3[] trailPoints=new Vector3[ProjectileCapacity*4];
        readonly int[] trailCounts=new int[ProjectileCapacity];
        readonly LineRenderer[] fuses=new LineRenderer[ProjectileCapacity];
        readonly List<GameObject> pickupModels=new List<GameObject>();
        // Pickup pads: a dark floor band under the item's glowing ring, which becomes a dim arc that refills while the
        // item respawns, so an ivory weapon over ivory stone reads from across the court.
        public const int PickupRingPoints=40;
        public const float PickupRingRadius=.6f;
        readonly List<LineRenderer> pickupRings=new List<LineRenderer>(),pickupTracks=new List<LineRenderer>();
        readonly List<Material> pickupMaterials=new List<Material>();
        readonly LineRenderer[] shields=new LineRenderer[8];
        readonly LineRenderer[] stuns=new LineRenderer[8];
        readonly LineRenderer[] immunities=new LineRenderer[8];
        // Active shield: a fresnel shell around the body that dims with the remaining shield (ShieldShell.shader).
        readonly Transform[] shells=new Transform[8];
        readonly Renderer[] shellRenderers=new Renderer[8];
        MaterialPropertyBlock shellBlock;
        Vector3 shellMeshSize=Vector3.one;
        // A broken shield's shell flares and swells for a moment instead of vanishing (unscaled time of the break).
        readonly float[] shellBreaks={-1,-1,-1,-1,-1,-1,-1,-1};
        public const float ShellBreakSeconds=.22f;
        // Stun halo painted solid (premultiplied) like the pickup rings: the additive green washed out on ivory.
        Material stunPaint;
        public const int StunPoints=16;
        Transform zoneDecal, zoneCurtain, zoneBeacon;
        Material zoneDecalMaterial, zoneCurtainMaterial, zoneBeaconMaterial;
        Mesh curtainMesh;
        Vector3 lastZone;
        float moteClock;
        ArenaVfx vfx;
        public ArenaVfx Vfx => vfx;
        // Cosmetic camera shake sink (FirstPersonView.AddShake); aim never reads the camera transform.
        public System.Action<Vector3,float> Shake;
        readonly Dictionary<int,MovementTrace> movement=new Dictionary<int,MovementTrace>();
        struct MovementTrace { public bool Grounded,Wall; public float VerticalSpeed,Distance; public double DashStartedAt; public Vector3 Last; }
        int burstIndex;
        PresentationCatalog catalog;
        Mesh octahedron;
        Material brightCore,sheath;
        // D029 hold-to-aim grenade preview: the simulated flight (ArenaMatch.StepGrenade) and the blast radius at rest.
        LineRenderer grenadeArc, grenadeRing;
        readonly Vector3[] grenadePath=new Vector3[GrenadePathCapacity];
        readonly Vector3[] grenadeLine=new Vector3[64];
        public const int GrenadePathCapacity=512;
        static readonly GeometryWorld Geometry=new GeometryWorld();
        public void Initialize(PresentationCatalog assets,ArenaMatch match)
        {
            catalog=assets;
            brightCore=new Material(assets.Beam) { name="Weapon bright core" };
            brightCore.SetColor("_BaseColor",new Color(3.44f,4f,4f));
            // Daylight contrast: additive cyan washes out to white on ivory stone and sky, so each laser also draws a dark
            // premultiplied sheath, queued just before the glow so it always sits underneath.
            sheath=new Material(assets.Beam) { name="Weapon beam sheath", renderQueue=assets.Beam.renderQueue-1 };
            sheath.SetColor("_BaseColor",new Color(0,.05f,.12f,.5f));
            sheath.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            octahedron=CreateOctahedron();
            for(int i=0;i<BurstCapacity;i++) bursts[i]=Line("Burst "+i,assets.Beam,.035f);
            grenadeArc=Line("Grenade arc",assets.Danger,.022f); grenadeArc.numCapVertices=2;
            grenadeArc.widthCurve=new AnimationCurve(new Keyframe(0,.4f),new Keyframe(.15f,1),new Keyframe(1,.8f));
            grenadeRing=Line("Grenade landing",assets.Danger,.03f); grenadeRing.loop=true; grenadeRing.positionCount=40;
            for(int i=0;i<ProjectileCapacity;i++)
            {
                var go=new GameObject("Projectile "+i,typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetParent(transform,false);
                go.GetComponent<MeshFilter>().sharedMesh=octahedron;
                projectileRenderers[i]=go.GetComponent<MeshRenderer>();
                projectileTransforms[i]=go.transform;
                trails[i]=Line("Projectile trail "+i,assets.Pulse,.025f);
                trails[i].widthCurve=AnimationCurve.Linear(0,1,1,.05f);
                fuses[i]=Line("Grenade fuse "+i,assets.Danger,.04f);
                go.SetActive(false);
            }
            if(match.Pickups.Count>PickupCapacity) throw new ArgumentException("Presentation pickup capacity exceeded.");
            for(int i=0;i<match.Pickups.Count;i++)
            {
                var pickup=match.Pickups[i];
                var prefab=assets.Pickup(pickup.Item);
                if(prefab==null) throw new ArgumentException("Missing pickup model: "+pickup.Item);
                var model=Instantiate(prefab,transform);
                model.name="Pickup "+pickup.Id+" "+pickup.Item;
                pickupModels.Add(model);
            }
            stunPaint=new Material(sheath) { name="Stun halo",renderQueue=sheath.renderQueue+1 };
            stunPaint.SetColor("_BaseColor",new Color(.1f,1,.5f,1));
            for(int i=0;i<shields.Length;i++)
            {
                shields[i]=Line("Shield "+i,assets.Shield,.045f);
                stuns[i]=Line("Stun "+i,stunPaint,.055f);
                immunities[i]=Line("Immunity "+i,assets.Blink,.04f);
            }
            vfx=new GameObject("Arena vfx",typeof(ArenaVfx)).GetComponent<ArenaVfx>();
            vfx.transform.SetParent(transform,false);
            vfx.Build(assets);
            if(assets.ZoneDecal==null || assets.ZoneCurtain==null) throw new ArgumentException("Missing hot zone materials.");
            zoneDecalMaterial=new Material(assets.ZoneDecal) { name="Hot zone decal" };
            zoneCurtainMaterial=new Material(assets.ZoneCurtain) { name="Hot zone curtain" };
            zoneBeaconMaterial=new Material(assets.ZoneCurtain) { name="Hot zone beacon" };
            curtainMesh=CreateCurtain(48);
            zoneDecal=Shape("Hot zone floor",Resources.GetBuiltinResource<Mesh>("Cube.fbx"),zoneDecalMaterial);
            zoneCurtain=Shape("Hot zone curtain",curtainMesh,zoneCurtainMaterial);
            zoneBeacon=Shape("Hot zone beacon",curtainMesh,zoneBeaconMaterial);
            if(assets.ShieldShell!=null)
                for(int i=0;i<shells.Length;i++)
                {
                    shellMeshSize=Resources.GetBuiltinResource<Mesh>("Sphere.fbx").bounds.size;
                    shells[i]=Shape("Shield shell "+i,Resources.GetBuiltinResource<Mesh>("Sphere.fbx"),assets.ShieldShell);
                    shellRenderers[i]=shells[i].GetComponent<Renderer>();
                }
            for(int i=0;i<match.Pickups.Count;i++)
            {
                var item=match.Pickups[i].Item;
                var kind=WeaponCatalog.FromPickup(item);
                var source=item=="blink"?assets.Blink:item=="shield"?assets.Shield:MaterialFor(kind);
                // Painted in the item's hue (premultiplied, like the beam sheath): additive glow washed out to white on ivory.
                var tint=Tint(kind,source); float peak=Mathf.Max(tint.r,Mathf.Max(tint.g,tint.b));
                var paint=new Material(sheath) { name="Pickup ring "+item,renderQueue=sheath.renderQueue+1 };
                paint.SetColor("_BaseColor",new Color(tint.r/peak*1.15f,tint.g/peak*1.15f,tint.b/peak*1.15f,1));
                pickupMaterials.Add(paint);
                var ring=Line("Pickup ring "+match.Pickups[i].Id,paint,.11f);
                var track=Line("Pickup track "+match.Pickups[i].Id,sheath,.3f);
                track.loop=true; track.positionCount=PickupRingPoints;
                pickupRings.Add(ring); pickupTracks.Add(track);
            }
        }
        Transform Shape(string name,Mesh mesh,Material material)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(transform,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
            go.SetActive(false);
            return go.transform;
        }
        // Open unit cylinder (radius 1, height 1) whose vertex alpha fades from the floor to the top.
        static Mesh CreateCurtain(int segments)
        {
            var mesh=new Mesh { name="Hot zone curtain" };
            var vertices=new Vector3[(segments+1)*2]; var uv=new Vector2[vertices.Length]; var colors=new Color[vertices.Length];
            var triangles=new int[segments*6];
            for(int i=0;i<=segments;i++)
            {
                float angle=i*2*Mathf.PI/segments; var ring=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                vertices[i*2]=ring; vertices[i*2+1]=ring+Vector3.up;
                uv[i*2]=new Vector2(i/(float)segments,0); uv[i*2+1]=new Vector2(i/(float)segments,1);
                colors[i*2]=Color.white; colors[i*2+1]=new Color(1,1,1,0);
                if(i==segments) break;
                int t=i*6,v=i*2;
                triangles[t]=v; triangles[t+1]=v+1; triangles[t+2]=v+3; triangles[t+3]=v; triangles[t+4]=v+3; triangles[t+5]=v+2;
            }
            mesh.vertices=vertices; mesh.uv=uv; mesh.colors=colors; mesh.triangles=triangles; mesh.RecalculateBounds();
            return mesh;
        }
        // Fills path with the grenade position at launch and after each simulated tick; the last point is where it detonates.
        public static int PredictGrenade(ICombatWorld world,int owner,Vector3 origin,Vector3 direction,int tickRate,Vector3[] path)
        {
            if(world==null || path==null || path.Length==0 || tickRate<=0) return 0;
            Vector3 position=origin,velocity=ArenaMatch.GrenadeLaunch(direction.normalized); float travel=0;
            int ticks=Math.Min(path.Length-1,WeaponCatalog.Ticks(WeaponCatalog.Get(WeaponKind.Grenade).Lifetime,tickRate));
            path[0]=position;
            for(int i=1;i<=ticks;i++) { ArenaMatch.StepGrenade(world,owner,0,1f/tickRate,ref position,ref velocity,ref travel); path[i]=position; }
            return ticks+1;
        }
        public void PresentGrenadeAim(bool show,Vector3 origin,Vector3 direction,int owner,int tickRate)
        {
            if(grenadeArc==null) return;
            int count=show?PredictGrenade(Geometry,owner,origin,direction,tickRate,grenadePath):0;
            grenadeArc.enabled=grenadeRing.enabled=count>1;
            if(count<=1) return;
            // Skip the first few centimetres so the arc does not start inside the lens.
            int first=Mathf.Min(count-1,Mathf.Max(1,tickRate/20)),points=Mathf.Min(grenadeLine.Length,count-first);
            for(int i=0;i<points;i++) grenadeLine[i]=grenadePath[first+(count-1-first)*i/Mathf.Max(1,points-1)];
            grenadeArc.positionCount=points; grenadeArc.SetPositions(grenadeLine);
            Vector3 end=grenadePath[count-1];
            float radius=WeaponCatalog.Get(WeaponKind.Grenade).Radius;
            for(int i=0;i<grenadeRing.positionCount;i++)
            {
                float angle=i*Mathf.PI*2/grenadeRing.positionCount;
                grenadeRing.SetPosition(i,end+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius+Vector3.up*.05f);
            }
        }
        // World geometry only: the preview ignores players, which grenades also bounce off.
        sealed class GeometryWorld : EmptyCombatWorld
        {
            readonly RaycastHit[] hits=new RaycastHit[16];
            public override bool Sweep(Vector3 origin,Vector3 displacement,float radius,int owner,int ignoredMask,out CombatHit hit)
            {
                hit=default; float distance=displacement.magnitude;
                if(distance<.000001f) return false;
                int count=Physics.SphereCastNonAlloc(origin,radius,displacement/distance,hits,distance,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore);
                float nearest=float.MaxValue;
                for(int i=0;i<count;i++)
                    if(hits[i].distance<nearest) { nearest=hits[i].distance; hit=new CombatHit(0,hits[i].point,hits[i].normal,hits[i].distance); }
                return nearest<float.MaxValue;
            }
        }
        LineRenderer Line(string name,Material material,float width)
        {
            var go=new GameObject(name,typeof(LineRenderer));
            go.transform.SetParent(transform,false);
            var line=go.GetComponent<LineRenderer>();
            line.sharedMaterial=material;
            line.useWorldSpace=true;
            line.widthMultiplier=width;
            line.numCapVertices=0;
            line.numCornerVertices=0;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows=false;
            line.enabled=false;
            return line;
        }
        LineRenderer Burst(Material material,float width,float seconds)
        {
            var index=burstIndex++%BurstCapacity;
            var line=bursts[index];
            line.sharedMaterial=material;
            line.widthMultiplier=width;
            line.widthCurve=Flat;
            line.loop=false;
            line.enabled=true;
            burstStarts[index]=Time.unscaledTime; burstEnds[index]=burstStarts[index]+seconds;
            burstWidths[index]=width; expanding[index]=false;
            return line;
        }
        public void Play(in CombatEvent e,ArenaMatch match,bool reducedMotion,Vector3 departure,IReadOnlyList<CombatEvent> events = null,Vector3? muzzle=null)
        {
            events=events??match.Events;
            Vector3 position=e.Position;
            if(e.Kind==CombatEventKind.Stunned || e.Kind==CombatEventKind.ShieldBroken || e.Kind==CombatEventKind.ShieldActivated || e.Kind==CombatEventKind.ShieldExpired)
            {
                var target=match.Find(e.Target);
                if(target!=null) position=target.Feet;
            }
            switch(e.Kind)
            {
                case CombatEventKind.Shot:
                    Remember(e);
                    if(e.Weapon==WeaponKind.Grenade) break;
                    MuzzleFlash(e,muzzle??e.Position,reducedMotion);
                    // Beams start at the barrel and carry their glow to the far end; walls they strike spark and flash.
                    // The muzzle itself is the flash sprite and lamp (MuzzleFlash), not a ring in front of the lens.
                    if(e.Weapon==WeaponKind.LaserShotgun)
                    {
                        for(int pellet=0;pellet<WeaponCatalog.HitscanPellets(e.Weapon);pellet++)
                        {
                            ShotSegment(e,events,muzzle,out var start,out var end,pellet);
                            Beam(start,end,catalog.Beam,reducedMotion?.025f:.04f,.15f);
                            if(!reducedMotion && pellet%2==0) Strike(e,start,end,.12f,2);
                        }
                        for(int pellet=0;pellet<WeaponCatalog.HitscanPellets(e.Weapon);pellet++)
                        {
                            ShotSegment(e,events,muzzle,out var start,out var end,pellet);
                            Beam(start,end,sheath,.09f,.15f);
                        }
                    }
                    else if(WeaponCatalog.IsHitscan(e.Weapon))
                    {
                        ShotSegment(e,events,muzzle,out var start,out var end);
                        bool pistol=e.Weapon==WeaponKind.LaserPistol,sniper=e.Weapon==WeaponKind.LaserSniper;
                        float width=pistol?.075f:sniper?.26f:reducedMotion?.055f:.11f,life=pistol?.13f:sniper?.3f:.14f;
                        Beam(start,end,catalog.Beam,width,life);
                        if(!reducedMotion)
                        {
                            Beam(start,end,brightCore,pistol?.022f:sniper?.075f:.028f,sniper?.4f:.10f);
                            if(sniper) Ring(end,e.Vector,.36f,brightCore,.30f);
                            Strike(e,start,end,sniper?.4f:pistol?.16f:.22f,sniper?8:4);
                        }
                        Beam(start,end,sheath,width*1.8f,life);
                    }
                    else if(e.Weapon==WeaponKind.PushLens)
                    {
                        for(int i=0;i<(reducedMotion?1:3);i++) Ring(e.Position+e.Vector*(1+i*.8f),e.Vector,.35f+i*.35f,catalog.Push,.28f+i*.03f);
                        if(!reducedMotion) Sparks(e.Position+e.Vector*1.8f,-e.Vector,catalog.Push,.9f,3,.2f);
                    }
                    else if(e.Weapon==WeaponKind.LaserSword)
                    {
                        Arc(e.Position,e.Vector,2.1f,catalog.Beam,reducedMotion?.12f:.3f,.20f);
                        if(!reducedMotion) Arc(e.Position,e.Vector,1.7f,brightCore,.06f,.13f);
                    }
                    else
                    {
                        Vector3 origin=muzzle??e.Position;
                        Sparks(origin,e.Vector,MaterialFor(e.Weapon),reducedMotion?.3f:.55f,reducedMotion?2:e.Weapon==WeaponKind.StunGun?4:3,.12f);
                    }
                    break;
                case CombatEventKind.Hit:
                    WeaponKind impact=e.Weapon!=WeaponKind.None?e.Weapon:ContextWeapon(e.Action);
                    Material impactMaterial=impact==WeaponKind.None?catalog.Danger:MaterialFor(impact);
                    Vector3 incoming=ContextDirection(e.Action);
                    if(impact==WeaponKind.LaserShotgun && e.SubIndex>=0 && e.SubIndex<WeaponCatalog.HitscanPellets(impact)) incoming=WeaponCatalog.HitscanDirection(impact,incoming,e.SubIndex,WeaponCatalog.SpreadDegrees(impact,0,0),0);
                    Ring(e.Position,incoming,impact==WeaponKind.LaserSniper?.42f:impact==WeaponKind.LaserShotgun?.14f:.24f,impactMaterial,.22f);
                    if(!reducedMotion) Sparks(e.Position,-incoming,impactMaterial,impact==WeaponKind.LaserSniper?.95f:.65f,impact==WeaponKind.LaserShotgun?2:4,.20f);
                    var tint=Tint(impact);
                    vfx.Flash(e.Position,tint*1.4f,impact==WeaponKind.LaserSniper?.9f:impact==WeaponKind.LaserShotgun?.25f:.45f,.07f);
                    vfx.SparkBurst(e.Position,-incoming,tint,reducedMotion?3:impact==WeaponKind.LaserShotgun?4:9,impact==WeaponKind.LaserSniper?9:6);
                    if(e.Headshot)
                    {
                        // Headshot read: a hot white crown burst above the plain hit, visible at range.
                        vfx.Flash(e.Position,HeadshotColor,impact==WeaponKind.LaserSniper?1.3f:.75f,.1f);
                        vfx.SparkBurst(e.Position,Vector3.up-incoming*.5f,HeadshotColor,reducedMotion?4:14,7,1.1f);
                        Ring(e.Position,Vector3.up,.32f,brightCore,.2f);
                    }
                    break;
                case CombatEventKind.Ricochet:
                    vfx.SparkBurst(e.Position,-ContextDirection(e.Action),Tint(WeaponKind.ReboundDisc),reducedMotion?3:10,7);
                    Ring(e.Position,Vector3.up,.30f,catalog.Disc,.22f);
                    if(!reducedMotion) Sparks(e.Position,-ContextDirection(e.Action),catalog.Disc,.85f,4,.20f);
                    break;
                case CombatEventKind.Explosion:
                    float radius=Mathf.Clamp(Mathf.Max(.5f,e.Amount),.5f,5.5f);
                    Blast(e,radius,reducedMotion);
                    Ring(e.Position,Vector3.up,radius,catalog.Danger,.38f);
                    if(!reducedMotion)
                    {
                        Ring(e.Position,Vector3.right,radius*.75f,catalog.Pulse,.32f);
                        Ring(e.Position,Vector3.forward,radius*.75f,catalog.Pulse,.32f);
                        Sparks(e.Position,Vector3.up,catalog.Danger,radius,3,.30f);
                    }
                    break;
                case CombatEventKind.Deflected:
                    // The reflected projectile's later hits and blast carry the push's action: let them read as that projectile.
                    var pushKey=new PresentationActionKey(e.Action);
                    for(int i=0;i<contextIds.Length;i++) if(contextIds[i].Equals(pushKey)) contextWeapons[i]=WeaponKind.None;
                    Remember(e);
                    Ring(e.Position,e.Vector,.5f,catalog.Push,.22f);
                    vfx.Flash(e.Position,Tint(WeaponKind.PushLens)*1.5f,.8f,.08f);
                    if(!reducedMotion) vfx.SparkBurst(e.Position,e.Vector,Tint(e.Weapon),8,6);
                    break;
                case CombatEventKind.Impulse: Ring(e.Position,Vector3.up,.8f,catalog.Push,.16f); break;
                case CombatEventKind.Stunned: Cross(position+Vector3.up*1.6f,catalog.Stun,.5f); break;
                case CombatEventKind.Blink:
                    vfx.Flash(departure+Vector3.up*.9f,Tint(WeaponKind.None,catalog.Blink)*1.5f,1.4f,.14f);
                    vfx.Flash(e.Position+Vector3.up*.9f,Tint(WeaponKind.None,catalog.Blink)*1.5f,1.6f,.18f);
                    if(!reducedMotion) vfx.SparkBurst(e.Position+Vector3.up*.9f,Vector3.up,Tint(WeaponKind.None,catalog.Blink),10,3,2.4f);
                    Ring(departure+Vector3.up*.8f,Vector3.up,.55f,catalog.Blink,.16f);
                    Ring(e.Position+Vector3.up*.8f,Vector3.up,.8f,catalog.Blink,.20f);
                    break;
                case CombatEventKind.ShieldActivated: Ring(position+Vector3.up*1.2f,Vector3.up,.48f,catalog.Shield,.18f); break;
                case CombatEventKind.ShieldBroken:
                    if(!reducedMotion)
                        for(int i=0;i<match.Players.Count && i<shellBreaks.Length;i++)
                            if(match.Players[i].Participant==e.Target) shellBreaks[i]=Time.unscaledTime;
                    Ring(position+Vector3.up*.9f,Vector3.up,.8f,catalog.Shield,.18f);
                    vfx.SparkBurst(position+Vector3.up*1.1f,Vector3.up,Tint(WeaponKind.None,catalog.Shield),reducedMotion?4:16,4,2.6f);
                    vfx.Flash(position+Vector3.up*1.1f,Tint(WeaponKind.None,catalog.Shield),1.2f,.1f);
                    break;
                case CombatEventKind.ShieldExpired: Ring(position+Vector3.up*.9f,Vector3.up,.55f,catalog.Shield,.10f); break;
                case CombatEventKind.Death:
                    Ring(e.Position+Vector3.up*.9f,Vector3.up,.7f,catalog.Danger,.22f);
                    if(!reducedMotion) vfx.Dust(e.Position,Vector3.zero,.5f,5,DustColor);
                    break;
            }
            if(reducedMotion)
                for(int i=0;i<BurstCapacity;i++) if(bursts[i].enabled)
                { burstEnds[i]=Mathf.Min(burstEnds[i],Time.unscaledTime+.07f); expanding[i]=false; }
        }
        static readonly Color DustColor=new Color(.62f,.58f,.52f,.55f), HeadshotColor=new Color(2.6f,2.3f,1.6f,1);
        Color Tint(WeaponKind weapon,Material material=null)
        {
            var source=material!=null?material:MaterialFor(weapon);
            var color=source!=null && source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):Color.white;
            float peak=Mathf.Max(color.r,Mathf.Max(color.g,color.b));
            return peak>1?new Color(color.r/peak,color.g/peak,color.b/peak,1)*1.6f:color;
        }
        void MuzzleFlash(in CombatEvent e,Vector3 origin,bool reducedMotion)
        {
            if(e.Weapon==WeaponKind.LaserSword) return;
            var tint=Tint(e.Weapon);
            bool heavy=e.Weapon==WeaponKind.LaserSniper || e.Weapon==WeaponKind.LaserShotgun || e.Weapon==WeaponKind.PulseLobber;
            vfx.Flash(origin,tint*1.6f,heavy?.55f:.32f,heavy?.09f:.05f);
            // Dim and short: brighter lamps left pale tinted blobs on the ivory floor in front of the shooter.
            vfx.Lamp(origin+e.Vector.normalized*.25f,tint,heavy?1.8f:1.1f,heavy?2.5f:2,heavy?.1f:.06f);
            if(!reducedMotion) vfx.SparkBurst(origin,e.Vector,tint,heavy?6:3,heavy?7:5,.35f);
        }
        void Blast(in CombatEvent e,float radius,bool reducedMotion)
        {
            var weapon=e.Weapon!=WeaponKind.None?e.Weapon:ContextWeapon(e.Action);
            bool grenade=weapon!=WeaponKind.PulseLobber;
            Color hot=grenade?new Color(1.6f,.9f,.42f):Tint(WeaponKind.PulseLobber)*1.2f;
            vfx.Flash(e.Position+Vector3.up*.3f,hot*1.6f,radius*1.4f,.12f);
            vfx.Lamp(e.Position+Vector3.up*.8f,hot,5,radius*1.8f,.22f);
            vfx.Fireball(e.Position+Vector3.up*.25f,radius*.45f,reducedMotion?3:9,hot);
            vfx.Wave(e.Position,Vector3.up,radius*1.25f,new Color(1,.85f,.65f,1)*.8f,.45f);
            vfx.Scorch(e.Position,radius*.6f);
            if(!reducedMotion)
            {
                vfx.Smoke(e.Position+Vector3.up*.4f,radius*.55f,8,new Color(.34f,.32f,.30f,.7f));
                vfx.SparkBurst(e.Position+Vector3.up*.2f,Vector3.up,hot,24,radius*3,1.4f);
                vfx.Debris(e.Position,radius*2.4f,10,new Color(.42f,.40f,.37f,1));
                vfx.Dust(e.Position,Vector3.zero,radius*.45f,8,DustColor);
            }
            Shake?.Invoke(e.Position,grenade?1:.7f);
        }
        // Slide sparks/dust, landing dust, dash puffs and wall-run sparks from each presented motor state.
        public void PresentMovement(int participant,in Wildstone.Movement.MotorState state,bool reducedMotion,float dt)
        {
            if(dt<=0) return;
            Vector3 feet=state.Position;
            movement.TryGetValue(participant,out var trace);
            bool fresh=trace.Last==Vector3.zero || (feet-trace.Last).sqrMagnitude>25;
            float step=fresh?0:Vector3.Distance(feet,trace.Last);
            if(!fresh && state.Grounded && !trace.Grounded && trace.VerticalSpeed<-6)
                vfx.Dust(feet,Vector3.zero,Mathf.Clamp(-trace.VerticalSpeed/22,.25f,.7f),reducedMotion?2:6,DustColor);
            if(!fresh && state.DashStartedAt!=trace.DashStartedAt && state.DashStartedAt>0)
            {
                vfx.Dust(feet+Vector3.up*.3f,-state.DashDirection*2,.35f,reducedMotion?1:4,DustColor);
                if(!reducedMotion) vfx.SparkBurst(feet+Vector3.up*.9f,-state.DashDirection,new Color(.9f,.95f,1,1),6,9,.25f);
            }
            trace.Distance+=step;
            float spacing=reducedMotion?1.2f:.35f;
            if(state.Sliding && state.Grounded && trace.Distance>=spacing)
            {
                var back=-new Vector3(state.Velocity.x,0,state.Velocity.z).normalized;
                vfx.SparkBurst(feet+back*.3f+Vector3.up*.05f,back+Vector3.up*.6f,new Color(1.4f,.95f,.55f,1),3,4,.5f);
                vfx.Dust(feet+back*.4f,back*1.5f,.3f,1,DustColor);
                trace.Distance=0;
            }
            else if(state.WallRunning && trace.Distance>=spacing*1.3f)
            {
                var contact=feet+Vector3.up*.6f-state.WallNormal*.4f;
                vfx.SparkBurst(contact,state.WallNormal-state.Velocity.normalized*.5f,new Color(1.3f,.95f,.6f,1),2,3,.6f);
                trace.Distance=0;
            }
            else if(!state.Sliding && !state.WallRunning) trace.Distance=Mathf.Min(trace.Distance,spacing);
            trace.Grounded=state.Grounded; trace.VerticalSpeed=state.Velocity.y; trace.DashStartedAt=state.DashStartedAt;
            trace.Wall=state.WallRunning; trace.Last=feet;
            movement[participant]=trace;
        }
        void Segment(Vector3 a,Vector3 b,Material material,float width,float life)
        {
            var line=Burst(material,width,life); line.positionCount=2; line.SetPosition(0,a); line.SetPosition(1,b);
        }
        static readonly AnimationCurve Flat=AnimationCurve.Constant(0,1,1);
        readonly AnimationCurve taper=new AnimationCurve();
        readonly Keyframe[] taperKeys=new Keyframe[3];
        // Roughly constant on-screen width: thin beside the lens, wider far away, sampled at both ends and the point nearest
        // the viewer (a remote shot passing the camera stays slim there).
        void Beam(Vector3 a,Vector3 b,Material material,float width,float life)
        {
            Segment(a,b,material,width,life);
            var viewer=Camera.main;
            if(viewer==null) return;
            Vector3 eye=viewer.transform.position,ab=b-a;
            float near=ab.sqrMagnitude>.0001f?Mathf.Clamp(Vector3.Dot(eye-a,ab)/ab.sqrMagnitude,.01f,.99f):.5f;
            float Scale(float t) => Mathf.Clamp(Vector3.Distance(eye,a+ab*t)/7,.35f,6f);
            taperKeys[0]=new Keyframe(0,Scale(0)); taperKeys[1]=new Keyframe(near,Scale(near)); taperKeys[2]=new Keyframe(1,Scale(1));
            taper.keys=taperKeys;
            for(int i=0;i<3;i++) taper.SmoothTangents(i,1);
            bursts[(burstIndex-1)%BurstCapacity].widthCurve=taper;
        }
        // Wall strike: sparks thrown back off the surface and a short flash, only when the beam ended on something.
        void Strike(in CombatEvent e,Vector3 start,Vector3 end,float flash,int sparks)
        {
            var direction=end-start;
            if(direction.sqrMagnitude<.0001f || direction.magnitude>=WeaponCatalog.Get(e.Weapon).Range-.05f) return;
            var tint=Tint(e.Weapon);
            vfx.Flash(end,tint*1.3f,flash,.06f);
            vfx.SparkBurst(end,-direction.normalized,tint,sparks,5,1.1f);
            // Cover that stopped the beam keeps a burn with a cooling ember; players are not in the world mask.
            float length=direction.magnitude;
            if(Physics.Raycast(start,direction/length,out var surface,length+.2f,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore) && surface.distance>length-.3f)
            {
                vfx.Burn(surface.point,surface.normal,e.Weapon==WeaponKind.LaserSniper?.2f:e.Weapon==WeaponKind.LaserShotgun?.07f:.11f);
                vfx.Flash(surface.point+surface.normal*.03f,tint*.9f,flash*.45f,.4f);
            }
        }
        void Remember(in CombatEvent e)
        {
            int slot=contextIndex++%contextIds.Length;
            contextIds[slot]=new PresentationActionKey(e.Action); contextWeapons[slot]=e.Weapon;
            contextDirections[slot]=e.Vector.sqrMagnitude>.0001f?e.Vector.normalized:Vector3.forward;
        }
        public WeaponKind ContextWeapon(in CombatAction action)
        {
            var key=new PresentationActionKey(action);
            for(int i=0;i<contextIds.Length;i++) if(contextWeapons[i]!=WeaponKind.None && contextIds[i].Equals(key)) return contextWeapons[i];
            return WeaponKind.None;
        }
        Vector3 ContextDirection(in CombatAction action)
        {
            var key=new PresentationActionKey(action);
            for(int i=0;i<contextIds.Length;i++) if(contextWeapons[i]!=WeaponKind.None && contextIds[i].Equals(key)) return contextDirections[i];
            return Vector3.up;
        }
        public static void ShotSegment(in CombatEvent shot,IReadOnlyList<CombatEvent> events,Vector3? muzzle,out Vector3 start,out Vector3 end,int pellet=-1)
        {
            Vector3 direction=shot.Vector.sqrMagnitude>.0001f?shot.Vector.normalized:Vector3.forward;
            if(WeaponCatalog.IsHitscan(shot.Weapon) && CombatRules.Direction(shot.Vector) && shot.Amount>=0 && shot.Amount<=45) direction=WeaponCatalog.ShotDirection(shot,pellet<0?0:pellet);
            float range=WeaponCatalog.Get(shot.Weapon).Range;
            end=shot.Position+direction*range;
            if(Physics.Raycast(shot.Position,direction,out var wall,range,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) end=wall.point;
            var identity=new PresentationActionKey(shot.Action);
            if(events!=null) for(int i=0;i<events.Count;i++)
            {
                var hit=events[i];
                if(hit.Kind==CombatEventKind.Hit && (pellet<0 || hit.SubIndex==pellet) && identity.Equals(new PresentationActionKey(hit.Action)) && hit.Tick==shot.Tick &&
                    Vector3.Dot(hit.Position-shot.Position,direction)>=0 && (hit.Position-shot.Position).sqrMagnitude<(end-shot.Position).sqrMagnitude) end=hit.Position;
            }
            start=muzzle??shot.Position;
            if(Vector3.Dot(end-start,direction)<=0 || Physics.CheckSphere(start,.025f,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) start=shot.Position;
            Vector3 segment=end-start;
            if(segment.sqrMagnitude>.0001f && Physics.Raycast(start,segment.normalized,out wall,segment.magnitude,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) end=wall.point;
        }
        void Sparks(Vector3 center,Vector3 forward,Material material,float reach,int count,float life)
        {
            forward=forward.sqrMagnitude>.0001f?forward.normalized:Vector3.up;
            Vector3 side=Vector3.Cross(forward,Mathf.Abs(forward.y)<.8f?Vector3.up:Vector3.right).normalized;
            Vector3 up=Vector3.Cross(forward,side);
            for(int i=0;i<count;i++)
            {
                float angle=i*2*Mathf.PI/count+.4f;
                Vector3 ray=(forward*.6f+side*Mathf.Cos(angle)+up*Mathf.Sin(angle)).normalized;
                Segment(center+ray*.06f,center+ray*reach,material,.035f,life);
            }
        }
        // A crescent, full width mid-swing and pointed at both tips, so the swing reads as a slash rather than a wire.
        static readonly AnimationCurve Crescent=new AnimationCurve(new Keyframe(0,0),new Keyframe(.5f,1),new Keyframe(1,0));
        void Arc(Vector3 eye,Vector3 direction,float radius,Material material,float width,float life)
        {
            var line=Burst(material,width,life); line.positionCount=17; line.widthCurve=Crescent;
            direction=direction.sqrMagnitude>.001f?direction.normalized:Vector3.forward;
            Quaternion rotation=Quaternion.LookRotation(direction,Mathf.Abs(direction.y)<.95f?Vector3.up:Vector3.right);
            for(int i=0;i<17;i++)
            {
                float angle=Mathf.Lerp(-65,65,i/16f)*Mathf.Deg2Rad;
                line.SetPosition(i,eye+rotation*new Vector3(Mathf.Sin(angle)*radius,-.22f+Mathf.Sin(angle)*.24f,Mathf.Cos(angle)*radius));
            }
        }
        void Cross(Vector3 center,Material material,float size)
        {
            Segment(center+new Vector3(-size,0,0),center+new Vector3(size,0,0),material,.04f,.15f);
            Segment(center+new Vector3(0,-size,0),center+new Vector3(0,size,0),material,.04f,.15f);
        }
        void Ring(Vector3 center,Vector3 normal,float radius,Material material,float life)
        {
            var line=Burst(material,.06f,life); line.loop=true; line.positionCount=24;
            int slot=(burstIndex-1)%BurstCapacity;
            burstCenters[slot]=center; burstNormals[slot]=normal; burstRadii[slot]=radius; expanding[slot]=true;
            var rotation=Quaternion.FromToRotation(Vector3.up,normal);
            for(int i=0;i<24;i++)
            {
                float angle=2*Mathf.PI*i/24;
                line.SetPosition(i,center+rotation*new Vector3(Mathf.Cos(angle)*radius*.35f,0,Mathf.Sin(angle)*radius*.35f));
            }
        }
        Material MaterialFor(WeaponKind weapon)
        {
            switch(weapon)
            {
                case WeaponKind.PulseLobber: return catalog.Pulse;
                case WeaponKind.ReboundDisc: return catalog.Disc;
                case WeaponKind.StunGun: return catalog.Stun;
                case WeaponKind.Grenade: return catalog.Danger;
                case WeaponKind.PushLens: return catalog.Push;
                case WeaponKind.LaserSniper: return brightCore;
                default: return catalog.Beam;
            }
        }
        public void Present(ArenaMatch match,bool reducedMotion,int? captor = null,bool? contested = null)
        {
            for(int i=0;i<ProjectileCapacity;i++) { projectileTransforms[i].gameObject.SetActive(false); trails[i].enabled=false; fuses[i].enabled=false; }
            int count=Math.Min(ProjectileCapacity,match.Projectiles.Count);
            for(int i=0;i<count;i++)
            {
                var shot=match.Projectiles[i]; var node=projectileTransforms[i];
                node.gameObject.SetActive(true);
                node.position=shot.Position;
                Vector3 direction=shot.Velocity.sqrMagnitude>.001f?shot.Velocity.normalized:Vector3.forward;
                node.rotation=Quaternion.LookRotation(direction,Mathf.Abs(direction.y)<.95f?Vector3.up:Vector3.right);
                if(!reducedMotion) node.rotation*=Quaternion.Euler(0,shot.Weapon==WeaponKind.ReboundDisc?Time.unscaledTime*540:0,shot.Weapon==WeaponKind.PulseLobber?Time.unscaledTime*180:0);
                node.localScale=shot.Weapon==WeaponKind.ReboundDisc?new Vector3(.28f,.07f,.28f):
                    shot.Weapon==WeaponKind.StunGun?new Vector3(.10f,.10f,.24f):Vector3.one*(shot.Weapon==WeaponKind.Grenade?.18f:.22f);
                projectileRenderers[i].sharedMaterial=MaterialFor(shot.Weapon);
                var identity=new PresentationActionKey(shot.Action);
                if(!trailIds[i].Equals(identity) || (shot.Position-trailEnds[i]).sqrMagnitude>=4) trailCounts[i]=0;
                int offset=i*4;
                if(trailCounts[i]==0 || (shot.Position-trailEnds[i]).sqrMagnitude>.000001f)
                {
                    int samples=Math.Min(4,trailCounts[i]+1);
                    for(int j=samples-1;j>0;j--) trailPoints[offset+j]=trailPoints[offset+j-1];
                    trailPoints[offset]=shot.Position; trailCounts[i]=samples;
                }
                if(!reducedMotion && trailCounts[i]>1)
                {
                    var trail=trails[i]; trail.sharedMaterial=MaterialFor(shot.Weapon);
                    trail.widthMultiplier=shot.Weapon==WeaponKind.PulseLobber?.075f:shot.Weapon==WeaponKind.ReboundDisc?.055f:.025f;
                    trail.positionCount=trailCounts[i];
                    for(int j=0;j<trailCounts[i];j++) trail.SetPosition(j,trailPoints[offset+j]);
                    trail.enabled=true;
                }
                trailIds[i]=identity; trailEnds[i]=shot.Position;
                if(shot.Weapon==WeaponKind.Grenade)
                {
                    float remaining=FuseRemaining(shot,match.Tick);
                    // Blink rate rises from 2 to 12 Hz as the fuse burns; the ring marks the blast radius on the floor.
                    float blink=reducedMotion || Mathf.Sin(Time.unscaledTime*Mathf.Lerp(12,75,1-remaining))>0?1:0;
                    var fuse=fuses[i]; fuse.enabled=true; fuse.sharedMaterial=catalog.Danger; fuse.loop=true; fuse.positionCount=16;
                    fuse.widthMultiplier=(.03f+(1-remaining)*.03f)*(.45f+.55f*blink);
                    float radius=WeaponCatalog.Get(WeaponKind.Grenade).Radius;
                    var floor=shot.Position;
                    if(Physics.Raycast(shot.Position+Vector3.up*.2f,Vector3.down,out var hitFloor,radius,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) floor=hitFloor.point;
                    for(int j=0;j<16;j++)
                    {
                        float angle=2*Mathf.PI*j/16;
                        fuse.SetPosition(j,floor+new Vector3(Mathf.Cos(angle)*radius,.05f,Mathf.Sin(angle)*radius));
                    }
                    if(blink>.5f && !reducedMotion) vfx.Flash(shot.Position,new Color(1.8f,.55f,.2f,1),.35f+(1-remaining)*.25f,.03f);
                }
                else
                {
                    var halo=fuses[i]; halo.enabled=true; halo.sharedMaterial=MaterialFor(shot.Weapon);
                    halo.loop=shot.Weapon!=WeaponKind.StunGun; halo.positionCount=shot.Weapon==WeaponKind.StunGun?7:16;
                    halo.widthMultiplier=shot.Weapon==WeaponKind.ReboundDisc?.045f:.025f;
                    float spin=reducedMotion?0:Time.unscaledTime*8;
                    Quaternion rotation=Quaternion.FromToRotation(Vector3.up,shot.Weapon==WeaponKind.ReboundDisc?Vector3.up:direction);
                    for(int j=0;j<halo.positionCount;j++)
                    {
                        float angle=j*2*Mathf.PI/halo.positionCount+spin;
                        Vector3 point=shot.Weapon==WeaponKind.StunGun?
                            node.rotation*new Vector3(j%2==0?-.06f:.06f,0,-j*.06f):
                            rotation*new Vector3(Mathf.Cos(angle)*.22f,0,Mathf.Sin(angle)*.22f);
                        halo.SetPosition(j,shot.Position+point);
                    }
                }
            }
            for(int i=count;i<ProjectileCapacity;i++) trailCounts[i]=0;
            for(int i=0;i<pickupModels.Count;i++)
            {
                var pickup=match.Pickups[i]; var model=pickupModels[i];
                bool ready=match.Tick>=pickup.AvailableTick;
                PresentPad(i,pickup.Position,ready?1:1-Mathf.Clamp01((pickup.AvailableTick-match.Tick)/(WeaponCatalog.PickupSeconds*match.TickRate)),reducedMotion);
                model.SetActive(ready);
                if(!model.activeSelf) continue;
                model.transform.position=pickup.Position+Vector3.up*(reducedMotion?.13f:.13f+Mathf.Sin(Time.unscaledTime*2+i)*.055f);
                if(!reducedMotion) model.transform.rotation=Quaternion.Euler(0,Time.unscaledTime*42+i*37,0);
            }
            for(int i=0;i<shields.Length;i++)
            {
                shields[i].enabled=false; stuns[i].enabled=false; immunities[i].enabled=false;
                if(shells[i]!=null) shells[i].gameObject.SetActive(false);
            }
            for(int playerIndex=0;playerIndex<match.Players.Count && playerIndex<shields.Length;playerIndex++)
            {
                var player=match.Players[playerIndex];
                if(!player.Alive) continue;
                var shell=shells[playerIndex];
                float pop=(Time.unscaledTime-shellBreaks[playerIndex])/ShellBreakSeconds;
                bool breaking=player.Shield<=0 && shellBreaks[playerIndex]>=0 && pop>=0 && pop<1;
                if(shell!=null && (player.Shield>0 || breaking))
                {
                    // Sized to the authoritative capsule, so a sliding player's shell crouches with it.
                    float height=Mathf.Max(.9f,player.CapsuleHeight)+.25f, swell=breaking?1+.35f*pop:1;
                    shell.gameObject.SetActive(true);
                    shell.SetPositionAndRotation(player.Feet+Vector3.up*(height*.5f-.05f),Quaternion.identity);
                    shell.localScale=new Vector3(1.15f*swell/shellMeshSize.x,height*swell/shellMeshSize.y,1.15f*swell/shellMeshSize.z);
                    shellBlock??=new MaterialPropertyBlock();
                    shellBlock.SetFloat("_Strength",breaking?2*(1-pop):.45f+.55f*Mathf.Clamp01(player.Shield/WeaponCatalog.Shield));
                    shellBlock.SetFloat("_Clock",reducedMotion?0:Time.unscaledTime);
                    shellRenderers[playerIndex].SetPropertyBlock(shellBlock);
                }
                else if(player.Shield>0)
                {
                    var line=shields[playerIndex]; line.enabled=true; line.loop=true; line.positionCount=12;
                    for(int i=0;i<12;i++)
                    {
                        float angle=2*Mathf.PI*i/12;
                        line.SetPosition(i,player.Feet+new Vector3(Mathf.Cos(angle)*.45f,1.2f,Mathf.Sin(angle)*.45f));
                    }
                }
                if(match.Tick<player.StunUntil)
                {
                    // A crackling halo of arcs around the head, re-struck every 50 ms (held still with reduced motion).
                    var line=stuns[playerIndex]; line.enabled=true; line.loop=true; line.positionCount=StunPoints;
                    var eye=player.Eye+Vector3.up*.16f;
                    int strike=reducedMotion?0:Mathf.FloorToInt(Time.unscaledTime*20);
                    for(int i=0;i<StunPoints;i++)
                    {
                        float angle=2*Mathf.PI*i/StunPoints;
                        float a=Mathf.Repeat(Mathf.Sin((i+1)*12.9898f+(strike+playerIndex*7)*78.233f)*43758.5453f,1);
                        float b=Mathf.Repeat(Mathf.Sin((i+1)*39.346f+(strike+playerIndex*7)*11.135f)*24634.6345f,1);
                        float radius=.36f+(a-.5f)*.14f;
                        line.SetPosition(i,eye+new Vector3(Mathf.Cos(angle)*radius,(b-.5f)*.22f,Mathf.Sin(angle)*radius));
                    }
                }
                if(match.Tick<player.ImmunityUntil)
                {
                    var line=immunities[playerIndex]; line.enabled=true; line.loop=true; line.positionCount=12;
                    for(int i=0;i<12;i++)
                    {
                        float angle=2*Mathf.PI*i/12;
                        line.SetPosition(i,player.Feet+new Vector3(Mathf.Cos(angle)*.52f,.08f,Mathf.Sin(angle)*.52f));
                    }
                }
            }
            PresentZone(match,reducedMotion,captor??match.Captor,contested??match.Contested);
        }
        void PresentZone(ArenaMatch match,bool reducedMotion,int captor,bool contested)
        {
            var center=match.ZonePosition;
            if(Physics.Raycast(center+Vector3.up*2,Vector3.down,out var ground,5,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore))
                center.y=ground.point.y;
            var travel=center-lastZone; travel.y=0;
            bool moving=travel.sqrMagnitude>.000001f && travel.sqrMagnitude<1;
            lastZone=center;
            var state=contested?catalog.ZoneContested:captor>=0?catalog.ZoneOwned:catalog.ZoneIdle;
            var color=Tint(WeaponKind.None,state);
            float now=Time.unscaledTime, clock=reducedMotion?0:now;
            float diameter=MovingZone.Radius*2;
            zoneDecal.gameObject.SetActive(true);
            zoneDecal.SetPositionAndRotation(center+Vector3.up*.2f,Quaternion.identity);
            zoneDecal.localScale=new Vector3(diameter,MovingZone.FloorTolerance*2+.6f,diameter);
            zoneDecalMaterial.SetColor("_BaseColor",color*.9f);
            zoneDecalMaterial.SetColor("_RimColor",color*1.3f);
            zoneDecalMaterial.SetFloat("_Clock",clock);
            zoneDecalMaterial.SetFloat("_Pulse",contested && !reducedMotion?1:0);
            if(moving) zoneDecalMaterial.SetVector("_Travel",new Vector4(travel.normalized.x,travel.normalized.z,1,0));
            zoneCurtain.gameObject.SetActive(true);
            zoneCurtain.SetPositionAndRotation(center,Quaternion.identity);
            zoneCurtain.localScale=new Vector3(MovingZone.Radius,contested?2.6f:1.8f,MovingZone.Radius);
            zoneCurtainMaterial.SetColor("_BaseColor",color*(contested?.55f+.25f*Mathf.Sin(clock*12):.4f));
            zoneCurtainMaterial.SetVector("_NoiseParams",new Vector4(.6f,6,1,clock*.08f));
            zoneBeacon.gameObject.SetActive(true);
            zoneBeacon.SetPositionAndRotation(center,Quaternion.identity);
            zoneBeacon.localScale=new Vector3(.22f,60,.22f);
            zoneBeaconMaterial.SetColor("_BaseColor",color*.5f);
            zoneBeaconMaterial.SetVector("_NoiseParams",new Vector4(0,1,1,0));
            if(!reducedMotion)
            {
                moteClock+=Mathf.Min(.1f,Time.unscaledDeltaTime);
                for(;moteClock>.08f;moteClock-=.08f)
                {
                    float angle=Mathf.Repeat(now*2.39f+moteClock*97,Mathf.PI*2), r=MovingZone.Radius*Mathf.Sqrt(Mathf.Repeat(now*.731f+moteClock*13,1));
                    vfx.Mote(center+new Vector3(Mathf.Cos(angle)*r,.1f,Mathf.Sin(angle)*r),color*.8f);
                }
            }
        }
        public static float FuseRemaining(ArenaProjectile shot,int tick)
        {
            int duration=Math.Max(1,shot.ExpireTick-shot.Action.Tick);
            return Math.Max(0,Math.Min(1,(shot.ExpireTick-tick)/(float)duration));
        }
        public void ResetPresentation()
        {
            vfx?.Clear(); movement.Clear();
            for(int i=0;i<BurstCapacity;i++) if(bursts[i]!=null) bursts[i].enabled=false;
            for(int i=0;i<ProjectileCapacity;i++) { trailCounts[i]=0; if(trails[i]!=null) trails[i].enabled=false; }
            Array.Clear(contextWeapons,0,contextWeapons.Length); contextIndex=0;
        }
        void Update()
        {
            AnimateTransient(Time.unscaledTime);
            vfx?.Animate(Time.unscaledTime);
        }
        public void AnimateTransient(float now)
        {
            for(int i=0;i<BurstCapacity;i++)
            {
                var line=bursts[i];
                if(line==null || !line.enabled) continue;
                if(now>=burstEnds[i]) { line.enabled=false; continue; }
                float age=Mathf.Clamp01((now-burstStarts[i])/Mathf.Max(.001f,burstEnds[i]-burstStarts[i]));
                line.widthMultiplier=burstWidths[i]*(1-age)*(1-age);
                if(!expanding[i]) continue;
                Quaternion rotation=Quaternion.FromToRotation(Vector3.up,burstNormals[i]);
                float radius=burstRadii[i]*Mathf.Lerp(.35f,1.35f,age);
                for(int j=0;j<24;j++)
                {
                    float angle=j*2*Mathf.PI/24;
                    line.SetPosition(j,burstCenters[i]+rotation*new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
                }
            }
        }
        // progress 1 = ready; below 1 the ring is a dim arc that grows back as the respawn runs out.
        void PresentPad(int index,Vector3 position,float progress,bool reducedMotion)
        {
            var center=new Vector3(position.x,position.y-.45f+.03f,position.z);
            var ring=pickupRings[index]; var track=pickupTracks[index];
            bool ready=progress>=1;
            int count=ready?PickupRingPoints:Mathf.Max(2,Mathf.RoundToInt(progress*PickupRingPoints)+1);
            track.enabled=true; ring.enabled=progress>0;
            ring.loop=ready; ring.positionCount=count;
            for(int j=0;j<PickupRingPoints;j++)
            {
                // Clockwise from the far side, so the refill reads like a clock hand.
                float angle=Mathf.PI*.5f-2*Mathf.PI*(ready?j/(float)PickupRingPoints:progress*j/(count-1f));
                var point=center+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*PickupRingRadius;
                track.SetPosition(j,center+new Vector3(Mathf.Cos(Mathf.PI*.5f-2*Mathf.PI*j/PickupRingPoints),0,Mathf.Sin(Mathf.PI*.5f-2*Mathf.PI*j/PickupRingPoints))*PickupRingRadius);
                if(j<count) ring.SetPosition(j,point);
            }
            float glow=ready?(reducedMotion?1:.85f+.15f*Mathf.Sin(Time.unscaledTime*3+index)):.35f;
            ring.startColor=ring.endColor=new Color(glow,glow,glow,1);
        }
        static Mesh CreateOctahedron()
        {
            var mesh=new Mesh { name="Projectile core" };
            mesh.vertices=new[] { Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back };
            mesh.triangles=new[] { 0,4,3, 0,2,4, 0,5,2, 0,3,5, 1,3,4, 1,4,2, 1,2,5, 1,5,3 };
            mesh.RecalculateNormals(); return mesh;
        }
        void OnDestroy()
        {
            if(octahedron!=null) Destroy(octahedron);
            if(curtainMesh!=null) Destroy(curtainMesh);
            foreach(var material in new[] { zoneDecalMaterial,zoneCurtainMaterial,zoneBeaconMaterial }) if(material!=null) Destroy(material);
            if(brightCore!=null) Destroy(brightCore);
            if(sheath!=null) Destroy(sheath);
            if(stunPaint!=null) Destroy(stunPaint);
            foreach(var material in pickupMaterials) if(material!=null) Destroy(material);
        }
    }
}
