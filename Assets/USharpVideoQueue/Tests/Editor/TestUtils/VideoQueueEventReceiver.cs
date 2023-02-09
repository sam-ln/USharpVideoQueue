
using UdonSharp;

namespace USharpVideoQueue.Tests.Editor.TestUtils
{
    public abstract class VideoQueueEventReceiver : UdonSharpBehaviour
    {
        public abstract void OnUSharpVideoQueueContentChange();

        public abstract void OnUSharpVideoQueuePlayingNextVideo();

        public abstract void OnUSharpVideoQueueSkippedError();

        public abstract void OnUSharpVideoQueueFinalVideoEnded();
    }
}