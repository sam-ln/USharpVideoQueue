﻿using NUnit.Framework;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using UdonSharp.Video;
using Moq;
using USharpVideoQueue.Runtime.Utility;
using USharpVideoQueue.Tests.Editor.TestUtils;

namespace USharpVideoQueue.Tests.Editor
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
            queueMock.Verify(queue => queue.synchronizeData(), Times.AtLeast(2));
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
            queue.RequestRemoveVideo(0);
            eventReceiver.Verify(rcv => rcv.OnUSharpVideoQueueContentChange(), Times.Exactly(2));
        }

        [Test]
        public void VideoPlayerIsClearedAfterLastVideoIsRemoved()
        {
            var url1 = UdonSharpTestUtils.CreateUniqueVRCUrl();
            queue.QueueVideo(url1);
            queue.OnUSharpVideoPlay();
            queue.RequestRemoveVideo(0);
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
            queue.RequestRemoveVideo(0);
            Assert.AreEqual(1, QueueArray.Count(queue.queuedVideos));
            queue.SendCustomEvent(nameof(VideoQueue.OnUSharpVideoPlay));
            queue.RequestRemoveVideo(0);
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
                queue.RequestRemoveVideo(0);
                Assert.AreEqual(0, queue.QueuedVideosCount());
            }
        }

        public void RemoveNotExistingVideo()
        {
            queue.RequestRemoveVideo(0);
        }
    }
}