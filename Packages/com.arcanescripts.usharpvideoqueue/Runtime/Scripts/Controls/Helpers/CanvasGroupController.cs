using UdonSharp;
using UnityEngine;

namespace USharpVideoQueue.Runtime.Controls.Helpers
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class CanvasGroupController : UdonSharpBehaviour
    {
        private CanvasGroup _canvasGroup;
        void Start()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void SetCanvasGroupActive(bool active)
        {
            _canvasGroup.interactable = active;
            _canvasGroup.alpha = active ? 1f : 0f;
            _canvasGroup.blocksRaycasts = active;
        }

        public void _Show()
        {
            SetCanvasGroupActive(true);
        }

        public void _Hide()
        {
            SetCanvasGroupActive(false);
        }
    }
}
