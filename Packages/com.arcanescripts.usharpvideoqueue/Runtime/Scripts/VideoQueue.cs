using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp.Video;
using static USharpVideoQueue.Runtime.Utility.QueueArray;
using USharpVideoQueue.Runtime.Utility;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

namespace USharpVideoQueue.Runtime
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DefaultExecutionOrder(-100)]
    public class VideoQueue : UdonSharpBehaviour
    {
        [Tooltip("Total limit for queued videos")] [SerializeField]
        internal int maxQueueItems = 8;

        [Tooltip("Enforce limit per user for queued videos")] [SerializeField] [UdonSynced]
        internal bool videoLimitPerUserEnabled = false;

        // This field is not enforced by the queue, but must be respected by UI elements which
        // add videos to the queue. IsLocalPlayerPermittedToQueueCustomVideos() should be used.
        [Tooltip("Allow users to enter custom urls.")] [SerializeField] [UdonSynced]
        internal bool customUrlInputEnabled = false;

        [Tooltip("Individual limit per user for queued videos")] [SerializeField] [UdonSynced]
        internal int videoLimitPerUser = 3;

        [Tooltip("Time to wait between videos")] [SerializeField]
        internal int pauseSecondsBetweenVideos = 5;

        [Tooltip("Should Debug messages be written to the log?")] [SerializeField]
        internal bool enableDebug = false;

        [Tooltip("The USharpVideoPlayer object that this queue should manage")]
        public USharpVideoPlayer VideoPlayer;

        public const string OnUSharpVideoQueueContentChangeEvent = "OnUSharpVideoQueueContentChange";
        public const string OnUSharpVideoQueuePlayingNextVideo = "OnUSharpVideoQueuePlayingNextVideo";
        public const string OnUSharpVideoQueueSkippedError = "OnUSharpVideoQueueSkippedError";
        public const string OnUSharpVideoQueueVideoEnded = "OnUSharpVideoQueueVideoEnded";
        public const string OnUSharpVideoQueueFinalVideoEnded = "OnUSharpVideoQueueFinalVideoEnded";
        public const string OnUSharpVideoQueueCleared = "OnUSharpVideoQueueCleared";
        public const string OnUSharpVideoQueueCurrentVideoRemoved = "OnUSharpVideoQueueCurrentVideoRemoved";
        public const string OnUSharpVideoQueueCustomURLsEnabled = "OnUSharpVideoQueueCustomURLsEnabled";
        public const string OnUSharpVideoQueueCustomURLsDisabled = "OnUSharpVideoQueueCustomURLsDisabled";
        public const string OnUSharpVideoQueueVideoLimitPerUserChanged = "OnUSharpVideoQueueVideoLimitPerUserChanged";

        internal UdonSharpBehaviour[] registeredCallbackReceivers;

        [UdonSynced] internal VRCUrl[] queuedVideos;
        [UdonSynced] internal string[] queuedTitles;
        [UdonSynced] internal int[] queuedByPlayer;

        [UdonSynced] internal bool _videoOwnerIsWaitingForPlayback = false;

        internal bool initialized = false;
        internal int localPlayerId;


        /// <summary>
        /// Will be true if the player is currently loading a video or 
        /// the queue is waiting for the timespan defined in [PauseSecondsBetweenVideos]. 
        /// While this is true, changes to the first video in queue are disregarded.
        /// </summary>
        public bool VideoOwnerIsWaitingForPlayback
        {
            get => _videoOwnerIsWaitingForPlayback;
            private set => _videoOwnerIsWaitingForPlayback = value;
        }

        protected internal virtual void Start()
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
            localPlayerId = getPlayerID(getLocalPlayer());

            if (registeredCallbackReceivers == null)
            {
                registeredCallbackReceivers = new UdonSharpBehaviour[0];
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

            logDebug(
                $"USharpVideoQueue initialized! Local Player ID is {localPlayerId}. You are {(isOwner() ? "" : "not ")}the owner!");
        }

        // Request Sending Methods

        public void QueueVideo(VRCUrl url) => QueueVideo(url, url.Get());

        public void QueueVideo(VRCUrl url, string title) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnQueueVideoRequested),
                localPlayerId, url, title);

        public void Clear() =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnClearRequested), localPlayerId);

        public void MoveVideo(int index, bool directionUp) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnMoveVideoRequested),
                localPlayerId, index, directionUp);

        public void RemoveVideo(int index) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnRemoveVideoRequested),
                localPlayerId, index);

        public void SetVideoLimitPerUser(int limit) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnSetVideoLimitPerUserRequested),
                localPlayerId, limit);

        public void SetVideoLimitPerUserEnabled(bool enabled) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnSetVideoLimitPerUserEnabledRequested),
                localPlayerId, enabled);

        public void SetCustomUrlInputEnabled(bool enabled) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnSetCustomUrlInputEnabledRequested),
                localPlayerId, enabled);


        // Request Executing Methods

        [NetworkCallable]
        public void OnQueueVideoRequested(int playerID, VRCUrl url, string title)
        {
            logDebug($"OnQueueVideoRequested from Player {playerID}: {title} ({url})");

            if (url == null || !Validation.ValidateURL(url.Get()))
            {
                logWarning($"Video with title '{title}' was not queued because the URL format was invalid!");
                return;
            }

            bool wasEmpty = IsEmpty(queuedVideos);
            enqueueVideoData(url, title, playerID);
            if (wasEmpty) MakePlayerPlayFirst();
        }

        [NetworkCallable]
        public void OnClearRequested(int playerID)
        {
            logDebug($"OnClearRequested from Player {playerID}");

            QueueArray.Clear(queuedVideos);
            QueueArray.Clear(queuedTitles);
            QueueArray.Clear(queuedByPlayer);
            synchronizeData();
            clearVideoPlayer();

            //TODO: QueueCallbackEvent(OnUSharpVideoQueueCleared); 
        }

        [NetworkCallable]
        public void OnMoveVideoRequested(int playerID, int index, bool directionUp)
        {
            logDebug(
                $"OnMoveVideoRequested from Player {playerID}: Index {index}, Move {(directionUp ? "Up" : "Down")}");

            if (directionUp) moveUpVideoData(index);
            else moveDownVideoData(index);
        }

        [NetworkCallable]
        public void OnRemoveVideoRequested(int playerID, int index)
        {
            logDebug($"OnRemoveVideoRequested from Player {playerID}: Index {index}");
            removeVideo(index);
        }


        [NetworkCallable]
        public void OnSetVideoLimitPerUserRequested(int playerID, int limit)
        {
            logDebug($"OnSetVideoLimitPerUserRequested from Player {playerID}: Limit = {limit}");

            if (limit < 0) return;
            videoLimitPerUser = limit;
            synchronizeData();
            //QueueCallbackEvent(OnUSharpVideoQueueVideoLimitPerUserChanged);
        }

        [NetworkCallable]
        public void OnSetVideoLimitPerUserEnabledRequested(int playerID, bool enabled)
        {
            logDebug($"OnSetVideoLimitPerUserEnabledRequested from Player {playerID}: Enabled = {enabled}");

            videoLimitPerUserEnabled = enabled;
            synchronizeData();
            //QueueCallbackEvent(OnUSharpVideoQueueVideoLimitPerUserChanged);
        }

        [NetworkCallable]
        public void OnSetCustomUrlInputEnabledRequested(int playerID, bool enabled)
        {
            logDebug($"OnSetCustomUrlInputEnabledRequested from Player {playerID}: Enabled = {enabled}");

            customUrlInputEnabled = enabled;
            synchronizeData();
            //QueueCallbackEvent(enabled ? OnUSharpVideoQueueCustomURLsEnabled : OnUSharpVideoQueueCustomURLsDisabled);
        }

        // Player Coordination

        public void MakePlayerPlayFirst()
        {
            if (QueuedVideosCount() == 0) return;

            VRCUrl nextURL = (VRCUrl)First(queuedVideos);
            int videoOwnerPlayerID = (int)First(queuedByPlayer);

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(InvokeUserPlay),
                videoOwnerPlayerID, nextURL);
        }
        
        public void SkipToNextVideo(bool force = false)
        {
            if (VideoOwnerIsWaitingForPlayback && !force)
            {
                logWarning("Couldn't remove first video because video owner is loading!");
                return;
            }

            removeVideoData(0);
            
            if(IsEmpty(queuedVideos)) clearVideoPlayer();
            else MakePlayerPlayFirst();
            //TODO: QueueCallbackEvent(OnUSharpVideoQueueCurrentVideoRemoved);   
        }

        [NetworkCallable]
        public void InvokeUserPlay(int playerID, VRCUrl url)
        {
            logDebug($"OnUserPlayURLInvoked sent to Player {playerID}: {url.Get()}");

            if (localPlayerId != playerID) return;

            VideoPlayer.PlayVideo(url);
        }

        [NetworkCallable]
        public void OnVideoOwnerVideoEnd(int playerID)
        {
            logDebug($"OnVideoOwnerVideoEnd received from Player {playerID}");
            SkipToNextVideo();
        }

        [NetworkCallable]
        public void OnVideoOwnerVideoError(int playerID)
        {
            logDebug($"OnVideoOwnerVideoError received from Player {playerID}");
            VideoOwnerIsWaitingForPlayback = false;
            synchronizeData();
            SkipToNextVideo();
        }

        [NetworkCallable]
        public void OnVideoOwnerVideoLoadStart(int playerID)
        {
            logDebug($"OnVideoOwnerVideoLoadStart received from Player {playerID}");
            VideoOwnerIsWaitingForPlayback = true;
            // TODO: Timeout?
            synchronizeData();
        }

        [NetworkCallable]
        public void OnVideoOwnerVideoPlay(int playerID)
        {
            logDebug($"OnVideoOwnerVideoPlay received from Player {playerID}");
            VideoOwnerIsWaitingForPlayback = false;
            synchronizeData();
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
        public bool IsPlayerPermittedToRemoveVideo(int playerID, int index)
        {
            if (playerWithIDHasElevatedRights(playerID)) return true;
            return GetVideoOwner(index) == playerID;
        }

        /// <summary>
        /// Returns whether the local player is permitted and able to move video with index up or down in the queue.
        /// </summary>
        public bool IsPlayerAbleToMoveVideo(int playerID, int index, bool directionUp)
        {
            if (!playerWithIDHasElevatedRights(playerID)) return false;

            // Index constrains moving upwards
            if (directionUp && (index > QueuedVideosCount() - 1 || index <= 0)) return false;

            //Index constrains moving downwards
            if (!directionUp && (index >= QueuedVideosCount() - 1 || index < 0)) return false;

            //Prevent moving the playing video
            if (directionUp && index == 1 || !directionUp && index == 0) return false;

            return true;
        }

        /// <summary>
        /// Returns whether the local player is permitted queue another video.
        /// This is the case if the [VideoLimitPerUser] was not reached yet or if they have elevated rights.
        /// </summary>
        public bool IsPlayerPermittedToQueueVideo(int playerID)
        {
            if (playerWithIDHasElevatedRights(playerID) || !videoLimitPerUserEnabled) return true;
            return QueuedVideosCountByUser(playerID) < videoLimitPerUser;
        }


        /// <summary>
        /// Returns whether the local player is permitted to add custom video links to the queue.
        /// This check is not affected by the [VideoLimitPerUser]
        /// </summary>
        public bool IsPlayerPermittedToQueueCustomVideos(int playerID)
        {
            return playerWithIDHasElevatedRights(playerID) || customUrlInputEnabled;
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

        public int GetVideoLimitPerUser() => videoLimitPerUser;


        public override void OnDeserialization()
        {
            logDebug("OnDeserialization run!");
            OnQueueContentChange();
        }

        public override void OnPreSerialization()
        {
            logDebug("Sending Serialized Data!");
        }

        internal void enqueueVideoData(VRCUrl url, string title, int playerId)
        {
            Enqueue(queuedVideos, url);
            Enqueue(queuedTitles, title);
            Enqueue(queuedByPlayer, playerId);
            synchronizeData();
        }

        internal void removeVideo(int index, bool force = false)
        {
            if (index == 0)
                SkipToNextVideo(force);
            else
                removeVideoData(index);
        }

        internal void removeVideoData(int index)
        {
            if (index >= QueuedVideosCount()) return;

            Remove(queuedVideos, index);
            Remove(queuedTitles, index);
            Remove(queuedByPlayer, index);
            synchronizeData();
        }

        internal void moveUpVideoData(int index)
        {
            MoveUp(queuedVideos, index);
            MoveUp(queuedTitles, index);
            MoveUp(queuedByPlayer, index);
            synchronizeData();
        }

        internal void moveDownVideoData(int index)
        {
            MoveDown(queuedVideos, index);
            MoveDown(queuedTitles, index);
            MoveDown(queuedByPlayer, index);
            synchronizeData();
        }


        internal void clearVideoPlayer()
        {
            VideoPlayer.TakeOwnership();
            VideoPlayer.StopVideo();
        }

        internal void removeVideosOfPlayerWhoLeft(int leftPlayerID)
        {
            for (int i = Count(queuedVideos) - 1; i >= 0; i--)
            {
                int videoOwnerPlayerID = GetVideoOwner(i);
                //VRChat is inconsistent with the VRCPlayerApi objects of players who just left (sometimes valid, sometimes null)
                //This why we check against both the validity of the video owner VRCPlayerApi object and their ID.
                if (videoOwnerPlayerID == leftPlayerID || !isPlayerWithIDValid(videoOwnerPlayerID))
                {
                    logDebug(
                        $"Removing video {queuedTitles[i]} with URL {queuedVideos[i]} because owner with Player-ID {leftPlayerID} has left the instance!");
                    removeVideo(i, true);
                }
            }
        }

        /// <summary>
        /// Override this function to integrate with other permission systems!
        /// </summary>
        protected virtual bool playerWithIDHasElevatedRights(int id)
        {
            if (!isPlayerWithIDValid(id)) return false;
            return VRCPlayerApi.GetPlayerById(id).isMaster;
        }

        internal bool isIndexValid(int index)
        {
            return index < QueuedVideosCount() && index >= 0;
        }

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

        internal virtual void synchronizeData()
        {
            Debug.Assert(isOwner());
            RequestSerialization();
            OnQueueContentChange();
        }

        internal virtual void becomeOwner() => Networking.SetOwner(Networking.LocalPlayer, gameObject);
        internal virtual bool isOwner() => Networking.IsOwner(Networking.LocalPlayer, gameObject);
        internal virtual VRCPlayerApi getLocalPlayer() => Networking.LocalPlayer;
        internal virtual int getPlayerID(VRCPlayerApi player) => player.playerId;

        internal virtual bool isPlayerWithIDValid(int id) => Utilities.IsValid(VRCPlayerApi.GetPlayerById(id));

        internal virtual bool isVideoPlayerOwner() =>
            Networking.IsOwner(Networking.LocalPlayer, VideoPlayer.gameObject);

        /* VRC Runtime Events */

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!isOwner()) return; 
            removeVideosOfPlayerWhoLeft(getPlayerID(player));
        }

        /* USharpVideoQueue Emitted Callbacks */

        protected internal void OnQueueContentChange()
        {
            SendCallback(OnUSharpVideoQueueContentChangeEvent);
        }

        /* USharpVideoPlayer Event Callbacks */

        public virtual void OnUSharpVideoEnd()
        {
            logDebug($"Received USharpVideoEnd! Is player Video Player owner? {isVideoPlayerOwner()}");
            if (isVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnVideoOwnerVideoEnd), localPlayerId);
        }

        public void OnUSharpVideoError()
        {
            logDebug($"Received USharpVideoError! Is player Video Player owner? {isVideoPlayerOwner()}");
            if (isVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnVideoOwnerVideoError),
                    localPlayerId);
        }

        public void OnUSharpVideoLoadStart()
        {
            logDebug($"Received USharpVideoLoadStart! Is player Video Player owner? {isVideoPlayerOwner()}");
            if (isVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnVideoOwnerVideoLoadStart),
                    localPlayerId);
        }

        public void OnUSharpVideoPlay()
        {
            logDebug($"Received USharpVideoPlay! Is player Video Player owner? {isVideoPlayerOwner()}");
            if (isVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnVideoOwnerVideoPlay),
                    localPlayerId);
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
    }
}