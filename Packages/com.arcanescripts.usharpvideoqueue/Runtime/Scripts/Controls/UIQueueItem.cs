using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace USharpVideoQueue.Runtime.Controls
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DefaultExecutionOrder(-1)]
    public class UIQueueItem : UdonSharpBehaviour
    {
        public Text Description;
        public Text QueuedBy;
        public Text RankText;
        public Button RemoveButton;
        public Button UpButton;
        public Button DownButton;
        public int Rank;
        public QueueControls QueueControls;

        internal bool hasDescription;
        internal bool hasQueuedBy;
        internal bool hasRank;
        internal bool hasRemoveButton;
        internal bool hasUpButton;
        internal bool hasDownButton;

        //internal state for tests
        internal bool active;
        internal bool removeEnabled;
        internal bool upEnabled;
        internal bool downEnabled;
        internal string description;
        internal string queuedBy;
        internal string displayedRank;

        internal void Start()
        {
            if (Equals(QueueControls, null))
            {
                Debug.LogError("UIQueueItem is missing QueueControls reference! Please check in the inspector!");
                return;
            }

            hasDescription = Description != null;
            hasQueuedBy = QueuedBy != null;
            hasRank = RankText != null;
            hasRemoveButton = RemoveButton != null;
            hasUpButton = UpButton != null;
            hasDownButton = DownButton != null;

            QueueControls.RegisterUIQueueItem(this);
            if (hasRank) RankText.text = $"{Rank + 1}";
        }

        public void OnRemovePressed()
        {
            QueueControls.RemoveRank(Rank);
        }

        public void OnUpPressed()
        {
            QueueControls.MoveUpRank(Rank);
        }

        public void OnDownPressed()
        {
            QueueControls.MoveDownRank(Rank);
        }

        public void SetContent(string description, string queuedBy)
        {
            this.description = description;
            this.queuedBy = queuedBy;
        }

        public void SetRank(string rank)
        {
            this.displayedRank = rank;
        }

        public void SetActive(bool active)
        {
            this.active = active;
        }

        public void SetRemoveEnabled(bool removeEnabled)
        {
            this.removeEnabled = removeEnabled;
        }

        public void SetUpEnabled(bool upEnabled)
        {
            this.upEnabled = upEnabled;
        }
        
        public void SetDownEnabled(bool downEnabled)
        {
            this.downEnabled = downEnabled;
        }

        public virtual void UpdateGameObjects()
        {
            gameObject.SetActive(active);
            if (hasRemoveButton) RemoveButton.gameObject.SetActive(removeEnabled);
            if (hasUpButton) UpButton.gameObject.SetActive(upEnabled);
            if (hasDownButton) DownButton.gameObject.SetActive(downEnabled);
            if (hasDescription) Description.text = description;
            if (hasQueuedBy) QueuedBy.text = queuedBy;
            if (hasRank) RankText.text = displayedRank;
        }
    }
}