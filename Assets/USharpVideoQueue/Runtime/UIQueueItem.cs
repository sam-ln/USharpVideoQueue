﻿
using UdonSharp;
using UnityEngine.UI;
using USharpVideoQueue.Runtime;

public class UIQueueItem : UdonSharpBehaviour
{
    public Text Description;
    public Text QueuedBy;
    public Text RankText;
    public int Rank;
    public QueueControls QueueControls;
    internal void Start()
    {
        QueueControls.RegisterUIQueueItem(this);
        if(RankText != null) RankText.text = $"{Rank+1}";
    }

    public void OnRemovePressed()
    {
        QueueControls.RemoveRank(Rank);
    }

    public virtual void SetContent(string content, string queuedBy)
    {
        Description.text = content;
        QueuedBy.text = queuedBy;
    }

    public virtual void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
