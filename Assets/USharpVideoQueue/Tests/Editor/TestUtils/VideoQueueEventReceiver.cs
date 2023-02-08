
using UdonSharp;

namespace USharpVideoQueue.Tests.Editor.TestUtils
{
    public abstract class VideoQueueEventReceiver : UdonSharpBehaviour
    {
        public abstract void OnUSharpVideoQueueContentChange();
    }
}