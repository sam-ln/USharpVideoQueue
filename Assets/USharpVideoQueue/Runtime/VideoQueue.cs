using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;
using static USharpVideoQueue.Runtime.Utility.QueueArray;
using System;
using USharpVideoQueue.Runtime.Utility;

namespace USharpVideoQueue.Runtime
{
    [DefaultExecutionOrder(-10)]
    public class VideoQueue : UdonSharpBehaviour
    {
      
        public const int MAX_QUEUE_LENGTH = 6;
        public const string OnUSharpVideoQueueContentChangeEvent = "OnUSharpVideoQueueContentChange";
        public USharpVideoPlayer VideoPlayer;
        internal UdonSharpBehaviour[] registeredCallbackReceivers;

        [UdonSynced] private VRCUrl[] queuedVideos;
        [UdonSynced] private string[] queuedTitles;
        [UdonSynced] private int[] queuedByPlayer;
        
        //Properties can't be [UdonSynced], so they are separated
        public VRCUrl[] QueuedVideos => queuedVideos;
        public string[] QueuedTitles => queuedTitles;
        public int[] QueuedByPlayer => queuedByPlayer;
       

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
            queuedByPlayer = new int[MAX_QUEUE_LENGTH];
            queuedTitles = new string[MAX_QUEUE_LENGTH];

            for (int i = 0; i < MAX_QUEUE_LENGTH; i++)
            {
                queuedVideos[i] = VRCUrl.Empty;
                queuedTitles[i] = String.Empty;
                queuedByPlayer[i] = -1;
            }
            
        }

        public void QueueVideo(VRCUrl url)
        {
            if(url == null || !Validation.ValidateURL(url.Get())) return;
            bool wasEmpty = IsEmpty(queuedVideos);
            ensureOwnership();
            enqueueVideoAndMeta(url, "placeholder");
            synchronizeQueueState();
            if (wasEmpty) playFirst();
        }

        public void Next()
        {
            ensureOwnership();
            dequeueVideoAndMeta();
            synchronizeQueueState();
            if (!IsEmpty(queuedVideos)) playFirst();
        }

        internal virtual void synchronizeQueueState()
        {
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            OnQueueContentChange();
        }

        internal void dequeueVideoAndMeta()
        {
            if (IsEmpty(queuedVideos)) return;
            Dequeue(queuedVideos);
            Dequeue(queuedTitles);
            Dequeue(queuedByPlayer);
            OnQueueContentChange();
        }

        internal void enqueueVideoAndMeta(VRCUrl url, string title)
        {
            Enqueue(queuedVideos, url);
            Enqueue(queuedTitles, title);
            int localPlayerID = getPlayerID(getLocalPlayer());
            Enqueue(queuedByPlayer, localPlayerID);
            OnQueueContentChange();
        }

        internal void removeVideoAndMeta(int index)
        {
            Remove(queuedVideos, index);
            Remove(queuedTitles, index);
            Remove(queuedByPlayer, index);
            OnQueueContentChange();
        }

        internal void playFirst() => VideoPlayer.PlayVideo((VRCUrl)First(queuedVideos));

        internal void ensureOwnership()
        {
            if (!isOwner())
            {
                becomeOwner();
            }
        }

        /* VRC SDK wrapper functions to enable mocking for tests */
        internal virtual void becomeOwner() => Networking.SetOwner(Networking.LocalPlayer, this.gameObject);
        internal virtual bool isOwner() => Networking.IsOwner(Networking.LocalPlayer, this.gameObject);
        internal virtual VRCPlayerApi getLocalPlayer() => Networking.LocalPlayer;
        internal virtual int getPlayerID(VRCPlayerApi player) => player.playerId;

        internal virtual bool isVideoPlayerOwner() =>
            Networking.IsOwner(Networking.LocalPlayer, VideoPlayer.gameObject);

        /* VRC Runtime Events */

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!isOwner()) return;

            //Remove all videos queued by player who left
            int playerId = getPlayerID(player);
            for (int i = Count(queuedVideos) - 1; i >= 0; i--)
            {
                if (queuedByPlayer[i] == playerId)
                {
                    //If user who left has a video currently playing,
                    //skip it and return (because Next() calls synchronizeQueueState() as well)
                    if (i == 0)
                    {
                        Next();
                        return;
                    }

                    removeVideoAndMeta(i);
                }
            }

            synchronizeQueueState();
        }

        /* USharpVideoQueue Emitted Callbacks */

        internal void OnQueueContentChange()
        {
            SendCallback(OnUSharpVideoQueueContentChangeEvent);
        }

        /* USharpVideoPlayer Event Callbacks */

        public void OnUSharpVideoEnd()
        {
            if (isVideoPlayerOwner())
            {
                Next();
            }
        }

        public void OnUSharpVideoError()
        {
            if (isVideoPlayerOwner())
            {
                Next();
            }
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