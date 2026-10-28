using System;
using System.Collections.Generic;
using UnityEngine;
using Wildstone.Maps;
using Wildstone.Movement;

namespace Wildstone.Combat
{
    // Native present-time queries for explicit offline Practice. This is not Fusion rewind.
    public sealed class UnityCombatWorld : ICombatWorld
    {
        readonly ArenaDefinition map;
        readonly Dictionary<int,ArenaMotor> motors=new Dictionary<int,ArenaMotor>();
        readonly Dictionary<Collider,int> owners=new Dictionary<Collider,int>();
        readonly Dictionary<int,Func<bool>> targetActive=new Dictionary<int,Func<bool>>();
        readonly RaycastHit[] hits=new RaycastHit[128];
        readonly Collider[] overlaps=new Collider[128];
        const int Mask=MotorTuning.WorldMask|(1<<MotorTuning.PlayerLayer);
        public UnityCombatWorld(ArenaDefinition definition) { map=definition??throw new ArgumentNullException(nameof(definition)); }
        public void Bind(int participant,ArenaMotor motor,Func<bool> isAlive=null)
        {
            if(participant<1 || participant>8 || motor==null) throw new ArgumentException("Invalid combat avatar binding.");
            Unbind(participant); motors.Add(participant,motor);
            if(isAlive!=null) targetActive.Add(participant,isAlive);
            foreach(var collider in motor.GetComponentsInChildren<Collider>()) owners[collider]=participant;
        }
        public void Unbind(int participant)
        {
            motors.Remove(participant);
            targetActive.Remove(participant);
            var old=new List<Collider>(); foreach(var pair in owners) if(pair.Value==participant) old.Add(pair.Key);
            foreach(var key in old) owners.Remove(key);
        }
        bool Ignored(Collider collider,int owner,int mask,out int participant)
        {
            owners.TryGetValue(collider,out participant);
            return participant!=0 && (participant==owner || (mask&(1<<participant))!=0 ||
                (targetActive.TryGetValue(participant,out var active) && !active()));
        }
        public bool Sweep(Vector3 origin,Vector3 displacement,float radius,int owner,int ignoredMask,out CombatHit hit)
        {
            hit=default; float distance=displacement.magnitude;
            if(distance<.000001f) return false;
            Vector3 direction=displacement/distance; bool found=false; float nearest=float.MaxValue; int nearestParticipant=int.MaxValue;
            int count=Physics.OverlapSphereNonAlloc(origin,Math.Max(.001f,radius),overlaps,Mask,QueryTriggerInteraction.Ignore);
            if(count==overlaps.Length) throw new InvalidOperationException("Combat overlap capacity exceeded.");
            for(int i=0;i<count;i++)
            {
                if(Ignored(overlaps[i],owner,ignoredMask,out int participant)) continue;
                if(participant<nearestParticipant)
                { nearest=0; nearestParticipant=participant; found=true; hit=new CombatHit(participant,origin,-direction,0); }
            }
            count=radius>0?Physics.SphereCastNonAlloc(origin,radius,direction,hits,distance,Mask,QueryTriggerInteraction.Ignore):
                Physics.RaycastNonAlloc(origin,direction,hits,distance,Mask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length) throw new InvalidOperationException("Combat sweep capacity exceeded.");
            for(int i=0;i<count;i++)
            {
                var candidate=hits[i];
                if(Ignored(candidate.collider,owner,ignoredMask,out int participant)) continue;
                if(candidate.distance<nearest || (candidate.distance==nearest && participant<nearestParticipant))
                { found=true; nearest=candidate.distance; nearestParticipant=participant; hit=new CombatHit(participant,candidate.point,candidate.normal,candidate.distance); }
            }
            return found;
        }
        public bool Visible(Vector3 from,Vector3 to)
        {
            var delta=to-from;
            return delta.sqrMagnitude<.000001f || !Physics.Raycast(from,delta.normalized,delta.magnitude,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore);
        }
        public bool TrySupport(Vector3 feet,out Vector3 point,out Vector3 normal)
        {
            if(Physics.Raycast(feet+Vector3.up*.2f,Vector3.down,out var hit,.45f,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore) && hit.normal.y>=.7071067f)
            { point=hit.point; normal=hit.normal; return true; }
            point=feet; normal=Vector3.up; return false;
        }
        public bool TryBlink(CombatState player,Vector3 direction,float distance,out Vector3 feet)
        {
            feet=player.Feet;
            if(!motors.TryGetValue(player.Participant,out var motor) || !CombatRules.Direction(direction)) return false;
            var controller=motor.GetComponent<CharacterController>();
            float radius=controller.radius, span=Math.Max(0,controller.height*.5f-radius);
            Vector3 center=player.Feet+controller.center, upper=center+Vector3.up*span, lower=center-Vector3.up*span;
            if(Physics.CapsuleCast(upper,lower,radius,direction,out var wall,distance,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore))
                distance=Math.Max(0,wall.distance-.06f);
            if(distance<.2f) return false;
            Vector3 destination=player.Feet+direction*distance;
            // Search back along the swept path for the furthest supported landing, never through an obstruction.
            for(float travel=distance;travel>=.2f;travel-=.1f)
            {
                destination=player.Feet+direction*travel;
                if(!Physics.Raycast(destination+Vector3.up*.2f,Vector3.down,out var ground,.5f,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore) || ground.normal.y<.7071067f) continue;
                destination.y=ground.point.y+.02f;
                if(!map.Contains(destination)) continue;
                Vector3 offset=destination-player.Feet;
                if(offset.sqrMagnitude>36.01f || Physics.CheckCapsule(upper+offset,lower+offset,radius*.95f,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) continue;
                // The final floor adjustment also needs a clear capsule sweep.
                float finalDistance=offset.magnitude;
                if(finalDistance<.2f) continue;
                if(Physics.CapsuleCast(upper,lower,radius*.95f,offset/finalDistance,out _,Math.Max(0,finalDistance-.04f),MotorTuning.WorldMask,QueryTriggerInteraction.Ignore)) continue;
                feet=destination; return true;
            }
            return false;
        }
    }
}
