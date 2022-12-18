
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
            Assert.True(isFull(full));
            Assert.False(isFull(half));
            Assert.False(isFull(empty));
        }

        [Test]
        public void IsEmpty()
        {
            Assert.False(isEmpty(full));
            Assert.False(isEmpty(half));
            Assert.True(isEmpty(empty));
        }

        [Test]
        public void FirstEmpty()
        {
            Assert.AreEqual((firstEmpty(empty)), 0);
            Assert.AreEqual((firstEmpty(half)), 3);
            Assert.AreEqual((firstEmpty(full)), -1);
        }

        [Test]
        public void EnqueueDequeue()
        {
            VRCUrl add = new VRCUrl("https://url.one");
            VRCUrl[] queue = createQueue(5, 1);
            enqueue(queue, add);
            Assert.AreEqual(queue[1], add);
            dequeue(queue);
            Assert.AreEqual(queue[0], add);
            dequeue(queue);
            Assert.True(isEmpty(queue));
        }

        [Test]
        public void IllegalEnqueue()
        {
            VRCUrl add = new VRCUrl("https://url.one");
            VRCUrl[] queue = createQueue(1, 1);
            Assert.False(enqueue(queue, add));
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
