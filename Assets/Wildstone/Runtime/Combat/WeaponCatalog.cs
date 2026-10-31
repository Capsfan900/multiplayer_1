using System;
using UnityEngine;

namespace Wildstone.Combat
{
    // Append new kinds last: numeric IDs are serialized in snapshots and network state.
    public enum WeaponKind { None, LineDriver, PulseLobber, ReboundDisc, PushLens, Grenade, StunGun, LaserSword, LaserPistol, LaserShotgun, LaserSniper, LaserSMG }
    public readonly struct WeaponSpec
    {
        public readonly float Damage, EdgeDamage, Range, Radius, Speed, Interval, Windup, Active, Lifetime;
        public readonly int Ammo;
        public WeaponSpec(float damage,float range,float interval,int ammo,float speed=0,float radius=0,float edge=0,float windup=0,float active=0,float lifetime=0)
        { Damage=damage; Range=range; Interval=interval; Ammo=ammo; Speed=speed; Radius=radius; EdgeDamage=edge; Windup=windup; Active=active; Lifetime=lifetime; }
    }
    public static class WeaponCatalog
    {
        public const int Revision=5, StarterAmmo=24, DefaultTickRate=120, MaximumProjectiles=64;
        public const float Health=100, Shield=60, ShieldSeconds=6, ReloadSeconds=1.2f, RespawnSeconds=3, PickupSeconds=15;
        public const float HealthPack=50; // proposal: a claimed health pack restores this much, up to Health
        // Clients choose and prompt within PickupReach of their predicted feet; the host's copy of a running player trails
        // by about a round trip, so it accepts PickupLagMargin more (PLAYTEST-FIX-LATER #3).
        public const float PickupReach=2.5f, PickupLagMargin=.5f;
        // Revision 5 proposal (PLAYTEST-FIX-LATER #10): a special weapon pickup carries this many magazines as one count.
        public const int SpecialMagazines=2;
        public static int PickupAmmo(WeaponKind kind) =>
            kind==WeaponKind.None?0:kind==WeaponKind.LineDriver || kind==WeaponKind.Grenade?Get(kind).Ammo:Get(kind).Ammo*SpecialMagazines;
        // Revision 4 proposed defaults (combat spec): bloom recovery starts after a short pause.
        public const float BloomDelaySeconds=.1f, BloomDecayPerSecond=2.5f;
        // D036 proposed default: a held automatic trigger may land this early or late on the host without losing or
        // shifting its cadence (send-frame and arrival jitter); the schedule carries, so the sustained rate never rises.
        public const float FireSlackSeconds=.025f;
        // Proposal (PLAYTEST-FIX-LATER #8): an early semi-automatic press waits this long for the cooldown, then fires once.
        public const float FireBufferSeconds=.2f;
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
                case WeaponKind.LaserPistol: return new WeaponSpec(32,65,.28f,18);
                case WeaponKind.LaserShotgun: return new WeaponSpec(9,24,.85f,8);
                case WeaponKind.LaserSniper: return new WeaponSpec(80,110,1.2f,6);
                case WeaponKind.LaserSMG: return new WeaponSpec(10,35,.08f,32);
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        public static bool IsHitscan(WeaponKind kind) => kind==WeaponKind.LineDriver || kind==WeaponKind.LaserPistol || kind==WeaponKind.LaserShotgun || kind==WeaponKind.LaserSniper || kind==WeaponKind.LaserSMG;
        // Held trigger repeats at the weapon interval through the shared input path; others fire per press.
        public static bool IsAutomatic(WeaponKind kind) => kind==WeaponKind.LineDriver || kind==WeaponKind.LaserSMG;
        public static int HitscanPellets(WeaponKind kind) => kind==WeaponKind.LaserShotgun?8:1;

