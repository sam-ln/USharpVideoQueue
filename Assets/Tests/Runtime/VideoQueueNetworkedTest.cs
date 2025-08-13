using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using USharpVideoQueue.Runtime;
using USharpVideoQueue.Runtime.Utility;
using USharpVideoQueue.Tests.Runtime.TestUtils;
using VRC.SDKBase;

namespace USharpVideoQueue.Tests.Runtime
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
            url0 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
        }

        [Test]
        public void OnPlayerLeftRemovesCurrentlyPlayingVideo()
        {
            // player 0 queues a video
            queue0.QueueVideo(url0);
            // player 1 queues a video
            queue1.QueueVideo(url1);
            //player 0 leaves, player 1 remains
            MockGroup.SimulatePlayerLeft(0);
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
            MockGroup.SimulatePlayerLeft(1);
            //Only 1 video remains
            Assert.AreEqual(1, QueueArray.Count(queue0.queuedVideos));
            //Remaining video is of player 1
            Assert.AreEqual(url0, queue0.queuedVideos[0]);
        }

        [Test]
        public void SecondPlayerCanQueueAfterFirstPlayerLeft()
        {
            UdonSharpTestUtils.VideoQueueMockSet stayingPlayer = MockGroup.MockSets[1];
            UdonSharpTestUtils.VideoQueueMockSet leavingPlayer = MockGroup.MockSets[0];
            
            queue0.QueueVideo(url0);

            //player 0 leaves
            queue1.becomeOwner();
            MockGroup.SimulatePlayerLeft(0);
            Assert.AreEqual(0, queue1.QueuedVideosCount());

            //enqueue as player 1
            queue1.QueueVideo(url0);

            //both players have initiated playback once. 2 videos in total.
            stayingPlayer.VideoPlayerMock.Verify(vp => vp.PlayVideo(url0), Times.Exactly(1));
            leavingPlayer.VideoPlayerMock.Verify(vp => vp.PlayVideo(url0), Times.Exactly(1));
        }

        [Test]
        public void FirstVideoCanOnlyBeRemovedAfterLoadingFinished()
        {
            queue0.QueueVideo(url0);
            queue0.OnUSharpVideoLoadStart();
            queue0.RequestRemoveVideo(0);
            // video was not removed because it is still loading
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            // video player signals loading is finished
            queue0.OnUSharpVideoLoadStart();
            queue0.OnUSharpVideoPlay();

            queue0.RequestRemoveVideo(0);
            // video can now be removed
            Assert.AreEqual(0, queue0.QueuedVideosCount());
        }

        [Test]
        public void FirstVideoIsRemovedAfterFailedManualRemovalAndPlayerError()
        {
            queue0.QueueVideo(url0);
            queue0.OnUSharpVideoLoadStart();
            queue0.RequestRemoveVideo(0);
            // video was not removed because it is still loading
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            // video player signals error
            
            queue0.OnUSharpVideoError();
            
            // video was removed
            Assert.AreEqual(0, queue0.QueuedVideosCount());
        }

        [Test]
        public void MultiplePlayersFillQueueAndWatchUntilItsEmpty([Range(1, 6)] int playerCount)
        {
            UdonSharpTestUtils.VideoQueueMockGroup mockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(playerCount);
            List<VRCUrl> testUrls = new List<VRCUrl>();
            VRCUrl initialURL = UdonSharpTestUtils.CreateUniqueVRCUrl();
            testUrls.Add(initialURL);

            mockGroup.MockSets[0].VideoQueueMock.Object.QueueVideo(initialURL);
            //simulate playing initial video
            mockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoLoadStart());
            mockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoPlay());
            for (int i = 1; i < playerCount; i++)
            {
                VRCUrl url = UdonSharpTestUtils.CreateUniqueVRCUrl();
                mockGroup.MockSets[i].VideoQueueMock.Object.QueueVideo(url);
                testUrls.Add(url);
            }

            //All players have synchronized all videos
            Assert.True(mockGroup.MockSets.TrueForAll(set =>
                set.VideoQueueMock.Object.QueuedVideosCount() == playerCount));

            // All videos are queued at this point, now starting to dequeue videos after they have been watched

            //simulate initial video has ended
            mockGroup.MockSets[0].VideoQueueMock.Object.OnUSharpVideoEnd();
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
                mockGroup.MockSets[i].VideoQueueMock.Object.OnUSharpVideoEnd();
            }

            //assert that every queue is empty now
            Assert.True(mockGroup.MockSets.TrueForAll(set =>
                set.VideoQueueMock.Object.QueuedVideosCount() == 0));

            //assert that no player has played a video they haven't queued
            for (int i = 0; i < playerCount; i++)
            {
                mockGroup.MockSets[i].VideoPlayerMock
                    .Verify(player => player.PlayVideo(It.IsAny<VRCUrl>()), Times.Once());
            }

        }

        [Test]
        public void LotsOfAlternatingNetworkedQueueingAndRemoving()
        {
            int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                queue0.QueueVideo(url0);
                queue0.OnUSharpVideoLoadStart();
                queue0.OnUSharpVideoPlay();
                Assert.AreEqual(1, queue0.QueuedVideosCount());
                Assert.AreEqual(1, queue1.QueuedVideosCount());
                queue0.RequestRemoveVideo(0);
                Assert.AreEqual(0, queue0.QueuedVideosCount());
                Assert.AreEqual(0, queue1.QueuedVideosCount());
                queue1.QueueVideo(url1);
                Assert.AreEqual(1, queue0.QueuedVideosCount());
                Assert.AreEqual(1, queue1.QueuedVideosCount());
                queue1.OnUSharpVideoLoadStart();
                queue1.OnUSharpVideoPlay();
                queue1.RequestRemoveVideo(0);
                Assert.AreEqual(0, queue0.QueuedVideosCount());
                Assert.AreEqual(0, queue1.QueuedVideosCount());
            }
        }

        [Test]
        public void CanOnlyRemoveFirstVideoOfOthersAfterLoadingHasFinished()
        {
            //make player 1 master of the session
            MockGroup.Master = MockGroup.MockSets[1];

            // non-master queues in two videos. After queueing in the first, the video player starts to load.
            queue0.QueueVideo(url0);
            MockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoLoadStart());
            queue0.QueueVideo(url1);
            MockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoPlay());
            
            //master removes playing song
            queue1.RequestRemoveVideo(0);
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(1, queue1.QueuedVideosCount());
            
            // new video starts loading
            MockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoLoadStart());
            
            // master unsuccessfully tries to remove loading video
            queue1.RequestRemoveVideo(0);
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(1, queue1.QueuedVideosCount());
            
            // second video starts playing
            MockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoPlay());
            
            // master successfully removes second video
            queue1.RequestRemoveVideo(0);
            Assert.AreEqual(0, queue0.QueuedVideosCount());
            Assert.AreEqual(0, queue1.QueuedVideosCount());
        }

        [Test]
        public void VideoErrorOnNonOwnerDoesNotAdvanceQueue()
        {
            queue0.QueueVideo(url0);
            MockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoLoadStart());
            MockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoPlay());
            //Non-owner experiences error
            queue1.OnUSharpVideoError();
            //Video should not be removed
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(1, queue1.QueuedVideosCount());
            
        }
        
        [Test]
        public void VideoErrorAdvancesQueueWhenVideoPlayerOwnerIsNotQueueOwner()
        {
            queue0.QueueVideo(url0);
            MockGroup.MockSets.ForEach(set => set.VideoQueueMock.Object.OnUSharpVideoLoadStart());
            queue1.QueueVideo(url1);
            //Video Player Owner is not longer Queue Owner
            //Only Video Player Owner experiences video player error
            queue1.OnUSharpVideoPlay();
            queue0.OnUSharpVideoError();
            
            //Video should be removed
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(1, queue1.QueuedVideosCount());
            
        }
        

        [Test]
        public void NonMasterCanOnlyRemoveOwnVideos()
        {
            queue0.QueueVideo(url0);
            queue0.QueueVideo(url1);
            queue1.QueueVideo(url1);
            Assert.AreEqual(3, queue0.QueuedVideosCount());
            Assert.AreEqual(3, queue1.QueuedVideosCount());
            //player 1 tries to remove video from player 0
            queue1.RequestRemoveVideo(1);
            //no video was removed because player 1 does not have permission to remove video from player 0
            Assert.AreEqual(3, queue0.QueuedVideosCount());
            Assert.AreEqual(3, queue1.QueuedVideosCount());
            
            //player 1 tries to remove video from player 1
            queue1.RequestRemoveVideo(2);
            
            Assert.AreEqual(2, queue0.QueuedVideosCount());
            Assert.AreEqual(2, queue1.QueuedVideosCount());
        }
        
        [Test]
        public void CannotQueueVideoIfUserLimitWouldBeExceeded()
        {
            queue0.SetVideoLimitPerUserEnabled(true);
            queue0.SetVideoLimitPerUser(1);
            queue1.QueueVideo(url0);
            //Video should be queued
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            queue1.QueueVideo(url1);
            //Video should not be queued
            Assert.AreEqual(1, queue1.QueuedVideosCount());
        }
        
        [Test]
        public void InstanceMasterCanExceedVideoLimit()
        {
            queue0.SetVideoLimitPerUserEnabled(true);
            queue0.SetVideoLimitPerUser(1);
            queue0.QueueVideo(url0);
            //Video should be queued
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            queue0.QueueVideo(url1);
            //Video should also be queued, although exceeding the user video limit
            Assert.AreEqual(2, queue1.QueuedVideosCount());
        }

        [Test]
        public void IncreasingVideoLimitPerVideo()
        {
            queue0.SetVideoLimitPerUserEnabled(true);
            queue0.SetVideoLimitPerUser(1);
            queue1.QueueVideo(url0);
            //Video should be queued
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(1, queue1.QueuedVideosCount());
            queue1.QueueVideo(url1);
            //Video should not be queued
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(1, queue1.QueuedVideosCount());

            queue0.SetVideoLimitPerUser(2);
            queue1.QueueVideo(url1);
            //Video should be queued
            Assert.AreEqual(2, queue0.QueuedVideosCount());
            Assert.AreEqual(2, queue1.QueuedVideosCount());
        }

        [Test]
        public void NonOwnerCannotClearQueue()
        {
            //player 0 is owner of the session
            MockGroup.Master = MockGroup.MockSets[0];
            //player 0 queues a video
            queue0.QueueVideo(url0);
            //player 1 (non-owner) tries to clear
            queue1.Clear();
            //Ensure video was not cleared
            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(1, queue1.QueuedVideosCount());
        }

        [Test]
        public void NonOwnerCannotMoveVideo()
        {
            //player 0 is master of the session
            MockGroup.Master = MockGroup.MockSets[0];
            queue0.QueueVideo(url0);
            queue0.OnUSharpVideoLoadStart();
            queue0.QueueVideo(url1);
            var url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue0.QueueVideo(url2);
            
            queue1.MoveVideo(2, true);
            Assert.AreEqual(url2, queue0.GetURL(2));
            Assert.AreEqual(url2, queue1.GetURL(2));
        }
    }
}