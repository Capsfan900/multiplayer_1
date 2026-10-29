using System;
using UnityEngine;

namespace Wildstone.Movement
{
    [Serializable] public struct LaunchOrbState { public int ReadyTick, ActionSequence; }

    [RequireComponent(typeof(SphereCollider))]
    public sealed class MotorLaunchOrb : MotorSurface
    {
        public const float LaunchSpeed=11;
        public const int RespawnTicks=300;
        LaunchOrbState state;
        void Awake()
        {
            gameObject.layer=8;
            var trigger=GetComponent<SphereCollider>(); trigger.isTrigger=true; trigger.radius=1.1f;
        }
        public LaunchOrbState CaptureState() => state;
        public void RestoreState(in LaunchOrbState saved)
        {
            if(saved.ReadyTick<0 || saved.ActionSequence<0) throw new ArgumentException("Invalid orb snapshot",nameof(saved));
            state=saved;
        }
        // The simulation owner restores orb states alongside player states before replay.
        public bool TryConsume(int tick,out int action)
        {
            action=0;
            if(Id<=0 || tick<state.ReadyTick || tick<0 || tick>int.MaxValue-RespawnTicks || state.ActionSequence==int.MaxValue) return false;
            state.ReadyTick=tick+RespawnTicks; action=++state.ActionSequence; return true;
        }
    }
}
