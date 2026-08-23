using System.Text.RegularExpressions;
using Moq;
using NUnit.Framework;
using UdonSharp.Video;
using UnityEngine;
using UnityEngine.TestTools;
using USharpVideoQueue.Runtime;
using USharpVideoQueue.Runtime.Utility;
using USharpVideoQueue.Tests.Runtime.TestUtils;
using VRC.SDKBase;

namespace USharpVideoQueue.Tests.Runtime
{
    public class VideoQueueLocalTest
    {
        private Mock<VideoQueue> queueMock;
        private VideoQueue queue;
        private NonVirtualMock<USharpVideoPlayer> vpMock;
        private Mock<VideoQueueEventReceiver> eventReceiverMock;

        [SetUp]
        public void Prepare()
        {
            var mockSet = UdonSharpTestUtils.CreateDefaultVideoQueueMockSet();
            queueMock = mockSet.VideoQueueMock;
            queue = mockSet.VideoQueueMock.Object;
            vpMock = mockSet.VideoPlayerMock;
            eventReceiverMock = mockSet.EventReceiverMock;
        }

        [Test]
        public void CreateBehavior()
        {
            Assert.False(VideoQueue.Equals(queue, null));
            Assert.True(queue.initialized);
            //Assert.True(VRC.SDKBase.Utilities.IsValid(queue));
        }

        [Test]
        public void CallbackRegisteredToPlayerAfterStart()
        {
            vpMock.Verify(vp => vp.RegisterCallbackReceiver(queue), Times.Once());
        }


        [Test]
        public void DuplicateOrLateVideoEndReportDoesNotSkipNextVideo()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.QueueVideo(url2);
            queue.OnUSharpVideoLoadStart();
            queue.OnUSharpVideoPlay();
            int playbackNumberOfFirstVideo = queue.videosPlayed;

            //first video ends regularly, second video is up
            queue.OnUSharpVideoEnd();
            Assert.AreEqual(1, queue.QueuedVideosCount());
            Assert.AreEqual(url2, queue.GetURL(0));

            //a duplicate end report for the first video arrives over the network
            //(e.g. AVPro firing OnVideoEnd twice); both videos were queued by the same player,
            //so the head-owner check alone would not catch it
            queue.RPC_OnVideoOwnerVideoEnd(1, playbackNumberOfFirstVideo);

            //the second video must not be skipped
            Assert.AreEqual(1, queue.QueuedVideosCount());
            Assert.AreEqual(url2, queue.GetURL(0));
        }

        [Test]
        public void DuplicateVideoErrorReportsOnlyRemoveCurrentVideo()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url3 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.QueueVideo(url2);
            queue.QueueVideo(url3);
            queue.OnUSharpVideoLoadStart();
            int playbackNumberOfFirstVideo = queue.videosPlayed;

            //first video fails to load
            queue.OnUSharpVideoError();
            Assert.AreEqual(2, queue.QueuedVideosCount());

            //further error events for the same video arrive over the network
            //(AVPro multi-fire, USharpVideo retries)
            queue.RPC_OnVideoOwnerVideoError(1, playbackNumberOfFirstVideo);
            queue.RPC_OnVideoOwnerVideoError(1, playbackNumberOfFirstVideo);

