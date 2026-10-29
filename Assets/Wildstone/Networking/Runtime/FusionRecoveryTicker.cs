using UnityEngine;
using Fusion;
namespace Wildstone.Networking
{
    // Runner-owned pump; destroying the retired runner also retires this callback source.
    public sealed class FusionRecoveryTicker : MonoBehaviour
    {
        internal FusionSession Session;
        NetworkRunner owned;
        void Awake() { owned=GetComponent<NetworkRunner>(); }
        void Update() { if(Session!=null && owned!=null) Session.ServiceRecovery(owned); }
    }
}
