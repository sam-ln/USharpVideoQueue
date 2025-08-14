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

        internal UdonSharpBehaviour[] registeredCallbackReceivers = new UdonSharpBehaviour[0];

        [UdonSynced] internal VRCUrl[] queuedVideos;
        [UdonSynced] internal string[] queuedTitles;
        [UdonSynced] internal int[] queuedByPlayer;

        [UdonSynced] internal bool _videoOwnerIsWaitingForPlayback = false;

        internal bool initialized = false;
        internal int localPlayerId;


        /// <summary>
        /// Will be true if the player is currently loading a video or 
        /// the queue is waiting for the timespan defined in <see cref="pauseSecondsBetweenVideos"/>. 
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
        /// Initializes internal state and registers this queue with the assigned <see cref="VideoPlayer"/>.
        /// Call this before using the queue; Safe to call multiple times.
        /// </summary>
        public void EnsureInitialized()
        {
            if (initialized) return;

            if (Equals(VideoPlayer, null))
            {
                _LogError(
                    "Critical! VideoQueue is missing USharpVideo Player reference! Please check in the inspector!");
                return;
            }

            initialized = true;
            localPlayerId = _GetPlayerID(_GetLocalPlayer());

            if (queuedVideos == null) queuedVideos = new VRCUrl[maxQueueItems];
            if (queuedByPlayer == null) queuedByPlayer = new int[maxQueueItems];
            if (queuedTitles == null) queuedTitles = new string[maxQueueItems];
            VideoPlayer.RegisterCallbackReceiver(this);

            for (int i = 0; i < maxQueueItems; i++)
            {
                queuedVideos[i] = VRCUrl.Empty;
                queuedTitles[i] = string.Empty;
                queuedByPlayer[i] = -1;
            }

            _LogDebug(
                $"USharpVideoQueue initialized! Local Player ID is {localPlayerId}. You are {(_IsOwner() ? "" : "not ")}the owner!");
        }

        // Request Sending Methods

        /// <summary>
        /// Queues a video by URL with a custom title. The request is sent to the object owner over the network.
        /// Will use the URL as the title of the video as well.
        /// </summary>
        /// <param name="url">The video URL to enqueue.</param>
        public void QueueVideo(VRCUrl url) => QueueVideo(url, url.Get());

        /// <summary>
        /// Queues a video by URL with a custom title. The request is sent to the object owner over the network.
        /// </summary>
        /// <param name="url">The video URL to enqueue.</param>
        /// <param name="title">Display title to associate with the URL.</param>
        public void QueueVideo(VRCUrl url, string title) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnQueueVideoRequested),
                localPlayerId, url, title);

        /// <summary>
        /// Clears the entire queue. The request is sent to the owner and will only be applied if the sender has elevated rights.
        /// </summary>
        public void Clear() =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnClearRequested), localPlayerId);

        /// <summary>
        /// Moves a queued video up or down within the list. The request is sent to the object owner over the network.
        /// </summary>
        /// <param name="index">Zero-based index of the video to move.</param>
        /// <param name="directionUp">True to move up; false to move down.</param>
        public void MoveVideo(int index, bool directionUp) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnMoveVideoRequested),
                localPlayerId, index, directionUp);

        /// <summary>
        /// Removes a video from the queue. The request is sent to the object owner over the network and will execute
        /// if the sending user has permission to remove the video.
        /// </summary>
        /// <param name="index">Zero-based index of the video to remove.</param>
        public void RemoveVideo(int index) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnRemoveVideoRequested),
                localPlayerId, index);

        /// <summary>
        /// Sets the per-user queue limit. The request is sent to the owner and will only be applied if the sender has elevated rights.
        /// </summary>
        /// <param name="limit">Maximum number of videos each user may queue (non-negative).</param>
        public void SetVideoLimitPerUser(int limit) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnSetVideoLimitPerUserRequested),
                localPlayerId, limit);

        /// <summary>
        /// Toggles enforcement of the per-user queue limit. The request is sent to the owner and will only be applied
        /// if the sender has elevated rights.
        /// </summary>
        /// <param name="enabled">True to enforce per-user limits; false to disable enforcement.</param>
        public void SetVideoLimitPerUserEnabled(bool enabled) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnSetVideoLimitPerUserEnabledRequested),
                localPlayerId, enabled);

        /// <summary>
        /// Enables or disables custom URL input. The request is sent to the owner and will only be applied if the
        /// sender has elevated rights.
        /// </summary>
        /// <param name="enabled">True to allow custom URL input; false to disallow.</param>
        public void SetCustomUrlInputEnabled(bool enabled) =>
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnSetCustomUrlInputEnabledRequested),
                localPlayerId, enabled);


        // Request Executing Methods

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side handler for incoming queue requests.
        /// Validates the URL, checks permissions and capacity, enqueues the video, and starts playback if queue was empty.
        /// </summary>
        /// <param name="playerID">Network player ID of the requester.</param>
        /// <param name="url">URL to enqueue.</param>
        /// <param name="title">Associated display title.</param>
        [NetworkCallable]
        public void RPC_OnQueueVideoRequested(int playerID, VRCUrl url, string title)
        {
            _LogDebug($"OnQueueVideoRequested from Player {playerID}: {title} ({url})", true);

            if (url == null || !Validation.ValidateURL(url.Get()))
            {
                _LogWarning(
                    $"Video with title '{title}', requested by player with ID {playerID}, was not queued because the URL format was invalid!",
                    true);
                return;
            }

            if (!IsPlayerPermittedToQueueVideo(playerID))
            {
                _LogWarning(
                    $"Video with title '{title}', requested by player with ID {playerID}, was not queued because the player reached their personal limit!",
                    true);
                return;
            }

            if (Count(queuedVideos) >= maxQueueItems)
            {
                _LogWarning(
                    $"Video with title '{title}', requested by player with ID {playerID}, was not queued because the queue is full!",
                    true);
                return;
            }

            bool wasEmpty = IsEmpty(queuedVideos);
            _EnqueueVideoData(url, title, playerID);
            if (wasEmpty) _MakePlayerPlayFirst();
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side handler to clear the queue. Only executes if the requester has elevated rights.
        /// </summary>
        /// <param name="playerID">Network player ID of the requester.</param>
        [NetworkCallable]
        public void RPC_OnClearRequested(int playerID)
        {
            _LogDebug($"OnClearRequested from Player {playerID}", true);

            if (!_PlayerWithIDHasElevatedRights(playerID))
            {
                _LogRequestDenied(nameof(Clear), playerID);
                return;
            }

            QueueArray.Clear(queuedVideos);
            QueueArray.Clear(queuedTitles);
            QueueArray.Clear(queuedByPlayer);
            _SynchronizeData();
            _ClearVideoPlayer();

            SendCallback(OnUSharpVideoQueueCleared, true);
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side handler to move a video within the queue.
        /// </summary>
        /// <param name="playerID">Network player ID of the requester.</param>
        /// <param name="index">Index of the video to move.</param>
        /// <param name="directionUp">True to move up; false to move down.</param>
        [NetworkCallable]
        public void RPC_OnMoveVideoRequested(int playerID, int index, bool directionUp)
        {
            _LogDebug(
                $"OnMoveVideoRequested from Player {playerID}: Index {index}, Move {(directionUp ? "Up" : "Down")}",
                true);

            if (!IsPlayerAbleToMoveVideo(playerID, index, directionUp))
            {
                _LogRequestDenied(nameof(MoveVideo), playerID);
                return;
            }

            if (directionUp) _MoveUpVideoData(index);
            else _MoveDownVideoData(index);
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side handler to remove a video from the queue.
        /// </summary>
        /// <param name="playerID">Network player ID of the requester.</param>
        /// <param name="index">Index of the video to remove.</param>
        [NetworkCallable]
        public void RPC_OnRemoveVideoRequested(int playerID, int index)
        {
            _LogDebug($"OnRemoveVideoRequested from Player {playerID}: Index {index}", true);
            if (!IsPlayerPermittedToRemoveVideo(playerID, index))
            {
                _LogRequestDenied(nameof(RemoveVideo), playerID);
                return;
            }

            _RemoveVideo(index);
        }


        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side handler to change the per-user queue limit.
        /// </summary>
        /// <param name="playerID">Network player ID of the requester.</param>
        /// <param name="limit">New non-negative per-user limit.</param>
        [NetworkCallable]
        public void RPC_OnSetVideoLimitPerUserRequested(int playerID, int limit)
        {
            _LogDebug($"OnSetVideoLimitPerUserRequested from Player {playerID}: Limit = {limit}", true);

            if (!_PlayerWithIDHasElevatedRights(playerID))
            {
                _LogRequestDenied(nameof(SetVideoLimitPerUser), playerID);
                return;
            }

            if (limit < 0) return;
            videoLimitPerUser = limit;
            _SynchronizeData();
            SendCallback(OnUSharpVideoQueueVideoLimitPerUserChanged, true);
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side handler to enable or disable per-user queue limits.
        /// </summary>
        /// <param name="playerID">Network player ID of the requester.</param>
        /// <param name="enabled">True to enable; false to disable.</param>
        [NetworkCallable]
        public void RPC_OnSetVideoLimitPerUserEnabledRequested(int playerID, bool enabled)
        {
            _LogDebug($"OnSetVideoLimitPerUserEnabledRequested from Player {playerID}: Enabled = {enabled}", true);

            if (!_PlayerWithIDHasElevatedRights(playerID))
            {
                _LogRequestDenied(nameof(SetVideoLimitPerUserEnabled), playerID);
                return;
            }

            videoLimitPerUserEnabled = enabled;
            _SynchronizeData();
            SendCallback(OnUSharpVideoQueueVideoLimitPerUserChanged, true);
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side handler to enable or disable custom URL input.
        /// </summary>
        /// <param name="playerID">Network player ID of the requester.</param>
        /// <param name="enabled">True to enable; false to disable.</param>
        [NetworkCallable]
        public void RPC_OnSetCustomUrlInputEnabledRequested(int playerID, bool enabled)
        {
            _LogDebug($"OnSetCustomUrlInputEnabledRequested from Player {playerID}: Enabled = {enabled}", true);

            if (!_PlayerWithIDHasElevatedRights(playerID))
            {
                _LogRequestDenied(nameof(SetCustomUrlInputEnabled), playerID);
                return;
            }

            customUrlInputEnabled = enabled;
            _SynchronizeData();
            SendCallback(enabled ? OnUSharpVideoQueueCustomURLsEnabled : OnUSharpVideoQueueCustomURLsDisabled, true);
        }

        // Player Coordination

        /// <summary>
        /// Instructs the owner to have the user who queued the first video start playback on their client.
        /// No-op if the queue is empty.
        /// </summary>
        private void _MakePlayerPlayFirst()
        {
            if (QueuedVideosCount() == 0) return;

            VRCUrl nextURL = (VRCUrl)First(queuedVideos);
            int videoOwnerPlayerID = (int)First(queuedByPlayer);

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(RPC_InvokeUserPlay),
                videoOwnerPlayerID, nextURL);
        }

        /// <summary>
        /// Skips the currently playing video and advances to the next item in the queue.
        /// </summary>
        /// <param name="force">If true, removes the first video even when the owner is currently loading.</param>
        private void _SkipToNextVideo(bool force = false)
        {
            if (VideoOwnerIsWaitingForPlayback && !force)
            {
                _LogWarning("Couldn't remove first video because video owner is loading!", true);
                return;
            }

            _RemoveVideoData(0);

            if (IsEmpty(queuedVideos))
            {
                _ClearVideoPlayer();
                SendCallback(OnUSharpVideoQueueFinalVideoEnded, true);
            }
            else
            {
                SendCallback(OnUSharpVideoQueueVideoEnded, true);
                _MakePlayerPlayFirst();
            }
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Client-side callback invoked across the network telling the designated user to play a URL.
        /// Only executes on the client whose <paramref name="playerID"/> matches the local player.
        /// </summary>
        /// <param name="playerID">Target player ID.</param>
        /// <param name="url">URL to play.</param>
        [NetworkCallable]
        public void RPC_InvokeUserPlay(int playerID, VRCUrl url)
        {
            _LogDebug($"OnUserPlayURLInvoked sent to Player {playerID}: {url.Get()}", true);

            if (localPlayerId != playerID) return;

            VideoPlayer.PlayVideo(url);
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side callback when the video finishes successfully on the video owner's client. Advances the queue.
        /// </summary>
        /// <param name="playerID">Video owner player ID.</param>
        [NetworkCallable]
        public void RPC_OnVideoOwnerVideoEnd(int playerID)
        {
            _LogDebug($"OnVideoOwnerVideoEnd received from Player {playerID}", true);
            _SkipToNextVideo();
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side callback when an error occurs while playing the video on the video-owners client.
        /// Clears the waiting flag and advances the queue.
        /// </summary>
        /// <param name="playerID">Video owner player ID.</param>
        [NetworkCallable]
        public void RPC_OnVideoOwnerVideoError(int playerID)
        {
            _LogDebug($"OnVideoOwnerVideoError received from Player {playerID}", true);
            VideoOwnerIsWaitingForPlayback = false;
            _SynchronizeData();
            _SkipToNextVideo();
            SendCallback(OnUSharpVideoQueueSkippedError, true);
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side callback when the video owner's client begins loading the video.
        /// Sets the waiting flag and synchronizes state.
        /// </summary>
        /// <param name="playerID">Video owner player ID.</param>
        [NetworkCallable]
        public void RPC_OnVideoOwnerVideoLoadStart(int playerID)
        {
            _LogDebug($"OnVideoOwnerVideoLoadStart received from Player {playerID}", true);
            VideoOwnerIsWaitingForPlayback = true;
            // TODO: Timeout?
            _SynchronizeData();
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Owner-side callback when the video owner's client begins playback.
        /// Clears the waiting flag and synchronizes state.
        /// </summary>
        /// <param name="playerID">Video owner player ID.</param>
        [NetworkCallable]
        public void RPC_OnVideoOwnerVideoPlay(int playerID)
        {
            _LogDebug($"OnVideoOwnerVideoPlay received from Player {playerID}", true);
            VideoOwnerIsWaitingForPlayback = false;
            _SynchronizeData();
        }


        /// <summary>
        /// Returns the count of currently queued videos.
        /// </summary>
        /// <returns>Number of valid entries in the queue.</returns>
        public int QueuedVideosCount()
        {
            return Count(queuedVideos);
        }

        /// <summary>
        /// Returns the <see cref="VRCUrl"/> currently queued at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Zero-based index into the queue.</param>
        /// <returns>The URL at the index, or <see cref="VRCUrl.Empty"/> if the index is invalid.</returns>
        public VRCUrl GetURL(int index)
        {
            if (!_IsIndexValid(index)) return VRCUrl.Empty;
            return queuedVideos[index];
        }

        /// <summary>
        /// Returns the title of the video currently queued at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Zero-based index into the queue.</param>
        /// <returns>The title at the index, or an empty string if the index is invalid.</returns>
        public string GetTitle(int index)
        {
            if (!_IsIndexValid(index)) return string.Empty;
            return queuedTitles[index];
        }

        /// <summary>
        /// Returns the player ID of the user who queued the video at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">Zero-based index into the queue.</param>
        /// <returns>The player ID, or -1 if the index is invalid.</returns>
        public int GetVideoOwner(int index)
        {
            if (!_IsIndexValid(index)) return -1;
            return queuedByPlayer[index];
        }

        /// <summary>
        /// Determines whether the player is permitted to remove the video at <paramref name="index"/>.
        /// </summary>
        /// <param name="playerID">Player ID to check permissions for.</param>
        /// <param name="index">Index of the video in the queue.</param>
        /// <returns>True if the player queued the video or has elevated rights; otherwise false.</returns>
        public bool IsPlayerPermittedToRemoveVideo(int playerID, int index)
        {
            if (_PlayerWithIDHasElevatedRights(playerID)) return true;
            return GetVideoOwner(index) == playerID;
        }

        /// <summary>
        /// Determines whether the player is permitted and able to move a video up or down in the queue.
        /// </summary>
        /// <param name="playerID">Player ID to check permissions for.</param>
        /// <param name="index">Index of the video to move.</param>
        /// <param name="directionUp">True to move up; false to move down.</param>
        /// <returns>True if the move is allowed; otherwise false.</returns>
        public bool IsPlayerAbleToMoveVideo(int playerID, int index, bool directionUp)
        {
            if (!_PlayerWithIDHasElevatedRights(playerID)) return false;

            // Index constrains moving upwards
            if (directionUp && (index > QueuedVideosCount() - 1 || index <= 0)) return false;

            //Index constrains moving downwards
            if (!directionUp && (index >= QueuedVideosCount() - 1 || index < 0)) return false;

            //Prevent moving the playing video
            if (directionUp && index == 1 || !directionUp && index == 0) return false;

            return true;
        }

        /// <summary>
        /// Determines whether the player is permitted to queue another video.
        /// This is true if the per-user limit is disabled, not yet reached, or the player has elevated rights.
        /// </summary>
        /// <param name="playerID">Player ID to check permissions for.</param>
        /// <returns>True if the player may queue another video; otherwise false.</returns>
        public bool IsPlayerPermittedToQueueVideo(int playerID)
        {
            if (_PlayerWithIDHasElevatedRights(playerID) || !videoLimitPerUserEnabled) return true;
            return QueuedVideosCountByUser(playerID) < videoLimitPerUser;
        }


        /// <summary>
        /// Determines whether the player is permitted to add custom video links to the queue.
        /// Not affected by the per-user video limit.
        /// </summary>
        /// <param name="playerID">Player ID to check permissions for.</param>
        /// <returns>True if the player has elevated rights or custom URL input is enabled; otherwise false.</returns>
        public bool IsPlayerPermittedToQueueCustomVideos(int playerID)
        {
            return _PlayerWithIDHasElevatedRights(playerID) || customUrlInputEnabled;
        }

        /// <summary>
        /// Counts all videos currently queued by the specified player.
        /// </summary>
        /// <param name="playerID">Player whose queued items should be counted.</param>
        /// <returns>Number of videos queued by the player.</returns>
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
        /// Gets the configured per-user queue limit.
        /// </summary>
        /// <returns>The maximum number of videos a single user may queue.</returns>
        public int GetVideoLimitPerUser() => videoLimitPerUser;


        /// <summary>
        /// Called when synchronized data is received from the network. Triggers local queue content change handling.
        /// </summary>
        public override void OnDeserialization()
        {
            _LogDebug("OnDeserialization run!");
            OnQueueContentChange();
        }

        /// <summary>
        /// Called before this behaviour serializes. Useful for debugging synchronization events.
        /// </summary>
        public override void OnPreSerialization()
        {
            _LogDebug("Sending Serialized Data!");
        }

        internal virtual void _SynchronizeData()
        {
            if (!_IsOwner())
            {
                _LogError("_SynchronizeData called by non-owner!");
                return;
            }

            RequestSerialization();
            OnQueueContentChange();
        }

        internal void _EnqueueVideoData(VRCUrl url, string title, int playerId)
        {
            Enqueue(queuedVideos, url);
            Enqueue(queuedTitles, title);
            Enqueue(queuedByPlayer, playerId);
            _SynchronizeData();
        }

        internal void _RemoveVideo(int index, bool force = false)
        {
            if (index == 0)
            {
                _SkipToNextVideo(force);
                SendCallback(OnUSharpVideoQueueCurrentVideoRemoved, true);
            }
            else
                _RemoveVideoData(index);
        }

        internal void _RemoveVideoData(int index)
        {
            if (index >= QueuedVideosCount()) return;

            Remove(queuedVideos, index);
            Remove(queuedTitles, index);
            Remove(queuedByPlayer, index);
            _SynchronizeData();
        }

        internal void _MoveUpVideoData(int index)
        {
            MoveUp(queuedVideos, index);
            MoveUp(queuedTitles, index);
            MoveUp(queuedByPlayer, index);
            _SynchronizeData();
        }

        internal void _MoveDownVideoData(int index)
        {
            MoveDown(queuedVideos, index);
            MoveDown(queuedTitles, index);
            MoveDown(queuedByPlayer, index);
            _SynchronizeData();
        }


        internal void _ClearVideoPlayer()
        {
            VideoPlayer.TakeOwnership();
            VideoPlayer.StopVideo();
            VideoOwnerIsWaitingForPlayback = false;
            _SynchronizeData();
        }

        internal void _RemoveVideosOfPlayerWhoLeft(int leftPlayerID)
        {
            _LogDebug($"Clearing all videos of leaving player {_GetPlayerInfo(leftPlayerID)}");
            for (int i = Count(queuedVideos) - 1; i >= 0; i--)
            {
                int videoOwnerPlayerID = GetVideoOwner(i);
                //VRChat is inconsistent with the VRCPlayerApi objects of players who just left (sometimes valid, sometimes null)
                //This why we check against both the validity of the video owner VRCPlayerApi object and their ID.
                if (videoOwnerPlayerID == leftPlayerID || !_IsPlayerWithIDValid(videoOwnerPlayerID))
                {
                    _LogDebug(
                        $"Removing video {queuedTitles[i]} with URL {queuedVideos[i].Get()} because owner {_GetPlayerInfo(leftPlayerID)} has left the instance! ",
                        true);
                       
                    _RemoveVideo(i, true);
                }
            }
        }

        /// <summary>
        /// Override this function to integrate with other permission systems!
        /// </summary>
        /// <param name="id">Player ID to test for elevated rights.</param>
        /// <returns>True if the player has elevated rights; otherwise false.</returns>
        protected virtual bool _PlayerWithIDHasElevatedRights(int id)
        {
            if (!_IsPlayerWithIDValid(id)) return false;
            return VRCPlayerApi.GetPlayerById(id).isMaster;
        }

        internal bool _IsIndexValid(int index)
        {
            return index < QueuedVideosCount() && index >= 0;
        }

        //Logging utilities

        internal void _LogDebug(string message, bool broadcast = false)
        {
            if (!enableDebug) return;
            if (broadcast)
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(RPC_ReceiveBroadcastDebug), message);
            else
                Debug.Log($"[DEBUG]USharpVideoQueue: {message}");
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Receives a debug log message broadcast and logs it locally if debug logging is enabled.
        /// </summary>
        /// <param name="message">The message to log.</param>
        [NetworkCallable]
        public void RPC_ReceiveBroadcastDebug(string message) => _LogDebug(message);

        internal void _LogWarning(string message, bool broadcast = false)
        {
            if (broadcast)
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(RPC_ReceiveBroadcastWarning), message);
            else
                Debug.LogWarning($"[WARNING]USharpVideoQueue: {message}");
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Receives a warning log message broadcast and logs it locally.
        /// </summary>
        /// <param name="message">The message to log.</param>
        [NetworkCallable]
        public void RPC_ReceiveBroadcastWarning(string message) => _LogWarning(message);

        internal void _LogError(string message, bool broadcast = false)
        {
            if (broadcast)
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(RPC_ReceiveBroadcastError), message);
            else
                Debug.LogError($"[ERROR]USharpVideoQueue: {message}");
        }

        /// <summary>
        /// NetworkCallable. Not intended to be called externally.
        /// Receives an error log message broadcast and logs it locally.
        /// </summary>
        /// <param name="message">The message to log.</param>
        [NetworkCallable]
        public void RPC_ReceiveBroadcastError(string message) => _LogError(message);

        internal void _LogRequestDenied(string requestName, int playerId)
        {
            _LogWarning(
                $"'{requestName}'-Request by user with ID {playerId} has been denied!", true);
        }
        
        internal string _GetPlayerInfo(int playerId)
        {
            return _GetPlayerInfo(VRCPlayerApi.GetPlayerById(playerId));
        }

        internal string _GetPlayerInfo(VRCPlayerApi player)
        {
            return Utilities.IsValid(player) ? $"[playerId: {player.playerId}, displayName: {player.displayName}]" : "[Invalid Player]";
        }

        internal void _LogOwnerAndMaster(bool broadcast = false)
        {
            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            string ownerInfo = _GetPlayerInfo(owner);
            VRCPlayerApi master = Networking.Master;
            string masterInfo = _GetPlayerInfo(master);
            
            _LogDebug($"\nOwner: {ownerInfo} \nMaster: {masterInfo}", broadcast);
        }

        /* VRC SDK wrapper functions to enable mocking for tests */

        internal virtual bool _IsMaster() => Networking.IsMaster;
        internal virtual void _BecomeOwner() => Networking.SetOwner(Networking.LocalPlayer, gameObject);
        internal virtual bool _IsOwner() => Networking.IsOwner(Networking.LocalPlayer, gameObject);
        internal virtual VRCPlayerApi _GetLocalPlayer() => Networking.LocalPlayer;
        internal virtual int _GetPlayerID(VRCPlayerApi player) => player.playerId;

        internal virtual bool _IsPlayerWithIDValid(int id) => Utilities.IsValid(VRCPlayerApi.GetPlayerById(id));

        internal virtual bool _IsVideoPlayerOwner() =>
            Networking.IsOwner(Networking.LocalPlayer, VideoPlayer.gameObject);

        /* VRC Runtime Events */

        /// <summary>
        /// Called when a player leaves the instance. If this behaviour is owned locally, removes any videos they owned from the queue.
        /// </summary>
        /// <param name="player">The player who left.</param>
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (!_IsOwner()) return;
            _RemoveVideosOfPlayerWhoLeft(_GetPlayerID(player));
        }

        /* USharpVideoQueue Emitted Callbacks */

        protected internal void OnQueueContentChange()
        {
            SendCallback(OnUSharpVideoQueueContentChangeEvent);
        }

        /* USharpVideoPlayer Event Callbacks */

        /// <summary>
        /// Receives the <c>OnUSharpVideoEnd</c> event from the linked <see cref="USharpVideoPlayer"/>.
        /// If this client owns the video player, notifies the owner to advance the queue.
        /// </summary>
        public virtual void OnUSharpVideoEnd()
        {
            _LogDebug($"Received USharpVideoEnd! Is player Video Player owner? {_IsVideoPlayerOwner()}");
            if (_IsVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnVideoOwnerVideoEnd), localPlayerId);
        }

        /// <summary>
        /// Receives the <c>OnUSharpVideoError</c> event from the linked <see cref="USharpVideoPlayer"/> and notifies the owner.
        /// </summary>
        public void OnUSharpVideoError()
        {
            _LogDebug($"Received USharpVideoError! Is player Video Player owner? {_IsVideoPlayerOwner()}");
            if (_IsVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnVideoOwnerVideoError),
                    localPlayerId);
        }

        /// <summary>
        /// Receives the <c>OnUSharpVideoLoadStart</c> event from the linked <see cref="USharpVideoPlayer"/> and notifies the owner.
        /// </summary>
        public void OnUSharpVideoLoadStart()
        {
            _LogDebug($"Received USharpVideoLoadStart! Is player Video Player owner? {_IsVideoPlayerOwner()}");
            if (_IsVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnVideoOwnerVideoLoadStart),
                    localPlayerId);
        }

        /// <summary>
        /// Receives the <c>OnUSharpVideoPlay</c> event from the linked <see cref="USharpVideoPlayer"/> and notifies the owner.
        /// Also broadcasts the <see cref="OnUSharpVideoQueuePlayingNextVideo"/> callback locally.
        /// </summary>
        public void OnUSharpVideoPlay()
        {
            _LogDebug($"Received USharpVideoPlay! Is player Video Player owner? {_IsVideoPlayerOwner()}");
            if (_IsVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RPC_OnVideoOwnerVideoPlay),
                    localPlayerId);
            SendCallback(OnUSharpVideoQueuePlayingNextVideo);
        }

        /* Callback Handling */
        //Taken from MerlinVR's USharpVideoPlayer (https://github.com/MerlinVR/USharpVideo)

        /// <summary>
        /// Registers an <see cref="UdonSharpBehaviour"/> as a callback receiver for events that happen on this video player.
        /// Callback receivers can be used to react to state changes without polling.
        /// </summary>
        /// <param name="callbackReceiver">The behaviour to receive callbacks.</param>
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

        /// <summary>
        /// Unregisters a previously registered callback receiver.
        /// </summary>
        /// <param name="callbackReceiver">The behaviour to remove from the callback list.</param>
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

        /// <summary>
        /// Sends a named callback to all registered receivers locally, or broadcasts a request to do so on all clients.
        /// </summary>
        /// <param name="callbackName">The name of the method to invoke on receivers.</param>
        /// <param name="broadcast">True to broadcast across the network; false to invoke locally.</param>
        [RecursiveMethod]
        internal virtual void SendCallback(string callbackName, bool broadcast = false)
        {
            if (string.IsNullOrEmpty(callbackName)) return;

            if (broadcast)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveBroadcastCallback), callbackName);
                return;
            }

            if (registeredCallbackReceivers == null || registeredCallbackReceivers.Length == 0) return;

            foreach (UdonSharpBehaviour callbackReceiver in registeredCallbackReceivers)
            {
                callbackReceiver.SendCustomEvent(callbackName);
                _LogDebug($"Sent Callback '{callbackName}'");
            }
        }

        /// <summary>
        /// Receives a broadcast callback name and forwards it to all locally registered receivers.
        /// </summary>
        /// <param name="callbackName">The name of the callback to invoke.</param>
        public void ReceiveBroadcastCallback(string callbackName) => SendCallback(callbackName);
    }
}