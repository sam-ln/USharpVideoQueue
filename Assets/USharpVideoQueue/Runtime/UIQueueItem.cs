
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using USharpVideoQueue.Runtime;

[DefaultExecutionOrder(-1)]
public class UIQueueItem : UdonSharpBehaviour
{
    public Text Description;
    public Text QueuedBy;
    public Text RankText;
    public Button RemoveButton;
    public int Rank;
    public QueueControls QueueControls;

    internal bool hasDescription;
    internal bool hasQueuedBy;
    internal bool hasRank;
    internal bool hasRemoveButton;
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
        
        QueueControls.RegisterUIQueueItem(this);
        if(hasRank) RankText.text = $"{Rank+1}";
    }

    public void OnRemovePressed()
    {
        QueueControls.RemoveRank(Rank);
    }

    public virtual void SetContent(string content, string queuedBy)
    {
        if(hasDescription) Description.text = content;
        if(hasQueuedBy) QueuedBy.text = queuedBy;
    }

    public virtual void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public virtual void SetRemoveEnabled(bool enabled)
    {
        if(hasRemoveButton) RemoveButton.gameObject.SetActive(enabled);
    }
}
