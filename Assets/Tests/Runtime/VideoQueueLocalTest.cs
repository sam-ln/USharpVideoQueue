using Moq;
using NUnit.Framework;
using UdonSharp.Video;
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
        private Mock<USharpVideoPlayer> vpMock;
        private Mock<VideoQueueEventReceiver> eventReceiver;

        [SetUp]
        public void Prepare()
        {
            var mockSet = UdonSharpTestUtils.CreateDefaultVideoQueueMockSet();
            queueMock = mockSet.VideoQueueMock;
            queue = mockSet.VideoQueueMock.Object;
            vpMock = mockSet.VideoPlayerMock;
            eventReceiver = mockSet.EventReceiver;
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
            eventReceiver.Verify(rcv => rcv.OnUSharpVideoQueueContentChange(), Times.Once);
        }

        [Test]
        public void ChangesToQueueEmitEvents()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.OnUSharpVideoPlay();
            eventReceiver.Verify(rcv => rcv.OnUSharpVideoQueueContentChange(), Times.Exactly(1));
            queue.RemoveVideo(0);
            eventReceiver.Verify(rcv => rcv.OnUSharpVideoQueueContentChange(), Times.Exactly(2));
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

        [Test]
        public void CanOnlyRemoveFirstVideoAfterLoadingHasFinished()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.SendCustomEvent(nameof(VideoQueue.OnUSharpVideoLoadStart));
            queue.RemoveVideo(0);
            Assert.AreEqual(1, QueueArray.Count(queue.queuedVideos));
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
            eventReceiver.Verify(receiver => receiver.OnUSharpVideoQueueContentChange(), Times.AtLeast(3));
            //first video has ended
            queue.OnUSharpVideoEnd();
            eventReceiver.Verify(receiver => receiver.OnUSharpVideoQueuePlayingNextVideo(), Times.Once);
            //second video starts loading
            queue.OnUSharpVideoLoadStart();
            //loading failed
            queue.OnUSharpVideoError();
            eventReceiver.Verify(receiver => receiver.OnUSharpVideoQueueSkippedError(), Times.Once);
            //third video starts playing
            queue.OnUSharpVideoLoadStart();
            queue.OnUSharpVideoPlay();
            //third video has ended
            queue.OnUSharpVideoEnd();
            eventReceiver.Verify(receiver => receiver.OnUSharpVideoQueueFinalVideoEnded(), Times.Once);
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

            eventReceiver.Verify(receiver => receiver.OnUSharpVideoQueueSkippedError(), Times.Once);
            eventReceiver.Verify(receiver => receiver.OnUSharpVideoQueueFinalVideoEnded(), Times.Once);
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