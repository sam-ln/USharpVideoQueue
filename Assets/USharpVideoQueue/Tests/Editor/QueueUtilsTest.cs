﻿
using System;
using NUnit.Framework;
using VRC.SDKBase;

using static USharpVideoQueue.Runtime.QueueArrayUtils;

namespace USharpVideoQueue.Tests.Editor.Utils
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
        public void EmptyReferencesDetermined()
        {
            string[] strArr = {"string"};
            Assert.AreEqual(EmptyReference(strArr), String.Empty);
            int[] intArr = {1};
            Assert.AreEqual(EmptyReference(intArr), -1);
            VRCUrl[] urlArr = {new VRCUrl("https://url.one")};
            Assert.AreEqual(EmptyReference(urlArr), VRCUrl.Empty);
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
        
        [Test]
        public void CountQueueElements()
        {
            Assert.AreEqual(Count(createQueue(5, 3)), 3);
            Assert.AreEqual(Count(createQueue(3, 3)), 3);
        }

        public VRCUrl[] createQueue(int size, int members)
        {
            VRCUrl[] queueArray = new VRCUrl[size];
            for (int i = 0; i < members; i++)
            {
                queueArray[i] = new VRCUrl("https://www.test.com");
            }
            for (int j = members; j < size; j++)
            {
                queueArray[j] = VRCUrl.Empty;
            }
            return queueArray;
        }
    }
}
