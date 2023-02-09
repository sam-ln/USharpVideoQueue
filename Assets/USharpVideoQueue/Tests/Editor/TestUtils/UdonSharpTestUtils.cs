using System;
using System.Reflection;
using Moq;
using UdonSharp;
using UdonSharp.Video;
using UnityEngine;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;
using System.Collections.Generic;

namespace USharpVideoQueue.Tests.Editor.TestUtils
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

        public static void SimulateSendCustomEvent<T>(T target, string eventName) where T : UdonSharpBehaviour
        {
            Type type = target.GetType();
            MethodInfo methodInfo = type.GetMethod(eventName);
            if (methodInfo == null)
            {
                Debug.LogWarning($"Invoked function {eventName} doesn't exist");
                return;
            }

            methodInfo.Invoke(target, null);
        }

        public static VideoQueueMockSet CreateDefaultVideoQueueMockSet(int playerId = 1)
        {
            Mock<VideoQueue> queueMock = new Mock<VideoQueue> { CallBase = true };
            Mock<USharpVideoPlayer> vpMock = new Mock<USharpVideoPlayer>();
            Mock<VideoQueueEventReceiver> eventReceiver = new Mock<VideoQueueEventReceiver>();
            MockDummySDKBehavior(queueMock);
            queueMock.Object.VideoPlayer = vpMock.Object;
            queueMock.Object.RegisterCallbackReceiver(eventReceiver.Object);
            VideoQueueMockSet mockSet = new VideoQueueMockSet
            {
                VideoQueueMock = queueMock,
                VideoPlayerMock = vpMock,
                EventReceiver = eventReceiver,
                PlayerId = playerId,
                Player = new VRCPlayerApi
                {
                    displayName = $"Player{playerId}"
                },
                ServerTime = 0
            };
            mockSet.VideoQueueMock.Setup(queue => queue.getCurrentServerTime()).Returns(() => ++mockSet.ServerTime);
            queueMock.Setup(queue => queue.getLocalPlayer()).Returns(mockSet.Player);
            queueMock.Setup(queue => queue.getPlayerID(mockSet.Player)).Returns(mockSet.PlayerId);
            queueMock.Object.Start();
            return mockSet;
        }

        public static void MockDummySDKBehavior(Mock<VideoQueue> queueMock)
        {
            queueMock.Setup(queue => queue.isOwner()).Returns(true);
            queueMock.Setup(queue => queue.isVideoPlayerOwner()).Returns(true);
            queueMock.Setup(queue => queue.getPlayerID(It.IsAny<VRCPlayerApi>())).Returns(1);
        }

        public class VideoQueueMockSet
        {
            public Mock<VideoQueue> VideoQueueMock { get; set; }
            public Mock<USharpVideoPlayer> VideoPlayerMock { get; set; }
            public Mock<VideoQueueEventReceiver> EventReceiver { get; set; }
            public VRCPlayerApi Player { get; set; }
            public int ServerTime { get; set; }
            public int PlayerId { get; set; }
        }

        public class VideoQueueMockGroup
        {

            public readonly string USharpVideoObjectName = "USharpVideo";
            public List<VideoQueueMockSet> MockSets { get; set; }
            public VideoQueueMockSet Owner;
            public int ServerTime;
            public Dictionary<string, VideoQueueMockSet> ObjectOwners;

            public VideoQueueMockGroup(int count)
            {
                ObjectOwners = new Dictionary<string, VideoQueueMockSet>();
                MockSets = new List<VideoQueueMockSet>();
                for (int i = 0; i < count; i++)
                {
                    MockSets.Add(CreateDefaultVideoQueueMockSet(i));
                }

                Owner = MockSets[0];
                ObjectOwners[USharpVideoObjectName] = MockSets[0];
                ServerTime = 10;
                SetupMocks();
            }

            public void SerializeGroup(VideoQueueMockSet source)
            {
                if (source != Owner) return;
                ServerTime += 10;
                foreach (var mockSet in MockSets)
                {
                    if(mockSet == source) continue;
                    SimulateSerialization(source.VideoQueueMock.Object, mockSet.VideoQueueMock.Object);
                }
            }

            public void SimulateSendCustomNetworkEvent(string eventName)
            {
                foreach (var mockSet in MockSets)
                {
                    SimulateSendCustomEvent(mockSet.VideoQueueMock.Object, eventName);
                }
            }

            public void SetupMocks()
            {
                foreach (var mockSet in MockSets)
                {
                    mockSet.VideoQueueMock.Setup((queue => queue.synchronizeData()))
                        .Callback(() =>
                        {
                            SerializeGroup(mockSet);
                        });

                    mockSet.VideoQueueMock.Setup(queue => queue.isOwner()).Returns(() => mockSet == Owner);
                    mockSet.VideoQueueMock.Setup(queue => queue.becomeOwner()).Callback(() => Owner = mockSet);
                    mockSet.VideoQueueMock.Setup(queue => queue.getCurrentServerTime()).Returns(() => ServerTime);
                    mockSet.VideoQueueMock.Setup(queue => queue.getPlayerID(It.IsAny<VRCPlayerApi>())).Returns(
                        (VRCPlayerApi player) => GetMockedPlayerId(player));
                    mockSet.VideoQueueMock.Setup(queue => queue.isVideoPlayerOwner())
                        .Returns(() => ObjectOwners[USharpVideoObjectName].Equals(mockSet));
                    mockSet.VideoPlayerMock.Setup(player => player.PlayVideo(It.IsAny<VRCUrl>()))
                        .Callback(() => ObjectOwners[USharpVideoObjectName] = mockSet);
                }
            }

            public int GetMockedPlayerId(VRCPlayerApi player)
            {
                foreach (var mockSet in MockSets)
                {
                    if (mockSet.Player.Equals(player)) return mockSet.PlayerId;
                }

                return -1;
            }
        }
    }
}