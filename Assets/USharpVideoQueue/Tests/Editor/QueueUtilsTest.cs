﻿
using NUnit.Framework;
using VRC.SDKBase;

using static USharpVideoQueue.Runtime.QueueArrayUtils;

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
            Assert.True(Runtime.QueueArrayUtils.IsFull(full));
            Assert.False(Runtime.QueueArrayUtils.IsFull(half));
            Assert.False(Runtime.QueueArrayUtils.IsFull(empty));
        }

        [Test]
        public void IsEmpty()
        {
            Assert.False(Runtime.QueueArrayUtils.IsEmpty(full));
            Assert.False(Runtime.QueueArrayUtils.IsEmpty(half));
            Assert.True(Runtime.QueueArrayUtils.IsEmpty(empty));
        }

        [Test]
        public void FirstEmpty()
        {
            Assert.AreEqual((Runtime.QueueArrayUtils.FirstEmpty(empty)), 0);
            Assert.AreEqual((Runtime.QueueArrayUtils.FirstEmpty(half)), 3);
            Assert.AreEqual((Runtime.QueueArrayUtils.FirstEmpty(full)), -1);
        }

        [Test]
        public void EnqueueDequeue()
        {
            VRCUrl add = new VRCUrl("https://url.one");
            VRCUrl[] queue = createQueue(5, 1);
            Enqueue(queue, add);
            Assert.AreEqual(queue[1], add);
            Dequeue(queue);
            Assert.AreEqual(queue[0], add);
            Dequeue(queue);
            Assert.True(Runtime.QueueArrayUtils.IsEmpty(queue));
        }

        [Test]
        public void IllegalEnqueue()
        {
            VRCUrl add = new VRCUrl("https://url.one");
            VRCUrl[] queue = createQueue(1, 1);
            Assert.False(Enqueue(queue, add));
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
