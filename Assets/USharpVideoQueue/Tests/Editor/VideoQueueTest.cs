
using NUnit.Framework;
using UnityEngine;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using UdonSharp.Video;
using Moq;
using USharpVideoQueue.Tests.Editor.Utils;

namespace USharpVideoQueue.Tests.Editor
{
    public class VideoQueueTest
    {
        private Mock<USharpVideoPlayer> vpMock;
        private Mock<VideoQueueEventReceiver> eventReceiver;
        private Mock<VideoQueue> queueMock;
        private VideoQueue queue;

        [SetUp]
        public void Prepare()
        {
            queueMock = new Mock<VideoQueue>() { CallBase = true };
            queue = queueMock.Object;
            vpMock = new Mock<USharpVideoPlayer>();
            eventReceiver = new Mock<VideoQueueEventReceiver>();
            queue.VideoPlayer = vpMock.Object;
            queue.Start();
            queue.RegisterCallbackReceiver(eventReceiver.Object);

            //set default behaviour for ownership functions which would otherwise cause a null-reference, because gameObject is not accessible in tests
            queueMock.Setup(queue => queue.isOwner()).Returns(true);
            queueMock.Setup(queue => queue.isVideoPlayerOwner()).Returns(true);
            queueMock.Setup(queue => queue.getLocalPlayer()).Returns(new VRCPlayerApi()
            {
                displayName = "dummy player"
            });
        }

        [Test]
        public void CreateBehavior()
        {
            Assert.False(VideoQueue.Equals(queue, null));
            Assert.True(queue.Initialized);
            //Assert.True(VRC.SDKBase.Utilities.IsValid(queue));
        }

        [Test]
        public void CallbackRegisteredToPlayerAfterStart()
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
        public void QueueMultipleVideos()
        {
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
        public void InvalidURLQueued()
        {


            var invalidURL = new VRCUrl("https://invalid.url");
            queue.QueueVideo(invalidURL);
            queue.SendCustomEvent("OnUSharpVideoError");
            Assert.True(QueueArrayUtils.IsEmpty(queue.queuedVideos));
        }

        [Test]
        public void OnQueueContentChangeEmitsEvent()
        {
            queue.OnQueueContentChange();
            //Make sure subscribed receiver has received event from queue
            eventReceiver.Verify(rcv => rcv.OnUSharpVideoQueueContentChange(), Times.Once);
        }

        [Test]
        public void ChangesToQueueEmitEvents()
        {

            var url1 = new VRCUrl("https://url.one");
            queue.QueueVideo(url1);
            eventReceiver.Verify(rcv => rcv.OnUSharpVideoQueueContentChange(), Times.Exactly(1));
            queue.Skip();
            eventReceiver.Verify(rcv => rcv.OnUSharpVideoQueueContentChange(), Times.Exactly(2));
        }
    }
}
