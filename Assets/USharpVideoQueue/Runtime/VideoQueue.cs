
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;
using VRC.Udon;

namespace USharpVideoQueue.Runtime
{
    public class VideoQueue : UdonSharpBehaviour
    {
        public USharpVideoPlayer VideoPlayer;

        internal VRCUrl[] queuedVideo = new VRCUrl[5];
        internal bool Initialized;
        internal void Start()
        {
            Initialized = true;
            VideoPlayer.RegisterCallbackReceiver(this);
            Debug.Log("Started");
        }

        public void QueueVideo(VRCUrl url) {
            queuedVideo[0] = url;
            VideoPlayer.PlayVideo(url);
        }
    }
}
