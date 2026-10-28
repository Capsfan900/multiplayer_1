using UnityEngine;

namespace Wildstone.Movement
{
    // IDs are authored per map and preserved by the authority's world snapshot.
    [DisallowMultipleComponent]
    public class MotorSurface : MonoBehaviour
    {
        [Min(1)] public int Id=1;
    }
}
