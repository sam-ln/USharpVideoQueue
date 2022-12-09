
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using UdonSharp.Video;
using Moq;

namespace USharpVideoQueue.Tests.Editor
{
    public abstract class VideoQueueEventReceiver : UdonSharpBehaviour
    {
        public abstract void OnUSharpVideoQueueContentChange();
    }
}