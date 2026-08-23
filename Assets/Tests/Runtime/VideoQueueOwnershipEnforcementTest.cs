using Moq;
using NUnit.Framework;
using USharpVideoQueue.Runtime;
using USharpVideoQueue.Tests.Runtime.TestUtils;
using VRC.SDKBase;

namespace USharpVideoQueue.Tests.Runtime
{
    /// <summary>
    /// Covers who is allowed to end or skip the video that is currently playing.
    /// </summary>
    /// <remarks>
    /// The queue hands the player it tells to play a video a token, and that player sends it back
    /// when the video ends or fails. With <see cref="VideoQueue.StrictVideoOwnershipEnforcement"/>
    /// the owner only advances the queue if the token still matches the video at the head, which
    /// rules out both a late event about a video that already finished and an event from a player
    /// who was never told to play anything. Every test gets it turned on by the harness, which is
    /// not what a world gets, so the tests about the looser check turn it back off.
    ///
    /// Tests call the end and error handlers directly, because that is where the decision is made.
    /// Going through the video player instead would first have to get past who owns it, which is a
    /// separate question and is covered in <see cref="VideoQueueNetworkedTest"/>.
    /// </remarks>
    public class VideoQueueOwnershipEnforcementTest
    {
        private UdonSharpTestUtils.VideoQueueMockGroup mockGroup;
        private VideoQueue queue0;
        private VideoQueue queue1;
        private VRCUrl url0;
        private VRCUrl url1;

        [SetUp]
        public void Prepare()
        {
            mockGroup = new UdonSharpTestUtils.VideoQueueMockGroup(2);
            queue0 = mockGroup.MockSets[0].VideoQueueMock.Object;
            queue1 = mockGroup.MockSets[1].VideoQueueMock.Object;
            url0 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
        }

        /// <summary>Turns the guard off for everyone, restoring how the queue behaves in a world.</summary>
        private void DisableStrictEnforcement()
        {
            mockGroup.MockSets.ForEach(set =>
                set.VideoQueueMock.Object.StrictVideoOwnershipEnforcement = false);
        }

        /// <summary>
        /// A queue dropped into a world starts with the guard off. The harness turns it on for every
        /// other test here, so this one builds a queue without the harness to see the real default.
        /// </summary>
        [Test]
        public void StrictEnforcementIsOffOnAQueueOutOfTheBox()
        {
            VideoQueue untouchedQueue = new Mock<VideoQueue>().Object;

            Assert.False(untouchedQueue.StrictVideoOwnershipEnforcement);
        }

        /// <summary>
        /// The token is handed out either way, so that turning enforcement on mid-session does not
        /// have to wait for the next video before it can decide anything.
        /// </summary>
        [Test]
        public void OnlyTheAddressedPlayerRecordsThePlaybackToken()
        {
            queue0.QueueVideo(url0);

            //Player 0 was told to play, so only player 0 can prove it later.
            Assert.AreEqual(queue0.playbackTokenCounter, queue0.playbackToken);
            Assert.AreEqual(VideoQueue.NoPlaybackToken, queue1.playbackToken);
        }

        /// <summary>
        /// The case the guard exists for: both videos belong to the same player, so the older check
        /// of whether the sender queued the first video cannot tell the second event from the first.
        /// </summary>
        [Test]
        public void LateEndEventForFinishedVideoDoesNotSkipTheNextVideo()
        {
            queue0.QueueVideo(url0);
            queue0.QueueVideo(url1);
            int tokenOfFirstVideo = queue0.playbackToken;

            //First video ends normally and the queue moves on to the second.
            queue0.OnUSharpVideoEnd();
            Assert.AreEqual(1, queue0.QueuedVideosCount());

            //A second end event for the video that already finished arrives late.
            queue0.RPC_OnVideoOwnerVideoEnd(0, tokenOfFirstVideo);

            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(url1, queue0.GetURL(0));
        }

        [Test]
        public void LateEndEventSkipsTheNextVideoWithoutStrictEnforcement()
        {
            DisableStrictEnforcement();
            queue0.QueueVideo(url0);
            queue0.QueueVideo(url1);
            int tokenOfFirstVideo = queue0.playbackToken;

            queue0.OnUSharpVideoEnd();
            Assert.AreEqual(1, queue0.QueuedVideosCount());

            queue0.RPC_OnVideoOwnerVideoEnd(0, tokenOfFirstVideo);

            //The older check sees the sender still owns the video at the head and lets it through.
            Assert.AreEqual(0, queue0.QueuedVideosCount());
        }

