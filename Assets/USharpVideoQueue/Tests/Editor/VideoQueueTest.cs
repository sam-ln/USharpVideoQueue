
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using UdonSharp.Video;
using USharpVideoQueue.Tests.Mock;

namespace USharpVideoQueue.Tests.Editor
{
    public class VideoQueueTest
    {
        private USharpVideoPlayerMock videoPlayer;
        private VideoQueue queue;

        [SetUp]
        public void Prepare()
        {
            videoPlayer = new GameObject().AddComponent<USharpVideoPlayerMock>();
            videoPlayer.Start();
            queue = new GameObject().AddComponent<VideoQueue>();
            queue.VideoPlayer = videoPlayer;   
            queue.Start();
        }


        [Test]
        public void CreateBehavior()
        {
            Assert.NotNull(queue);
            Assert.True(queue.Initialized);
            Assert.True(VRC.SDKBase.Utilities.IsValid(queue));
        }

        [Test]
        public void CallbackRegistered()
        {
            Assert.Contains(queue, videoPlayer._registeredCallbackReceivers);
        }

        [Test]
        public void QueueVideo()
        {
            var url = new VRCUrl("https://www.youtube.com/watch?v=3dm_5qWWDV8");
            queue.QueueVideo(url);
            Assert.AreEqual(url, videoPlayer.Url);
        }


        /*
        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator BasicTestWithEnumeratorPasses()
        {
            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
        }
        */
    }
}
