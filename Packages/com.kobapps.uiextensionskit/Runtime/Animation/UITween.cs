using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>A running animation. Kill it to stop; check <see cref="IsActive"/> to see if it still owns its target.</summary>
    public interface IUITweenHandle
    {
        bool IsActive { get; }

        /// <summary>Stop the animation. <paramref name="complete"/> jumps to the end value first.</summary>
        void Kill(bool complete = false);
    }

    /// <summary>
    /// An animation backend. Implement this and <see cref="UITween.Register"/> it to plug in a tween
    /// library of your own; the built-in runner and the optional DOTween adapter both implement it.
    /// </summary>
    public interface IUITweenRunner
    {
        /// <summary>Stable id used to select this backend (e.g. "native", "dotween").</summary>
        string Id { get; }

        /// <summary>
        /// Animate a value 0→1 (eased) over <paramref name="duration"/>, auto-playing. The callback
        /// receives eased progress, so the caller lerps whatever it likes.
        /// </summary>
        IUITweenHandle Animate(
            float duration,
            UIEase ease,
            Action<float> onUpdate,
            float delay = 0f,
            Action onComplete = null,
            bool unscaledTime = true);
    }

    /// <summary>
    /// Animation façade for the whole package. Every button animates through here, so the backend is
    /// swappable: the dependency-free built-in runner, the DOTween adapter, or your own. The package
    /// never hard-depends on any tween library.
    /// </summary>
    /// <remarks>
    /// UI normally wants <b>unscaled</b> time — menus keep animating while the game is paused at
    /// <c>Time.timeScale = 0</c> — so every entry point defaults to it.
    /// </remarks>
    public static class UITween
    {
        /// <summary>Id of the built-in, dependency-free backend. Always registered.</summary>
        public const string NativeId = "native";

        /// <summary>Id the DOTween adapter registers itself under, when it is compiled in.</summary>
        public const string DOTweenId = "dotween";

        private static readonly Dictionary<string, IUITweenRunner> s_Runners =
            new Dictionary<string, IUITweenRunner>(StringComparer.OrdinalIgnoreCase);

        private static readonly NativeTweenRunner s_Native = new NativeTweenRunner();
        private static string s_BackendId = NativeId;

        static UITween()
        {
            Register(s_Native);
        }

        /// <summary>Register a backend so it can be selected by its <see cref="IUITweenRunner.Id"/>.</summary>
        public static void Register(IUITweenRunner runner)
        {
            if (runner != null && !string.IsNullOrEmpty(runner.Id)) s_Runners[runner.Id] = runner;
        }

        /// <summary>Whether a backend with this id is registered (e.g. is the DOTween adapter compiled in).</summary>
        public static bool IsAvailable(string id) => !string.IsNullOrEmpty(id) && s_Runners.ContainsKey(id);

        /// <summary>Ids of every registered backend.</summary>
        public static IEnumerable<string> AvailableIds => s_Runners.Keys;

        /// <summary>
        /// Which backend new animations use. Setting an id that isn't registered is ignored and leaves
        /// the current one in place, so a project that selected DOTween still runs (on the built-in
        /// backend) if DOTween is later removed.
        /// </summary>
        public static string BackendId
        {
            get => s_BackendId;
            set { if (IsAvailable(value)) s_BackendId = value; }
        }

        /// <summary>
        /// The active backend — the one <see cref="BackendId"/> names, else the built-in one.
        /// </summary>
        /// <remarks>
        /// Outside play mode this is always the built-in runner. Third-party engines have no edit-mode
        /// clock, so a tween handed to one there would never advance and would strand the button
        /// mid-pose; the editor preview steps the built-in runner by hand instead.
        /// </remarks>
        public static IUITweenRunner Active
        {
            get
            {
                if (!Application.isPlaying) return s_Native;
                return s_Runners.TryGetValue(s_BackendId, out var runner) ? runner : s_Native;
            }
        }

        /// <inheritdoc cref="IUITweenRunner.Animate"/>
        public static IUITweenHandle Animate(
            float duration,
            UIEase ease,
            Action<float> onUpdate,
            float delay = 0f,
            Action onComplete = null,
            bool unscaledTime = true)
            => Active.Animate(duration, ease, onUpdate, delay, onComplete, unscaledTime);

        /// <summary>Kill and clear a stored handle (no-op if null or already finished).</summary>
        public static void Kill(ref IUITweenHandle handle, bool complete = false)
        {
            handle?.Kill(complete);
            handle = null;
        }

        /// <summary>
        /// Advance the <b>built-in</b> backend by hand. Play mode drives it automatically; this exists
        /// so the editor can preview a button outside play mode and so tests can step animations
        /// deterministically without waiting on frames. No-op for third-party backends, which run on
        /// their own clocks.
        /// </summary>
        public static void ManualTick(float deltaTime) => NativeTweenRunner.Tick(deltaTime, deltaTime);
    }
}
