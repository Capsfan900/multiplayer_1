using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Wildstone.Combat;
using Wildstone.Maps;
using Wildstone.Movement;

namespace Wildstone.Networking
{
    public readonly struct FusionHitscanRecord
    {
        public readonly CombatAction Action;
        public readonly int RunnerTick, ViewFrom, ViewTo, Target;
        public readonly float Alpha, RewindSeconds;
        public readonly bool Hit;
        public FusionHitscanRecord(CombatAction action,int now,int from,int to,float alpha,float rewind,bool hit,int target)
        { Action=action; RunnerTick=now; ViewFrom=from; ViewTo=to; Alpha=alpha; RewindSeconds=rewind; Hit=hit; Target=target; }
    }

    // Host-only historical hitscan; support, Blink and travelling collisions remain on the authoritative timeline.
    public sealed class FusionCombatWorld : ICombatWorld,IHitscanCombatWorld
    {
        public const float MaximumRewindSeconds=.150f;
        readonly NetworkRunner runner;
        readonly UnityCombatWorld present;
        readonly Dictionary<int,Binding> participants=new Dictionary<int,Binding>();
        readonly Dictionary<Hitbox,int> owners=new Dictionary<Hitbox,int>();
        readonly List<LagCompensatedHit> hits=new List<LagCompensatedHit>(64);
        public event Action<FusionHitscanRecord> ShotResolved;
        sealed class Binding
        {
            public PlayerRef Player;
            public Func<bool> Alive;
            public Hitbox[] Hitboxes;
            public Func<int> PoseEpoch;
            public int ObservedEpoch, EarliestTick;
        }
        public FusionCombatWorld(NetworkRunner runner,ArenaDefinition map)
        { this.runner=runner!=null?runner:throw new ArgumentNullException(nameof(runner)); present=new UnityCombatWorld(map); }
        public void Bind(int participant,PlayerRef player,ArenaMotor motor,HitboxRoot root,Func<bool> alive,Func<int> poseEpoch=null,bool recoveringTarget=false)
        {
            if(participant<1 || participant>8 || (player==PlayerRef.None && !recoveringTarget) || root==null || alive==null || motor==null)
                throw new ArgumentException("A verified player, live avatar and hitbox root are required.");
            var boxes=root.GetComponentsInChildren<Hitbox>(true);
            if(boxes.Length==0) throw new ArgumentException("The avatar requires a Fusion hitbox.");
            foreach(var box in boxes)
                if(owners.TryGetValue(box,out int previous) && previous!=participant)
                    throw new ArgumentException("Hitboxes cannot belong to multiple players.");
            Unbind(participant); present.Bind(participant,motor,alive);
            participants.Add(participant,new Binding {Player=player,Alive=alive,Hitboxes=boxes,
                PoseEpoch=poseEpoch,ObservedEpoch=poseEpoch?.Invoke()??0,EarliestTick=runner.Tick.Raw});
            foreach(var box in boxes)
            {
                owners.Add(box,participant);
            }
        }
        public void Unbind(int participant)
        {
            present.Unbind(participant);
            if(!participants.TryGetValue(participant,out var binding)) return;
            foreach(var box in binding.Hitboxes) owners.Remove(box);
            participants.Remove(participant);
        }
        public bool Sweep(Vector3 origin,Vector3 displacement,float radius,int owner,int ignoredMask,out CombatHit hit) =>
            present.Sweep(origin,displacement,radius,owner,ignoredMask,out hit);
        public bool Visible(Vector3 from,Vector3 to) => present.Visible(from,to);
        public bool TrySupport(Vector3 feet,out Vector3 point,out Vector3 normal) => present.TrySupport(feet,out point,out normal);
        public bool TryBlink(CombatState player,Vector3 direction,float distance,out Vector3 feet) => present.TryBlink(player,direction,distance,out feet);
        public void ObservePoseEpochs()
        {
            foreach(var binding in participants.Values)
            {
                int epoch=binding.PoseEpoch?.Invoke()??binding.ObservedEpoch;
                if(epoch==binding.ObservedEpoch) continue;
                binding.ObservedEpoch=epoch; binding.EarliestTick=runner.Tick.Raw;
            }
        }

        public bool Hitscan(CombatAction action,Vector3 origin,Vector3 displacement,int ignoredMask,out CombatHit hit)
        {
            hit=default;
            if(!runner.IsServer || !runner.IsRunning || runner.LagCompensation==null)
                throw new InvalidOperationException("Hitscan requires the active host lag compensation manager.");
            if(!participants.TryGetValue(action.Participant,out var shooter) || shooter.Player==PlayerRef.None || !shooter.Alive()) return false;
            float distance=displacement.magnitude;
            if(!CombatRules.Direction(displacement.normalized) || float.IsNaN(distance) || float.IsInfinity(distance) || distance<.000001f)
                return false;
            int now=runner.Tick.Raw;
            ObservePoseEpochs();
            runner.LagCompensation.GetPlayerTickAndAlpha(shooter.Player,out int? sdkFrom,out int? sdkTo,out float? sdkAlpha);
            BoundView(now,runner.DeltaTime,sdkFrom,sdkTo,sdkAlpha,out int from,out int to,out float alpha);
            var direction=displacement/distance;
            bool found=Physics.Raycast(origin,direction,out var wall,distance,MotorTuning.WorldMask,QueryTriggerInteraction.Ignore);
            float nearest=found?wall.distance:distance;
            if(found) hit=new CombatHit(0,wall.point,wall.normal,wall.distance);
            // Static geometry is queried separately so live avatar colliders cannot bypass historical hitboxes.
            runner.LagCompensation.RaycastAll(origin,direction,distance,from,to,alpha,hits,
                1<<MotorTuning.PlayerLayer,true,HitOptions.SubtickAccuracy,QueryTriggerInteraction.Ignore);
            foreach(var candidate in hits)
            {
                if(candidate.Hitbox==null || !owners.TryGetValue(candidate.Hitbox,out int participant) ||
                    participant==action.Participant || (ignoredMask&(1<<participant))!=0 || !participants[participant].Alive() ||
                    from<participants[participant].EarliestTick) continue;
                // At an exact cover tie the wall wins; equal player hits use stable participant order.
                if(candidate.Distance<nearest || (!found && candidate.Distance==nearest) ||
                    (found && hit.Participant!=0 && candidate.Distance==nearest && participant<hit.Participant))
                {
                    nearest=candidate.Distance; found=true;
                    hit=new CombatHit(participant,candidate.Point,candidate.Normal,candidate.Distance);
                }
            }
            ShotResolved?.Invoke(new FusionHitscanRecord(action,now,from,to,alpha,
                (now-(from+(to-from)*alpha))*runner.DeltaTime,found,found?hit.Participant:0));
            return found;
        }
        internal static void BoundView(int now,float deltaTime,int? sdkFrom,int? sdkTo,float? sdkAlpha,
            out int from,out int to,out float alpha)
        {
            if(deltaTime<=0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            int oldest=now-(int)Math.Floor(MaximumRewindSeconds/deltaTime+.00001f);
            from=Math.Max(oldest,Math.Min(now,sdkFrom??now));
            to=Math.Max(from,Math.Max(oldest,Math.Min(now,sdkTo??from)));
            alpha=sdkAlpha??0;
            if(float.IsNaN(alpha) || float.IsInfinity(alpha)) alpha=0;
            alpha=Math.Max(0,Math.Min(1,alpha));
        }
    }
}
