using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace USharpVideoQueue.Runtime.InstanceSettings
{
    public class InstanceSettings : UdonSharpBehaviour
    {
        [SerializeField] private VideoQueue _videoQueue;

        [SerializeField] private Text _videoLimitText;
        [SerializeField] private Text _urlInputFieldText;
        [SerializeField] private VRCUrlInputField _urlInputField;

        [UdonSynced] private int videoLimit = 2;

        private void Start()
        {
            if (!Networking.IsMaster) return;
            videoLimit = _videoQueue.videoLimitPerUser;
            UpdateVideoLimitDisplay();
        }

        public void _EnableCustomURLs()
        {
            _urlInputFieldText.text = "Enter Video URL...";
            _urlInputField.readOnly = false;
        }

        public void _DisableCustomURLs()
        {
            _urlInputFieldText.text = "URL Input is disabled!";
            _urlInputField.readOnly = true;
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
            if (!Networking.IsMaster) return;
            videoLimit += 1;
            UpdateVideoLimitDisplay();
            RequestSerialization();
            _videoQueue.SetVideoLimitPerUser(videoLimit);
        }

        public void _DecreaseVideoLimit()
        {
            if (!Networking.IsMaster) return;
            if (videoLimit == 0) return;
            videoLimit -= 1;
            UpdateVideoLimitDisplay();
            RequestSerialization();
            _videoQueue.SetVideoLimitPerUser(videoLimit);
        }

        public void _ResetQueue()
        {
            _videoQueue.Clear();
        }

        private void UpdateVideoLimitDisplay()
        {
            _videoLimitText.text = videoLimit.ToString();
        }

        public override void OnDeserialization()
        {
            UpdateVideoLimitDisplay();
        }
    }
}