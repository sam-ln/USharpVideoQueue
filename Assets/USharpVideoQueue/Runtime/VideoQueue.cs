using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;
using static USharpVideoQueue.Runtime.QueueArrayUtils;

namespace USharpVideoQueue.Runtime
{
    public class VideoQueue : UdonSharpBehaviour
    {
        public const int MAX_QUEUE_LENGTH = 5;
        public const string OnUSharpVideoQueueContentChangeEvent = "OnUSharpVideoQueueContentChange";
        public USharpVideoPlayer VideoPlayer;
        internal UdonSharpBehaviour[] registeredCallbackReceivers;
        [UdonSynced]
        public VRCUrl[] queuedVideos;

        internal bool Initialized;

        internal void Start()
        {
            Initialized = true;
            if (registeredCallbackReceivers == null)
            {
                registeredCallbackReceivers = new UdonSharpBehaviour[0];
            }
            VideoPlayer.RegisterCallbackReceiver(this);
            queuedVideos = new VRCUrl[MAX_QUEUE_LENGTH];
        }

        public void QueueVideo(VRCUrl url)
        {
            bool wasEmpty = IsEmpty(queuedVideos);
            Enqueue(queuedVideos, url);
            OnQueueContentChange();
            if (wasEmpty) playFirst();
        }

        /* USharpVideoPlayer Event Callbacks */

        public void OnUSharpVideoEnd()
        {
            Skip();
        }

        public void OnUSharpVideoError()
        {
            Skip();
        }

        public void Skip()
        {
            Dequeue(queuedVideos);
            OnQueueContentChange();
            if (!IsEmpty(queuedVideos))
            {
                playFirst();
            }
        }

        internal virtual void synchronizeQueueState()
        {
            RequestSerialization();
        }

        public override void OnDeserialization()
        {

        }

        internal void playFirst() => VideoPlayer.PlayVideo((VRCUrl)First(queuedVideos));


        internal void OnQueueContentChange()
        {
            SendCallback(OnUSharpVideoQueueContentChangeEvent);
        }

        /* Callback Handling */
        //Taken from MerlinVR's USharpVideoPlayer (https://github.com/MerlinVR/USharpVideo)

        /// <summary>
        /// Registers an UdonSharpBehaviour as a callback receiver for events that happen on this video player.
        /// Callback receivers can be used to react to state changes on the video player without needing to check periodically.
        /// </summary>
        /// <param name="callbackReceiver"></param>
        public void RegisterCallbackReceiver(UdonSharpBehaviour callbackReceiver)
        {
            //Nullcheck with Equals for testability reasons
            if (UdonSharpBehaviour.Equals(callbackReceiver, null))
                return;

            if (registeredCallbackReceivers == null)
                registeredCallbackReceivers = new UdonSharpBehaviour[0];
            foreach (UdonSharpBehaviour currReceiver in registeredCallbackReceivers)
            {
                if (callbackReceiver == currReceiver)
                    return;
            }

            UdonSharpBehaviour[] newControlHandlers = new UdonSharpBehaviour[registeredCallbackReceivers.Length + 1];
            registeredCallbackReceivers.CopyTo(newControlHandlers, 0);
            registeredCallbackReceivers = newControlHandlers;

            registeredCallbackReceivers[registeredCallbackReceivers.Length - 1] = callbackReceiver;
        }

        public void UnregisterCallbackReceiver(UdonSharpBehaviour callbackReceiver)
        {
            if (!callbackReceiver)
                return;

            if (registeredCallbackReceivers == null)
                registeredCallbackReceivers = new UdonSharpBehaviour[0];

            int callbackReceiverCount = registeredCallbackReceivers.Length;
            for (int i = 0; i < callbackReceiverCount; ++i)
            {
                UdonSharpBehaviour currHandler = registeredCallbackReceivers[i];

                if (callbackReceiver == currHandler)
                {
                    UdonSharpBehaviour[] newCallbackReceivers = new UdonSharpBehaviour[callbackReceiverCount - 1];

                    for (int j = 0; j < i; ++j)
                        newCallbackReceivers[j] = registeredCallbackReceivers[j];

                    for (int j = i + 1; j < callbackReceiverCount; ++j)
                        newCallbackReceivers[j - 1] = registeredCallbackReceivers[j];

                    registeredCallbackReceivers = newCallbackReceivers;

                    return;
                }
            }
        }

        internal void SendCallback(string callbackName)
        {
            foreach (UdonSharpBehaviour callbackReceiver in registeredCallbackReceivers)
            {
                if (!UdonSharpBehaviour.Equals(callbackName, null))
                {
                    Debug.Log($"Callback {callbackName} sent to receiver");
                    callbackReceiver.SendCustomEvent(callbackName);
                }
            }
        }
    }

}
