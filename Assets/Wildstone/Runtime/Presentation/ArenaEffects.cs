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
        readonly LineRenderer[] shields=new LineRenderer[8];
        readonly LineRenderer[] stuns=new LineRenderer[8];
        readonly LineRenderer[] immunities=new LineRenderer[8];
        LineRenderer zone;
        int burstIndex;
        PresentationCatalog catalog;
        Mesh octahedron;
        Material brightCore;
        public void Initialize(PresentationCatalog assets,ArenaMatch match)
        {
            catalog=assets;
            brightCore=new Material(assets.Beam) { name="Weapon bright core" };
            brightCore.SetColor("_BaseColor",new Color(.86f,1f,1f));
            octahedron=CreateOctahedron();
            for(int i=0;i<BurstCapacity;i++) bursts[i]=Line("Burst "+i,assets.Beam,.035f);
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
            for(int i=0;i<shields.Length;i++)
            {
                shields[i]=Line("Shield "+i,assets.Shield,.045f);
                stuns[i]=Line("Stun "+i,assets.Stun,.055f);
                immunities[i]=Line("Immunity "+i,assets.Blink,.04f);
            }
            zone=Line("Capture boundary",assets.ZoneIdle,.055f);
            zone.loop=true;
            zone.positionCount=32;
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
            line.loop=false;
            line.enabled=true;
            burstStarts[index]=Time.realtimeSinceStartup; burstEnds[index]=burstStarts[index]+seconds;
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
                    if(e.Weapon==WeaponKind.LineDriver)
                    {
                        ShotSegment(e,events,muzzle,out var start,out var end);
                        Segment(start,end,catalog.Beam,reducedMotion?.055f:.11f,.14f);
                        if(!reducedMotion)
                        {
                            Segment(start,end,brightCore,.028f,.10f);
                            Ring(start,e.Vector,.16f,catalog.Beam,.13f);
                            Ring(end,e.Vector,.18f,catalog.Beam,.16f);
                        }
                    }
                    else if(e.Weapon==WeaponKind.PushLens)
                    {
                        for(int i=0;i<(reducedMotion?1:3);i++) Ring(e.Position+e.Vector*(1+i*.8f),e.Vector,.35f+i*.35f,catalog.Push,.28f+i*.03f);
                        if(!reducedMotion) Sparks(e.Position+e.Vector*1.8f,-e.Vector,catalog.Push,.9f,3,.2f);
                    }
                    else if(e.Weapon==WeaponKind.LaserSword)
                    {
                        Arc(e.Position,e.Vector,2.1f,catalog.Beam,reducedMotion?.07f:.14f,.20f);
                        if(!reducedMotion) Arc(e.Position,e.Vector,1.7f,brightCore,.035f,.13f);
                    }
                    else
                    {
                        Vector3 origin=muzzle??e.Position;
                        Ring(origin,e.Vector,e.Weapon==WeaponKind.PulseLobber?.22f:.15f,MaterialFor(e.Weapon),.18f);
                        if(!reducedMotion) Sparks(origin,e.Vector,MaterialFor(e.Weapon),.55f,e.Weapon==WeaponKind.StunGun?4:3,.12f);
                    }
                    break;
                case CombatEventKind.Hit:
                    WeaponKind impact=ContextWeapon(e.Action);
                    Material impactMaterial=impact==WeaponKind.None?catalog.Danger:MaterialFor(impact);
                    Vector3 incoming=ContextDirection(e.Action);
                    Ring(e.Position,incoming,.24f,impactMaterial,.22f);
                    if(!reducedMotion) Sparks(e.Position,-incoming,impactMaterial,.65f,4,.20f);
                    break;
                case CombatEventKind.Ricochet:
                    Ring(e.Position,Vector3.up,.30f,catalog.Disc,.22f);
                    if(!reducedMotion) Sparks(e.Position,-ContextDirection(e.Action),catalog.Disc,.85f,4,.20f);
                    break;
                case CombatEventKind.Explosion:
                    float radius=Mathf.Clamp(Mathf.Max(.5f,e.Amount),.5f,5.5f);
                    Ring(e.Position,Vector3.up,radius,catalog.Danger,.38f);
                    if(!reducedMotion)
                    {
                        Ring(e.Position,Vector3.right,radius*.75f,catalog.Pulse,.32f);
                        Ring(e.Position,Vector3.forward,radius*.75f,catalog.Pulse,.32f);
                        Sparks(e.Position,Vector3.up,catalog.Danger,radius,3,.30f);
                    }
                    break;
                case CombatEventKind.Impulse: Ring(e.Position,Vector3.up,.8f,catalog.Push,.16f); break;
                case CombatEventKind.Stunned: Cross(position+Vector3.up*1.6f,catalog.Stun,.5f); break;
                case CombatEventKind.Blink:
                    Ring(departure+Vector3.up*.8f,Vector3.up,.55f,catalog.Blink,.16f);
                    Ring(e.Position+Vector3.up*.8f,Vector3.up,.8f,catalog.Blink,.20f);
                    break;
                case CombatEventKind.ShieldActivated: Ring(position+Vector3.up*1.2f,Vector3.up,.48f,catalog.Shield,.18f); break;
                case CombatEventKind.ShieldBroken: Ring(position+Vector3.up*.9f,Vector3.up,.8f,catalog.Shield,.18f); break;
                case CombatEventKind.ShieldExpired: Ring(position+Vector3.up*.9f,Vector3.up,.55f,catalog.Shield,.10f); break;
                case CombatEventKind.Death: Ring(e.Position+Vector3.up*.9f,Vector3.up,.7f,catalog.Danger,.22f); break;
            }
            if(reducedMotion)
                for(int i=0;i<BurstCapacity;i++) if(bursts[i].enabled)
                { burstEnds[i]=Mathf.Min(burstEnds[i],Time.realtimeSinceStartup+.07f); expanding[i]=false; }
        }
        void Segment(Vector3 a,Vector3 b,Material material,float width,float life)
        {
            var line=Burst(material,width,life); line.positionCount=2; line.SetPosition(0,a); line.SetPosition(1,b);
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
        public static void ShotSegment(in CombatEvent shot,IReadOnlyList<CombatEvent> events,Vector3? muzzle,out Vector3 start,out Vector3 end)
        {
            Vector3 direction=shot.Vector.sqrMagnitude>.0001f?shot.Vector.normalized:Vector3.forward;
            float range=WeaponCatalog.Get(shot.Weapon).Range;
            end=shot.Position+direction*range;
            if(Physics.Raycast(shot.Position,direction,out var wall,range,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) end=wall.point;
            var identity=new PresentationActionKey(shot.Action);
            if(events!=null) for(int i=0;i<events.Count;i++)
            {
                var hit=events[i];
                if(hit.Kind==CombatEventKind.Hit && identity.Equals(new PresentationActionKey(hit.Action)) && hit.Tick==shot.Tick &&
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
        void Arc(Vector3 eye,Vector3 direction,float radius,Material material,float width,float life)
        {
            var line=Burst(material,width,life); line.positionCount=17;
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
                if(!reducedMotion) node.rotation*=Quaternion.Euler(0,shot.Weapon==WeaponKind.ReboundDisc?Time.realtimeSinceStartup*540:0,shot.Weapon==WeaponKind.PulseLobber?Time.realtimeSinceStartup*180:0);
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
                    var fuse=fuses[i]; fuse.enabled=true; fuse.sharedMaterial=catalog.Danger; fuse.loop=true; fuse.positionCount=16;
                    fuse.widthMultiplier=.035f+(1-remaining)*.055f;
                    float radius=.16f+(1-remaining)*.13f;
                    for(int j=0;j<16;j++)
                    {
                        float angle=2*Mathf.PI*j/16;
                        fuse.SetPosition(j,shot.Position+new Vector3(Mathf.Cos(angle)*radius,.03f,Mathf.Sin(angle)*radius));
                    }
                }
                else
                {
                    var halo=fuses[i]; halo.enabled=true; halo.sharedMaterial=MaterialFor(shot.Weapon);
                    halo.loop=shot.Weapon!=WeaponKind.StunGun; halo.positionCount=shot.Weapon==WeaponKind.StunGun?7:16;
                    halo.widthMultiplier=shot.Weapon==WeaponKind.ReboundDisc?.045f:.025f;
                    float spin=reducedMotion?0:Time.realtimeSinceStartup*8;
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
                model.SetActive(match.Tick>=pickup.AvailableTick);
                if(!model.activeSelf) continue;
                model.transform.position=pickup.Position+Vector3.up*(reducedMotion?.13f:.13f+Mathf.Sin(Time.realtimeSinceStartup*2+i)*.055f);
                if(!reducedMotion) model.transform.rotation=Quaternion.Euler(0,Time.realtimeSinceStartup*42+i*37,0);
            }
            for(int i=0;i<shields.Length;i++) { shields[i].enabled=false; stuns[i].enabled=false; immunities[i].enabled=false; }
            for(int playerIndex=0;playerIndex<match.Players.Count && playerIndex<shields.Length;playerIndex++)
            {
                var player=match.Players[playerIndex];
                if(!player.Alive) continue;
                if(player.Shield>0)
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
                    var line=stuns[playerIndex]; line.enabled=true; line.loop=false; line.positionCount=4;
                    var eye=player.Eye;
                    line.SetPosition(0,eye+new Vector3(-.25f,.17f,0));
                    line.SetPosition(1,eye+new Vector3(-.08f,.42f,0));
                    line.SetPosition(2,eye+new Vector3(.08f,.17f,0));
                    line.SetPosition(3,eye+new Vector3(.25f,.42f,0));
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
            var pos=match.ZonePosition+Vector3.up*.06f;
            zone.sharedMaterial=(contested??match.Contested)?catalog.ZoneContested:(captor??match.Captor)>=0?catalog.ZoneOwned:catalog.ZoneIdle;
            zone.enabled=true;
            for(int i=0;i<32;i++)
            {
                float angle=2*Mathf.PI*i/32;
                var edge=pos+new Vector3(Mathf.Cos(angle)*MovingZone.Radius,0,Mathf.Sin(angle)*MovingZone.Radius);
                if(Physics.Raycast(edge+Vector3.up*2,Vector3.down,out var ground,4,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore))
                    edge.y=ground.point.y+.06f;
                zone.SetPosition(i,edge);
            }
        }
        public static float FuseRemaining(ArenaProjectile shot,int tick)
        {
            int duration=Math.Max(1,shot.ExpireTick-shot.Action.Tick);
            return Math.Max(0,Math.Min(1,(shot.ExpireTick-tick)/(float)duration));
        }
        public void ResetPresentation()
        {
            for(int i=0;i<BurstCapacity;i++) if(bursts[i]!=null) bursts[i].enabled=false;
            for(int i=0;i<ProjectileCapacity;i++) { trailCounts[i]=0; if(trails[i]!=null) trails[i].enabled=false; }
            Array.Clear(contextWeapons,0,contextWeapons.Length); contextIndex=0;
        }
        void Update()
        {
            AnimateTransient(Time.realtimeSinceStartup);
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
            if(brightCore!=null) Destroy(brightCore);
        }
    }
}
