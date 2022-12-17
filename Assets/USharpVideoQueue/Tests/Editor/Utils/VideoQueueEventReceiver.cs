
using UdonSharp;

namespace USharpVideoQueue.Tests.Editor.Utils
{
    public abstract class VideoQueueEventReceiver : UdonSharpBehaviour
    {
        public abstract void OnUSharpVideoQueueContentChange();
    }
}