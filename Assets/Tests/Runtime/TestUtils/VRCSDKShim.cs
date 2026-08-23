using System;
using System.Collections.Generic;
using System.Linq;
using VRC.SDKBase;

namespace USharpVideoQueue.Tests.Runtime.TestUtils
{
    /// <summary>
    /// Fills in the player lookups the VRChat SDK leaves empty outside a running world.
    /// </summary>
    /// <remarks>
    /// This is a shim, not a mock: it takes the place of no object and cannot be inspected. Looking
    /// up a player by id, asking whether someone is the master, and asking who the local player is
    /// all go through functions only the VRChat client fills in, so in the editor they throw. The
    /// queue calls them while putting log messages together, and it builds those messages even when
    /// logging is off, so tests would fail on that alone.
    ///
    /// One thing to know: VRChat treats our stand-in players as invalid, so the queue still prints
    /// them as unknown. That only affects log text. Tests that need a player to count as valid say
    /// so on the queue itself.
    /// </remarks>
    internal static class VRCSDKShim
    {
        private static readonly Dictionary<int, VRCPlayerApi> PlayersById = new Dictionary<int, VRCPlayerApi>();

        private static bool installed;

        /// <summary>Decides who counts as the master, which is what gives a player extra rights.</summary>
        public static Func<VRCPlayerApi, bool> MasterPredicate { get; set; } = _ => false;

        /// <summary>The player this client belongs to.</summary>
        public static VRCPlayerApi LocalPlayer { get; set; }

        public static void Install()
        {
            if (installed) return;
            installed = true;

            if (VRCPlayerApi.sPlayers == null) VRCPlayerApi.sPlayers = new List<VRCPlayerApi>();

            VRCPlayerApi._GetPlayerById = id => PlayersById.TryGetValue(id, out VRCPlayerApi player) ? player : null;
            VRCPlayerApi._GetPlayerId = PlayerId;
            VRCPlayerApi._isMasterDelegate = player => player != null && MasterPredicate(player);

            Networking._LocalPlayer = () => LocalPlayer;
            Networking._GetMaster = () => PlayersById.Values.FirstOrDefault(player => MasterPredicate(player));
            // A queue built for a test has no GameObject to find an owner from. Only used for logs.
            Networking._GetOwner = _ => null;
        }

        /// <summary>
        /// Makes a player findable by id. Registering the same id again replaces the old player, so
        /// ids reused by later tests do not point at leftovers.
        /// </summary>
        public static void Register(int playerId, VRCPlayerApi player)
        {
            Install();
            PlayersById[playerId] = player;
        }

        public static void Unregister(int playerId) => PlayersById.Remove(playerId);

        private static int PlayerId(VRCPlayerApi player)
        {
            if (player == null) return -1;

            foreach (KeyValuePair<int, VRCPlayerApi> known in PlayersById)
            {
                if (ReferenceEquals(known.Value, player)) return known.Key;
            }

            return -1;
        }
    }
}
