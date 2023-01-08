using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UdonSharp;
using UnityEngine;
using System;
using Moq;
using UdonSharp.Video;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;

namespace USharpVideoQueue.Tests.Editor.Utils
{
    public static class UdonSharpTestUtils
    {
        /// <summary>
        /// Simulates the RequestSerialization operation with UdonSharp.
        /// Calls OnPreSerialization on source, Copies members which have the [UdonSynced] attribute from source to target,
        /// calls OnDeserialization on target and calls OnPostDeserialization on source.
        /// </summary>
        /// <typeparam name="T">Class derived from UdonSharpBehavior</typeparam>
        /// <param name="source"></param>
        /// <param name="target"></param>
        public static void SimulateSerialization<T>(T source, T target) where T : UdonSharpBehaviour
        {
            source.OnPreSerialization();
            foreach (FieldInfo prop in typeof(VideoQueue).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (Attribute.IsDefined(prop, typeof(UdonSyncedAttribute)))
                {
                    if (prop.FieldType.IsArray)
                    {
                        Array sourceArray = (Array)prop.GetValue(source);
                        Array clonedArray = (Array)sourceArray.Clone();
                        prop.SetValue(target, clonedArray);
                    }
                    else
                    {
                        prop.SetValue(target, prop.GetValue(source));
                    }
                }
            }
            target.OnDeserialization();
            source.OnPostSerialization(new VRC.Udon.Common.SerializationResult(true, 10));
        }

        public static VideoQueueMockSet CreateDefaultVideoQueueMockSet()
        {
            Mock<VideoQueue> queueMock = new Mock<VideoQueue>{ CallBase = true };
            Mock<USharpVideoPlayer> vpMock = new Mock<USharpVideoPlayer>();
            Mock<VideoQueueEventReceiver> eventReceiver = new Mock<VideoQueueEventReceiver>();
            queueMock.Object.VideoPlayer = vpMock.Object;
            queueMock.Object.Start();
            queueMock.Object.RegisterCallbackReceiver(eventReceiver.Object);
            MockDefaultSDKBehavior(queueMock);
            return new VideoQueueMockSet
            {
                VideoQueueMock = queueMock,
                VideoPlayerMock = vpMock,
                EventReceiver = eventReceiver
            };
        }

        public static void MockDefaultSDKBehavior(Mock<VideoQueue> queueMock)
        {
            queueMock.Setup(queue => queue.isOwner()).Returns(true);
            queueMock.Setup(queue => queue.isVideoPlayerOwner()).Returns(true);
            queueMock.Setup(queue => queue.getLocalPlayer()).Returns(new VRCPlayerApi
            {
                displayName = "dummy player",
                isLocal = true
            });
            queueMock.Setup(queue => queue.getPlayerID(It.IsAny<VRCPlayerApi>())).Returns(1);
        }

        public class VideoQueueMockSet
        {
            public Mock<VideoQueue> VideoQueueMock { get; set; }
            public Mock<USharpVideoPlayer> VideoPlayerMock { get; set; }
            public Mock<VideoQueueEventReceiver> EventReceiver { get; set; }
        }

    }
}
