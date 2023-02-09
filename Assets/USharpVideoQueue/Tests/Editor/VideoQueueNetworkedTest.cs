using System.Collections.Generic;
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
        private VRCUrl url0;
        private VRCUrl url1;

        [SetUp]
        public void Prepare()
        {
            MockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(2);
            queue0 = MockGroup.MockSets[0].VideoQueueMock.Object;
            queue1 = MockGroup.MockSets[1].VideoQueueMock.Object;
            url0 = new VRCUrl("https://url.zero");
            url1 = new VRCUrl("https://url.one");
        }

        [Test]
        public void OnPlayerLeftRemovesCurrentlyPlayingVideo()
        {
            // player 0 queues a video
            queue0.QueueVideo(url0);
            // player 1 queues a video
            queue1.QueueVideo(url1);
            //player 0 leaves, player 1 remains
            queue1.OnPlayerLeft(MockGroup.MockSets[0].Player);
            //Only 1 video should remain after first video is removed
            Assert.AreEqual(1, QueueArray.Count(queue1.queuedVideos));
            //Remaining video should be the one queued by player 1
            Assert.AreEqual(url1, queue1.queuedVideos[0]);
        }

        [Test]
        public void OnPlayerLeftRemovesLeftoverVideos()
        {
            //enqueue as player 0
            queue0.QueueVideo(url0);
            //enqueue as player 1
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

        [Test]
        public void FirstVideoCanOnlyBeRemovedAfterLoadingFinished()
        {
            queue0.QueueVideo(url0);
            queue0.OnUSharpVideoLoadStart();
            queue0.RemoveVideo(0);
            // video was not removed because it is still loading
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            // video player signals loading is finished
            queue0.OnUSharpVideoLoadStart();
            queue0.OnUSharpVideoPlay();

            queue0.RemoveVideo(0);
            // video can now be removed
            Assert.AreEqual(0, queue0.QueuedVideosCount());
        }

        [Test]
        public void FirstVideoCanBeRemovedAfterError()
        {
            queue0.QueueVideo(url0);
            // video was not removed because it is still loading
            // video player signals error
            queue0.OnUSharpVideoLoadStart();
            queue0.OnUSharpVideoError();
            queue0.RemoveVideo(0);
            // video was removed
            Assert.AreEqual(0, queue0.QueuedVideosCount());
        }

        [Test]
        public void MultiplePlayersFillQueueAndWatchUntilItsEmpty([Range(1, 6)] int playerCount)
        {
            UdonSharpTestUtils.VideoQueueMockGroup mockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(playerCount);
            List<VRCUrl> testUrls = new List<VRCUrl>();
            VRCUrl initialURL = new VRCUrl("https://initial.url");
            testUrls.Add(initialURL);
            
            mockGroup.MockSets[0].VideoQueueMock.Object.QueueVideo(initialURL);
            //simulate playing initial video
            mockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoLoadStart());
            mockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoPlay());
            for (int i = 1; i < playerCount; i++)
            {
                VRCUrl url = new VRCUrl($"https://url.number/{i}");
                mockGroup.MockSets[i].VideoQueueMock.Object.QueueVideo(url);
                testUrls.Add(url);
            }

            //All players have synchronized all videos
            Assert.True(mockGroup.MockSets.TrueForAll(set =>
                set.VideoQueueMock.Object.QueuedVideosCount() == playerCount));
            
            //simulate initial video has ended
            mockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoEnd());
            int expectedRemainingVideos = playerCount - 1;
            for (int i = 1; i < playerCount; i++)
            {
                //assert that all players have the expected amount of remaining videos in queue
                Assert.True(mockGroup.MockSets.TrueForAll(set =>
                    set.VideoQueueMock.Object.QueuedVideosCount() == expectedRemainingVideos));
                expectedRemainingVideos--;
                
                //verify that this player played the video that they queued
                mockGroup.MockSets[i].VideoPlayerMock.Verify(player => player.PlayVideo(testUrls[i]), Times.Once);
                
                //simulate new video has started
                mockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoLoadStart());
                mockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoPlay());
                
                //simulate this video has ended
                mockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoEnd());
            }
            
            //assert that every queue is empty now
            Assert.True(mockGroup.MockSets.TrueForAll(set =>
                set.VideoQueueMock.Object.QueuedVideosCount() == 0));
        }
    }
}