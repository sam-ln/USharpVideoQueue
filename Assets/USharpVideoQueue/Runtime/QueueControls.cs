﻿
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace USharpVideoQueue.Runtime
{
    public class QueueControls : UdonSharpBehaviour
    {

        public VideoQueue Queue;
        public Text UIQueueContent;
        public VRCUrlInputField UIURLInput;

        internal void Start()
        {
            Queue.RegisterCallbackReceiver(this);
        }

        public void OnUSharpVideoQueueContentChange()
        {
            UIQueueContent.text = formatQueueContent();
        }

        public void OnURLInput() {
            VRCUrl url = UIURLInput.GetUrl();
            if(url != null) {
                Queue.QueueVideo(url);
            }
        }

        internal string formatQueueContent()
        {
            string formattedUrls = "";
            foreach (VRCUrl video in Queue.queuedVideos)
            {
                if(video == null) continue;
                formattedUrls += $"{video.Get()} \n";
            }
            return formattedUrls;
        }
    }
}