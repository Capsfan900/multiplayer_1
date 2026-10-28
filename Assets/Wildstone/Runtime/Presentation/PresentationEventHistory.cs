using System;
using System.Collections.Generic;
using Wildstone.Combat;

namespace Wildstone.Presentation
{
    public readonly struct PresentationActionKey : IEquatable<PresentationActionKey>
    {
        readonly int authority,participant,life,sequence;
        public PresentationActionKey(in CombatAction action)
        { authority=action.AuthorityGeneration; participant=action.Participant; life=action.LifeGeneration; sequence=action.Sequence; }
        public bool Equals(PresentationActionKey other) => authority==other.authority && participant==other.participant && life==other.life && sequence==other.sequence;
        public override bool Equals(object other) => other is PresentationActionKey key && Equals(key);
        public override int GetHashCode()
        {
            unchecked { int hash=authority; hash=hash*31+participant; hash=hash*31+life; return hash*31+sequence; }
        }
    }
    public readonly struct PresentationEventKey : IEquatable<PresentationEventKey>
    {
        readonly PresentationActionKey action;
        readonly int tick,kind,target,targetLife,subIndex;
        public PresentationEventKey(in CombatEvent e)
        { action=new PresentationActionKey(e.Action); tick=e.Tick; kind=(int)e.Kind; target=e.Target; targetLife=e.TargetLife; subIndex=e.SubIndex; }
        public bool Equals(PresentationEventKey other) => action.Equals(other.action) && tick==other.tick && kind==other.kind && target==other.target && targetLife==other.targetLife && subIndex==other.subIndex;
        public override bool Equals(object other) => other is PresentationEventKey key && Equals(key);
        public override int GetHashCode()
        {
            unchecked { int hash=action.GetHashCode(); hash=hash*31+tick; hash=hash*31+kind; hash=hash*31+target; hash=hash*31+targetLife; return hash*31+subIndex; }
        }
    }
    // Eviction advances a conservative watermark: no event from that tick can replay.
    public sealed class PresentationEventHistory
    {
        const int Capacity=2048, WindowTicks=512;
        readonly HashSet<PresentationEventKey> seen=new HashSet<PresentationEventKey>();
        readonly Queue<(PresentationEventKey key,int tick)> order=new Queue<(PresentationEventKey,int)>();
        int latestTick=-1,rejectedThrough=-1;
        public bool Accept(in CombatEvent e)
        {
            if(e.Tick<0 || e.Tick<=rejectedThrough || e.Tick<latestTick-WindowTicks) return false;
            var key=new PresentationEventKey(e);
            if(!seen.Add(key)) return false;
            if(e.Tick>latestTick) latestTick=e.Tick;
            order.Enqueue((key,e.Tick));
            while(order.Count>Capacity)
            {
                var removed=order.Dequeue();
                seen.Remove(removed.key);
                rejectedThrough=Math.Max(rejectedThrough,removed.tick);
            }
            return true;
        }
    }
}
