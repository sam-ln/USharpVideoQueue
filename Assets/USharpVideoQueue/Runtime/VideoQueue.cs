
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;
using VRC.Udon;

namespace USharpVideoQueue.Runtime
{
    public class VideoQueue : UdonSharpBehaviour
    {
        public const int MAX_QUEUE_LENGTH = 5;
        public USharpVideoPlayer VideoPlayer;

        internal VRCUrl[] queuedVideos = new VRCUrl[MAX_QUEUE_LENGTH];
        internal bool Initialized;


        internal void Start()
        {
            Initialized = true;
            VideoPlayer.RegisterCallbackReceiver(this);
            Debug.Log("Started");
        }

        public void QueueVideo(VRCUrl url)
        {
            bool wasEmpty = isEmpty(queuedVideos);
            enqueue(queuedVideos, url);
            if (wasEmpty) playFirst();
        }

        /* USharpVideoPlayer Event Callbacks */

        public void OnUSharpVideoEnd()
        {
            queuedVideos = dequeue(queuedVideos);
            if (!isEmpty(queuedVideos))
            {
                playFirst();
            }
        }

        internal void playFirst() => VideoPlayer.PlayVideo(first(queuedVideos));

        /* Queue Utilities */
        // These would preferrably be in a different class but USharp is not kind with frequent class-to-class interactions

        internal static bool isFull(VRCUrl[] queue)
        {
            return queue[queue.Length - 1] != null;
        }

        internal static bool isEmpty(VRCUrl[] queue)
        {
            return queue[0] == null;
        }

        internal static VRCUrl first(VRCUrl[] queue)
        {
            return queue[0];
        }

       internal static int firstEmpty(VRCUrl[] queue)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] == null) return i;
            }
            return -1;
        }

        internal static void enqueue(VRCUrl[] queue, VRCUrl element)
        {
            queue[firstEmpty(queue)] = element;
        }

        internal static VRCUrl[] dequeue(VRCUrl[] queue)
        {
            //TODO: Change dequeue to be non-pure
            return remove(queue, 0);
        }

        internal static VRCUrl[] remove(VRCUrl[] array, int index)
        {
            VRCUrl[] newArray = new VRCUrl[array.Length - 1];
            newArray = copy(array, newArray, 0, 0, index);
            newArray = copy(array, newArray, index + 1, index, array.Length - index - 1);
            return newArray;
        }

        internal static VRCUrl[] copy(VRCUrl[] source, VRCUrl[] destination, int sourceIndex, int destinationIndex, int count)
        {
            //Replicate destination to prevent mutating the original array. (pure function)
            VRCUrl[] newArray = new VRCUrl[destination.Length];
            for (int i = 0; i < destination.Length; i++)
            {
                newArray[i] = destination[i];
            }
            int indexDiff = destinationIndex - sourceIndex;
            for (int i = 0; i < count; i++)
            {
                newArray[destinationIndex + i] = source[sourceIndex + i];
            }
            return newArray;
        }

    }

}