            //only the failed video was removed
            Assert.AreEqual(2, queue.QueuedVideosCount());
            Assert.AreEqual(url2, queue.GetURL(0));
        }

        [Test]
        public void RemoveRequestWithOutdatedIndexIsIgnored()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.QueueVideo(url2);
            queue.OnUSharpVideoLoadStart();
            queue.OnUSharpVideoPlay();

            //a remove request computed against an outdated replica: the requester saw url1
            //at index 1, but a different video sits there by now
            queue.RPC_OnRemoveVideoRequested(1, 1, url1.Get());
            Assert.AreEqual(2, queue.QueuedVideosCount());

            //a request whose expected URL matches the entry is executed
            queue.RPC_OnRemoveVideoRequested(1, 1, url2.Get());
            Assert.AreEqual(1, queue.QueuedVideosCount());
        }

        [Test]
        public void EnsureInitializedPreservesStateReceivedBeforeStart()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);

            //Simulate a late joiner whose initial deserialization arrives before Start has run
            Mock<VideoQueue> joinerMock = new Mock<VideoQueue> { CallBase = true };
            UdonSharpTestUtils.MockSDKCalls(joinerMock);
            VideoQueue joiner = joinerMock.Object;
            joiner.VideoPlayer = new Mock<USharpVideoPlayer>().Object;
            joiner.timer = new Mock<RPCTimer>().Object;

            UdonSharpTestUtils.SimulateSerialization(queue, joiner);
            Assert.AreEqual(1, joiner.QueuedVideosCount());

            //Start runs after the initial network state was received
            joiner.EnsureInitialized();

            //the received queue state must survive initialization
            Assert.AreEqual(1, joiner.QueuedVideosCount());
            Assert.AreEqual(url1, joiner.GetURL(0));
        }

        [Test]
        public void EmptyTitleFallsBackToUrl()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1, "");
            queue.QueueVideo(url2, "second");

            //an empty title would collide with the empty-slot sentinel of the titles array
            //and shift all later titles by one position
            Assert.AreEqual(url1.Get(), queue.GetTitle(0));
            Assert.AreEqual("second", queue.GetTitle(1));
        }

        [Test]
        public void QueueAndFinishVideo()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.SendCustomEvent("OnUSharpVideoEnd");
            vpMock.Verify((vp => vp.PlayVideo(url1)), Times.Once);
        }

        [Test]
        public void QueueMultipleVideos()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.OnUSharpVideoPlay();
            queue.QueueVideo(url2);
            //Queued Video were serialized to other players
            queueMock.Verify(queue => queue._SynchronizeData(), Times.AtLeast(2));
            //Video Player has played first url
            vpMock.Verify((vp => vp.PlayVideo(url1)), Times.Once);
            queue.SendCustomEvent("OnUSharpVideoEnd");
            //Video Player has played the second url
            vpMock.Verify((vp => vp.PlayVideo(url1)), Times.Once);
            vpMock.Verify((vp => vp.PlayVideo(url2)), Times.Once);
        }

        [Test]
        public void InvalidURLQueued()
        {
            var invalidURL = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(invalidURL);
            queue.SendCustomEvent("OnUSharpVideoError");
            Assert.True(QueueArray.IsEmpty(queue.queuedVideos));
        }

        [Test]
        public void OnQueueContentChangeEmitsEvent()
        {
            queue.OnQueueContentChange();
            //Make sure subscribed receiver has received event from queue
            eventReceiverMock.Verify(rcv => rcv.OnUSharpVideoQueueContentChange(), Times.Once);
        }

        /// <summary>
        /// Asserts that mutating the queue notifies subscribers. The absolute number of events is
        /// deliberately not pinned down: the queue emits one content change per synchronization, and
        /// a single queue-and-play cycle legitimately synchronizes several times (enqueue, playback
        /// armed, advance counted, playback confirmed).
        /// </summary>
        [Test]
        public void ChangesToQueueEmitEvents()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.OnUSharpVideoPlay();
            int afterQueueing = UdonSharpTestUtils.CountCalls(eventReceiverMock,
                nameof(VideoQueueEventReceiver.OnUSharpVideoQueueContentChange));
            Assert.Greater(afterQueueing, 0, "Queueing a video should emit a content change event");

            queue.RemoveVideo(0);

            int afterRemoving = UdonSharpTestUtils.CountCalls(eventReceiverMock,
                nameof(VideoQueueEventReceiver.OnUSharpVideoQueueContentChange));
            Assert.Greater(afterRemoving, afterQueueing,
                "Removing a video should emit at least one further content change event");
        }

        [Test]
        public void VideoPlayerIsClearedAfterLastVideoIsRemoved()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.OnUSharpVideoPlay();
            queue.RemoveVideo(0);
            vpMock.Verify(vp => vp.StopVideo(), Times.Once);
        }

        [Test]
        public void VideoPlayerIsClearedAfterLastVideoFinished()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.OnUSharpVideoPlay();
            queue.OnUSharpVideoEnd();
            vpMock.Verify(vp => vp.StopVideo(), Times.Once);
        }

        /// <summary>
        /// Removing the video that is currently loading used to be rejected outright. Since
        /// "Allowing removing currently loading videos, but showing a warning" the removal goes
        /// through and the queue only warns about it.
        /// </summary>
        [Test]
        public void FirstVideoCanBeRemovedWhileLoadingButWarns()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.SendCustomEvent(nameof(VideoQueue.OnUSharpVideoLoadStart));

            LogAssert.Expect(LogType.Warning, new Regex("currently being loaded"));
            queue.RemoveVideo(0);

            Assert.AreEqual(0, QueueArray.Count(queue.queuedVideos));
        }

        [Test]
        public void FirstVideoCanBeRemovedAfterLoadingHasFinished()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.SendCustomEvent(nameof(VideoQueue.OnUSharpVideoLoadStart));
            queue.SendCustomEvent(nameof(VideoQueue.OnUSharpVideoPlay));

            queue.RemoveVideo(0);

            Assert.AreEqual(0, QueueArray.Count(queue.queuedVideos));
        }

        [Test]
        public void PublicQueueArrayAccessors()
        {
            int outOfBoundsNumber = 500;

            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            Assert.AreEqual(1, queue.QueuedVideosCount());
            Assert.AreEqual(url1, queue.GetURL(0));
            Assert.AreEqual(VRCUrl.Empty, queue.GetURL(outOfBoundsNumber));
        }

        [Test]
        public void CallbacksAreEmitted()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            //queue first video
            queue.QueueVideo(url1);
            //first video starts playing
            queue.OnUSharpVideoLoadStart();
            queue.OnUSharpVideoPlay();
            //queue second video
            queue.QueueVideo(url1);
            //queue third video
            queue.QueueVideo(url1);
            eventReceiverMock.Verify(receiver => receiver.OnUSharpVideoQueueContentChange(), Times.AtLeast(3));
            //first video has ended
            queue.OnUSharpVideoEnd();
            eventReceiverMock.Verify(receiver => receiver.OnUSharpVideoQueuePlayingNextVideo(), Times.Once);
            //second video starts loading
            queue.OnUSharpVideoLoadStart();
            //loading failed
            queue.OnUSharpVideoError();
            eventReceiverMock.Verify(receiver => receiver.OnUSharpVideoQueueSkippedError(), Times.Once);
            //third video starts playing
            queue.OnUSharpVideoLoadStart();
            queue.OnUSharpVideoPlay();
            //third video has ended
            queue.OnUSharpVideoEnd();
            eventReceiverMock.Verify(receiver => receiver.OnUSharpVideoQueueFinalVideoEnded(), Times.Once);
        }

        [Test]
        public void FinalVideoEndedEmittedAfterFinalVideoFails()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            //queue first video
            queue.QueueVideo(url1);
            //video fails to play
            queue.OnUSharpVideoLoadStart();
            queue.OnUSharpVideoError();

            eventReceiverMock.Verify(receiver => receiver.OnUSharpVideoQueueSkippedError(), Times.Once);
            eventReceiverMock.Verify(receiver => receiver.OnUSharpVideoQueueFinalVideoEnded(), Times.Once);
        }

        [Test]

        public void LotsOfQueueingAndRemoving()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            int iterations = 100;

            for (int i = 0; i < iterations; i++)
            {
                queue.QueueVideo(url1);
                Assert.AreEqual(1, queue.QueuedVideosCount());
                queue.OnUSharpVideoLoadStart();
                queue.OnUSharpVideoPlay();
                queue.RemoveVideo(0);
                Assert.AreEqual(0, queue.QueuedVideosCount());
            }
        }

        [Test]
        //Assert no Exception
        public void RemoveNotExistingVideo()
        {
            queue.RemoveVideo(0);
        }

        [Test]

        public void ClearQueue()
        {
            //Queue video
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            //Simulate USharpVideoPlayer behavior (starts playing)
            queue.OnUSharpVideoLoadStart();
            queue.OnUSharpVideoPlay();
            //Add another entry
            queue.QueueVideo(url2);
            //Clear queue while video plays
            queue.Clear();
            //Ensure player gets stopped
            vpMock.Verify(vp => vp.StopVideo(), Times.Once);
            //Ensure all videos are cleared
            Assert.AreEqual(0, queue.QueuedVideosCount());
        }
        
        /// <summary>
        /// Test that the player is stopped, even though it is still loading.
        /// Removing the playing video is not allowed usually, but in this case it's
        /// done anyway to resolve player issues.
        /// </summary>
        [Test]
        public void ClearQueueWhileVideoIsLoading()
        {
            //Queue video
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            //Simulate USharpVideoPlayer behavior (starts playing)
            queue.OnUSharpVideoLoadStart();
            //Clear queue while video is still loading
            queue.Clear();
            //Ensure player gets stopped
            vpMock.Verify(vp => vp.StopVideo(), Times.Once);
            //Ensure all videos are cleared
            Assert.AreEqual(0, queue.QueuedVideosCount());
        }

        [Test]

        public void ShiftVideosAround()
        {
            var url0 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            var url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url0);
            queue.OnUSharpVideoLoadStart();
            queue.QueueVideo(url1);
            queue.QueueVideo(url2);
            
            //shift url2 up
            queue.MoveVideo(2, true);
            //Ensure url was shifted
            Assert.AreEqual(url0, queue.GetURL(0));
            Assert.AreEqual(url2, queue.GetURL(1));
            Assert.AreEqual(url1, queue.GetURL(2));
            //make illegal requests and ensure that queue stays the same (move index 0 down or index 1 up)
            queue.MoveVideo(0, false);
            Assert.AreEqual(url0, queue.GetURL(0));
            Assert.AreEqual(url2, queue.GetURL(1));
            Assert.AreEqual(url1, queue.GetURL(2));
            queue.MoveVideo(1, true);
            Assert.AreEqual(url0, queue.GetURL(0));
            Assert.AreEqual(url2, queue.GetURL(1));
            Assert.AreEqual(url1, queue.GetURL(2));
            //shift url2 back down
            queue.MoveVideo(1,false);
            //ensure initial positions are restored
            Assert.AreEqual(url0, queue.GetURL(0));
            Assert.AreEqual(url1, queue.GetURL(1));
            Assert.AreEqual(url2, queue.GetURL(2));
        }
    }
}