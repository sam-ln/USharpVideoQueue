using UdonSharp;
using UnityEngine.UI;
using VRC.SDKBase;

namespace USharpVideoQueue.Runtime.InstanceSettings
{
    public class InstanceOwnerDisplay : UdonSharpBehaviour
    {
        public Text Display;
        private string PRE_MESSAGE = "Current Instance Owner is: ";
        void Start()
        {
            VRCPlayerApi currentOwner = Networking.GetOwner(gameObject);
            Display.text = PRE_MESSAGE + currentOwner.displayName;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            Display.text = PRE_MESSAGE + player.displayName;
        }

    
    }
}
