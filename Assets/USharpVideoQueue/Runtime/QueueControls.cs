﻿using UdonSharp;
using UnityEngine;
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
        internal UIQueueItem[] registeredQueueItems;
        internal bool initialized = false;

        internal int currentPage = 0;

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

            if (registeredQueueItems == null)
                registeredQueueItems = new UIQueueItem[0];
        }

        public void OnUSharpVideoQueueContentChange()
        {
            UpdateQueueItems();
        }

        public void SetCurrentPage(int currentPage)
        {
            this.currentPage = currentPage;
            UpdateQueueItems();
        }

        public void UpdateQueueItems()
        {
            Queue.EnsureInitialized();

            foreach (var queueItem in registeredQueueItems)
            {
                if (Equals(queueItem, null)) continue;
                queueItem.SetActive(false);
            }

            int firstDisplayedVideo = currentPage * registeredQueueItems.Length;
            int videosOnCurrentPage = Mathf.Min(Queue.QueuedVideosCount() - firstDisplayedVideo, registeredQueueItems.Length);
            for (int i = 0; i < videosOnCurrentPage; i++)
            {
                int videoIndex = i + firstDisplayedVideo;
                if (Equals(registeredQueueItems[i], null)) continue;
                registeredQueueItems[i].SetActive(true);
                string description = Queue.GetTitle(videoIndex);
                string playerName = getPlayerNameByID(Queue.GetVideoOwner(videoIndex));
                registeredQueueItems[i].SetContent(description, playerName);
                registeredQueueItems[i].SetRemoveEnabled(Queue.IsLocalPlayerPermittedToRemoveVideo(videoIndex));
            }
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
            Queue.RequestRemoveVideo(rank);
        }

        /* VRC SDK wrapper functions to enable mocking for tests */

        internal virtual string getPlayerNameByID(int id)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(id);
            return player != null ? player.displayName : "Player not found!";
        }
    }
}