        // ADS applies to hitscan guns only; sighted weapons hide the hip crosshair, only the sniper scopes.
        public static bool CanAim(WeaponKind kind) => IsHitscan(kind);
        public static bool HasSights(WeaponKind kind) => kind==WeaponKind.LineDriver || kind==WeaponKind.LaserSMG || kind==WeaponKind.LaserPistol || kind==WeaponKind.LaserSniper;
        public static bool IsScoped(WeaponKind kind) => kind==WeaponKind.LaserSniper;
        public const float ScopeThreshold=.9f, SightThreshold=.5f;
        // Presentation gates: the scope overlay replaces the view near full ADS; sighted guns hide the hip crosshair past halfway.
        public static bool ScopeActive(WeaponKind kind,float ads) => IsScoped(kind) && ads>=ScopeThreshold;
        public static bool HipCrosshairVisible(WeaponKind kind,float ads) => !(HasSights(kind) && ads>SightThreshold);
        // Divides tan(vertical FOV/2) at full ADS: zoom 2 halves the visible half-height.
        public static float AdsZoom(WeaponKind kind)
        {
            switch(kind)
            {
                case WeaponKind.LineDriver: return 1.35f;
                case WeaponKind.LaserSMG: return 1.2f;
                case WeaponKind.LaserPistol: return 1.15f;
                case WeaponKind.LaserSniper: return 4f;
                default: return 1;
            }
        }
        public static float AdsSeconds(WeaponKind kind)
        {
            switch(kind)
            {
                case WeaponKind.LaserSMG: case WeaponKind.LaserPistol: return .15f;
                case WeaponKind.LaserSniper: return .30f;
                default: return .20f;
            }
        }
        public static float HeadshotMultiplier(WeaponKind kind)
        {
            switch(kind)
            {
                case WeaponKind.LineDriver: case WeaponKind.LaserSMG: case WeaponKind.LaserPistol: return 1.5f;
                case WeaponKind.LaserSniper: return 2f;
                default: return 1;
            }
        }
        public static float BloomPerShot(WeaponKind kind)
        {
            switch(kind)
            {
                case WeaponKind.LineDriver: return .18f;
                case WeaponKind.LaserSMG: return .12f;
                case WeaponKind.LaserPistol: return .35f;
                case WeaponKind.LaserSniper: return 1f;
                default: return 0;
            }
        }
        static void Cone(WeaponKind kind,out float hip,out float bloom,out float ads)
        {
            switch(kind)
            {
                case WeaponKind.LineDriver: hip=1.1f; bloom=1.2f; ads=.15f; return;
                case WeaponKind.LaserSMG: hip=1.8f; bloom=1.5f; ads=.3f; return;
                case WeaponKind.LaserPistol: hip=.9f; bloom=1.2f; ads=.2f; return;
                case WeaponKind.LaserSniper: hip=6f; bloom=1.2f; ads=0; return;
                case WeaponKind.LaserShotgun: hip=5f; bloom=0; ads=.85f; return;
                default: hip=0; bloom=0; ads=1; return;
            }
        }
        static float Unit(float value) => float.IsNaN(value)?0:Math.Max(0,Math.Min(1,value));
        // Cone half-angle in degrees shared by the rules and the dynamic crosshair. The shotgun returns its fixed ring angle.
        public static float SpreadDegrees(WeaponKind kind,float ads,float bloom)
        {
            Cone(kind,out float hip,out float bloomDegrees,out float adsScale);
            ads=Unit(ads);
            // The scope overlay appears at ScopeThreshold, so a scoped weapon is fully accurate from that point.
            if(IsScoped(kind)) ads=Unit(ads/ScopeThreshold);
            return (hip+bloomDegrees*Unit(bloom))*(1+(adsScale-1)*ads);
        }
        // Gameplay field of view at the given ADS progress; presentation and look sensitivity use the same value.
        public static float AdsVerticalFov(float hipVerticalDegrees,WeaponKind kind,float ads)
        {
            float zoom=1+(AdsZoom(kind)-1)*Unit(ads);
            return 2*Mathf.Atan(Mathf.Tan(hipVerticalDegrees*.5f*Mathf.Deg2Rad)/zoom)*Mathf.Rad2Deg;
        }

        // Stable integer mixing; never System.Random/UnityEngine.Random, so every peer reproduces a shot.
        static uint Mix(uint h) { h^=h>>16; h*=0x7feb352du; h^=h>>15; h*=0x846ca68bu; h^=h>>16; return h; }
        public static uint ShotSeed(in CombatAction action)
        {
            uint h=Mix((uint)action.Participant^0x9e3779b9u);
            h=Mix(h^(uint)action.LifeGeneration); h=Mix(h^(uint)action.Sequence);
            h=Mix(h^(uint)action.Tick); return Mix(h^(uint)action.AuthorityGeneration);
        }
        // Deterministic per-pellet direction within spreadDegrees of aim. The shotgun keeps its fixed ring pattern.
        public static Vector3 HitscanDirection(WeaponKind kind,Vector3 aim,int pellet,float spreadDegrees,uint seed)
        {
            if(!IsHitscan(kind) || !CombatRules.Direction(aim) || pellet<0 || pellet>=HitscanPellets(kind) || !CombatRules.Finite(spreadDegrees) || spreadDegrees<0 || spreadDegrees>45)
                throw new ArgumentOutOfRangeException(nameof(pellet));
            aim=aim.normalized;
            if(spreadDegrees==0 || (kind==WeaponKind.LaserShotgun && pellet==0)) return aim;
            Vector3 right=Vector3.Cross(Mathf.Abs(aim.y)>.99f?Vector3.forward:Vector3.up,aim).normalized;
            Vector3 up=Vector3.Cross(aim,right).normalized;
            float angle,radius;
            if(kind==WeaponKind.LaserShotgun) { angle=(pellet-1)*(2*Mathf.PI/7); radius=spreadDegrees; }
            else
            {
                uint a=Mix(seed^((uint)pellet*0x85ebca6bu+0x27d4eb2fu)), b=Mix(a+0x632be5abu);
                // Square-root radius gives uniform density over the small cone's cross-section.
                angle=(b>>8)*(2*Mathf.PI/16777216f); radius=spreadDegrees*Mathf.Sqrt((a>>8)/16777216f);
            }
            radius*=Mathf.Deg2Rad;
            return (aim*Mathf.Cos(radius)+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*Mathf.Sin(radius)).normalized;
        }
        // Reproduces an authoritative Shot event's pellet direction on any peer: Vector is the aim, Amount the spread.
        public static Vector3 ShotDirection(in CombatEvent shot,int pellet) =>
            HitscanDirection(shot.Weapon,shot.Vector,pellet,shot.Amount,ShotSeed(shot.Action));
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
                case "laser-pistol": return WeaponKind.LaserPistol;
                case "laser-shotgun": return WeaponKind.LaserShotgun;
                case "laser-sniper": return WeaponKind.LaserSniper;
                case "laser-smg": return WeaponKind.LaserSMG;
                default: return WeaponKind.None;
            }
        }
    }
}
