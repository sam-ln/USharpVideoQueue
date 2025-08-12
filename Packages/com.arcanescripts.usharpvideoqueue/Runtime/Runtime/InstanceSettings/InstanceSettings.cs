using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

namespace USharpVideoQueue.Runtime.InstanceSettings
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class InstanceSettings : UdonSharpBehaviour
    {
        [SerializeField] private VideoQueue _videoQueue;

        [SerializeField] private Text _videoLimitText;

        private void Start()
        {
            if (_videoQueue == null)
            {
                Debug.LogError("VideoQueue is null. Please set the reference in the editor!");
                return;
            }
            _videoQueue.RegisterCallbackReceiver(this);
        }

        public void _EnableCustomURLs()
        {
            _videoQueue.SetCustomUrlInputEnabled(true);
        }

        public void _DisableCustomURLs()
        {
            _videoQueue.SetCustomUrlInputEnabled(false);
        }

        public void _EnableVideoLimit()
        {
            _videoQueue.SetVideoLimitPerUserEnabled(true);
        }


        public void _DisableVideoLimit()
        {
            _videoQueue.SetVideoLimitPerUserEnabled(false);
        }

        public void _IncreaseVideoLimit()
        {
            int currentVideoLimit = _videoQueue.GetVideoLimitPerUser();
            _videoQueue.SetVideoLimitPerUser(currentVideoLimit + 1);
        }

        public void _DecreaseVideoLimit()
        {
            int currentVideoLimit = _videoQueue.GetVideoLimitPerUser();
            _videoQueue.SetVideoLimitPerUser(currentVideoLimit - 1);
        }

        public void _ResetQueue()
        {
            _videoQueue.Clear();
        }

        public void OnUSharpVideoQueueVideoLimitPerUserChanged()
        {
            _videoLimitText.text = _videoQueue.GetVideoLimitPerUser().ToString();
        }
    }
}
