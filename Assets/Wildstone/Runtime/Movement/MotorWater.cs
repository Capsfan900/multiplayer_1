using UnityEngine;

namespace Wildstone.Movement
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MotorWater : MotorSurface
    {
        public Vector3 FlowDirection=Vector3.forward;
        [Min(0)] public float FlowSpeed=6;
        void Awake() { gameObject.layer=8; GetComponent<BoxCollider>().isTrigger=true; }
    }
}
