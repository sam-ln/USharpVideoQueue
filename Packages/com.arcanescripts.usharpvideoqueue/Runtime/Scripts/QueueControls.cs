using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace USharpVideoQueue.Runtime
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DefaultExecutionOrder(-10)]
    public class QueueControls : UdonSharpBehaviour
    {
        public VideoQueue Queue;
        public VRCUrlInputField UIURLInput;
        public Text UIURLInputText;
        public bool SetPageAutomatically;
        internal UIQueueItem[] registeredQueueItems;
        internal bool initialized = false;
        public int CurrentPage = 0;
        public Paginator Paginator;
        internal bool hasPaginator;
        private int localPlayerId;

        internal void Start()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            if (Equals(Queue, null))
            {
                Debug.LogError("Queue Controls are missing VideoQueue reference! Please check in the inspector!");
            }
            else
            {
                Queue.RegisterCallbackReceiver(this);
            }

            hasPaginator = !(Paginator == null);

            if (registeredQueueItems == null)
                registeredQueueItems = new UIQueueItem[0];

            localPlayerId = Networking.LocalPlayer.playerId;
        }

        public void OnUSharpVideoQueueContentChange()
        {
            UpdateQueueItems();
            UpdateURLInputFieldEnabled(Queue.IsPlayerPermittedToQueueCustomVideos(localPlayerId));
        }

        public void SetCurrentPage(int currentPage)
        {
            this.CurrentPage = currentPage;
            UpdateQueueItems();
        }

        internal virtual void UpdateURLInputFieldEnabled(bool customUrlsEnabled)
        {
            if (UIURLInputText != null) UIURLInputText.text = customUrlsEnabled ? "Enter Video URL..." : "URL input disabled!";
            if (UIURLInput != null) UIURLInput.readOnly = !customUrlsEnabled;
        }

        public void UpdateQueueItems()
        {
            Queue.EnsureInitialized();

            if (SetPageAutomatically) ensureCurrentPageHasVideos();

            foreach (var queueItem in registeredQueueItems)
            {
                if (Equals(queueItem, null)) continue;
                queueItem.SetActive(false);
                queueItem.UpdateGameObjects();
            }

            int firstDisplayedVideo = firstIndexOfPage(CurrentPage);
            int videosOnCurrentPage =
                Mathf.Min(Queue.QueuedVideosCount() - firstDisplayedVideo, registeredQueueItems.Length);
            for (int i = 0; i < videosOnCurrentPage; i++)
            {
                int videoIndex = i + firstDisplayedVideo;
                if (Equals(registeredQueueItems[i], null)) continue;
                registeredQueueItems[i].SetActive(true);
                string description = Queue.GetTitle(videoIndex);
                string playerName = getPlayerNameByID(Queue.GetVideoOwner(videoIndex));
                string rank = (firstIndexOfPage(CurrentPage) + i + 1).ToString();
                registeredQueueItems[i].SetContent(description, playerName);
                registeredQueueItems[i].SetRemoveEnabled(Queue.IsPlayerPermittedToRemoveVideo(localPlayerId, videoIndex));
                registeredQueueItems[i].SetUpEnabled(Queue.IsPlayerAbleToMoveVideo(localPlayerId, videoIndex, true));
                registeredQueueItems[i].SetDownEnabled(Queue.IsPlayerAbleToMoveVideo(localPlayerId, videoIndex, false));
                registeredQueueItems[i].SetRank(rank);
                registeredQueueItems[i].UpdateGameObjects();
            }

            if (hasPaginator) Paginator.UpdatePageNumber();
        }

        internal void ensureCurrentPageHasVideos()
        {
            bool currentPageHasVideos = firstIndexOfPage(CurrentPage) < Queue.QueuedVideosCount();
            if (currentPageHasVideos) return;
            if (registeredQueueItems.Length == 0 || Queue.QueuedVideosCount() == 0)
            {
                CurrentPage = 0;
                return;
            }

            CurrentPage = LastPage();
        }

        internal int firstIndexOfPage(int page)
        {
            return CurrentPage * registeredQueueItems.Length;
        }

        internal int pageOfIndex(int index)
        {
            return index / registeredQueueItems.Length;
        }

        public int LastPage()
        {
            return pageOfIndex(Queue.QueuedVideosCount() - 1);
        }

        public void OnURLInput()
        {
            if (UIURLInput == null) return;

            VRCUrl url = UIURLInput.GetUrl();
            if (url != null)
            {
                Queue.QueueVideo(url);
            }

            UIURLInput.SetUrl(VRCUrl.Empty);
        }

        public void RegisterUIQueueItem(UIQueueItem queueItem)
        {
            EnsureInitialized();
            //Nullcheck with Equals for testability reasons
            if (UIQueueItem.Equals(queueItem, null))
                return;

            if (queueItem.Rank >= registeredQueueItems.Length - 1)
            {
                UIQueueItem[] newControlHandlers = new UIQueueItem[queueItem.Rank + 1];

                registeredQueueItems.CopyTo(newControlHandlers, 0);
                registeredQueueItems = newControlHandlers;
            }

            registeredQueueItems[queueItem.Rank] = queueItem;
            UpdateQueueItems();
        }

        public void RemoveRank(int rank)
        {
            Queue.RemoveVideo(firstIndexOfPage(CurrentPage) + rank);
        }

        public void MoveUpRank(int rank)
        {
            Queue.MoveVideo(firstIndexOfPage(CurrentPage) + rank, true);
        }

        public void MoveDownRank(int rank)
        {
            Queue.MoveVideo(firstIndexOfPage(CurrentPage) + rank, false);
        }

        /* VRC SDK wrapper functions to enable mocking for tests */

        internal virtual string getPlayerNameByID(int id)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(id);
            return player != null ? player.displayName : "Player not found!";
        }
    }
}
