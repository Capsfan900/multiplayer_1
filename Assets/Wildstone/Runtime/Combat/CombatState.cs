using System;
using UnityEngine;

namespace Wildstone.Combat
{
    public enum CombatActionKind { Fire, Reload, SelectStarter, SelectSpecial, Grenade, Blink, Shield, Pickup }
    public enum CombatEventKind { Accepted, Rejected, Shot, Hit, ShieldActivated, ShieldBroken, ShieldExpired, Stunned, Blink, Impulse, Pickup, Death, Respawn, Explosion, Ricochet }
    [Serializable] public struct CombatAction
    {
        // Participant must be supplied by the authenticated transport adapter, never copied from an RPC payload.
        public int Participant, ConnectionGeneration, LifeGeneration, AuthorityGeneration, Tick, Sequence;
        public CombatActionKind Kind;
        public Vector3 Direction;
        public int PickupId, PickupGeneration;
    }
    [Serializable] public sealed class CombatState
    {
        public int Participant, Team, ConnectionGeneration, LifeGeneration;
        public bool Connected=true, Alive=true, SelectedSpecial;
        public float Health=WeaponCatalog.Health, Shield, CapsuleHeight=1.8f;
        public int StarterAmmo=12, SpecialAmmo, GrenadeAmmo, BlinkCharges, ShieldCharges;
        public WeaponKind SpecialWeapon;
        public int LastActionSequence=-1, FireReadyTick, GrenadeReadyTick, ReloadUntil, ShieldUntil, StunUntil, ImmunityUntil, RespawnTick, DisconnectUntil;
        public Vector3 Feet, Aim=Vector3.forward, SupportPoint, SupportNormal=Vector3.up;
        public bool Supported;
        public int DiscontinuityTick=-1;
        public PendingAttack Pending;
        internal PendingAttack AttackStorage=new PendingAttack();
        public CombatState Copy() { var p=(CombatState)MemberwiseClone(); p.AttackStorage=Pending?.Copy()??new PendingAttack(); p.Pending=Pending==null?null:p.AttackStorage; return p; }
        public Vector3 Eye => Feet+Vector3.up*(CapsuleHeight-.15f);
        public bool Suppressed(int tick) => !Alive || !Connected || tick<StunUntil;
    }
    [Serializable] public sealed class PendingAttack
    {
        public CombatAction Action;
        public WeaponKind Weapon;
        public int ResolveTick, EndTick, HitMask, LastSampleTick=-1;
        public Vector3 Direction, PreviousTip;
        public PendingAttack Copy() => (PendingAttack)MemberwiseClone();
    }
    public readonly struct CombatEvent
    {
        public readonly CombatEventKind Kind;
        public readonly CombatAction Action;
        public readonly WeaponKind Weapon;
        public readonly int Tick, Target, TargetLife, SubIndex;
        public readonly Vector3 Position, Vector;
        public readonly float Amount;
        public readonly string Reason;
        public string Id => Action.AuthorityGeneration+":"+Action.Participant+":"+Action.LifeGeneration+":"+Action.Sequence+":"+Tick+":"+(int)Kind+":"+Target+":"+TargetLife+":"+SubIndex;
        public CombatEvent(CombatEventKind kind,CombatAction action,int tick,int target=0,int targetLife=0,Vector3 position=default,Vector3 vector=default,float amount=0,string reason=null,int subIndex=0,WeaponKind weapon=WeaponKind.None)
        { Kind=kind; Action=action; Weapon=weapon; Tick=tick; Target=target; TargetLife=targetLife; Position=position; Vector=vector; Amount=amount; Reason=reason; SubIndex=subIndex; }
    }
    public readonly struct CombatHit
    {
        public readonly int Participant;
        public readonly Vector3 Point, Normal;
        public readonly float Distance;
        public CombatHit(int participant,Vector3 point,Vector3 normal,float distance) { Participant=participant; Point=point; Normal=normal; Distance=distance; }
    }
    public interface ICombatWorld
    {
        bool Sweep(Vector3 origin,Vector3 displacement,float radius,int owner,int ignoredMask,out CombatHit hit);
        bool Visible(Vector3 from,Vector3 to);
        bool TryBlink(CombatState player,Vector3 direction,float distance,out Vector3 feet);
        bool TrySupport(Vector3 feet,out Vector3 point,out Vector3 normal);
    }
    // Explicitly empty test world. Practice uses UnityCombatWorld with native physics.
    public class EmptyCombatWorld : ICombatWorld
    {
        public virtual bool Sweep(Vector3 origin,Vector3 displacement,float radius,int owner,int ignoredMask,out CombatHit hit) { hit=default; return false; }
        public virtual bool Visible(Vector3 from,Vector3 to) => true;
        public virtual bool TryBlink(CombatState player,Vector3 direction,float distance,out Vector3 feet) { feet=player.Feet; return false; }
        public virtual bool TrySupport(Vector3 feet,out Vector3 point,out Vector3 normal) { point=feet; normal=Vector3.up; return true; }
    }
}
