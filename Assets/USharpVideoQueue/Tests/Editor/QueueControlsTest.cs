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

        private string MOCK_PLAYER_NAME = "Player Name";

        [SetUp]
        public void Prepare()
        {
            MockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(2);
            queueMock = MockGroup.MockSets[0].VideoQueueMock;
            queue0 = MockGroup.MockSets[0].VideoQueueMock.Object;
            queue1 = MockGroup.MockSets[1].VideoQueueMock.Object;

            controlsMock = new Mock<QueueControls> { CallBase = true };
            controls = controlsMock.Object;
            controlsMock.Setup(controls => controls.getPlayerNameByID(It.IsAny<int>())).Returns(MOCK_PLAYER_NAME);

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
                Assert.AreEqual(false, queueItem.Object.active);
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
            VRCUrl url = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue0.QueueVideo(url);
            Assert.AreEqual(true, queueItems[0].Object.active);
            Assert.AreEqual(url.ToString(), queueItems[0].Object.description);
            Assert.AreEqual(MOCK_PLAYER_NAME, queueItems[0].Object.queuedBy);

            Assert.AreEqual(false, queueItems[1].Object.active);
        }

        [Test]
        public void PressingRemoveRemovesCorrectRankAndVideoMovesForwardInQueue()
        {
            queueItems = createQueueItems(2, controls);
            VRCUrl url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue0.QueueVideo(url1);
            queue0.OnUSharpVideoPlay();
            VRCUrl url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue0.QueueVideo(url2);
            queueItems[0].Object.OnRemovePressed();
            Assert.AreEqual(1, Count(queue0.queuedVideos));
        }

        [Test]
        public void RemoveButtonOnlyEnabledOnPermittedVideos()
        {
            //make player 1 master of the instance
            VRCUrl url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            MockGroup.Master = MockGroup.MockSets[1];
            queueItems = createQueueItems(2, controls);
            queue0.QueueVideo(url1);
            queue1.QueueVideo(url1);

            //video 0 should be removable, video 1 should not
            Assert.AreEqual(true, queueItems[0].Object.removeEnabled);
            Assert.AreEqual(false, queueItems[1].Object.removeEnabled);
        }

        [Test]
        public void SwitchPageForthAndBack()
        {
            queueItems = createQueueItems(2, controls);
            for (int i = 0; i < 3; i++)
            {
                queue0.QueueVideo(UdonSharpTestUtils.CreateUniqueVRCUrl());
            }
            controls.SetCurrentPage(1);
            Assert.AreEqual(true, queueItems[0].Object.active);
            Assert.AreEqual(false, queueItems[1].Object.active);
        }

        [Test]
        public void ShowingPreviousPageIfCurrentPageBecomesEmpty()
        {
            controls.SetPageAutomatically = true;
            queueItems = createQueueItems(2, controls);
            for (int i = 0; i < 3; i++)
            {
                queue0.QueueVideo(UdonSharpTestUtils.CreateUniqueVRCUrl());
            }
            controls.SetCurrentPage(1);
            Assert.AreEqual(1, controls.CurrentPage);

            // one video plays through and is removed
            queue0.OnUSharpVideoLoadStart();
            queue0.OnUSharpVideoPlay();
            queue0.OnUSharpVideoEnd();

            Assert.AreEqual(0, controls.CurrentPage);
        }

        public void RemovingLastVideo()
        {
            controls.SetPageAutomatically = true;
            queueItems = createQueueItems(2, controls);
            for (int i = 0; i < 1; i++)
            {
                queue0.QueueVideo(UdonSharpTestUtils.CreateUniqueVRCUrl());
            }
            Assert.AreEqual(0, controls.CurrentPage);

            // last video plays through and is removed
            queue0.OnUSharpVideoLoadStart();
            queue0.OnUSharpVideoPlay();
            queue0.OnUSharpVideoEnd();

            Assert.AreEqual(0, controls.CurrentPage);
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
                queueItem.Setup(item => item.UpdateGameObjects());
                queueItems[i] = queueItem;
            }

            return queueItems;
        }
    }
}