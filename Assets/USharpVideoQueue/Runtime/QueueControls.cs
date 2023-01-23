using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using static USharpVideoQueue.Runtime.Utility.QueueArray;

namespace USharpVideoQueue.Runtime
{
    [DefaultExecutionOrder(-10)]
    public class QueueControls : UdonSharpBehaviour
    {
        public VideoQueue Queue;
        public VRCUrlInputField UIURLInput;
        internal UIQueueItem[] registeredQueueItems;

        internal void Start()
        {
            Queue.RegisterCallbackReceiver(this);
            if (registeredQueueItems == null)
                registeredQueueItems = new UIQueueItem[0];
        }

        public void OnUSharpVideoQueueContentChange()
        {
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

            for (int i = 0; i < Mathf.Min(registeredQueueItems.Length, Queue.QueuedVideosCount()); i++)
            {
                if (Equals(registeredQueueItems, null)) continue;
                registeredQueueItems[i].SetActive(true);
                string description = Queue.GetTitle(i);
                string playerName = getPlayerNameByID(Queue.GetQueuedByPlayer(i));
                registeredQueueItems[i].SetContent(description, playerName);
            }
        }

        public void OnURLInput()
        {
            VRCUrl url = UIURLInput.GetUrl();
            if (url != null)
            {
                Queue.QueueVideo(url);
            }

            UIURLInput.SetUrl(VRCUrl.Empty);
        }

        public void RegisterUIQueueItem(UIQueueItem queueItem)
        {
            //Nullcheck with Equals for testability reasons
            if (UIQueueItem.Equals(queueItem, null))
                return;

            if (registeredQueueItems == null)
                registeredQueueItems = new UIQueueItem[0];

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
            Queue.RemoveVideo(rank);
        }

        /* VRC SDK wrapper functions to enable mocking for tests */

        internal virtual string getPlayerNameByID(int id)
        {
            VRCPlayerApi player = VRCPlayerApi.GetPlayerById(id);
            return player != null ? player.displayName : "Player not found!";
        }
    }
}