
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
            dequeue(queuedVideos);
            if (!isEmpty(queuedVideos))
            {
                playFirst();
            }
        }

        internal void playFirst() => VideoPlayer.PlayVideo(first(queuedVideos));

        /* Queue Utilities */

        // These would preferably be object methods of a separate class Queue<T>,
        // but Udon Sharp does not allow for Object instantiation at runtime,
        // Type<T> or subclasses.

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

        internal static bool enqueue(VRCUrl[] queue, VRCUrl element)
        {
            int index = firstEmpty(queue);
            if(index == -1) return false;
            queue[index] = element;
            return true;
        }

        internal static void dequeue(VRCUrl[] queue)
        {
            //TODO: Change dequeue to be non-pure
            remove(queue, 0);
        }

        internal static void remove(VRCUrl[] array, int index)
        {
            array[index] = null;
            for(int i = index; i < array.Length-1; i++) {
                array[i] = array[i+1];
            }
        }



    }

}
