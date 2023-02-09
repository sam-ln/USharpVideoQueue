using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;
using static USharpVideoQueue.Runtime.Utility.QueueArray;
using USharpVideoQueue.Runtime.Utility;

namespace USharpVideoQueue.Runtime
{
    [DefaultExecutionOrder(-100)]
    public class VideoQueue : UdonSharpBehaviour
    {
        public int MaxQueueItems = 6;
        public bool EnableDebug = false;

        public const string OnUSharpVideoQueueContentChangeEvent = "OnUSharpVideoQueueContentChange";
        public const string OnUSharpVideoQueuePlayingNextVideo = "OnUSharpVideoQueuePlayingNextVideo";
        public const string OnUSharpVideoQueueSkippedError = "OnUSharpVideoQueueSkippedError";
        public const string OnUSharpVideoQueueFinalVideoEnded = "OnUSharpVideoQueueFinalVideoEnded";

        public USharpVideoPlayer VideoPlayer;
        internal UdonSharpBehaviour[] registeredCallbackReceivers;

        [UdonSynced] internal VRCUrl[] queuedVideos;
        [UdonSynced] internal string[] queuedTitles;
        [UdonSynced] internal int[] queuedByPlayer;

        internal int dataCriticalEventSize = 8;
        [UdonSynced] internal string[] dataCriticalEvents;
        internal int eventTimestampThreshold;

        internal bool initialized = false;
        public bool VideoPlayerIsLoading { get; private set; }

        internal void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (initialized) return;

            initialized = true;
            VideoPlayerIsLoading = false;
            eventTimestampThreshold = getCurrentServerTime();

            if (registeredCallbackReceivers == null)
            {
                registeredCallbackReceivers = new UdonSharpBehaviour[0];
            }

            dataCriticalEvents = new string[dataCriticalEventSize];

            for (int i = 0; i < dataCriticalEventSize; i++)
            {
                dataCriticalEvents[i] = string.Empty;
            }

            queuedVideos = new VRCUrl[MaxQueueItems];
            queuedByPlayer = new int[MaxQueueItems];
            queuedTitles = new string[MaxQueueItems];
            VideoPlayer.RegisterCallbackReceiver(this);

            for (int i = 0; i < MaxQueueItems; i++)
            {
                queuedVideos[i] = VRCUrl.Empty;
                queuedTitles[i] = string.Empty;
                queuedByPlayer[i] = -1;
            }
        }

        public void QueueVideo(VRCUrl url)
        {
            if (url == null) return;
            QueueVideo(url, url.Get());
        }

        public void QueueVideo(VRCUrl url, string title)
        {
            if (url == null || !Validation.ValidateURL(url.Get())) return;
            bool wasEmpty = IsEmpty(queuedVideos);
            ensureOwnership();
            enqueueVideoAndMeta(url, title);
            invokeEventsAndSynchronize();
            if (wasEmpty) playFirst();
        }

        public void RemoveVideo(int index, bool skipChecks = false)
        {
            if (index == 0)
            {
                if (skipChecks) skipToNextVideo();
                else RequestNext();
                return;
            }

            ensureOwnership();
            removeVideoAndMeta(index);
            invokeEventsAndSynchronize();
        }

        public void RequestNext()
        {
            if (VideoPlayerIsLoading) return;
            skipToNextVideo();
        }

        internal void skipToNextVideo()
        {
            if (IsEmpty(queuedVideos)) return;

            ensureOwnership();
            dequeueVideoAndMeta();
            if (IsEmpty(queuedVideos))
            {
                clearVideoPlayer();
                QueueCallbackEvent(OnUSharpVideoQueueFinalVideoEnded);
            }
            else
            {
                QueueCallbackEvent(OnUSharpVideoQueuePlayingNextVideo);
                QueueFunctionEvent(nameof(PlayFirstIfVideoOwner));
            }

            invokeEventsAndSynchronize();
        }

        public void PlayFirstIfVideoOwner()
        {
            VideoPlayerIsLoading = true;
            if (!isFirstVideoOwner()) return;
            playFirst();
        }

        public int QueuedVideosCount()
        {
            return Count(queuedVideos);
        }

        public VRCUrl GetURL(int index)
        {
            if (index >= QueuedVideosCount()) return VRCUrl.Empty;
            return queuedVideos[index];
        }

        public string GetTitle(int index)
        {
            if (index >= QueuedVideosCount()) return string.Empty;
            return queuedTitles[index];
        }

        public int GetQueuedByPlayer(int index)
        {
            if (index >= QueuedVideosCount()) return -1;
            return queuedByPlayer[index];
        }


        internal void invokeEventsAndSynchronize()
        {
            invokePendingEvents();
            synchronizeData();
        }

        public override void OnDeserialization()
        {
            LogDebug("OnDeserialization run!");
            invokePendingEvents();
            OnQueueContentChange();
        }

