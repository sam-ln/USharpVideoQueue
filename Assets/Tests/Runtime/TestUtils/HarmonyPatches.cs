using System;
using System.Reflection;
using HarmonyLib;
using UdonSharp;
using UdonSharp.Video;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace USharpVideoQueue.Tests.Runtime.TestUtils
{
    /// <summary>
    /// Reroutes the few methods the tests cannot reach any other way.
    /// </summary>
    /// <remarks>
    /// Both belong to packages this project does not own, so they cannot simply be made virtual:
    /// USharpVideoPlayer, whose calls the tests want to record, and SendCustomNetworkEvent, which
    /// has to reach the right player (see <see cref="NetworkedEventsEmulator"/>). Everything in our own
    /// packages uses a virtual method instead and is mocked with plain Moq.
    ///
    /// A patch only acts on instances the tests registered, so scenes and play mode keep working
    /// normally. Patches disappear on the next script reload.
    /// </remarks>
    internal static class HarmonyPatches
    {
        private static bool installed;

        public static void Install()
        {
            if (installed) return;
            installed = true;

            Harmony harmony = new Harmony("usharpvideoqueue.tests");

            // The video player methods the queue calls. Running them for real needs a video player
            // in a scene, so mocked instances record the call and skip the body.
            Patch(harmony, typeof(USharpVideoPlayer), nameof(USharpVideoPlayer.PlayVideo),
                new[] { typeof(VRCUrl) }, nameof(PlayVideo));
            Patch(harmony, typeof(USharpVideoPlayer), nameof(USharpVideoPlayer.StopVideo),
                Type.EmptyTypes, nameof(StopVideo));
            Patch(harmony, typeof(USharpVideoPlayer), nameof(USharpVideoPlayer.TakeOwnership),
                Type.EmptyTypes, nameof(TakeOwnership));
            Patch(harmony, typeof(USharpVideoPlayer), nameof(USharpVideoPlayer.RegisterCallbackReceiver),
                new[] { typeof(UdonSharpBehaviour) }, nameof(RegisterCallbackReceiver));

            // SendCustomNetworkEvent has one overload per number of event arguments.
            PatchNetworkEvent(harmony, 0, nameof(SendNetworkEvent));
            PatchNetworkEvent(harmony, 1, nameof(SendNetworkEventWith1Argument));
            PatchNetworkEvent(harmony, 2, nameof(SendNetworkEventWith2Arguments));
            PatchNetworkEvent(harmony, 3, nameof(SendNetworkEventWith3Arguments));
        }

        private static void PatchNetworkEvent(Harmony harmony, int argumentCount, string prefix)
        {
            Type[] parameters = new Type[argumentCount + 2];
            parameters[0] = typeof(NetworkEventTarget);
            parameters[1] = typeof(string);
            for (int i = 0; i < argumentCount; i++) parameters[i + 2] = typeof(object);

            Patch(harmony, typeof(UdonSharpBehaviour), nameof(UdonSharpBehaviour.SendCustomNetworkEvent),
                parameters, prefix);
        }

        private static void Patch(Harmony harmony, Type type, string method, Type[] parameters, string prefix)
        {
            MethodInfo original = AccessTools.Method(type, method, parameters);
            if (original == null)
            {
                Debug.LogError($"Cannot patch {type.Name}.{method}, it no longer exists. Mocking it will not work.");
                return;
            }

            harmony.Patch(original, new HarmonyMethod(AccessTools.Method(typeof(HarmonyPatches), prefix)));
        }

        // Every method below returns false to skip the real method, or true to let it run.

        private static bool PlayVideo(USharpVideoPlayer __instance, VRCUrl url) =>
            !MockRegistry.TryRecord(__instance, nameof(USharpVideoPlayer.PlayVideo), url);

        private static bool StopVideo(USharpVideoPlayer __instance) =>
            !MockRegistry.TryRecord(__instance, nameof(USharpVideoPlayer.StopVideo));

        private static bool TakeOwnership(USharpVideoPlayer __instance) =>
            !MockRegistry.TryRecord(__instance, nameof(USharpVideoPlayer.TakeOwnership));

        private static bool RegisterCallbackReceiver(USharpVideoPlayer __instance,
            UdonSharpBehaviour callbackReceiver) =>
            !MockRegistry.TryRecord(__instance, nameof(USharpVideoPlayer.RegisterCallbackReceiver), callbackReceiver);

        private static bool SendNetworkEvent(UdonSharpBehaviour __instance,
            NetworkEventTarget target, string eventName) =>
            !NetworkedEventsEmulator.TryRoute(__instance, target, eventName);

        private static bool SendNetworkEventWith1Argument(UdonSharpBehaviour __instance,
            NetworkEventTarget target, string eventName, object parameter0) =>
            !NetworkedEventsEmulator.TryRoute(__instance, target, eventName, parameter0);

        private static bool SendNetworkEventWith2Arguments(UdonSharpBehaviour __instance,
            NetworkEventTarget target, string eventName, object parameter0, object parameter1) =>
            !NetworkedEventsEmulator.TryRoute(__instance, target, eventName, parameter0, parameter1);

        private static bool SendNetworkEventWith3Arguments(UdonSharpBehaviour __instance,
            NetworkEventTarget target, string eventName, object parameter0, object parameter1,
            object parameter2) =>
            !NetworkedEventsEmulator.TryRoute(__instance, target, eventName, parameter0, parameter1, parameter2);
    }
}
