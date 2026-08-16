using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// Pumps <see cref="NativeTweenRunner"/> during play mode, from Unity's player loop.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to be a hidden <c>MonoBehaviour</c> created on demand, and that was a real bug:
    /// setting <c>HideFlags.HideAndDontSave</c> before <c>DontDestroyOnLoad</c> could leave the host
    /// GameObject belonging to no scene at all, and a scene-less GameObject never receives
    /// <c>Update</c>. Tweens then sat in the queue untouched — a button froze mid-press — until some
    /// unrelated animation started, called <c>Ensure</c>, produced a driver that did land in a live
    /// scene, and released every stranded tween at once. That is why pressing anywhere on screen
    /// "unstuck" it. It also leaked one orphan per play session.
    /// </para>
    /// <para>
    /// A player-loop system has none of those failure modes: there is no object to orphan, destroy,
    /// deactivate or leak, it needs no scene, and it behaves identically in the editor and a build.
    /// Crucially it is installed once at startup rather than lazily when the first tween begins, so
    /// the pump's existence no longer depends on anything the game happens to be doing.
    /// </para>
    /// <para>
    /// Outside play mode the player loop does not run; the editor steps the runner itself via
    /// <see cref="UITween.ManualTick"/> so inspector previews still animate.
    /// </para>
    /// </remarks>
    internal static class UITweenDriver
    {
        /// <summary>Marker type identifying our system inside the player loop.</summary>
        private struct UIExtensionsKitTweenUpdate { }

        private static bool s_Installed;

        /// <summary>
        /// Install the pump. Runs before the first scene loads, in builds and on entering play mode.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Install()
        {
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();

            // Remove first: with "enter play mode without domain reload" the previous session's
            // system is still installed, and a second copy would tick every tween twice a frame.
            Remove(ref root, typeof(UIExtensionsKitTweenUpdate));

            var system = new PlayerLoopSystem
            {
                type = typeof(UIExtensionsKitTweenUpdate),
                updateDelegate = Tick,
            };

            // Alongside MonoBehaviour.Update, so button animation advances at the same point in the
            // frame it always did.
            if (!InsertAfter(ref root, typeof(Update.ScriptRunBehaviourUpdate), system))
                Append(ref root, typeof(Update), system);

            PlayerLoop.SetPlayerLoop(root);
            s_Installed = true;
        }

        /// <summary>
        /// Kept for the runner to call when it starts a tween. Installation is normally handled at
        /// startup; this only matters if the player loop was replaced wholesale by other code.
        /// </summary>
        internal static void Ensure()
        {
            if (!s_Installed && Application.isPlaying) Install();
        }

        private static void Tick() => NativeTweenRunner.Tick(Time.deltaTime, Time.unscaledDeltaTime);

        #region Player loop surgery

        private static bool Remove(ref PlayerLoopSystem parent, Type type)
        {
            if (parent.subSystemList == null) return false;

            var kept = new List<PlayerLoopSystem>(parent.subSystemList.Length);
            bool removed = false;

            foreach (PlayerLoopSystem child in parent.subSystemList)
            {
                if (child.type == type)
                {
                    removed = true;
                    continue;
                }

                PlayerLoopSystem copy = child;
                removed |= Remove(ref copy, type);
                kept.Add(copy);
            }

            if (removed) parent.subSystemList = kept.ToArray();
            return removed;
        }

        private static bool InsertAfter(ref PlayerLoopSystem parent, Type anchor, PlayerLoopSystem system)
        {
            if (parent.subSystemList == null) return false;

            for (int i = 0; i < parent.subSystemList.Length; i++)
            {
                if (parent.subSystemList[i].type == anchor)
                {
                    var list = new List<PlayerLoopSystem>(parent.subSystemList);
                    list.Insert(i + 1, system);
                    parent.subSystemList = list.ToArray();
                    return true;
                }

                PlayerLoopSystem child = parent.subSystemList[i];
                if (!InsertAfter(ref child, anchor, system)) continue;

                parent.subSystemList[i] = child;
                return true;
            }

            return false;
        }

        private static bool Append(ref PlayerLoopSystem parent, Type phase, PlayerLoopSystem system)
        {
            if (parent.subSystemList == null) return false;

            for (int i = 0; i < parent.subSystemList.Length; i++)
            {
                if (parent.subSystemList[i].type != phase) continue;

                PlayerLoopSystem target = parent.subSystemList[i];
                var list = new List<PlayerLoopSystem>(target.subSystemList ?? Array.Empty<PlayerLoopSystem>());
                list.Add(system);
                target.subSystemList = list.ToArray();
                parent.subSystemList[i] = target;
                return true;
            }

            return false;
        }

        #endregion
    }
}
