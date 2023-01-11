using NUnit.Framework;
using UnityEngine;
using USharpVideoQueue.Runtime;
using Moq;
using VRC.SDK3.Components;
using USharpVideoQueue.Tests.Editor.Utils;
using VRC.SDKBase;
using static USharpVideoQueue.Runtime.Utility.QueueArray;

namespace USharpVideoQueue.Tests.Editor
{
    public class QueueControlsTest
    {
        private Mock<VideoQueue> queueMock;
        private VideoQueue queue;

        private QueueControls controls;
        private Mock<QueueControls> controlsMock;
        private VRCUrlInputField uiURLInput;
        private Mock<UIQueueItem>[] queueItems;

        [SetUp]
        public void Prepare()
        {
            var mockSet = UdonSharpTestUtils.CreateDefaultVideoQueueMockSet();
            queueMock = mockSet.VideoQueueMock;
            queue = mockSet.VideoQueueMock.Object;

            controlsMock = new Mock<QueueControls>{CallBase = true};
            controls = controlsMock.Object;
            controlsMock.Setup(controls => controls.getPlayerNameByID(It.IsAny<int>())).Returns("Player Name");
            
            uiURLInput = new GameObject().AddComponent<VRCUrlInputField>();
            controls.Queue = queue;
            controls.UIURLInput = uiURLInput;
            controls.Start();
        }

        [Test]
        public void QueueItemsAreInactiveOnStartup([NUnit.Framework.Range(1, 10)]int queueItemCount)
        {
            queueItems = createQueueItems(queueItemCount, controls);
            foreach (var queueItem in queueItems)
            {
                queueItem.Verify(item => item.SetActive(true), Times.Never);
                queueItem.Verify(item => item.SetActive(false), Times.AtLeastOnce);
            }
            
        }
        
        [Test]
        public void QueueControlsRegistersCorrectAmountOfQueueItems()
        {
            queueItems = createQueueItems(2, controls);
            Assert.AreEqual(2, controls.registeredQueueItems.Length);
        }

        [Test]
        public void QueueingVideoFillsQueueItems()
        {
            queueItems = createQueueItems(2, controls);
            queue.QueueVideo(new VRCUrl("https://url.one"));
            queueMock.Verify(queue => queue.SendCallback("OnUSharpVideoQueueContentChange"));
            //Mocked USharpBehaviors can't receive events naturally
            controls.SendCustomEvent("OnUSharpVideoQueueContentChange");
            queueItems[0].Verify(item => item.SetContent(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            queueItems[0].Verify(item => item.SetActive(true));
            queueItems[1].Verify(item => item.SetContent(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            queueItems[1].Verify(item => item.SetActive(false));
        }
        
        [Test]
        public void PressingRemoveRemovesCorrectRankAndVideoMovesForwardInQueue()
        {
            queueItems = createQueueItems(2, controls);
            VRCUrl url1 = new VRCUrl("https://url.one");
            queue.QueueVideo(url1);
            VRCUrl url2 = new VRCUrl("https://url.two");
            queue.QueueVideo(url2);
            queueItems[0].Object.OnRemovePressed();
            Assert.AreEqual(1, Count(queue.QueuedVideos));
        }

        private Mock<UIQueueItem>[] createQueueItems(int count, QueueControls queueControls)
        {
            Mock<UIQueueItem>[] queueItems = new Mock<UIQueueItem>[count];
            for (int i = 0; i < count; i++)
            {
                Mock<UIQueueItem> queueItem = new Mock<UIQueueItem>();
                queueItem.Object.Rank = i;
                queueItem.Object.QueueControls = queueControls;
                queueItem.Object.Start();
                queueItem.Setup(item => item.SetContent(It.IsAny<string>(), It.IsAny<string>()));
                queueItem.Setup(item => item.SetActive(It.IsAny<bool>()));
                queueItems[i] = queueItem;
            }

            return queueItems;
        }
    }
}