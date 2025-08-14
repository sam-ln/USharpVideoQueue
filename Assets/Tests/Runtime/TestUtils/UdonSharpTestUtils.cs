using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Moq;
using UdonSharp;
using UdonSharp.Video;
using UnityEngine;
using USharpVideoQueue.Runtime;
using VRC.SDKBase;

namespace USharpVideoQueue.Tests.Runtime.TestUtils
{
    public static class UdonSharpTestUtils
    {
        public static VRCUrl CreateUniqueVRCUrl()
        {
            return new VRCUrl($"https://{Math.Abs(Guid.NewGuid().GetHashCode())}.com/video.mp4");
        }

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
            var allFields = typeof(VideoQueue).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var udonSyncedFields = allFields.Where(field => Attribute.IsDefined(field, typeof(UdonSyncedAttribute)));
            foreach (FieldInfo prop in udonSyncedFields)
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
            queueMock.Setup(queue => queue._GetLocalPlayer()).Returns(mockSet.Player);
            queueMock.Setup(queue => queue._GetPlayerID(mockSet.Player)).Returns(mockSet.PlayerId);
            queueMock.Object.Start();
            return mockSet;
        }

        public static void MockDummySDKBehavior(Mock<VideoQueue> queueMock)
        {
            queueMock.Setup(queue => queue._IsOwner()).Returns(true);
            queueMock.Setup(queue => queue._IsVideoPlayerOwner()).Returns(true);
            queueMock.Setup(queue => queue._GetPlayerID(It.IsAny<VRCPlayerApi>())).Returns(1);
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
            public VideoQueueMockSet Master { get; set; }
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
                Master = MockSets[0];
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
                    if (mockSet == source) continue;
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
                    mockSet.VideoQueueMock.Setup((queue => queue._SynchronizeData()))
                        .Callback(() => { SerializeGroup(mockSet); });

                    mockSet.VideoQueueMock.Setup(queue => queue._IsOwner()).Returns(() => mockSet == Owner);
                    mockSet.VideoQueueMock.Setup(queue => queue._BecomeOwner()).Callback(() => Owner = mockSet);
                    mockSet.VideoQueueMock.Setup(queue => queue._GetPlayerID(It.IsAny<VRCPlayerApi>())).Returns(
                        (VRCPlayerApi player) => GetMockedPlayerId(player));
                    mockSet.VideoQueueMock.Setup(queue => queue._IsVideoPlayerOwner())
                        .Returns(() => ObjectOwners[USharpVideoObjectName].Equals(mockSet));
                    mockSet.VideoQueueMock.Setup(queue => queue._IsMaster()).Returns(() => mockSet.Equals(Master));
                    mockSet.VideoQueueMock.Setup(queue => queue._IsPlayerWithIDValid(It.IsAny<int>()))
                        .Returns((int id) => MockSets.Exists(set => set.PlayerId == id));
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

            public void SimulatePlayerLeft(int playerID)
            {
                VideoQueueMockSet removedPlayer = MockSets.Find(set => set.PlayerId == playerID);
                if (removedPlayer == null)
                {
                    Debug.LogWarning("Player to be removed doesn't exist!");
                    return;
                }
                MockSets.Remove(removedPlayer);
                Debug.Log($"Player was removed. New Player count: {MockSets.Count}");
                if (Master == removedPlayer)
                {
                    Master = MockSets[0];
                    Debug.Log($"New master is {MockSets[0].PlayerId}");
                }
                foreach (var mockSet in MockSets)
                {
                    mockSet.VideoQueueMock.Object.OnPlayerLeft(removedPlayer.Player);
                }
            }
        }
    }
}