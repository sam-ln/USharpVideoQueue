
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;
using UdonSharp.Video;
using Moq;

namespace USharpVideoQueue.Tests.Editor
{
    public class VideoQueueTest
    {
        private Mock<USharpVideoPlayer> vpMock;
        private VideoQueue queue;

        

        [SetUp]
        public void Prepare()
        {
            queue = new GameObject().AddComponent<VideoQueue>();
            vpMock = new Mock<USharpVideoPlayer>();
            queue.VideoPlayer = vpMock.Object;   
            queue.Start();        
        }


        [Test]
        public void CreateBehavior()
        {
            Assert.NotNull(queue);
            Assert.True(queue.Initialized);
            Assert.True(VRC.SDKBase.Utilities.IsValid(queue));
        }

        [Test]
        public void CallbackRegisteredAfterStart()
        {
            vpMock.Verify(vp => vp.RegisterCallbackReceiver(queue), Times.Once());
        }
     

        [Test]
        public void QueueVideo()
        {
            var url = new VRCUrl("https://www.youtube.com/watch?v=3dm_5qWWDV8");
            queue.QueueVideo(url);
            vpMock.Verify((vp => vp.PlayVideo(url)), Times.Once);
        }


        /*
        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator BasicTestWithEnumeratorPasses()
        {
            // Use the Assert class to test conditions.
            // Use yield to skip a frame.
            yield return null;
        }
        */
    }
}
