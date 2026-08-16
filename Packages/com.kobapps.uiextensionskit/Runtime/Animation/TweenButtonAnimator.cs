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
    /// <para>
    /// The state pose and the click punch run as two independent tweens that write to separate
    /// fields and then compose in <see cref="Apply"/>. Doing it that way means a click landing
    /// mid-hover doesn't fight the hover tween or snap the button — the punch simply rides on top of
    /// wherever the state tween currently is.
    /// </para>
    /// <para>
    /// <b>Sharing the button.</b> Two rules keep this out of the way of anything else animating the
    /// same object. It writes only the channels its preset genuinely uses, so a scale-only preset
    /// never touches position at all; and when it notices a channel has changed underneath it, it
    /// adopts the new value as the authored pose instead of stamping it back. Between them, an
    /// Animator sliding a button in and a preset bouncing it on hover compose rather than fight.
    /// </para>
    /// </remarks>
    public sealed class TweenButtonAnimator : IEnhancedButtonAnimator
    {
        private EnhancedButton _button;
        private RectTransform _target;
        private Graphic _tintTarget;
        private Graphic _labelTarget;

        // The authored pose. Everything below is expressed as a delta from this — and it is not
        // fixed: AdoptExternalChanges re-reads it whenever something else has moved the button.
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _basePosition;
        private Vector3 _baseEuler;
        private Color _baseColor = Color.white;
        private Color _baseLabelColor = Color.white;
        private bool _hasBase;

        // What we last wrote, per channel. A value that no longer matches means somebody else wrote
        // it, which is the signal to re-baseline rather than fight them.
        private Vector3 _wroteScale;
        private Vector2 _wrotePosition;
        private float _wroteZ;
        private Color _wroteColor;
        private Color _wroteLabelColor;
        private bool _wrote;

        private ButtonAnimationChannels _channels = ButtonAnimationChannels.All;

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
                return $"Tween ({UITween.Active.Id}) — {_state}, {stateTween}{punch}, writes {_channels}";
            }
        }

        public void Initialize(EnhancedButton button)
        {
            _button = button;
            _target = button != null ? button.AnimationTarget : null;
            _tintTarget = button != null ? button.TintTarget : null;
            _labelTarget = button != null ? button.LabelTarget : null;

            RefreshChannels();
            CaptureBase();
        }

        /// <summary>The channels this animator will actually write. Everything else is left alone.</summary>
        public ButtonAnimationChannels Channels => _channels;

        /// <summary>
        /// Re-derive which channels to write from the button's motion and its channel mask. Called
        /// on initialize and whenever the resolved motion changes.
        /// </summary>
        public void RefreshChannels()
        {
            if (_button == null)
            {
                _channels = ButtonAnimationChannels.All;
                return;
            }

            _channels = _button.MotionSet.UsedChannels & _button.AnimatedChannels;
            _wrote = false;
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
            _wrote = false;

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

            AdoptExternalChanges();

            if (Writes(ButtonAnimationChannels.Scale))
            {
                Vector3 scale = _stateScale + _punchScale;
                _wroteScale = new Vector3(
                    _baseScale.x * scale.x,
                    _baseScale.y * scale.y,
                    _baseScale.z * scale.z);
                _target.localScale = _wroteScale;
            }

            if (Writes(ButtonAnimationChannels.Position))
            {
                _wrotePosition = _basePosition + _stateOffset;
                _target.anchoredPosition = _wrotePosition;
            }

            if (Writes(ButtonAnimationChannels.Rotation))
            {
                // Only Z is ours; X and Y stay as authored, so a button deliberately tilted in 3D
                // space isn't flattened the first time it's hovered.
                _wroteZ = _baseEuler.z + _stateRotation + _punchRotation;
                _target.localEulerAngles = new Vector3(_baseEuler.x, _baseEuler.y, _wroteZ);
            }

            if (_tintTarget != null && Writes(ButtonAnimationChannels.Tint))
            {
                _wroteColor = _baseColor * _stateTint;
                _tintTarget.color = _wroteColor;
            }

            // The label is tinted separately so a preset can dim the background without washing out
            // the text, which is the usual want for a disabled state.
            if (_labelTarget != null && Writes(ButtonAnimationChannels.LabelTint))
            {
                _wroteLabelColor = _baseLabelColor * _stateLabelTint;
                _labelTarget.color = _wroteLabelColor;
            }

            _wrote = true;
        }

        private bool Writes(ButtonAnimationChannels channel) => (_channels & channel) != 0;

        /// <summary>
        /// Re-read the base for any channel that no longer holds what we last wrote.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the value changed since our last write, something else owns this object too — an
        /// Animator, a screen transition, a hand-rolled tween. Rebasing on their value keeps our
        /// contribution as a delta on top of theirs, which is the only composition that doesn't look
        /// broken: the alternative is the two systems trading writes every frame and the button
        /// visibly snapping between them.
        /// </para>
        /// <para>
        /// The very first Apply has nothing to compare against, so it trusts the base captured at
        /// initialize and skips this.
        /// </para>
        /// </remarks>
        private void AdoptExternalChanges()
        {
            if (!_wrote) return;

            if (Writes(ButtonAnimationChannels.Scale) && _target.localScale != _wroteScale)
            {
                Vector3 current = _target.localScale;
                Vector3 factor = _stateScale + _punchScale;
                _baseScale = new Vector3(
                    Divide(current.x, factor.x, _baseScale.x),
                    Divide(current.y, factor.y, _baseScale.y),
                    Divide(current.z, factor.z, _baseScale.z));
            }

            if (Writes(ButtonAnimationChannels.Position) && _target.anchoredPosition != _wrotePosition)
                _basePosition = _target.anchoredPosition - _stateOffset;

            if (Writes(ButtonAnimationChannels.Rotation)
                && !Mathf.Approximately(_target.localEulerAngles.z, _wroteZ))
            {
                Vector3 euler = _target.localEulerAngles;
                _baseEuler = new Vector3(euler.x, euler.y, euler.z - _stateRotation - _punchRotation);
            }

            if (_tintTarget != null && Writes(ButtonAnimationChannels.Tint) && _tintTarget.color != _wroteColor)
                _baseColor = Unmultiply(_tintTarget.color, _stateTint, _baseColor);

            if (_labelTarget != null && Writes(ButtonAnimationChannels.LabelTint)
                && _labelTarget.color != _wroteLabelColor)
                _baseLabelColor = Unmultiply(_labelTarget.color, _stateLabelTint, _baseLabelColor);
        }

        /// <summary>Recover a base from a written value, falling back when the factor cannot be undone.</summary>
        private static float Divide(float written, float factor, float fallback) =>
            Mathf.Abs(factor) < 0.0001f ? fallback : written / factor;

        private static Color Unmultiply(Color written, Color factor, Color fallback) => new Color(
            Divide(written.r, factor.r, fallback.r),
            Divide(written.g, factor.g, fallback.g),
            Divide(written.b, factor.b, fallback.b),
            Divide(written.a, factor.a, fallback.a));
    }
}
