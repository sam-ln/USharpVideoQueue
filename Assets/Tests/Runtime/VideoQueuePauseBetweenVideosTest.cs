using Moq;
using NUnit.Framework;
using UdonSharp.Video;
using USharpVideoQueue.Runtime;
using USharpVideoQueue.Tests.Runtime.TestUtils;
using VRC.SDKBase;

namespace USharpVideoQueue.Tests.Runtime
{
    /// <summary>
    /// Covers the pause the queue waits out before starting the next video.
    /// </summary>
    /// <remarks>
    /// Every other fixture sets <c>waitSecondsBeforePlayback</c> to 0 so playback happens inline.
    /// These tests opt back in to the delay and drive it through <see cref="RPCTimerMock"/>, which
    /// stands in for the RPCTimer the queue would otherwise wait on in real time.
    /// </remarks>
    public class VideoQueuePauseBetweenVideosTest
    {
        private const int PauseSeconds = 5;

        private UdonSharpTestUtils.VideoQueueMockSet mockSet;
        private VideoQueue queue;
        private NonVirtualMock<USharpVideoPlayer> vpMock;
        private VRCUrl url1;
        private VRCUrl url2;

        [SetUp]
        public void Prepare()
        {
            mockSet = UdonSharpTestUtils.CreateDefaultVideoQueueMockSet();
            mockSet.SetWaitSecondsBeforePlayback(PauseSeconds);
            queue = mockSet.VideoQueueMock.Object;
            vpMock = mockSet.VideoPlayerMock;
            url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            url2 = UdonSharpTestUtils.CreateUniqueVRCUrl();
        }

        [Test]
        public void PlaybackIsDeferredUntilPauseElapses()
        {
            queue.QueueVideo(url1);

            //the video is queued, but the player must not have been told to play it yet
            Assert.AreEqual(1, queue.QueuedVideosCount());
            vpMock.Verify(vp => vp.PlayVideo(It.IsAny<VRCUrl>()), Times.Never());
            Assert.IsTrue(queue.waitingForPauseBetweenVideos);
            Assert.IsTrue(mockSet.TimerMock.HasPending);

            mockSet.TimerMock.FireAll();

            vpMock.Verify(vp => vp.PlayVideo(url1), Times.Once());
            Assert.IsFalse(queue.waitingForPauseBetweenVideos);
            Assert.IsFalse(mockSet.TimerMock.HasPending);
        }

        /// <summary>
        /// Regression test for "Prevent video skips during pause between videos": a duplicate or late
        /// end event arriving while the queue is waiting must not advance it a second time.
        /// </summary>
        [Test]
        public void VideoEndDuringPauseDoesNotSkipNextVideo()
        {
            queue.QueueVideo(url1);
            mockSet.TimerMock.FireAll();
            queue.OnUSharpVideoLoadStart();
            queue.OnUSharpVideoPlay();
            queue.QueueVideo(url2);

            //first video ends, so the queue starts waiting out the pause before the second one
            queue.OnUSharpVideoEnd();
            Assert.AreEqual(1, queue.QueuedVideosCount());
            Assert.IsTrue(queue.waitingForPauseBetweenVideos);

            //a second end event arrives while still waiting
            queue.OnUSharpVideoEnd();

            //the pending video must still be there
            Assert.AreEqual(1, queue.QueuedVideosCount());
            Assert.AreEqual(url2, queue.GetURL(0));

            mockSet.TimerMock.FireAll();
            vpMock.Verify(vp => vp.PlayVideo(url2), Times.Once());
        }

        [Test]
        public void RemovingVideoDuringPauseCancelsScheduledPlayback()
        {
            queue.QueueVideo(url1);
            Assert.IsTrue(mockSet.TimerMock.HasPending);

            queue.RemoveVideo(0);

            Assert.IsFalse(mockSet.TimerMock.HasPending);
            Assert.IsFalse(queue.waitingForPauseBetweenVideos);
            Assert.AreEqual(0, queue.QueuedVideosCount());

            //nothing may start playing afterwards
            mockSet.TimerMock.FireAll();
            vpMock.Verify(vp => vp.PlayVideo(It.IsAny<VRCUrl>()), Times.Never());
        }

        [Test]
        public void ClearingQueueDuringPauseCancelsScheduledPlayback()
        {
            queue.QueueVideo(url1);
            Assert.IsTrue(mockSet.TimerMock.HasPending);

            queue.Clear();

            Assert.IsFalse(mockSet.TimerMock.HasPending);
            Assert.IsFalse(queue.waitingForPauseBetweenVideos);
            Assert.AreEqual(0, queue.QueuedVideosCount());
        }

        [Test]
        public void LosingOwnershipDuringPauseCancelsScheduledPlayback()
        {
            var mockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(2);
            mockGroup.SetWaitSecondsBeforePlayback(PauseSeconds);
            var previousOwner = mockGroup.MockSets[0];
            var newOwner = mockGroup.MockSets[1];

            previousOwner.VideoQueueMock.Object.QueueVideo(UdonSharpTestUtils.CreateUniqueVRCUrl());
            Assert.IsTrue(previousOwner.TimerMock.HasPending);

            newOwner.VideoQueueMock.Object._BecomeOwner();
            previousOwner.VideoQueueMock.Object.OnOwnershipTransferred(newOwner.Player);

            //the player who is no longer the owner must not start playback anymore
            Assert.IsFalse(previousOwner.TimerMock.HasPending);
        }

        [Test]
        public void BecomingOwnerDuringPauseReArmsScheduledPlayback()
        {
            var mockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(2);
            mockGroup.SetWaitSecondsBeforePlayback(PauseSeconds);
            var previousOwner = mockGroup.MockSets[0];
            var newOwner = mockGroup.MockSets[1];

            previousOwner.VideoQueueMock.Object.QueueVideo(url1);
            //the waiting state has been synchronized to the other player
            Assert.IsTrue(newOwner.VideoQueueMock.Object.waitingForPauseBetweenVideos);

            newOwner.VideoQueueMock.Object._BecomeOwner();
            newOwner.VideoQueueMock.Object.OnOwnershipTransferred(newOwner.Player);

            //the new owner takes over the pending playback
            Assert.IsTrue(newOwner.TimerMock.HasPending);

            newOwner.TimerMock.FireAll();

            //playback still happens on the client that queued the video, not on the new owner
            previousOwner.VideoPlayerMock.Verify(vp => vp.PlayVideo(url1), Times.Once());
            newOwner.VideoPlayerMock.Verify(vp => vp.PlayVideo(It.IsAny<VRCUrl>()), Times.Never());
        }
    }
}
