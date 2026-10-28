using UnityEngine;
using Wildstone.Combat;

namespace Wildstone.Presentation
{
    public sealed class PresentationCatalog : ScriptableObject
    {
        public GameObject[] Humans = new GameObject[4];
        public GameObject Hands;
        public GameObject[] Weapons = new GameObject[7];
        public GameObject BlinkPickup, ShieldPickup;
        public Material Beam, Pulse, Disc, Push, Stun, Blink, Shield, Danger, ZoneIdle, ZoneOwned, ZoneContested;

        public GameObject Weapon(WeaponKind kind)
        {
            int index=(int)kind-1;
            return index>=0 && index<Weapons.Length ? Weapons[index] : null;
        }
        public GameObject Pickup(string item)
        {
            if(item=="blink") return BlinkPickup;
            if(item=="shield") return ShieldPickup;
            return Weapon(WeaponCatalog.FromPickup(item));
        }
    }
}
