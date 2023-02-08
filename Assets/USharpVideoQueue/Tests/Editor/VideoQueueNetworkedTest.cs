using NUnit.Framework;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using Moq;
using USharpVideoQueue.Runtime.Utility;
using USharpVideoQueue.Tests.Editor.TestUtils;

namespace USharpVideoQueue.Tests.Editor
{
    public class VideoQueueNetworkedTest
    {
        private UdonSharpTestUtils.VideoQueueMockGroup MockGroup;
        private VideoQueue queue0;
        private VideoQueue queue1;

        [SetUp]
        public void Prepare()
        {
            MockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(2);
            queue0 = MockGroup.MockSets[0].VideoQueueMock.Object;
            queue1 = MockGroup.MockSets[1].VideoQueueMock.Object;
        }

        [Test]
        public void OnPlayerLeftRemovesCurrentlyPlayingVideo()
        {
            var url0 = new VRCUrl("https://url.zero");
            queue0.QueueVideo(url0);
            //enqueue as player 1
            var url1 = new VRCUrl("https://url.one");
            queue1.QueueVideo(url1);
            //player 0 leaves, player 1 remains
            queue1.OnPlayerLeft(MockGroup.MockSets[0].Player);
            //Only 1 video should remain after first video is removed
            Assert.AreEqual(1, QueueArray.Count(queue1.queuedVideos));
            //Remaining video was queued by player 1
            Assert.AreEqual(url1, queue1.queuedVideos[0]);
        }

        [Test]
        public void OnPlayerLeftRemovesLeftoverVideos()
        {
            //enqueue as player 0
            var url0 = new VRCUrl("https://url.zero");
            queue0.QueueVideo(url0);
            //enqueue as player 1
            var url1 = new VRCUrl("https://url.one");
            queue1.QueueVideo(url1);
            //player 1 leaves
            queue0.becomeOwner();
            queue0.OnPlayerLeft(MockGroup.MockSets[1].Player);
            //Only 1 video remains
            Assert.AreEqual(1, QueueArray.Count(queue0.queuedVideos));
            //Remaining video is of player 1
            Assert.AreEqual(url0, queue0.queuedVideos[0]);
        }

        [Test]
        public void SecondPlayerCanQueueAfterFirstPlayerLeft()
        {
            var url0 = new VRCUrl("https://url.zero");
            queue0.QueueVideo(url0);

            //player 0 leaves
            queue1.becomeOwner();
            queue1.OnPlayerLeft(MockGroup.MockSets[0].Player);
            Assert.AreEqual(0, queue1.QueuedVideosCount());

            //enqueue as player 1
            queue1.QueueVideo(url0);

            //both players have initiated playback once. 2 videos in total.
            MockGroup.MockSets[0].VideoPlayerMock.Verify(vp => vp.PlayVideo(url0), Times.Exactly(1));
            MockGroup.MockSets[1].VideoPlayerMock.Verify(vp => vp.PlayVideo(url0), Times.Exactly(1));
        }
    }
}