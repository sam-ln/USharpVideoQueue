
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;

namespace USharpVideoQueue.Tests.Mock
{
    public class USharpVideoPlayerMock : USharpVideoPlayer
    {
        public VRCUrl Url;
        public override void PlayVideo(VRCUrl url) {
            Url = url;
            Debug.Log("Play triggered!");
       }
    }
}
