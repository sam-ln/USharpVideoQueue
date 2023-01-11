
using UdonSharp;
using UnityEngine.UI;
using USharpVideoQueue.Runtime;

public class UIQueueItem : UdonSharpBehaviour
{
    public Text Description;
    public int Rank;
    public QueueControls QueueControls;
    internal void Start()
    {
        QueueControls.RegisterUIQueueItem(this);
    }

    public void OnRemovePressed()
    {
        QueueControls.RemoveRank(Rank);
    }

    public virtual void SetContent(string content)
    {
        Description.text = content;
    }

    public virtual void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }
}
