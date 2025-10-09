using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace USharpVideoQueue.Runtime.Controls
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class Paginator : UdonSharpBehaviour
    {
        public QueueControls QueueControls;
        public Text PageDisplay;
        [SerializeField] private string PageDisplayTextPattern;

        void Start()
        {
            if (QueueControls == null)
            {
                Debug.LogError("Paginator is missing QueueControls Reference!");
            }
        }

        public void UpdatePageNumber()
        {
            string currentPage = (QueueControls.CurrentPage + 1).ToString();
            string lastPage = (QueueControls.LastPage() + 1).ToString();
            string updatedPageText = PageDisplayTextPattern.Replace("{CurrentPage}", currentPage)
                .Replace("{LastPage}", lastPage);
            PageDisplay.text = updatedPageText;
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