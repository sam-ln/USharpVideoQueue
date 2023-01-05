using NUnit.Framework;
using UnityEngine;
using USharpVideoQueue.Runtime;
using UdonSharp.Video;
using Moq;
using UnityEngine.UI;
using VRC.SDK3.Components;
using USharpVideoQueue.Tests.Editor.Utils;

namespace USharpVideoQueue.Tests.Editor
{
    public class QueueControlsTest
    {
        private Mock<USharpVideoPlayer> vpMock;
        private Mock<VideoQueueEventReceiver> eventReceiver;
        private VideoQueue queue;

        private QueueControls controls;
        private Mock<Text> uiQueueContentMock;
        private VRCUrlInputField uiURLInput;

        [SetUp]
        public void Prepare()
        {
            var mockSet = UdonSharpTestUtils.CreateDefaultVideoQueueMockSet();
            queue = mockSet.VideoQueueMock.Object;
            vpMock = mockSet.VideoPlayerMock;
            eventReceiver = mockSet.EventReceiver;
            
            controls = new GameObject().AddComponent<QueueControls>();
            uiQueueContentMock = new Mock<Text>();
            uiURLInput = new GameObject().AddComponent<VRCUrlInputField>();
            controls.Queue = queue;
            controls.UIQueueContent = uiQueueContentMock.Object;
            controls.UIURLInput = uiURLInput;
            controls.Start();
        }

        [Test]
        public void URLInputChangesDisplayedQueueEntries()
        {
            uiURLInput.text = "https://url.one";
            controls.OnURLInput();
            uiQueueContentMock.VerifySet(text => text.text = It.IsAny<string>(), Times.Once);
        }


    }
}
