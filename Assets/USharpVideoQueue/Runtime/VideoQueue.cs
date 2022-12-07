
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;

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
            Skip();
        }

        public void OnUSharpVideoError() {
            Skip();
        }

        public void Skip() {
            dequeue(queuedVideos);
            if (!isEmpty(queuedVideos))
            {
                playFirst();
            }
        }

        internal void playFirst() => VideoPlayer.PlayVideo((VRCUrl)first(queuedVideos));

        /* Queue Utilities */

        // These would preferably be object methods of a separate class Queue<T>,
        // but Udon Sharp does not allow for Object instantiation at runtime,
        // Type<T> or subclasses.

        internal static bool isFull(System.Object[] queue)
        {
            return queue[queue.Length - 1] != null;
        }

        internal static bool isEmpty(System.Object[] queue)
        {
            return queue[0] == null;
        }

        internal static System.Object first(System.Object[] queue)
        {
            return queue[0];
        }

       internal static int firstEmpty(System.Object[] queue)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] == null) return i;
            }
            return -1;
        }

        internal static bool enqueue(System.Object[] queue, System.Object element)
        {
            int index = firstEmpty(queue);
            if(index == -1) return false;
            queue[index] = element;
            return true;
        }

        internal static void dequeue(System.Object[] queue)
        {
            remove(queue, 0);
        }

        internal static void remove(System.Object[] queue, int index)
        {
            queue[index] = null;
            for(int i = index; i < queue.Length-1; i++) {
                queue[i] = queue[i+1];
            }
        }



    }

}
