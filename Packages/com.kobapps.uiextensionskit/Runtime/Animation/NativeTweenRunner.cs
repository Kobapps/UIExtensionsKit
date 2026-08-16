using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// The built-in animation backend. No third-party dependency, no per-tween allocation beyond the
    /// tween object itself, and it can be stepped by hand (see <see cref="UITween.ManualTick"/>) so
    /// the editor can preview outside play mode and tests can run without waiting on frames.
    /// </summary>
    public sealed class NativeTweenRunner : IUITweenRunner
    {
        private static readonly List<NativeTween> s_Active = new List<NativeTween>(32);

        // Ticking must tolerate callbacks that start or kill tweens, so we iterate a snapshot rather
        // than the live list. Reused between ticks; never resized down.
        private static readonly List<NativeTween> s_Buffer = new List<NativeTween>(32);

        public string Id => UITween.NativeId;

        /// <summary>How many animations this backend is currently running. Surfaced by the debugger window.</summary>
        public static int ActiveCount => s_Active.Count;

        public IUITweenHandle Animate(
            float duration,
            UIEase ease,
            Action<float> onUpdate,
            float delay = 0f,
            Action onComplete = null,
            bool unscaledTime = true)
        {
            // Nothing to interpolate — resolve now rather than leaving a one-frame gap at the old value.
            if (duration <= 0f && delay <= 0f)
            {
                onUpdate?.Invoke(1f);
                onComplete?.Invoke();
                return NativeTween.Completed;
            }

            var tween = new NativeTween
            {
                Duration = duration,
                Delay = delay,
                Ease = ease,
                UnscaledTime = unscaledTime,
                OnUpdate = onUpdate,
                OnComplete = onComplete,
            };

            // Seed the starting value immediately. Callers lerp from a captured "current", so eased(0)
            // is a no-op visually — it just prevents a frame of stale pose before the first tick.
            if (delay <= 0f) onUpdate?.Invoke(UIEasing.Evaluate(ease, 0f));

            s_Active.Add(tween);
            UITweenDriver.Ensure();
            return tween;
        }

        /// <summary>Advance every running animation. Called by the driver in play mode, or by hand.</summary>
        internal static void Tick(float scaledDelta, float unscaledDelta)
        {
            if (s_Active.Count == 0) return;

            s_Buffer.Clear();
            s_Buffer.AddRange(s_Active);

            for (int i = 0; i < s_Buffer.Count; i++)
                s_Buffer[i].Advance(scaledDelta, unscaledDelta);

            s_Active.RemoveAll(t => !t.IsActive);
        }

        /// <summary>
        /// Drop every running animation without firing callbacks. Runs on subsystem registration so
        /// "enter play mode without domain reload" doesn't inherit tweens from the previous session.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetState()
        {
            s_Active.Clear();
            s_Buffer.Clear();
        }
    }

    /// <summary>One running animation owned by <see cref="NativeTweenRunner"/>.</summary>
    internal sealed class NativeTween : IUITweenHandle
    {
        /// <summary>Shared handle for animations that finished before they ever needed a tick.</summary>
        internal static readonly NativeTween Completed = new NativeTween { _finished = true };

        internal float Duration;
        internal float Delay;
        internal UIEase Ease;
        internal bool UnscaledTime;
        internal Action<float> OnUpdate;
        internal Action OnComplete;

        private float _elapsed;
        private bool _finished;

        public bool IsActive => !_finished;

        public void Kill(bool complete = false)
        {
            if (_finished) return;
            _finished = true;

            if (!complete) return;

            var onUpdate = OnUpdate;
            var onComplete = OnComplete;
            OnUpdate = null;
            OnComplete = null;

            onUpdate?.Invoke(UIEasing.Evaluate(Ease, 1f));
            onComplete?.Invoke();
        }

        internal void Advance(float scaledDelta, float unscaledDelta)
        {
            if (_finished) return;

            float dt = UnscaledTime ? unscaledDelta : scaledDelta;

            if (Delay > 0f)
            {
                Delay -= dt;
                if (Delay > 0f) return;
                // Spend whatever of this frame's delta outlived the delay on the tween itself.
                dt = -Delay;
                Delay = 0f;
            }

            _elapsed += dt;

            float t = Duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / Duration);
            OnUpdate?.Invoke(UIEasing.Evaluate(Ease, t));

            // A callback may have killed us; don't then fire completion on top of that.
            if (t < 1f || _finished) return;

            _finished = true;
            var onComplete = OnComplete;
            OnUpdate = null;
            OnComplete = null;
            onComplete?.Invoke();
        }
    }
}
