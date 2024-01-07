using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;
using UnityEngine.Serialization;
using static USharpVideoQueue.Runtime.Utility.QueueArray;
using USharpVideoQueue.Runtime.Utility;

namespace USharpVideoQueue.Runtime
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(-100)]
    public class VideoQueue : UdonSharpBehaviour
    {
        [Tooltip("Total limit for queued videos")]
        [SerializeField]
        internal int maxQueueItems = 8;
        [Tooltip("Enforce limit per user for queued videos")]
        [SerializeField]
        [UdonSynced]
        internal bool videoLimitPerUserEnabled = false;
        [Tooltip("Individual limit per user for queued videos")]
        [SerializeField]
        [UdonSynced]
        internal int videoLimitPerUser = 3;
        [Tooltip("Time to wait between videos")]
        [SerializeField]
        internal int pauseSecondsBetweenVideos = 5;
        [Tooltip("Should Debug messages be written to the log?")]
        [SerializeField]
        internal bool enableDebug = false;
        [Tooltip("The USharpVideoPlayer object that this queue should manage")]
        public USharpVideoPlayer VideoPlayer;


        public const string OnUSharpVideoQueueContentChangeEvent = "OnUSharpVideoQueueContentChange";
        public const string OnUSharpVideoQueuePlayingNextVideo = "OnUSharpVideoQueuePlayingNextVideo";
        public const string OnUSharpVideoQueueSkippedError = "OnUSharpVideoQueueSkippedError";
        public const string OnUSharpVideoQueueVideoEnded = "OnUSharpVideoQueueVideoEnded";
        public const string OnUSharpVideoQueueFinalVideoEnded = "OnUSharpVideoQueueFinalVideoEnded";

        internal UdonSharpBehaviour[] registeredCallbackReceivers;

        [UdonSynced] internal VRCUrl[] queuedVideos;
        [UdonSynced] internal string[] queuedTitles;
        [UdonSynced] internal int[] queuedByPlayer;

        internal int dataCriticalEventSize = 8;
        [UdonSynced] internal string[] dataCriticalEvents;
        internal int eventTimestampThreshold;

        internal bool initialized = false;
        internal int localPlayerId;

        internal readonly string FunctionEventIdentifier = "func";
        internal readonly string CallbackEventIdentifier = "call";

        /// <summary>
        /// Will be true if the player is currently loading a video or 
        /// the queue is waiting for the timespan defined in [PauseSecondsBetweenVideos]. 
        /// While this is true, changes to the first video in queue are disregarded.
        /// </summary>
        public bool WaitingForPlayback { get; private set; }

        internal void Start()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// Should be run by any script that accesses the queue before it was initialized.
        /// </summary>
        public void EnsureInitialized()
        {
            if (initialized) return;

            if (Equals(VideoPlayer, null))
            {
                logError("VideoQueue is missing USharpVideo Player reference! Please check in the inspector!");
            }

            initialized = true;
            WaitingForPlayback = false;
            eventTimestampThreshold = getCurrentServerTime();
            localPlayerId = getPlayerID(getLocalPlayer());

            if (registeredCallbackReceivers == null)
            {
                registeredCallbackReceivers = new UdonSharpBehaviour[0];
            }

            dataCriticalEvents = new string[dataCriticalEventSize];

            for (int i = 0; i < dataCriticalEventSize; i++)
            {
                dataCriticalEvents[i] = string.Empty;
            }

            queuedVideos = new VRCUrl[maxQueueItems];
            queuedByPlayer = new int[maxQueueItems];
            queuedTitles = new string[maxQueueItems];
            VideoPlayer.RegisterCallbackReceiver(this);

            for (int i = 0; i < maxQueueItems; i++)
            {
                queuedVideos[i] = VRCUrl.Empty;
                queuedTitles[i] = string.Empty;
                queuedByPlayer[i] = -1;
            }
        }


        /// <summary>
        /// Adds a VRCUrl to the queue if the queue is not full and the user limit would not be exceeded. Uses the url string as title.
        /// </summary>
        /// <param name="url"></param>
        public void QueueVideo(VRCUrl url)
        {
            if (url == null) return;
            QueueVideo(url, url.Get());
        }

        /// <summary>
        /// Adds a VRCUrl to the queue if the queue is not full and the user limit would not be exceeded.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="title"></param>
        public void QueueVideo(VRCUrl url, string title)
        {
            if (url == null || !Validation.ValidateURL(url.Get()))
            {
                logWarning($"Video with title '{title}' was not queued because the URL format was invalid!");
                return;
            }

            if (!IsLocalPlayerPermittedToQueueVideo())
            {
                logWarning($"Video with title '{title}' was not queued because the video limit per user was reached!");
                return;
            }

            bool wasEmpty = IsEmpty(queuedVideos);
            ensureOwnership();
            enqueueVideoData(url, title);
            invokeEventsAndSynchronize();
            if (wasEmpty) playFirst();
        }


        /// <summary>
        /// Removes the video at [index] from the queue if the user has permission to do so. 
        /// This is the case if the user has queued the video themselves or they have elevated rights.
        /// </summary>
        /// <param name="index"></param>
        public void RequestRemoveVideo(int index)
        {
            //Check if user is allowed to remove video
            if (!IsLocalPlayerPermittedToRemoveVideo(index)) return;

            if (index != 0)
            {
                removeVideo(index);
                return;
            }

            // video with index 0 is only allowed to be removed when it is currently loading to prevent player inconsitencies.
            if (!WaitingForPlayback) skipToNextVideo();

        }

        internal void removeVideo(int index)
        {
            if (index == 0)
            {
                skipToNextVideo();
                return;
            }
            ensureOwnership();
            removeVideoData(index);
            invokeEventsAndSynchronize();
        }


        internal void skipToNextVideo()
        {
            if (IsEmpty(queuedVideos)) return;

            ensureOwnership();
            removeVideoData(0);
            clearVideoPlayer();
            if (IsEmpty(queuedVideos))
            {
                QueueCallbackEvent(OnUSharpVideoQueueFinalVideoEnded);
            }
            else
            {
                QueueCallbackEvent(OnUSharpVideoQueueVideoEnded);
                QueueFunctionEvent(nameof(SchedulePlayFirstAfterPauseIfVideoOwner));
            }

            invokeEventsAndSynchronize();
        }

        public void SchedulePlayFirstAfterPauseIfVideoOwner()
        {
            WaitingForPlayback = true;
            if (isFirstVideoOwner())
            {
                SendCustomEventDelayedSeconds(nameof(playFirst), pauseSecondsBetweenVideos);
            }
        }

        /// <summary>
        /// Returns the count of currently queued videos.
        /// </summary>
        public int QueuedVideosCount()
        {
            return Count(queuedVideos);
        }

        /// <summary>
        /// Returns the VRCUrl currently queued at [index]. Returns VRCUrl.Empty if [index] not valid.
        /// </summary>
        public VRCUrl GetURL(int index)
        {
            if (!isIndexValid(index)) return VRCUrl.Empty;
            return queuedVideos[index];
        }
        /// <summary>
        /// Returns the title of the video currently queued at [index]. Returns an empty string if [index] is not valid. 
        /// </summary>
        public string GetTitle(int index)
        {
            if (!isIndexValid(index)) return string.Empty;
            return queuedTitles[index];
        }

        /// <summary>
        /// Returns the player id of the player who queued the video at [index]. Returns -1 if [index] is not valid. 
        /// </summary>
        public int GetVideoOwner(int index)
        {
            if (!isIndexValid(index)) return -1;
            return queuedByPlayer[index];
        }

        /// <summary>
        /// Returns whether the local player is permitted to remove the video at [index].
        /// This is the case if the user has queued the video themselves or they have elevated rights. 
        /// </summary>
        public bool IsLocalPlayerPermittedToRemoveVideo(int index)
        {
            if (localPlayerHasElevatedRights()) return true;
            return GetVideoOwner(index) == localPlayerId;
        }

        /// <summary>
        /// Returns whether the local player is permitted queue another video.
        /// This is the case if the [VideoLimitPerUser] was not reached yet or if they have elevated rights.
        /// </summary>
        public bool IsLocalPlayerPermittedToQueueVideo()
        {
            if (localPlayerHasElevatedRights() || !videoLimitPerUserEnabled) return true;
            return QueuedVideosCountByUser(localPlayerId) < videoLimitPerUser;
        }

        /// <summary>
        /// Returns the count of all videos currently queued by the player with the id [playerID].
        /// </summary>
        public int QueuedVideosCountByUser(int playerID)
        {
            int videoCount = 0;
            for (int i = 0; i < QueuedVideosCount(); i++)
            {
                if (queuedByPlayer[i] == playerID) videoCount++;
            }

            return videoCount;
        }

        /// <summary>
        /// Sets the queued video limit for players without elevated rights. Requires elevated rights.
        /// </summary>
        public void SetVideoLimitPerUser(int limit)
        {
            if (!localPlayerHasElevatedRights()) return;
            ensureOwnership();
            videoLimitPerUser = limit;
            synchronizeData();
        }

        /// <summary>
        /// Sets whether the limit for players without elevated rights should be enforced. Requires elevated rights.
        /// </summary>
        public void SetVideoLimitPerUserEnabled(bool enabled)
        {
            if (!localPlayerHasElevatedRights()) return;
            ensureOwnership();
            videoLimitPerUserEnabled = enabled;
            synchronizeData();
        }

        public override void OnDeserialization()
        {
            logDebug("OnDeserialization run!");
            invokePendingEvents();
            OnQueueContentChange();
        }

        public override void OnPreSerialization()
        {
            logDebug("Sending Serialized Data!");
        }

        internal void enqueueVideoData(VRCUrl url, string title)
        {
            Enqueue(queuedVideos, url);
            Enqueue(queuedTitles, title);
            Enqueue(queuedByPlayer, localPlayerId);
            OnQueueContentChange();
        }

        internal void removeVideoData(int index)
        {
            if (index >= QueuedVideosCount()) return;

            Remove(queuedVideos, index);
            Remove(queuedTitles, index);
            Remove(queuedByPlayer, index);
            OnQueueContentChange();
        }

        internal void invokeEventsAndSynchronize()
        {
            Debug.Assert(isOwner());
            invokePendingEvents();
            synchronizeData();
        }

        //should be considered internal, must be public to be called by SendCustomEventDelayedSeconds
        public void playFirst()
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

        /// <summary>
        /// Override this function to integrate with other permission systems!
        /// </summary>
        internal virtual bool localPlayerHasElevatedRights()
        {
            return isMaster();
        }

        internal bool isIndexValid(int index)
        {
            return index < QueuedVideosCount() && index >= 0;
        }

        internal bool isFirstVideoOwner() => queuedByPlayer[0] == localPlayerId;

        internal void logDebug(string message)
        {
            if (enableDebug) Debug.Log($"[DEBUG]USharpVideoQueue: {message}");
        }

        internal void logWarning(string message)
        {
            Debug.LogWarning($"[WARNING]USharpVideoQueue: {message}");
        }

        internal void logError(string message)
        {
            Debug.LogError($"[ERROR]USharpVideoQueue: {message}");
        }

        /* VRC SDK wrapper functions to enable mocking for tests */

        internal virtual bool isMaster() => Networking.IsMaster;
        internal virtual void synchronizeData() => RequestSerialization();
        internal virtual void becomeOwner() => Networking.SetOwner(Networking.LocalPlayer, gameObject);
        internal virtual bool isOwner() => Networking.IsOwner(Networking.LocalPlayer, gameObject);
        internal virtual VRCPlayerApi getLocalPlayer() => Networking.LocalPlayer;
        internal virtual int getPlayerID(VRCPlayerApi player) => player.playerId;

        internal virtual bool isVideoPlayerOwner() =>
            Networking.IsOwner(Networking.LocalPlayer, VideoPlayer.gameObject);

        internal virtual int getCurrentServerTime() => Networking.GetServerTimeInMilliseconds();

        internal virtual void SendCustomEventDelayedSeconds(string name, int delay) =>
            base.SendCustomEventDelayedSeconds(name, delay);

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
                    removeVideo(i);
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
            logDebug($"Received USharpVideoEnd! Is player Video Player owner? {isVideoPlayerOwner()}");
            if (isVideoPlayerOwner())
            {
                skipToNextVideo();
            }
        }

        public void OnUSharpVideoError()
        {
            logDebug($"Received USharpVideoError! Is player Video Player owner? {isVideoPlayerOwner()}");
            WaitingForPlayback = false;
            if (isVideoPlayerOwner())
            {
                QueueCallbackEvent(OnUSharpVideoQueueSkippedError);
                skipToNextVideo();
            }
        }

        public void OnUSharpVideoLoadStart()
        {
            WaitingForPlayback = true;
        }

        public void OnUSharpVideoPlay()
        {
            WaitingForPlayback = false;
            SendCallback(OnUSharpVideoQueuePlayingNextVideo);
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
                if (callbackReceiver == currReceiver
                    //Type-check is necessary with mocked receivers in tests
#if UNITY_EDITOR
                    && callbackReceiver.GetType() == currReceiver.GetType()
#endif
                   )
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

        [RecursiveMethod]
        internal virtual void SendCallback(string callbackName)
        {
            foreach (UdonSharpBehaviour callbackReceiver in registeredCallbackReceivers)
            {
                if (!UdonSharpBehaviour.Equals(callbackName, null))
                {
                    callbackReceiver.SendCustomEvent(callbackName);
                    logDebug($"Sent Callback '{callbackName}'");
                }
            }
        }

        internal virtual void QueueFunctionEvent(string functionName)
        {
            AddDataCriticalEvent(FunctionEventIdentifier, functionName, getCurrentServerTime().ToString());
        }

        internal virtual void QueueCallbackEvent(string callbackName)
        {
            AddDataCriticalEvent(CallbackEventIdentifier, callbackName, getCurrentServerTime().ToString());
        }

        internal virtual void AddDataCriticalEvent(string type, string value, string timestamp)
        {
            ShiftBack(dataCriticalEvents);
            string formattedEvent = $"{type}:{value}:{timestamp}";
            dataCriticalEvents[0] = formattedEvent;
            logDebug($"Queued Data Critical Function Event with content '{formattedEvent}'");
        }

        [RecursiveMethod]
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
                    logDebug($"Received DataCriticalEvent {value} with timestamp {timestamp}. " +
                             $"Most recent received event had timestamp {eventTimestampThreshold}");
                    latestEventTimestamp = timestamp;
                    if (type == FunctionEventIdentifier)
                    {
                        SendCustomEvent(value);
                    }
                    else if (type == CallbackEventIdentifier)
                    {
                        SendCallback(value);
                    }
                }
                else
                {
                    logDebug(
                        $"Disregarded DataCritical event {value}, because timestamp '{timestamp}'\n occured before most recent event '{eventTimestampThreshold}");
                }
            }

            eventTimestampThreshold = latestEventTimestamp;
        }
    }
}