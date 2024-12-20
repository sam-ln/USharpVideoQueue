﻿using System;
using NUnit.Framework;
using USharpVideoQueue.Runtime.Utility;
using USharpVideoQueue.Tests.Runtime.TestUtils;
using VRC.SDKBase;
using static USharpVideoQueue.Runtime.Utility.QueueArray;

namespace USharpVideoQueue.Tests.Runtime
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
            string[] strArr = { "string" };
            Assert.AreEqual(EmptyReference(strArr), String.Empty);
            int[] intArr = { 1 };
            Assert.AreEqual(EmptyReference(intArr), -1);
            VRCUrl[] urlArr = { UdonSharpTestUtils.CreateUniqueVRCUrl() };
            Assert.AreEqual(EmptyReference(urlArr), VRCUrl.Empty);
        }

        [Test]
        public void IsFull()
        {
            Assert.True(QueueArray.IsFull(full));
            Assert.False(QueueArray.IsFull(half));
            Assert.False(QueueArray.IsFull(empty));
        }

        [Test]
        public void IsEmpty()
        {
            Assert.False(QueueArray.IsEmpty(full));
            Assert.False(QueueArray.IsEmpty(half));
            Assert.True(QueueArray.IsEmpty(empty));
        }

        [Test]
        public void FirstEmpty()
        {
            Assert.AreEqual((QueueArray.FirstEmpty(empty)), 0);
            Assert.AreEqual((QueueArray.FirstEmpty(half)), 3);
            Assert.AreEqual((QueueArray.FirstEmpty(full)), -1);
        }

        [Test]
        public void EnqueueDequeue()
        {
            VRCUrl add = UdonSharpTestUtils.CreateUniqueVRCUrl();
            VRCUrl[] queue = createQueue(5, 1);
            Enqueue(queue, add);
            Assert.AreEqual(queue[1], add);
            Dequeue(queue);
            Assert.AreEqual(queue[0], add);
            Dequeue(queue);
            Assert.True(QueueArray.IsEmpty(queue));
        }

        [Test]
        public void IllegalEnqueue()
        {
            VRCUrl add = UdonSharpTestUtils.CreateUniqueVRCUrl();
            VRCUrl[] queue = createQueue(1, 1);
            Assert.False(Enqueue(queue, add));
        }

        [Test]
        public void CountQueueElements()
        {
            Assert.AreEqual(Count(createQueue(5, 3)), 3);
            Assert.AreEqual(Count(createQueue(3, 3)), 3);
        }

        [Test]
        public void RemoveMiddleFromFullQueue()
        {
            Remove(full, 3);
            Assert.AreEqual(VRCUrl.Empty, full[full.Length - 1]);
            Assert.AreEqual(full.Length - 1, Count(full));
        }

        [Test]
        public void RemoveEndFromFullQueue()
        {
            Remove(full, full.Length - 1);
            Assert.AreEqual(VRCUrl.Empty, full[full.Length - 1]);
            Assert.AreEqual(full.Length - 1, Count(full));
        }

        [Test]
        public void ShiftContents()
        {
            string[] array = ExampleArray();
            ShiftBack(array);
            Assert.AreEqual(string.Empty, array[0]);
            Assert.AreEqual("one", array[1]);
            Assert.AreEqual("two", array[2]);
        }

        [Test]
        public void MoveUpFromMiddle()
        {
            string[] array = ExampleArray();
            MoveUp(array, 1);
            Assert.AreEqual("two", array[0]);
            Assert.AreEqual("one", array[1]);
            Assert.AreEqual("three", array[2]);
        }

        [Test]
        public void MoveUpFromStart_ShouldDoNothing()
        {
            string[] array = ExampleArray();
            MoveUp(array, 0);
            Assert.AreEqual("one", array[0]);
            Assert.AreEqual("two", array[1]);
            Assert.AreEqual("three", array[2]);
        }

        [Test]
        public void MoveUpWithNegativeIndex_ShouldDoNothing()
        {
            string[] array = ExampleArray();
            MoveUp(array, -1);
            Assert.AreEqual("one", array[0]);
            Assert.AreEqual("two", array[1]);
            Assert.AreEqual("three", array[2]);
        }

        [Test]
        public void MoveUpWithIndexOutOfRange_ShouldDoNothing()
        {
            string[] array = ExampleArray();
            MoveUp(array, 3);
            Assert.AreEqual("one", array[0]);
            Assert.AreEqual("two", array[1]);
            Assert.AreEqual("three", array[2]);
        }

        [Test]
        public void MoveDownFromMiddle()
        {
            string[] array = ExampleArray();
            MoveDown(array, 1);
            Assert.AreEqual("one", array[0]);
            Assert.AreEqual("three", array[1]);
            Assert.AreEqual("two", array[2]);
        }

        [Test]
        public void MoveDownFromStart()
        {
            string[] array = ExampleArray();
            MoveDown(array, 0);
            Assert.AreEqual("two", array[0]);
            Assert.AreEqual("one", array[1]);
            Assert.AreEqual("three", array[2]);
        }

        [Test]
        public void MoveDownFromEnd_ShouldDoNothing()
        {
            string[] array = ExampleArray();
            MoveDown(array, 2);
            Assert.AreEqual("one", array[0]);
            Assert.AreEqual("two", array[1]);
            Assert.AreEqual("three", array[2]);
        }

        [Test]
        public void MoveDownWithNegativeIndex_ShouldDoNothing()
        {
            string[] array = ExampleArray();
            MoveDown(array, -1);
            Assert.AreEqual("one", array[0]);
            Assert.AreEqual("two", array[1]);
            Assert.AreEqual("three", array[2]);
        }

        [Test]
        public void MoveDownWithIndexOutOfRange_ShouldDoNothing()
        {
            string[] array = ExampleArray();
            MoveDown(array, 3);
            Assert.AreEqual("one", array[0]);
            Assert.AreEqual("two", array[1]);
            Assert.AreEqual("three", array[2]);
        }


        private string[] ExampleArray() => new[] { "one", "two", "three" };

        private VRCUrl[] createQueue(int size, int members)
        {
            VRCUrl[] queueArray = new VRCUrl[size];
            for (int i = 0; i < members; i++)
            {
                queueArray[i] = UdonSharpTestUtils.CreateUniqueVRCUrl();
            }

            for (int j = members; j < size; j++)
            {
                queueArray[j] = VRCUrl.Empty;
            }

            return queueArray;
        }
    }
}