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
        readonly MeshRenderer[] projectileRenderers=new MeshRenderer[ProjectileCapacity];
        readonly Transform[] projectileTransforms=new Transform[ProjectileCapacity];
        readonly LineRenderer[] trails=new LineRenderer[ProjectileCapacity];
        readonly PresentationActionKey[] trailIds=new PresentationActionKey[ProjectileCapacity];
        readonly Vector3[] trailEnds=new Vector3[ProjectileCapacity];
        readonly LineRenderer[] fuses=new LineRenderer[ProjectileCapacity];
        readonly List<GameObject> pickupModels=new List<GameObject>();
        readonly LineRenderer[] shields=new LineRenderer[8];
        readonly LineRenderer[] stuns=new LineRenderer[8];
        readonly LineRenderer[] immunities=new LineRenderer[8];
        LineRenderer zone;
        int burstIndex;
        PresentationCatalog catalog;
        Mesh octahedron;
        public void Initialize(PresentationCatalog assets,ArenaMatch match)
        {
            catalog=assets;
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
            burstEnds[index]=Time.realtimeSinceStartup+seconds;
            return line;
        }
        public void Play(in CombatEvent e,ArenaMatch match,bool reducedMotion,Vector3 departure)
        {
            Vector3 position=e.Position;
            if(e.Kind==CombatEventKind.Stunned || e.Kind==CombatEventKind.ShieldBroken || e.Kind==CombatEventKind.ShieldActivated || e.Kind==CombatEventKind.ShieldExpired)
            {
                var target=match.Find(e.Target);
                if(target!=null) position=target.Feet;
            }
            switch(e.Kind)
            {
                case CombatEventKind.Shot:
                    if(e.Weapon==WeaponKind.LineDriver)
                    {
                        Vector3 end=e.Position+e.Vector*WeaponCatalog.Get(e.Weapon).Range;
                        if(Physics.Raycast(e.Position,e.Vector,out var wall,WeaponCatalog.Get(e.Weapon).Range,Wildstone.Movement.MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) end=wall.point;
                        for(int i=0;i<match.Events.Count;i++)
                        {
                            var hit=match.Events[i];
                            if(hit.Kind==CombatEventKind.Hit && hit.Action.Participant==e.Action.Participant && hit.Action.Sequence==e.Action.Sequence && hit.Tick==e.Tick &&
                               (hit.Position-e.Position).sqrMagnitude<(end-e.Position).sqrMagnitude) end=hit.Position;
                        }
                        Segment(e.Position,end,catalog.Beam,.045f,.09f);
                    }
                    else if(e.Weapon==WeaponKind.PushLens) Ring(e.Position+e.Vector*2,Mathf.Abs(e.Vector.y)<.8f?Vector3.up:Vector3.forward,1.1f,catalog.Push,.10f);
                    else if(e.Weapon==WeaponKind.LaserSword) Segment(e.Position,e.Position+e.Vector*2.2f,catalog.Beam,.13f,.15f);
                    else Segment(e.Position,e.Position+e.Vector*.65f,MaterialFor(e.Weapon),.10f,.10f);
                    break;
                case CombatEventKind.Hit: Cross(e.Position,catalog.Danger,.22f); break;
                case CombatEventKind.Ricochet: Cross(e.Position,catalog.Disc,.22f); break;
                case CombatEventKind.Explosion: Ring(e.Position,Vector3.up,Mathf.Max(.5f,e.Amount),catalog.Danger,.24f); break;
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
                for(int i=0;i<BurstCapacity;i++) if(bursts[i].enabled) burstEnds[i]=Mathf.Min(burstEnds[i],Time.realtimeSinceStartup+.07f);
        }
        void Segment(Vector3 a,Vector3 b,Material material,float width,float life)
        {
            var line=Burst(material,width,life); line.positionCount=2; line.SetPosition(0,a); line.SetPosition(1,b);
        }
        void Cross(Vector3 center,Material material,float size)
        {
            Segment(center+new Vector3(-size,0,0),center+new Vector3(size,0,0),material,.04f,.15f);
            Segment(center+new Vector3(0,-size,0),center+new Vector3(0,size,0),material,.04f,.15f);
        }
        void Ring(Vector3 center,Vector3 normal,float radius,Material material,float life)
        {
            var line=Burst(material,.06f,life); line.loop=true; line.positionCount=24;
            var rotation=Quaternion.FromToRotation(Vector3.up,normal);
            for(int i=0;i<24;i++)
            {
                float angle=2*Mathf.PI*i/24;
                line.SetPosition(i,center+rotation*new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
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
                default: return catalog.Beam;
            }
        }
        public void Present(ArenaMatch match,bool reducedMotion)
        {
            for(int i=0;i<ProjectileCapacity;i++) { projectileTransforms[i].gameObject.SetActive(false); trails[i].enabled=false; fuses[i].enabled=false; }
            int count=Math.Min(ProjectileCapacity,match.Projectiles.Count);
            for(int i=0;i<count;i++)
            {
                var shot=match.Projectiles[i]; var node=projectileTransforms[i];
                node.gameObject.SetActive(true);
                node.position=shot.Position;
                node.localScale=shot.Weapon==WeaponKind.ReboundDisc?new Vector3(.28f,.07f,.28f):
                    shot.Weapon==WeaponKind.StunGun?new Vector3(.10f,.10f,.24f):Vector3.one*(shot.Weapon==WeaponKind.Grenade?.18f:.22f);
                projectileRenderers[i].sharedMaterial=MaterialFor(shot.Weapon);
                var identity=new PresentationActionKey(shot.Action);
                if(!reducedMotion && trailIds[i].Equals(identity) && (shot.Position-trailEnds[i]).sqrMagnitude<4)
                {
                    var trail=trails[i]; trail.sharedMaterial=MaterialFor(shot.Weapon);
                    trail.positionCount=2; trail.SetPosition(0,trailEnds[i]); trail.SetPosition(1,shot.Position); trail.enabled=true;
                }
                trailIds[i]=identity; trailEnds[i]=shot.Position;
                if(shot.Weapon==WeaponKind.Grenade)
                {
                    float remaining=FuseRemaining(shot,match.Tick);
                    var fuse=fuses[i]; fuse.enabled=true; fuse.loop=true; fuse.positionCount=16;
                    fuse.widthMultiplier=.035f+(1-remaining)*.055f;
                    float radius=.16f+(1-remaining)*.13f;
                    for(int j=0;j<16;j++)
                    {
                        float angle=2*Mathf.PI*j/16;
                        fuse.SetPosition(j,shot.Position+new Vector3(Mathf.Cos(angle)*radius,.03f,Mathf.Sin(angle)*radius));
                    }
                }
            }
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
            zone.sharedMaterial=match.Contested?catalog.ZoneContested:match.Captor>=0?catalog.ZoneOwned:catalog.ZoneIdle;
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
        void Update()
        {
            float now=Time.realtimeSinceStartup;
            for(int i=0;i<BurstCapacity;i++) if(bursts[i]!=null && bursts[i].enabled && now>=burstEnds[i]) bursts[i].enabled=false;
        }
        static Mesh CreateOctahedron()
        {
            var mesh=new Mesh { name="Projectile core" };
            mesh.vertices=new[] { Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back };
            mesh.triangles=new[] { 0,4,3, 0,2,4, 0,5,2, 0,3,5, 1,3,4, 1,4,2, 1,2,5, 1,5,3 };
            mesh.RecalculateNormals(); return mesh;
        }
        void OnDestroy() { if(octahedron!=null) Destroy(octahedron); }
    }
}
