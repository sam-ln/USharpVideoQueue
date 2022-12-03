
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;
using UdonSharp;

namespace USharpVideoQueue.Tests.Mock
{
    public class USharpVideoPlayerMock : USharpVideoPlayer
    {
        public VRCUrl Url;
        public override void PlayVideo(VRCUrl url)
        {
            Url = url;
            Debug.Log("Play triggered!");
        }

        internal override void Start()
        {
            if (_ranInit)
                return;

            _ranInit = true;

            if (_registeredCallbackReceivers == null)
                _registeredCallbackReceivers = new UdonSharpBehaviour[0];

        }
    }
}
