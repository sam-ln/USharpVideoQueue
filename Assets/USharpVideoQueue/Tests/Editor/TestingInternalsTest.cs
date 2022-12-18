using NUnit.Framework;
using UnityEngine;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using UdonSharp.Video;
using Moq;
using USharpVideoQueue.Tests.Editor.Utils;

namespace USharpVideoQueue.Tests.Editor
{
    public class TestingInternalsTest
    {
        private Mock<USharpVideoPlayer> vpMock;
        private Mock<VideoQueueEventReceiver> eventReceiver;
        private Mock<VideoQueue> queueMock;

        private VideoQueue queue;

        [SetUp]
        public void Prepare()
        {
            queueMock = new Mock<VideoQueue>() { CallBase = true };
            //queue = new GameObject().AddComponent<VideoQueue>();
            queue = queueMock.Object;
            vpMock = new Mock<USharpVideoPlayer>();
            queue.VideoPlayer = vpMock.Object;
            queue.Start();
        }

        [Test]
        public void TestSerialization()
        {
            //Test if I can check for calls to RequestSerialization() though mocking
            queue.synchronizeQueueState();
            queueMock.Verify(queue => queue.synchronizeQueueState(), Times.Once);

        }

        [Test]
        public void TestSerializationSimulation()
        {
            //Simulate two players by creating two distinct queues. Only queue 1 has a video queued.
            VideoQueue queueP1 = new GameObject().AddComponent<VideoQueue>();
            queueP1.VideoPlayer = vpMock.Object;
            queueP1.Start();
            var url1 = new VRCUrl("https://url.one");
            queueP1.QueueVideo(url1);
            VideoQueue queueP2 = new GameObject().AddComponent<VideoQueue>();
            queueP2.VideoPlayer = vpMock.Object;
            queueP2.Start();

            //Copy contents of queue 1 to queue 2      
            UdonSharpTestUtils.SimulateSerialization<VideoQueue>(queueP1, queueP2);

            //Check if queue 2 received queued video from queue 1
            Assert.False(QueueArrayUtils.isEmpty(queueP2.queuedVideos));
            var url2 = new VRCUrl("https://url.two");
            //Check that queue 2 has a copy and not a reference
            queueP2.QueueVideo(url2);
            Assert.AreEqual(QueueArrayUtils.firstEmpty(queueP2.queuedVideos), 2);
            Assert.AreEqual(QueueArrayUtils.firstEmpty(queueP1.queuedVideos), 1);
        }

    }
}
