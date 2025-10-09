using UnityEngine;
using USharpVideoQueue.Runtime.Controls.Helpers;

namespace USharpVideoQueue.Runtime.Controls {
    
    public class VideoRemovalWarningModal : SettingsModal
    {
        [SerializeField] private QueueControls controls;

        public void OnConfirmPressed()
        {
            controls.ConfirmFirstVideoRemoval();
            Close();
        }

        public void OnCancelPressed()
        {
            Close();
        }
    }
}