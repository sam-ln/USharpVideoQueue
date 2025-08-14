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
                _LogError("VideoQueue is missing USharpVideo Player reference! Please check in the inspector!");
            }

            initialized = true;
            localPlayerId = _GetPlayerID(_GetLocalPlayer());

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

            _LogDebug(
                $"USharpVideoQueue initialized! Local Player ID is {localPlayerId}. You are {(_IsOwner() ? "" : "not ")}the owner!");
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
            if (wasEmpty) MakePlayerPlayFirst();
        }

        [NetworkCallable]
        public void OnClearRequested(int playerID)
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

        [NetworkCallable]
        public void OnMoveVideoRequested(int playerID, int index, bool directionUp)
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

        [NetworkCallable]
        public void OnRemoveVideoRequested(int playerID, int index)
        {
            _LogDebug($"OnRemoveVideoRequested from Player {playerID}: Index {index}", true);
            if (!IsPlayerPermittedToRemoveVideo(playerID, index))
            {
                _LogRequestDenied(nameof(RemoveVideo), playerID);
                return;
            }

            _RemoveVideo(index);
        }


        [NetworkCallable]
        public void OnSetVideoLimitPerUserRequested(int playerID, int limit)
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

        [NetworkCallable]
        public void OnSetVideoLimitPerUserEnabledRequested(int playerID, bool enabled)
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

        [NetworkCallable]
        public void OnSetCustomUrlInputEnabledRequested(int playerID, bool enabled)
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
                MakePlayerPlayFirst();
            }
        }

        [NetworkCallable]
        public void InvokeUserPlay(int playerID, VRCUrl url)
        {
            _LogDebug($"OnUserPlayURLInvoked sent to Player {playerID}: {url.Get()}", true);

            if (localPlayerId != playerID) return;

            VideoPlayer.PlayVideo(url);
        }

        [NetworkCallable]
        public void OnVideoOwnerVideoEnd(int playerID)
        {
            _LogDebug($"OnVideoOwnerVideoEnd received from Player {playerID}", true);
            SkipToNextVideo();
        }

        [NetworkCallable]
        public void OnVideoOwnerVideoError(int playerID)
        {
            _LogDebug($"OnVideoOwnerVideoError received from Player {playerID}", true);
            VideoOwnerIsWaitingForPlayback = false;
            _SynchronizeData();
            SkipToNextVideo();
            SendCallback(OnUSharpVideoQueueSkippedError, true);
        }

        [NetworkCallable]
        public void OnVideoOwnerVideoLoadStart(int playerID)
        {
            _LogDebug($"OnVideoOwnerVideoLoadStart received from Player {playerID}", true);
            VideoOwnerIsWaitingForPlayback = true;
            // TODO: Timeout?
            _SynchronizeData();
        }

        [NetworkCallable]
        public void OnVideoOwnerVideoPlay(int playerID)
        {
            _LogDebug($"OnVideoOwnerVideoPlay received from Player {playerID}", true);
            VideoOwnerIsWaitingForPlayback = false;
            _SynchronizeData();
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
            if (!_IsIndexValid(index)) return VRCUrl.Empty;
            return queuedVideos[index];
        }

        /// <summary>
        /// Returns the title of the video currently queued at [index]. Returns an empty string if [index] is not valid. 
        /// </summary>
        public string GetTitle(int index)
        {
            if (!_IsIndexValid(index)) return string.Empty;
            return queuedTitles[index];
        }

        /// <summary>
        /// Returns the player id of the player who queued the video at [index]. Returns -1 if [index] is not valid. 
        /// </summary>
        public int GetVideoOwner(int index)
        {
            if (!_IsIndexValid(index)) return -1;
            return queuedByPlayer[index];
        }

        /// <summary>
        /// Returns whether the local player is permitted to remove the video at [index].
        /// This is the case if the user has queued the video themselves or they have elevated rights. 
        /// </summary>
        public bool IsPlayerPermittedToRemoveVideo(int playerID, int index)
        {
            if (_PlayerWithIDHasElevatedRights(playerID)) return true;
            return GetVideoOwner(index) == playerID;
        }

        /// <summary>
        /// Returns whether the local player is permitted and able to move video with index up or down in the queue.
        /// </summary>
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
        /// Returns whether the local player is permitted queue another video.
        /// This is the case if the [VideoLimitPerUser] was not reached yet or if they have elevated rights.
        /// </summary>
        public bool IsPlayerPermittedToQueueVideo(int playerID)
        {
            if (_PlayerWithIDHasElevatedRights(playerID) || !videoLimitPerUserEnabled) return true;
            return QueuedVideosCountByUser(playerID) < videoLimitPerUser;
        }


        /// <summary>
        /// Returns whether the local player is permitted to add custom video links to the queue.
        /// This check is not affected by the [VideoLimitPerUser]
        /// </summary>
        public bool IsPlayerPermittedToQueueCustomVideos(int playerID)
        {
            return _PlayerWithIDHasElevatedRights(playerID) || customUrlInputEnabled;
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
            _LogDebug("OnDeserialization run!");
            OnQueueContentChange();
        }

        public override void OnPreSerialization()
        {
            _LogDebug("Sending Serialized Data!");
        }
        
        internal virtual void _SynchronizeData()
        {
            Debug.Assert(_IsOwner());
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
                SkipToNextVideo(force);
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
        }

        internal void _RemoveVideosOfPlayerWhoLeft(int leftPlayerID)
        {
            for (int i = Count(queuedVideos) - 1; i >= 0; i--)
            {
                int videoOwnerPlayerID = GetVideoOwner(i);
                //VRChat is inconsistent with the VRCPlayerApi objects of players who just left (sometimes valid, sometimes null)
                //This why we check against both the validity of the video owner VRCPlayerApi object and their ID.
                if (videoOwnerPlayerID == leftPlayerID || !_IsPlayerWithIDValid(videoOwnerPlayerID))
                {
                    _LogDebug(
                        $"Removing video {queuedTitles[i]} with URL {queuedVideos[i]} because owner with Player-ID {leftPlayerID} has left the instance! " +
                        $"Owner of the queue is currently {Networking.GetOwner(gameObject).playerId}." +
                        $" Instance master is currently {Networking.Master.playerId}", true);
                    _RemoveVideo(i, true);
                }
            }
        }

        /// <summary>
        /// Override this function to integrate with other permission systems!
        /// </summary>
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
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveBroadcastDebug), message);
            else
                Debug.Log($"[DEBUG]USharpVideoQueue: {message}");
        }

        public void ReceiveBroadcastDebug(string message) => _LogDebug(message);

        internal void _LogWarning(string message, bool broadcast = false)
        {
            if (broadcast)
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveBroadcastWarning), message);
            else
                Debug.LogWarning($"[WARNING]USharpVideoQueue: {message}");
        }

        public void ReceiveBroadcastWarning(string message) => _LogWarning(message);

        internal void _LogError(string message, bool broadcast = false)
        {
            if (broadcast)
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveBroadcastError), message);
            else
                Debug.LogError($"[ERROR]USharpVideoQueue: {message}");
        }

        public void ReceiveBroadcastError(string message) => _LogWarning(message);

        internal void _LogRequestDenied(string requestName, int playerId)
        {
            _LogWarning(
                $"'{requestName}'-Request by user with ID {playerId} has been denied!", true);
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

        public virtual void OnUSharpVideoEnd()
        {
            _LogDebug($"Received USharpVideoEnd! Is player Video Player owner? {_IsVideoPlayerOwner()}");
            if (_IsVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnVideoOwnerVideoEnd), localPlayerId);
        }

        public void OnUSharpVideoError()
        {
            _LogDebug($"Received USharpVideoError! Is player Video Player owner? {_IsVideoPlayerOwner()}");
            if (_IsVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnVideoOwnerVideoError),
                    localPlayerId);
        }

        public void OnUSharpVideoLoadStart()
        {
            _LogDebug($"Received USharpVideoLoadStart! Is player Video Player owner? {_IsVideoPlayerOwner()}");
            if (_IsVideoPlayerOwner())
                SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OnVideoOwnerVideoLoadStart),
                    localPlayerId);
        }

        public void OnUSharpVideoPlay()
        {
            _LogDebug($"Received USharpVideoPlay! Is player Video Player owner? {_IsVideoPlayerOwner()}");
            if (_IsVideoPlayerOwner())
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
        internal virtual void SendCallback(string callbackName, bool broadcast = false)
        {
            if (broadcast)
            {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveBroadcastCallback), callbackName);
                return;
            }

            foreach (UdonSharpBehaviour callbackReceiver in registeredCallbackReceivers)
            {
                if (!UdonSharpBehaviour.Equals(callbackName, null))
                {
                    callbackReceiver.SendCustomEvent(callbackName);
                    _LogDebug($"Sent Callback '{callbackName}'");
                }
            }
        }

        public void ReceiveBroadcastCallback(string callbackName) => SendCallback(callbackName);
    }
}