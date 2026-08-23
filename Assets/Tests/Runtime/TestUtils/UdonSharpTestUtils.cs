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
    /// <summary>
    /// Builds the players a test runs against.
    /// </summary>
    /// <remarks>
    /// Two kinds of helper live next to this file, and they are named for which one they are:
    ///
    /// A <b>Mock</b> takes the place of an object the queue talks to, so a test can set up how it
    /// answers and check what it was asked to do. <see cref="RPCTimerMock"/> and
    /// <see cref="NonVirtualMock{T}"/> are mocks, as is anything Moq builds.
    ///
    /// A <b>Shim</b> fills in VRChat functions that are simply missing outside a running world.
    /// It stands in for nothing and cannot be inspected; it only stops those calls from failing.
    /// <see cref="VRCSDKShim"/> is the only one.
    /// </remarks>
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

        /// <summary>
        /// Counts how often a method was called on a mock, for assertions about relative call counts
        /// where pinning down an absolute number would be brittle.
        /// </summary>
        public static int CountCalls<T>(Mock<T> mock, string methodName) where T : class =>
            mock.Invocations.Count(invocation => invocation.Method.Name == methodName);

        /// <summary>
        /// Calls a public method by name, the way Udon delivers an event.
        /// </summary>
        public static void SimulateSendCustomEvent(UdonSharpBehaviour target, string eventName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(candidate =>
                    candidate.Name == eventName && candidate.GetParameters().Length == arguments.Length);

            if (method == null)
            {
                Debug.LogWarning($"{target.GetType().Name} has no public method {eventName} " +
                                 $"taking {arguments.Length} argument(s).");
                return;
            }

            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Builds one player: a queue that is ready to use, plus stand-ins for everything it talks to.
        /// </summary>
        /// <remarks>
        /// The pause before a video starts is turned off here, so playback begins immediately and
        /// tests stay easy to follow. Tests about the pause itself turn it back on with
        /// <see cref="VideoQueueMockSet.SetWaitSecondsBeforePlayback"/> and then decide when it ends
        /// using <see cref="VideoQueueMockSet.TimerMock"/>.
        ///
        /// <see cref="VideoQueue.StrictVideoOwnershipEnforcement"/> is turned on, which is not the
        /// default a world gets. See the note where it is set.
        /// </remarks>
        public static VideoQueueMockSet CreateDefaultVideoQueueMockSet(int playerId = 1)
        {
            Mock<VideoQueue> queueMock = new Mock<VideoQueue> { CallBase = true };
            NonVirtualMock<USharpVideoPlayer> vpMock = new NonVirtualMock<USharpVideoPlayer>();
            RPCTimerMock timerMock = new RPCTimerMock();
            Mock<VideoQueueEventReceiver> eventReceiverMock = new Mock<VideoQueueEventReceiver>();
            MockSDKCalls(queueMock);
            queueMock.Object.VideoPlayer = vpMock.Object;
            queueMock.Object.timer = timerMock.Object;
            queueMock.Object.waitSecondsBeforePlayback = 0;
            // On for tests, off for worlds. The queue ships with the looser check so that a world
            // cannot get a stuck queue out of the box, but tests should hold the queue to the
            // stricter rule. Tests about the looser check turn it back off.
            queueMock.Object.StrictVideoOwnershipEnforcement = true;
            queueMock.Object.RegisterCallbackReceiver(eventReceiverMock.Object);
            VideoQueueMockSet mockSet = new VideoQueueMockSet
            {
                VideoQueueMock = queueMock,
                VideoPlayerMock = vpMock,
                TimerMock = timerMock,
                EventReceiverMock = eventReceiverMock,
                PlayerId = playerId,
                Player = new VRCPlayerApi
                {
                    displayName = $"Player{playerId}"
                },
                ServerTime = 0
            };
            queueMock.Setup(queue => queue._GetLocalPlayer()).Returns(mockSet.Player);
            queueMock.Setup(queue => queue._GetPlayerID(mockSet.Player)).Returns(mockSet.PlayerId);
            VRCSDKShim.Register(mockSet.PlayerId, mockSet.Player);
            // On their own, this player is the master. A group overrides that once it has everyone.
            VRCSDKShim.MasterPredicate = player => ReferenceEquals(player, mockSet.Player);
            VRCSDKShim.LocalPlayer = mockSet.Player;
            // Last, because starting up looks up who the local player is.
            queueMock.Object.EnsureInitialized();
            return mockSet;
        }

        /// <summary>
        /// Answers the questions the queue asks VRChat, which has no answers outside of a running world.
        /// </summary>
        public static void MockSDKCalls(Mock<VideoQueue> queueMock)
        {
            queueMock.Setup(queue => queue._IsOwner()).Returns(true);
            queueMock.Setup(queue => queue._IsVideoPlayerOwner()).Returns(true);
            queueMock.Setup(queue => queue._GetPlayerID(It.IsAny<VRCPlayerApi>())).Returns(1);
            // VRChat treats our stand-in players as invalid, which would leave nobody with the
            // rights to remove or clear anything.
            queueMock.Setup(queue => queue._IsPlayerWithIDValid(It.IsAny<int>())).Returns(true);
            // Looking up the owner needs a GameObject, which a queue built for a test does not have.
            // It only writes a log line, so skipping it loses nothing.
            queueMock.Setup(queue => queue._LogOwnerAndMaster(It.IsAny<bool>()));
        }

        public class VideoQueueMockSet
        {
            public Mock<VideoQueue> VideoQueueMock { get; set; }
            public NonVirtualMock<USharpVideoPlayer> VideoPlayerMock { get; set; }
            public RPCTimerMock TimerMock { get; set; }
            public Mock<VideoQueueEventReceiver> EventReceiverMock { get; set; }
            public VRCPlayerApi Player { get; set; }
            public int ServerTime { get; set; }
            public int PlayerId { get; set; }

            /// <summary>
            /// Turns the pause between videos back on. Playback then waits until the test calls
            /// <see cref="RPCTimerMock.FireAll"/> on <see cref="TimerMock"/>.
            /// </summary>
            public void SetWaitSecondsBeforePlayback(int seconds) =>
                VideoQueueMock.Object.waitSecondsBeforePlayback = seconds;
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

                // Checked when asked, so tests can hand the role to someone else mid-test.
                VRCSDKShim.MasterPredicate =
                    player => Master != null && ReferenceEquals(player, Master.Player);
                // The first player in the group is the one running this client.
                VRCSDKShim.LocalPlayer = MockSets[0].Player;

                // Without this every network event would come back to the sender instead of
                // reaching the player it was addressed to.
                foreach (var mockSet in MockSets)
                {
                    NetworkedEventsEmulator.Add(mockSet.VideoQueueMock.Object, this);
                }

                SetupMocks();
            }

            /// <summary>
            /// Stands in for the queue sending its state to everyone else.
            /// </summary>
            /// <remarks>
            /// The real method sends the state out and then reports the change locally. The other
            /// players learn about it when they receive the data, but the player who made the
            /// change only hears about it from that second step. Leaving it out would keep their own
            /// listeners, such as QueueControls, silent.
            /// </remarks>
            public void SynchronizeData(VideoQueueMockSet source)
            {
                // The real method does nothing for a player who is not the owner.
                if (source != Owner) return;

                SerializeGroup(source);
                source.VideoQueueMock.Object.OnQueueContentChange();
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

            /// <summary>
            /// Re-enables the pause between videos for every player in the group.
            /// </summary>
            public void SetWaitSecondsBeforePlayback(int seconds)
            {
                foreach (var mockSet in MockSets)
                {
                    mockSet.SetWaitSecondsBeforePlayback(seconds);
                }
            }

            /// <summary>
            /// Simulates the pause elapsing for every player. Only the queue owner ever has a timer
            /// armed, so this is safe to call regardless of who owns the queue.
            /// </summary>
            public void FireAllTimers()
            {
                foreach (var mockSet in MockSets)
                {
                    mockSet.TimerMock.FireAll();
                }
            }

            public void SetupMocks()
            {
                foreach (var mockSet in MockSets)
                {
                    mockSet.VideoQueueMock.Setup((queue => queue._SynchronizeData()))
                        .Callback(() => SynchronizeData(mockSet));

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
                // A player who left must stop receiving network events and stop being resolvable.
                NetworkedEventsEmulator.Remove(removedPlayer.VideoQueueMock.Object);
                VRCSDKShim.Unregister(playerID);
                Debug.Log($"Player was removed. New Player count: {MockSets.Count}");
                if (Master == removedPlayer)
                {
                    Master = MockSets[0];
                    Debug.Log($"New master is {MockSets[0].PlayerId}");
                }

                // VRChat reassigns ownership of objects owned by a player who leaves. Without this
                // the queue would keep pointing at the departed owner, and nobody would consider
                // themselves responsible for cleaning up that player's videos.
                if (Owner == removedPlayer)
                {
                    Owner = MockSets[0];
                    Debug.Log($"New queue owner is {MockSets[0].PlayerId}");
                }

                if (ObjectOwners[USharpVideoObjectName] == removedPlayer)
                {
                    ObjectOwners[USharpVideoObjectName] = MockSets[0];
                }

                foreach (var mockSet in MockSets)
                {
                    mockSet.VideoQueueMock.Object.OnPlayerLeft(removedPlayer.Player);
                }
            }
        }
    }
}