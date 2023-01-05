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

        private Mock<VideoQueue> queueMock1;
        private VideoQueue queue1;

        private Mock<VideoQueue> queueMock2;
        private VideoQueue queue2;

        [SetUp]
        public void Prepare()
        {
            queueMock1 = UdonSharpTestUtils.CreateDefaultVideoQueueMockSet().VideoQueueMock;
            queue1 = queueMock1.Object;
            queueMock2 = UdonSharpTestUtils.CreateDefaultVideoQueueMockSet().VideoQueueMock;
            queue2 = queueMock2.Object;
        }

        [Test]
        public void TestSerialization()
        {
            //Test if I can check for calls to RequestSerialization() though mocking
            queue1.synchronizeQueueState();
            queueMock1.Verify(queue => queue.synchronizeQueueState(), Times.Once);

        }

        [Test]
        public void TestSerializationSimulation()
        {
            //Simulate two players by creating two distinct queues. Only queue 1 has a video queued.

            var url1 = new VRCUrl("https://url.one");
            queue1.QueueVideo(url1);
            
            //Copy contents of queue 1 to queue 2      
            UdonSharpTestUtils.SimulateSerialization(queue1, queue2);

            //Check if queue 2 received queued video from queue 1
            Assert.False(QueueArrayUtils.IsEmpty(queue2.queuedVideos));
            var url2 = new VRCUrl("https://url.two");
            //Check that queue 2 has a copy and not a reference
            queue1.QueueVideo(url2);
            Assert.AreEqual( 2, QueueArrayUtils.Count(queue1.queuedVideos));
            Assert.AreEqual( 1, QueueArrayUtils.Count(queue2.queuedVideos));
        }

    }
}
