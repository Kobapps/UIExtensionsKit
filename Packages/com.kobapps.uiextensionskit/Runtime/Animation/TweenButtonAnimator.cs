using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// Animates a button by tweening scale, position, rotation and tint <b>relative to the pose the
    /// button was authored with</b>. Nothing here is absolute, so one preset drives buttons of any
    /// size, colour or anchoring.
    /// </summary>
    /// <remarks>
    /// The state pose and the click punch run as two independent tweens that write to separate
    /// fields and then compose in <see cref="Apply"/>. Doing it that way means a click landing
    /// mid-hover doesn't fight the hover tween or snap the button — the punch simply rides on top of
    /// wherever the state tween currently is.
    /// </remarks>
    public sealed class TweenButtonAnimator : IEnhancedButtonAnimator
    {
        private EnhancedButton _button;
        private RectTransform _target;
        private Graphic _tintTarget;
        private Graphic _labelTarget;

        // The authored pose, captured once. Everything below is expressed as a delta from this.
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _basePosition;
        private Vector3 _baseEuler;
        private Color _baseColor = Color.white;
        private Color _baseLabelColor = Color.white;
        private bool _hasBase;

        // Current state-pose values (multipliers / deltas), written by the state tween.
        private Vector3 _stateScale = Vector3.one;
        private Vector2 _stateOffset;
        private float _stateRotation;
        private Color _stateTint = Color.white;
        private Color _stateLabelTint = Color.white;

        // Current punch values, written by the punch tween and added on top.
        private Vector3 _punchScale;
        private float _punchRotation;

        private IUITweenHandle _stateTween;
        private IUITweenHandle _punchTween;

        private EnhancedButtonVisualState _state = EnhancedButtonVisualState.Normal;

        public string DebugSummary
        {
            get
            {
                string stateTween = _stateTween != null && _stateTween.IsActive ? "tweening" : "settled";
                string punch = _punchTween != null && _punchTween.IsActive ? ", punching" : string.Empty;
                return $"Tween ({UITween.Active.Id}) — {_state}, {stateTween}{punch}";
            }
        }

        public void Initialize(EnhancedButton button)
        {
            _button = button;
            _target = button != null ? button.AnimationTarget : null;
            _tintTarget = button != null ? button.TintTarget : null;
            _labelTarget = button != null ? button.LabelTarget : null;

            CaptureBase();
        }

        /// <summary>
        /// Re-read the authored pose from the transform. Call after deliberately moving, resizing or
        /// recolouring a button at runtime, otherwise the animator keeps returning it to where it
        /// used to be.
        /// </summary>
        public void CaptureBase()
        {
            if (_target == null)
            {
                _hasBase = false;
                return;
            }

            _baseScale = _target.localScale;
            _basePosition = _target.anchoredPosition;
            _baseEuler = _target.localEulerAngles;
            _baseColor = _tintTarget != null ? _tintTarget.color : Color.white;
            _baseLabelColor = _labelTarget != null ? _labelTarget.color : Color.white;
            _hasBase = true;

            // A button authored at zero scale can never animate back to anything visible.
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
        }

        public void ApplyState(EnhancedButtonVisualState state, bool instant)
        {
            _state = state;
            if (_button == null || !_hasBase) return;

            ButtonStateMotion motion = _button.MotionSet.Get(state).Sanitized();

            UITween.Kill(ref _stateTween);

            if (instant || motion.duration <= 0f)
            {
                _stateScale = motion.scale;
                _stateOffset = motion.offset;
                _stateRotation = motion.rotation;
                _stateTint = motion.tint;
                _stateLabelTint = motion.labelTint;
                Apply();
                return;
            }

            // Interpolate from wherever we actually are, not from the previous state's target — a
            // hover interrupted halfway must continue from halfway, not jump back.
            Vector3 fromScale = _stateScale;
            Vector2 fromOffset = _stateOffset;
            float fromRotation = _stateRotation;
            Color fromTint = _stateTint;
            Color fromLabelTint = _stateLabelTint;

            // A custom curve cannot be handed to a backend that only speaks named eases, so the
            // tween runs on linear time and the curve is applied here instead. Named eases still go
            // through the backend, which keeps the DOTween path native.
            bool custom = motion.UsesCustomCurve;

            _stateTween = UITween.Animate(
                motion.duration,
                custom ? UIEase.Linear : motion.ease,
                raw =>
                {
                    float t = custom ? motion.Evaluate(raw) : raw;
                    // Unclamped: OutBack and OutElastic pass 1 and come back, and clamping would
                    // flatten exactly the overshoot that makes them worth using.
                    _stateScale = Vector3.LerpUnclamped(fromScale, motion.scale, t);
                    _stateOffset = Vector2.LerpUnclamped(fromOffset, motion.offset, t);
                    _stateRotation = Mathf.LerpUnclamped(fromRotation, motion.rotation, t);
                    _stateTint = Color.LerpUnclamped(fromTint, motion.tint, t);
                    _stateLabelTint = Color.LerpUnclamped(fromLabelTint, motion.labelTint, t);
                    Apply();
                },
                unscaledTime: _button.UseUnscaledTime);
        }

        public void PlayClick()
        {
            if (_button == null || !_hasBase) return;

            ButtonPunch punch = _button.MotionSet.click;
            if (!punch.enabled || punch.duration <= 0f) return;

            UITween.Kill(ref _punchTween);

            // Linear, because the punch shapes itself: Envelope() is the curve, and easing the input
            // as well would distort the oscillation into something lopsided.
            _punchTween = UITween.Animate(
                punch.duration,
                UIEase.Linear,
                t =>
                {
                    float envelope = punch.Envelope(t);
                    _punchScale = punch.scaleAmplitude * envelope;
                    _punchRotation = punch.rotationAmplitude * envelope;
                    Apply();
                },
                onComplete: () =>
                {
                    _punchScale = Vector3.zero;
                    _punchRotation = 0f;
                    Apply();
                },
                unscaledTime: _button.UseUnscaledTime);
        }

        public void Stop()
        {
            UITween.Kill(ref _stateTween);
            UITween.Kill(ref _punchTween);
        }

        public void ResetToBase()
        {
            Stop();

            _stateScale = Vector3.one;
            _stateOffset = Vector2.zero;
            _stateRotation = 0f;
            _stateTint = Color.white;
            _stateLabelTint = Color.white;
            _punchScale = Vector3.zero;
            _punchRotation = 0f;

            Apply();
        }

        /// <summary>Compose state pose and punch into the actual transform and graphic.</summary>
        private void Apply()
        {
            if (!_hasBase || _target == null) return;

            Vector3 scale = _stateScale + _punchScale;
            _target.localScale = new Vector3(
                _baseScale.x * scale.x,
                _baseScale.y * scale.y,
                _baseScale.z * scale.z);

            _target.anchoredPosition = _basePosition + _stateOffset;

            // Only Z is ours; X and Y stay as authored, so a button deliberately tilted in 3D space
            // isn't flattened the first time it's hovered.
            _target.localEulerAngles = new Vector3(
                _baseEuler.x,
                _baseEuler.y,
                _baseEuler.z + _stateRotation + _punchRotation);

            if (_tintTarget != null) _tintTarget.color = _baseColor * _stateTint;

            // The label is tinted separately so a preset can dim the background without washing out
            // the text, which is the usual want for a disabled state.
            if (_labelTarget != null) _labelTarget.color = _baseLabelColor * _stateLabelTint;
        }
    }
}
