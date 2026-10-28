using System;
using UnityEngine;
namespace Wildstone.Match
{
    [Serializable] public sealed class PickupState
    {
        public int Id, Generation=1, AvailableTick;
        public string Item;
        public Vector3 Position;
        public PickupState Copy() => (PickupState)MemberwiseClone();
    }
}
