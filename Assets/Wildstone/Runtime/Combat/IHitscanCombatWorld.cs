using UnityEngine;

namespace Wildstone.Combat
{
    // Only immediate hitscan enters rewind; travelling projectiles use ICombatWorld.Sweep.
    public interface IHitscanCombatWorld
    {
        bool Hitscan(CombatAction action,Vector3 origin,Vector3 displacement,int ignoredMask,out CombatHit hit);
    }
}
