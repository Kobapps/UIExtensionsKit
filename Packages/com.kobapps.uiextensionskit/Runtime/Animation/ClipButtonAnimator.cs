using System;
using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// The legacy <see cref="AnimationClip"/> assigned to each visual state, plus the one-shot click clip.
    /// </summary>
    /// <remarks>
    /// Clips must be marked <b>Legacy</b> (Debug inspector ▸ Animation Clip ▸ Legacy), because this
    /// plays them through the legacy <see cref="Animation"/> component rather than an Animator
    /// controller. That is deliberate: a controller would need a state machine, parameters and an
    /// asset per button, which is far more setup than "hover looks like this clip".
    /// </remarks>
    [Serializable]
    public struct ButtonClipSet
    {
        public AnimationClip normal;
        public AnimationClip highlighted;
        public AnimationClip pressed;
        public AnimationClip selected;
        public AnimationClip disabled;

        [Tooltip("Played once on click, blended over the state clip.")]
        public AnimationClip click;

        [Tooltip("Seconds to blend between state clips. 0 cuts.")]
        public float crossFade;

        /// <summary>The clip for <paramref name="state"/>, or null if none is assigned.</summary>
        public AnimationClip Get(EnhancedButtonVisualState state)
        {
            switch (state)
            {
                case EnhancedButtonVisualState.Highlighted: return highlighted;
                case EnhancedButtonVisualState.Pressed: return pressed;
                case EnhancedButtonVisualState.Selected: return selected;
                case EnhancedButtonVisualState.Disabled: return disabled;
                default: return normal;
            }
        }

        /// <summary>Whether any clip at all is assigned. Used to warn about a mode that would do nothing.</summary>
        public bool HasAnyClip =>
            normal != null || highlighted != null || pressed != null ||
            selected != null || disabled != null || click != null;
    }

    /// <summary>
    /// Animates a button by playing one legacy clip per visual state through an <see cref="Animation"/>
    /// component. For teams whose buttons are already animated by hand in the Animation window, and
    /// for effects a scale/tint tween can't express.
    /// </summary>
    /// <remarks>
    /// State clips play on layer 0 and the click clip on layer 1, so a click blends over the current
    /// state instead of cancelling it and leaving the button stuck in a hover pose it never returns from.
    /// </remarks>
    public sealed class ClipButtonAnimator : IEnhancedButtonAnimator
    {
        private const int StateLayer = 0;
        private const int ClickLayer = 1;

        private EnhancedButton _button;
        private Animation _animation;
        private string _currentClipName;
        private bool _warned;

        public string DebugSummary
        {
            get
            {
                if (_animation == null) return "Animation Clip — no Animation component";
                string playing = string.IsNullOrEmpty(_currentClipName) ? "none" : _currentClipName;
                return $"Animation Clip — playing '{playing}'";
            }
        }

        public void Initialize(EnhancedButton button)
        {
            _button = button;
            _currentClipName = null;
            _warned = false;

            if (button == null) return;

            _animation = button.GetComponent<Animation>();
            if (_animation == null)
            {
                Warn($"'{button.name}' uses Animation Clip mode but has no Animation component. Add one, " +
                     "or switch the button's Animation Mode to Tween.");
                return;
            }

            _animation.playAutomatically = false;

            ButtonClipSet clips = button.Clips;
            if (!clips.HasAnyClip)
            {
                Warn($"'{button.name}' uses Animation Clip mode but has no clips assigned — it will not animate.");
                return;
            }

            Register(clips.normal, StateLayer);
            Register(clips.highlighted, StateLayer);
            Register(clips.pressed, StateLayer);
            Register(clips.selected, StateLayer);
            Register(clips.disabled, StateLayer);
            Register(clips.click, ClickLayer);
        }

        public void ApplyState(EnhancedButtonVisualState state, bool instant)
        {
            if (_animation == null || _button == null) return;

            AnimationClip clip = _button.Clips.Get(state);

            // No clip for this state is a legitimate choice — hold whatever is showing rather than
            // snapping to a default pose the author never described.
            if (clip == null) return;

            string clipName = clip.name;
            if (_animation[clipName] == null) return;

            _currentClipName = clipName;

            float blend = instant ? 0f : Mathf.Max(0f, _button.Clips.crossFade);
            if (blend <= 0f) _animation.Play(clipName, PlayMode.StopSameLayer);
            else _animation.CrossFade(clipName, blend, PlayMode.StopSameLayer);
        }

        public void PlayClick()
        {
            if (_animation == null || _button == null) return;

            AnimationClip clip = _button.Clips.click;
            if (clip == null || _animation[clip.name] == null) return;

            // StopSameLayer keeps this to layer 1, leaving the state clip on layer 0 running underneath.
            _animation.Play(clip.name, PlayMode.StopSameLayer);
        }

        public void Stop()
        {
            if (_animation != null) _animation.Stop();
            _currentClipName = null;
        }

        public void ResetToBase()
        {
            Stop();

            // Rewinding the state clip to frame 0 and sampling once puts the button back where the
            // clip starts, which is the closest thing to an authored rest pose this mode has.
            if (_animation == null || _button == null) return;

            // This runs from OnDisable too, and Animation.Play on a deactivated object only logs a
            // warning and does nothing — so there is no pose to restore and no point trying.
            if (!_animation.isActiveAndEnabled) return;

            AnimationClip normal = _button.Clips.normal;
            if (normal == null || _animation[normal.name] == null) return;

            _animation[normal.name].time = 0f;
            _animation.Play(normal.name, PlayMode.StopSameLayer);
            _animation.Sample();
            _animation.Stop();
        }

        private void Register(AnimationClip clip, int layer)
        {
            if (clip == null || _animation == null) return;

            if (!clip.legacy)
            {
                Warn($"Clip '{clip.name}' is not marked Legacy, so the Animation component cannot play it. " +
                     "Set Legacy on the clip (Debug inspector), or switch the button to Tween mode.");
                return;
            }

            if (_animation.GetClip(clip.name) == null) _animation.AddClip(clip, clip.name);

            AnimationState state = _animation[clip.name];
            if (state == null) return;

            state.layer = layer;
            if (layer == ClickLayer) state.wrapMode = WrapMode.Once;
        }

        // One warning per initialize keeps a misconfigured button from filling the console every frame.
        private void Warn(string message)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[UIExtensionsKit] {message}", _button);
        }
    }
}
