using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using USharpVideoQueue.Runtime;
using VRC.Udon.Common.Interfaces;

namespace USharpVideoQueue.Tests.Runtime.TestUtils
{
    /// <summary>
    /// Delivers network events to the other simulated players.
    /// </summary>
    /// <remarks>
    /// In the editor, SendCustomNetworkEvent ignores who the event is addressed to and simply calls
    /// the method on the sender. With a single player that makes no difference, but it quietly
    /// breaks multiplayer tests: a player who is not the owner sends their queue request to the
    /// owner, and the editor hands it back to the sender instead of to the owner who actually holds
    /// the queue.
    ///
    /// Queues registered here get the real behaviour. Owner reaches the current owner, All reaches
    /// everyone including the sender. Anything not registered is left alone.
    /// </remarks>
    internal static class NetworkedEventsEmulator
    {
        private static readonly Dictionary<object, UdonSharpTestUtils.VideoQueueMockGroup> Groups =
            new Dictionary<object, UdonSharpTestUtils.VideoQueueMockGroup>(ReferenceComparer.Instance);

        public static void Add(VideoQueue queue, UdonSharpTestUtils.VideoQueueMockGroup group)
        {
            HarmonyPatches.Install();
            Groups[queue] = group;
        }

        /// <summary>Stops a queue from receiving events, for example after that player left.</summary>
        public static void Remove(VideoQueue queue) => Groups.Remove(queue);

        /// <summary>Delivers an event to the players it is addressed to.</summary>
        /// <returns>False if the sender belongs to no group, which means the editor should handle it.</returns>
        public static bool TryRoute(UdonSharpBehaviour sender, NetworkEventTarget target, string eventName,
            params object[] arguments)
        {
            // ReferenceEquals, not ==: Unity reports objects without a GameObject as null.
            if (ReferenceEquals(sender, null)) return false;
            if (!Groups.TryGetValue(sender, out UdonSharpTestUtils.VideoQueueMockGroup group)) return false;

            foreach (VideoQueue receiver in Receivers(group, target))
            {
                UdonSharpTestUtils.SimulateSendCustomEvent(receiver, eventName, arguments);
            }

            return true;
        }

        /// <summary>
        /// Resolved when the event is sent, because owners change during a test. The list is copied
        /// because handling an event can add or remove players.
        /// </summary>
        private static VideoQueue[] Receivers(UdonSharpTestUtils.VideoQueueMockGroup group,
            NetworkEventTarget target)
        {
            if (target == NetworkEventTarget.Owner) return new[] { group.Owner.VideoQueueMock.Object };

            return group.MockSets.Select(set => set.VideoQueueMock.Object).ToArray();
        }
    }
}
