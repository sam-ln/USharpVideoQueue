
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
    public class QueueUtilsTest
    {
        VRCUrl[] full;
        VRCUrl[] half;
        VRCUrl[] empty;


        [SetUp]
        public void Prepare()
        {
            full = createQueue(5, 5);
            half = createQueue(5, 3);
            empty = createQueue(5, 0);
        }

        [Test]
        public void IsFull()
        {
            Assert.True(VideoQueue.isFull(full));
            Assert.False(VideoQueue.isFull(half));
            Assert.False(VideoQueue.isFull(empty));
        }

        [Test]
        public void IsEmpty()
        {
            Assert.False(VideoQueue.isEmpty(full));
            Assert.False(VideoQueue.isEmpty(half));
            Assert.True(VideoQueue.isEmpty(empty));
        }

        [Test]
        public void FirstEmpty()
        {
            Assert.AreEqual((VideoQueue.firstEmpty(empty)), 0);
            Assert.AreEqual((VideoQueue.firstEmpty(half)), 3);
            Assert.AreEqual((VideoQueue.firstEmpty(full)), -1);
        }

        [Test]
        public void EnqueueDequeue()
        {
            VRCUrl add = new VRCUrl("https://url.one");
            VRCUrl[] queue = createQueue(5, 1);
            VideoQueue.enqueue(queue, add);
            Assert.AreEqual(queue[1], add);
            VideoQueue.dequeue(queue);
            Assert.AreEqual(queue[0], add);
            VideoQueue.dequeue(queue);
            Assert.True(VideoQueue.isEmpty(queue));
        }

        [Test]
        public void IllegalEnqueue()
        {
            VRCUrl add = new VRCUrl("https://url.one");
            VRCUrl[] queue = createQueue(1, 1);
            Assert.False(VideoQueue.enqueue(queue, add));
        }

        public VRCUrl[] createQueue(int size, int members)
        {
            VRCUrl[] queueArray = new VRCUrl[size];
            for (int i = 0; i < members; i++)
            {
                queueArray[i] = new VRCUrl("https://www.test.com");
            }
            return queueArray;
        }
    }
}
