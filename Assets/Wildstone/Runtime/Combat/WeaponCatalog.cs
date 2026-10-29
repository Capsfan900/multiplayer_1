using System;

namespace Wildstone.Combat
{
    public enum WeaponKind { None, LineDriver, PulseLobber, ReboundDisc, PushLens, Grenade, StunGun, LaserSword }
    public readonly struct WeaponSpec
    {
        public readonly float Damage, EdgeDamage, Range, Radius, Speed, Interval, Windup, Active, Lifetime;
        public readonly int Ammo;
        public WeaponSpec(float damage,float range,float interval,int ammo,float speed=0,float radius=0,float edge=0,float windup=0,float active=0,float lifetime=0)
        { Damage=damage; Range=range; Interval=interval; Ammo=ammo; Speed=speed; Radius=radius; EdgeDamage=edge; Windup=windup; Active=active; Lifetime=lifetime; }
    }
    public static class WeaponCatalog
    {
        public const int Revision=2, StarterAmmo=24, DefaultTickRate=120, MaximumProjectiles=64;
        public const float Health=100, Shield=60, ShieldSeconds=6, ReloadSeconds=1.2f, RespawnSeconds=3, PickupSeconds=15;
        public static int Ticks(float seconds,int rate) => (int)Math.Ceiling(seconds*rate-0.00001);
        public static WeaponSpec Get(WeaponKind kind)
        {
            switch(kind)
            {
                case WeaponKind.LineDriver: return new WeaponSpec(13,70,.15f,StarterAmmo);
                case WeaponKind.PulseLobber: return new WeaponSpec(60,90,.90f,10,18,4,20,lifetime:5);
                case WeaponKind.ReboundDisc: return new WeaponSpec(35,112,.60f,10,28,lifetime:4);
                case WeaponKind.PushLens: return new WeaponSpec(0,8,.85f,6,windup:.18f);
                case WeaponKind.Grenade: return new WeaponSpec(90,100,.70f,2,16,5.5f,15,lifetime:1.6f);
                case WeaponKind.StunGun: return new WeaponSpec(15,35,1.20f,8,45,lifetime:35f/45);
                case WeaponKind.LaserSword: return new WeaponSpec(45,2.3f,.65f,0,windup:.12f,active:.13f);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public static WeaponKind FromPickup(string id)
        {
            switch(id)
            {
                case "line-driver": return WeaponKind.LineDriver;
                case "pulse-lobber": return WeaponKind.PulseLobber;
                case "rebound-disc": return WeaponKind.ReboundDisc;
                case "push-lens": return WeaponKind.PushLens;
                case "grenade": return WeaponKind.Grenade;
                case "stun-gun": return WeaponKind.StunGun;
                case "laser-sword": return WeaponKind.LaserSword;
                default: return WeaponKind.None;
            }
        }
    }
}
