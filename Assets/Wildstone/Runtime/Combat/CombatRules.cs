using System;
using UnityEngine;

namespace Wildstone.Combat
{
    public static class CombatRules
    {
        public static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        public static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        public static bool Direction(Vector3 v) => Finite(v) && v.sqrMagnitude>=.99f && v.sqrMagnitude<=1.01f;
        public static float Damage(CombatState p,float amount)
        {
            if(!p.Alive || !Finite(amount) || amount<=0) return 0;
            float shield=Math.Min(p.Shield,amount); p.Shield-=shield; amount-=shield;
            float health=Math.Min(p.Health,amount); p.Health-=health; return shield+health;
        }
        public static bool Stun(CombatState p,int tick,int rate)
        {
            if(!p.Alive || tick<p.ImmunityUntil || tick<p.StunUntil) return false;
            p.StunUntil=tick+WeaponCatalog.Ticks(.45f,rate);
            p.ImmunityUntil=p.StunUntil+WeaponCatalog.Ticks(1.75f,rate); p.Pending=null; return true;
        }
        public static void ClearLife(CombatState p)
        {
            p.Shield=0; p.ShieldUntil=0; p.StunUntil=0; p.ImmunityUntil=0; p.ReloadUntil=0;
            p.SpecialWeapon=WeaponKind.None; p.SpecialAmmo=0; p.SelectedSpecial=false;
            p.GrenadeAmmo=0; p.BlinkCharges=0; p.ShieldCharges=0; p.Pending=null;
        }
        public static bool ValidPlayer(CombatState p)
        {
            if(p==null || p.Participant<1 || p.Participant>8 || p.Team<0 || p.Team>3 || p.ConnectionGeneration<1 || p.LifeGeneration<1 || p.LifeGeneration==int.MaxValue ||
                !Finite(p.Health) || p.Health<0 || p.Health>100 || !Finite(p.Shield) || p.Shield<0 || p.Shield>60 ||
                !Finite(p.CapsuleHeight) || p.CapsuleHeight<.8f || p.CapsuleHeight>1.8f ||
                p.StarterAmmo<0 || p.StarterAmmo>WeaponCatalog.StarterAmmo || p.GrenadeAmmo<0 || p.GrenadeAmmo>2 ||
                p.BlinkCharges<0 || p.BlinkCharges>1 || p.ShieldCharges<0 || p.ShieldCharges>1 ||
                !Finite(p.Feet) || !Direction(p.Aim) || !Finite(p.SupportPoint) || !Finite(p.SupportNormal) ||
                p.LastActionSequence< -1 || p.LastActionSequence>=int.MaxValue-2 || p.FireReadyTick<0 || p.GrenadeReadyTick<0 || p.ReloadUntil<0 || p.ShieldUntil<0 ||
                p.StunUntil<0 || p.ImmunityUntil<p.StunUntil || p.RespawnTick<0 || p.DisconnectUntil<0 ||
                !Enum.IsDefined(typeof(WeaponKind),p.SpecialWeapon) || p.SpecialWeapon==WeaponKind.LineDriver || p.SpecialWeapon==WeaponKind.Grenade) return false;
            int ammo=p.SpecialWeapon==WeaponKind.None?0:WeaponCatalog.Get(p.SpecialWeapon).Ammo;
            if(p.SpecialAmmo<0 || p.SpecialAmmo>ammo || (p.SelectedSpecial && p.SpecialWeapon==WeaponKind.None) ||
                (p.Alive && (p.Health<=0 || p.RespawnTick!=0)) || (!p.Alive && (p.Health!=0 || p.Shield!=0 || p.Pending!=null ||
                    p.SpecialWeapon!=WeaponKind.None || p.GrenadeAmmo!=0 || p.BlinkCharges!=0 || p.ShieldCharges!=0 || p.StunUntil!=0 || p.ImmunityUntil!=0 || p.ReloadUntil!=0))) return false;
            if(p.Pending!=null)
            {
                var a=p.Pending;
                if((a.Weapon!=WeaponKind.PushLens && a.Weapon!=WeaponKind.LaserSword) || a.Action.Participant!=p.Participant ||
                    a.Action.LifeGeneration!=p.LifeGeneration || a.Action.ConnectionGeneration<1 || a.Action.ConnectionGeneration>p.ConnectionGeneration || a.Action.Sequence<0 || a.Action.Sequence>p.LastActionSequence ||
                    a.Action.Kind!=CombatActionKind.Fire || a.ResolveTick<a.Action.Tick || a.EndTick<a.ResolveTick || !Direction(a.Direction) ||
                    !Direction(a.Action.Direction) || !Finite(a.PreviousTip) || (a.HitMask&~510)!=0) return false;
            }
            return true;
        }
    }
}