        /// <summary>
        /// A player who joined mid-video was never told to play it, so they hold no token. Naming
        /// the player who did queue it is not enough to pass under strict enforcement.
        /// </summary>
        [Test]
        public void EndEventWithoutAPlaybackTokenIsIgnored()
        {
            queue1.QueueVideo(url1);

            queue0.RPC_OnVideoOwnerVideoEnd(1, VideoQueue.NoPlaybackToken);

            Assert.AreEqual(1, queue0.QueuedVideosCount());
        }

        [Test]
        public void EndEventWithoutAPlaybackTokenAdvancesQueueWithoutStrictEnforcement()
        {
            DisableStrictEnforcement();
            queue1.QueueVideo(url1);

            queue0.RPC_OnVideoOwnerVideoEnd(1, VideoQueue.NoPlaybackToken);

            Assert.AreEqual(0, queue0.QueuedVideosCount());
        }

        /// <summary>
        /// Without a token the error path has nothing to check at all, because it deliberately
        /// accepts an error from whoever owns the video player rather than from whoever queued it.
        /// </summary>
        [Test]
        public void ErrorEventWithoutAPlaybackTokenIsIgnored()
        {
            queue1.QueueVideo(url1);

            queue0.RPC_OnVideoOwnerVideoError(1, VideoQueue.NoPlaybackToken);

            Assert.AreEqual(1, queue0.QueuedVideosCount());
        }

        [Test]
        public void ErrorEventWithoutAPlaybackTokenAdvancesQueueWithoutStrictEnforcement()
        {
            DisableStrictEnforcement();
            queue1.QueueVideo(url1);

            queue0.RPC_OnVideoOwnerVideoError(1, VideoQueue.NoPlaybackToken);

            Assert.AreEqual(0, queue0.QueuedVideosCount());
        }

        [Test]
        public void LateErrorEventForFinishedVideoDoesNotSkipTheNextVideo()
        {
            queue0.QueueVideo(url0);
            queue0.QueueVideo(url1);
            int tokenOfFirstVideo = queue0.playbackToken;

            queue0.OnUSharpVideoEnd();
            Assert.AreEqual(1, queue0.QueuedVideosCount());

            queue0.RPC_OnVideoOwnerVideoError(0, tokenOfFirstVideo);

            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(url1, queue0.GetURL(0));
        }

        /// <summary>
        /// Clearing the queue hands out no new tokens, so an event still in flight for the video
        /// that was playing must not remove the first video of the refilled queue.
        /// </summary>
        [Test]
        public void EndEventFromBeforeAClearDoesNotSkipTheRefilledQueue()
        {
            queue0.QueueVideo(url0);
            int tokenOfClearedVideo = queue0.playbackToken;

            queue0.Clear();
            queue1.QueueVideo(url1);
            Assert.AreEqual(1, queue0.QueuedVideosCount());

            queue0.RPC_OnVideoOwnerVideoEnd(0, tokenOfClearedVideo);

            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(url1, queue0.GetURL(0));
        }

        [Test]
        public void StrictVideoOwnershipEnforcementIsSynchronized()
        {
            Assert.True(queue1.StrictVideoOwnershipEnforcement);

            queue0.StrictVideoOwnershipEnforcement = false;
            queue0._SynchronizeData();

            Assert.False(queue1.StrictVideoOwnershipEnforcement);
        }

        /// <summary>
        /// The guard must not get in the way of the ordinary case, where the player who was told to
        /// play reports back about the video they were told to play.
        /// </summary>
        [Test]
        public void AddressedPlayerCanStillAdvanceTheQueue()
        {
            queue0.QueueVideo(url0);
            queue1.QueueVideo(url1);

            queue0.OnUSharpVideoEnd();

            Assert.AreEqual(1, queue0.QueuedVideosCount());
            Assert.AreEqual(url1, queue0.GetURL(0));
            mockGroup.MockSets[1].VideoPlayerMock.Verify(player => player.PlayVideo(url1), Times.Once);
        }
    }
}
