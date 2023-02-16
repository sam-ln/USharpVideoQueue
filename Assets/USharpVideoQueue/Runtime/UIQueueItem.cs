
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

    public virtual void SetRemoveEnabled(bool enabled)
    {
        RemoveButton.gameObject.SetActive(enabled);
    }
}
