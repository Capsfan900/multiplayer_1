using System;
using UnityEngine;

namespace Wildstone.Presentation
{
    public static class GripAttachment
    {
        public static Transform Find(Transform root,string name)
        {
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) if(child.name==name) return child;
            return null;
        }
        public static void Align(Transform model,Transform socket)
        {
            if(model==null || socket==null) throw new ArgumentException("Missing model or hand socket.");
            var grip=Find(model,"Grip");
            if(grip==null) throw new ArgumentException("Weapon model has no authored Grip.");
            model.rotation=socket.rotation*Quaternion.Inverse(grip.rotation)*model.rotation;
            model.position+=socket.position-grip.position;
        }
    }
}