        public override void OnPreSerialization()
        {
            LogDebug("Sending Serialized Data!");
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


        internal void playFirst()
        {
            VideoPlayer.PlayVideo((VRCUrl)First(queuedVideos));
        }

        internal void ensureOwnership()
        {
            if (!isOwner())
            {
                becomeOwner();
            }
        }

        internal void clearVideoPlayer()
        {
            VideoPlayer.TakeOwnership();
            VideoPlayer.StopVideo();
        }

        internal void LogDebug(string message)
        {
            if (EnableDebug) Debug.Log($"[DEBUG]USharpVideoQueue: {message}");
        }

        internal bool isFirstVideoOwner() => queuedByPlayer[0] == getPlayerID(getLocalPlayer());


        /* VRC SDK wrapper functions to enable mocking for tests */

        internal virtual void synchronizeData() => RequestSerialization();
        internal virtual void becomeOwner() => Networking.SetOwner(Networking.LocalPlayer, gameObject);
        internal virtual bool isOwner() => Networking.IsOwner(Networking.LocalPlayer, gameObject);
        internal virtual VRCPlayerApi getLocalPlayer() => Networking.LocalPlayer;
        internal virtual int getPlayerID(VRCPlayerApi player) => player.playerId;

        internal virtual bool isVideoPlayerOwner() =>
            Networking.IsOwner(Networking.LocalPlayer, VideoPlayer.gameObject);

        internal virtual int getCurrentServerTime() => Networking.GetServerTimeInMilliseconds();

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
                    RemoveVideo(i, skipChecks: true);
                }
            }
        }

        /* USharpVideoQueue Emitted Callbacks */

        internal void OnQueueContentChange()
        {
            SendCallback(OnUSharpVideoQueueContentChangeEvent);
        }

        /* USharpVideoPlayer Event Callbacks */

        public virtual void OnUSharpVideoEnd()
        {
            LogDebug($"Received USharpVideoEnd! Is player Video Player owner? {isVideoPlayerOwner()}");
            if (isVideoPlayerOwner())
            {
                skipToNextVideo();
            }
        }

        public void OnUSharpVideoError()
        {
            LogDebug($"Received USharpVideoError! Is player Video Player owner? {isVideoPlayerOwner()}");
            VideoPlayerIsLoading = false;
            if (isVideoPlayerOwner())
            {
                QueueCallbackEvent(OnUSharpVideoQueueSkippedError);
                skipToNextVideo();
            }
        }

        public void OnUSharpVideoLoadStart()
        {
            VideoPlayerIsLoading = true;
        }

        public void OnUSharpVideoPlay()
        {
            VideoPlayerIsLoading = false;
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

        internal virtual void SendCallback(string callbackName)
        {
            foreach (UdonSharpBehaviour callbackReceiver in registeredCallbackReceivers)
            {
                if (!UdonSharpBehaviour.Equals(callbackName, null))
                {
                    callbackReceiver.SendCustomEvent(callbackName);
                    LogDebug($"Sent Callback '{callbackName}'");
                }
            }
        }

        internal virtual void QueueFunctionEvent(string functionName)
        {
            AddDataCriticalEvent("function", functionName, getCurrentServerTime().ToString());
        }

        internal virtual void QueueCallbackEvent(string callbackName)
        {
            AddDataCriticalEvent("callback", callbackName, getCurrentServerTime().ToString());
        }

        internal virtual void AddDataCriticalEvent(string type, string value, string timestamp)
        {
            Debug.Assert(isOwner());
            ShiftBack(dataCriticalEvents);
            string formattedEvent = $"{type}:{value}:{timestamp}";
            dataCriticalEvents[0] = formattedEvent;
            LogDebug($"Queued Data Critical Function Event with content '{formattedEvent}'");
        }

        internal virtual void invokePendingEvents()
        {
            var latestEventTimestamp = eventTimestampThreshold;
            for (int i = Count(dataCriticalEvents) - 1; i >= 0; i--)
            {
                string[] splitEvent = dataCriticalEvents[i].Split(':');

                string type = splitEvent[0];
                string value = splitEvent[1];
                int timestamp = int.Parse(splitEvent[2]);

                if (timestamp > eventTimestampThreshold)
                {
                    LogDebug($"Received DataCriticalEvent {value} with timestamp {timestamp}. " +
                             $"Most recent received event had timestamp {eventTimestampThreshold}");
                    latestEventTimestamp = timestamp;
                    if (type == "function")
                    {
                        SendCustomEvent(value);
                    }
                    else if (type == "callback")
                    {
                        SendCallback(value);
                    }
                }
                else
                {
                    LogDebug(
                        $"Disregarded DataCritical event {value}, because timestamp '{timestamp}'\n occured before most recent event '{eventTimestampThreshold}");
                }
            }

            eventTimestampThreshold = latestEventTimestamp;
        }
    }
}