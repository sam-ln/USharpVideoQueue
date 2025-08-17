using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace USharpVideoQueue.Runtime.Controls
{
    public class Paginator : UdonSharpBehaviour
    {

        public QueueControls QueueControls;
        public Text PageDisplay;

        void Start()
        {
            if (QueueControls == null)
            {
                Debug.LogError("Paginator is missing QueueControls Reference!");
            }
        }

        public void UpdatePageNumber()
        {
            PageDisplay.text = $"Page {QueueControls.CurrentPage + 1} of {QueueControls.LastPage() + 1}";
        }

        public void OnNextPressed()
        {
            if (QueueControls.CurrentPage >= QueueControls.LastPage()) return;
            QueueControls.SetCurrentPage(QueueControls.CurrentPage + 1);
        }

        public void OnPreviousPressed()
        {
            if (QueueControls.CurrentPage == 0) return;
            QueueControls.SetCurrentPage(QueueControls.CurrentPage - 1);
        }

    }
}
