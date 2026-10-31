using UnityEngine;
using Wildstone.Combat;

namespace Wildstone.Presentation
{
    public sealed class PresentationCatalog : ScriptableObject
    {
        public GameObject[] Humans = new GameObject[4];
        public GameObject Hands;
        public GameObject[] Weapons = new GameObject[11];
        public GameObject BlinkPickup, ShieldPickup, HealthPickup;
        public Material Beam, Pulse, Disc, Push, Stun, Blink, Shield, Health, Danger, ZoneIdle, ZoneOwned, ZoneContested;
        public Material VfxBlade, VfxSpark, VfxGlow, VfxFire, VfxSmoke, VfxDust, VfxDebris, VfxShockwave, Scorch, ZoneDecal, ZoneCurtain, VfxPixel, Derez, ShieldShell;

        public GameObject Weapon(WeaponKind kind)
        {
            int index=(int)kind-1;
            return index>=0 && index<Weapons.Length ? Weapons[index] : null;
        }
        public GameObject Pickup(string item)
        {
            if(item=="blink") return BlinkPickup;
            if(item=="shield") return ShieldPickup;
            if(item=="health") return HealthPickup;
            return Weapon(WeaponCatalog.FromPickup(item));
        }
    }
}
