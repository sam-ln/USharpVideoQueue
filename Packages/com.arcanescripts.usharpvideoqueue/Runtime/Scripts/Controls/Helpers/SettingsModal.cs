using UdonSharp;
using UnityEngine;

namespace USharpVideoQueue.Runtime.Controls.Helpers
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class SettingsModal : UdonSharpBehaviour
    {
    
        [SerializeField] private CanvasGroupController canvasGroupController;
    
        public void Open()
        {
            canvasGroupController._Show();
            OnOpen();
        }

        public void Close() => canvasGroupController._Hide();

        protected virtual void OnOpen() {}
    }
}
