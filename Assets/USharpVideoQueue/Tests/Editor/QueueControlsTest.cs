using NUnit.Framework;
using UnityEngine;
using USharpVideoQueue.Runtime;
using Moq;
using USharpVideoQueue.Tests.Editor.TestUtils;
using VRC.SDK3.Components;
using VRC.SDKBase;
using static USharpVideoQueue.Runtime.Utility.QueueArray;

namespace USharpVideoQueue.Tests.Editor
{
    public class QueueControlsTest
    {
        private Mock<VideoQueue> queueMock;
        private VideoQueue queue0;
        private VideoQueue queue1;

        private QueueControls controls;
        private Mock<QueueControls> controlsMock;
        private VRCUrlInputField uiURLInput;
        private Mock<UIQueueItem>[] queueItems;
        private UdonSharpTestUtils.VideoQueueMockGroup MockGroup;

        [SetUp]
        public void Prepare()
        {
            MockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(2);
            queueMock = MockGroup.MockSets[0].VideoQueueMock;
            queue0 = MockGroup.MockSets[0].VideoQueueMock.Object;
            queue1 = MockGroup.MockSets[1].VideoQueueMock.Object;

            controlsMock = new Mock<QueueControls>{CallBase = true};
            controls = controlsMock.Object;
            controlsMock.Setup(controls => controls.getPlayerNameByID(It.IsAny<int>())).Returns("Player Name");

            uiURLInput = new GameObject().AddComponent<VRCUrlInputField>();
            controls.Queue = queue0;
            controls.UIURLInput = uiURLInput;
            controls.EnsureInitialized();
        }

        [Test]
        public void QueueItemsAreInactiveOnStartup([NUnit.Framework.Range(1, 10)] int queueItemCount)
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
            queue0.QueueVideo(new VRCUrl("https://url.one"));
            queueMock.Verify(queue => queue.SendCallback("OnUSharpVideoQueueContentChange"));
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
            queue0.QueueVideo(url1);
            queue0.OnUSharpVideoPlay();
            VRCUrl url2 = new VRCUrl("https://url.two");
            queue0.QueueVideo(url2);
            queueItems[0].Object.OnRemovePressed();
            Assert.AreEqual(1, Count(queue0.queuedVideos));
        }

        [Test]
        public void RemoveButtonOnlyEnabledOnPermittedVideos()
        {
            //make player 1 master of the instance
            VRCUrl url1 = new VRCUrl("https://url.one");
            MockGroup.Master = MockGroup.MockSets[1];
            queueItems = createQueueItems(2, controls);
            queue0.QueueVideo(url1);
            queue1.QueueVideo(url1);

            //video 0 should be removable, video 1 should not
            queueItems[0].Verify(item => item.SetRemoveEnabled(true));
            queueItems[1].Verify(item => item.SetRemoveEnabled(false));
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