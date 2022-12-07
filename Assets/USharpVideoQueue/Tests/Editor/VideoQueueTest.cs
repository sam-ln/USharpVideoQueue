
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
    public class VideoQueueTest
    {
        private Mock<USharpVideoPlayer> vpMock;
        private VideoQueue queue;

        

        [SetUp]
        public void Prepare()
        {
            queue = new GameObject().AddComponent<VideoQueue>();
            vpMock = new Mock<USharpVideoPlayer>();
            queue.VideoPlayer = vpMock.Object;   
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
        public void CallbackRegisteredAfterStart()
        {
            vpMock.Verify(vp => vp.RegisterCallbackReceiver(queue), Times.Once());
        }
     

        [Test]
        public void QueueAndFinishVideo()
        {
            var url1 = new VRCUrl("https://url.one");
            queue.QueueVideo(url1);
            queue.SendCustomEvent("OnUSharpVideoEnd");
            vpMock.Verify((vp => vp.PlayVideo(url1)), Times.Once);
        }

        [Test]
        public void QueueMultipleVideos() {
            var url1 = new VRCUrl("https://url.one");
            var url2 = new VRCUrl("https://url.two");
            queue.QueueVideo(url1);
            queue.QueueVideo(url2);
            vpMock.Verify((vp => vp.PlayVideo(url1)), Times.Once);
            queue.SendCustomEvent("OnUSharpVideoEnd");
            vpMock.Verify((vp => vp.PlayVideo(url1)), Times.Once);
            vpMock.Verify((vp => vp.PlayVideo(url2)), Times.Once);
        }
        [Test]
        public void InvalidURLQueued() {
            var invalidURL = new VRCUrl("https://invalid.url");
            queue.QueueVideo(invalidURL);
            queue.SendCustomEvent("OnUSharpVideoError");
            Assert.True(VideoQueue.isEmpty(queue.queuedVideos));
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
