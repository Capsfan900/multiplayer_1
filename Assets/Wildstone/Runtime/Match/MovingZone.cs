using System;
using UnityEngine;
namespace Wildstone.Match
{
    public sealed class MovingZone
    {
        readonly Vector3[] route;
        readonly float[] lengths;
        readonly float total;
        readonly int rate;
        public const float Speed=2, Radius=4, FloorTolerance=1.25f;
        public MovingZone(Vector3[] points,int tickRate)
        {
            if(points==null || points.Length<3 || tickRate<1) throw new ArgumentException("Zone needs a valid circuit and tick rate.");
            rate=tickRate; route=(Vector3[])points.Clone(); lengths=new float[route.Length];
            for(int i=0;i<route.Length;i++)
            {
                if(!Combat.CombatRules.Finite(route[i])) throw new ArgumentException("Nonfinite zone point.");
                lengths[i]=(route[(i+1)%route.Length]-route[i]).magnitude;
                if(lengths[i]<.01f) throw new ArgumentException("Zero length zone segment.");
                total+=lengths[i];
            }
        }
        public Vector3 Position(int elapsedTicks)
        {
            double distance=Math.Max(0,elapsedTicks)*(double)Speed/rate%total;
            for(int i=0;i<lengths.Length;i++)
            {
                if(distance<=lengths[i]) return route[i]+(route[(i+1)%route.Length]-route[i])*(float)(distance/lengths[i]);
                distance-=lengths[i];
            }
            return route[0];
        }
        public bool Contains(int elapsedTicks,Vector3 feet,bool supported,Vector3 floorPoint,Vector3 floorNormal)
        {
            if(!supported || !Combat.CombatRules.Finite(feet) || !Combat.CombatRules.Finite(floorPoint) || !Combat.CombatRules.Finite(floorNormal) || floorNormal.y<.707f) return false;
            var center=Position(elapsedTicks); var delta=feet-center;
            if(delta.x*delta.x+delta.z*delta.z>Radius*Radius || Math.Abs(feet.y-floorPoint.y)>.25f) return false;
            // Compare the support plane at the route center, so a ramp's slope is preserved.
            float floorAtCenter=floorPoint.y-(floorNormal.x*(center.x-floorPoint.x)+floorNormal.z*(center.z-floorPoint.z))/floorNormal.y;
            return Math.Abs(floorAtCenter-center.y)<=FloorTolerance;
        }
    }
}
