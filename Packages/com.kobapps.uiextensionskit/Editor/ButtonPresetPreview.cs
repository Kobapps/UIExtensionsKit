using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// A button you can actually hover, press and click inside an editor window, animated by a real
    /// <see cref="ButtonMotionSet"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Numbers in an inspector do not tell you whether a preset feels good; a preset is a feel, and
    /// the only way to judge it is to poke it. This drives a UI Toolkit element through the same
    /// <see cref="UIEasing"/> curves and the same <see cref="ButtonPunch.Envelope"/> the runtime
    /// uses, so what you feel here is what the button will do — without entering play mode, without
    /// a scene, and without a single GameObject.
    /// </para>
    /// <para>
    /// It deliberately re-implements the composition rather than reusing
    /// <see cref="TweenButtonAnimator"/>: that one writes to a RectTransform and a uGUI Graphic,
    /// neither of which exists here. The maths is shared; only the surface differs.
    /// </para>
    /// </remarks>
    public sealed class ButtonPresetPreview : VisualElement
    {
        private readonly Label _label;
        private readonly Label _stateBadge;
        private readonly VisualElement _body;

        private ButtonMotionSet _motion = ButtonPresetLibrary.Get(ButtonPresetLibrary.Default);
        private Color _baseColor = new Color(0.22f, 0.55f, 0.95f);

        private EnhancedButtonVisualState _state = EnhancedButtonVisualState.Normal;
        private bool _pointerInside;
        private bool _pointerDown;
        private bool _latched;

        // Where the state tween started, so an interrupted transition continues from where it is.
        private Vector3 _fromScale = Vector3.one;
        private Vector2 _fromOffset;
        private float _fromRotation;
        private Color _fromTint = Color.white;
        private Color _fromLabelTint = Color.white;

        private Vector3 _curScale = Vector3.one;
        private Vector2 _curOffset;
        private float _curRotation;
        private Color _curTint = Color.white;
        private Color _curLabelTint = Color.white;

        private double _stateStarted;
        private float _stateDuration;
        private UIEase _stateEase = UIEase.OutQuad;

        private double _punchStarted = -1d;

        // While scrubbing, the pose is a pure function of the slider rather than of a clock.
        private bool _scrubbing;
        private float _scrubPunch = -1f;
        private EnhancedButtonVisualState _scrubFrom = EnhancedButtonVisualState.Normal;
        private EnhancedButtonVisualState _scrubTo = EnhancedButtonVisualState.Highlighted;

        private double _lastTick;

        /// <summary>Raised when the preview is clicked, so a host can mirror it elsewhere.</summary>
        public event System.Action Clicked;

        /// <summary>Raised when a pointer takes control back from the scrubber.</summary>
        public event System.Action ScrubReleased;

        /// <summary>Whether the scrubber currently owns the pose.</summary>
        public bool IsScrubbing => _scrubbing;

        public ButtonPresetPreview(string caption = "Preview")
        {
            style.alignItems = Align.Center;
            style.justifyContent = Justify.Center;
            style.height = 150;

            _body = new VisualElement
            {
                style =
                {
                    width = 210,
                    height = 68,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10, borderBottomRightRadius = 10,
                    backgroundColor = _baseColor,
                },
            };

            _label = new Label(caption)
            {
                style = { color = Color.white, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 16 },
            };
            _body.Add(_label);
            Add(_body);

            _stateBadge = new Label("Normal") { style = { marginTop = 8, opacity = 0.65f } };
            Add(_stateBadge);

            // Touching the preview always takes control back. Anything else leaves it dead to
            // the pointer for as long as a scrubber happens to be armed somewhere on screen —
            // which is exactly how the timeline broke clicking the moment it was added.
            _body.RegisterCallback<PointerEnterEvent>(_ =>
            {
                _pointerInside = true;
                if (_scrubbing) EndScrub(); else Resolve();
            });
            _body.RegisterCallback<PointerLeaveEvent>(_ => { _pointerInside = false; _pointerDown = false; Resolve(); });
            _body.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (_scrubbing) EndScrub();
                _pointerDown = true;
                Resolve();
            });
            _body.RegisterCallback<PointerUpEvent>(_ =>
            {
                bool wasDown = _pointerDown;
                _pointerDown = false;
                Resolve();

                if (!wasDown) return;
                Punch();
                Clicked?.Invoke();
            });

            _lastTick = EditorApplication.timeSinceStartup;
            schedule.Execute(Tick).Every(16);

            GoTo(EnhancedButtonVisualState.Normal, instant: true);
        }

        /// <summary>The motion being previewed.</summary>
        public void SetMotion(ButtonMotionSet motion)
        {
            _motion = motion.Sanitized();
            GoTo(_state, instant: true);
        }

        /// <summary>Tint of the preview button, so a style can be judged against its real colour.</summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            Apply();
        }

        /// <summary>Hold a state regardless of the pointer — what the state strip drives.</summary>
        public void ForceState(EnhancedButtonVisualState state)
        {
            _latched = state != EnhancedButtonVisualState.Normal;
            GoTo(state, instant: false);
        }

        /// <summary>Return to following the pointer.</summary>
        public void ReleaseForcedState()
        {
            _latched = false;
            Resolve();
        }

        /// <summary>Fire the click reaction on demand.</summary>
        public void Punch() => _punchStarted = EditorApplication.timeSinceStartup;


        /// <summary>Seconds the scrubbed transition takes at full speed. Drives the time readout.</summary>
        public float ScrubDuration =>
            _scrubPunch >= 0f
                ? Mathf.Max(0.0001f, _motion.click.duration)
                : Mathf.Max(0.0001f, _motion.Get(_scrubTo).duration);

        /// <summary>
        /// Take manual control of the pose and scrub the transition from one state into another.
        /// </summary>
        /// <remarks>
        /// Watching a 90ms press at full speed tells you almost nothing. Holding it at t=0.6 and
        /// seeing exactly how far OutBack overshoots is the whole reason to have a scrubber.
        /// </remarks>
        public void BeginScrubTransition(EnhancedButtonVisualState from, EnhancedButtonVisualState to)
        {
            _scrubbing = true;
            _scrubPunch = -1f;
            _scrubFrom = from;
            _scrubTo = to;
            _punchStarted = -1d;
            _stateDuration = 0f;

            _stateBadge.text = $"{from} → {to}";
            ScrubTo(0f);
        }

        /// <summary>Take manual control and scrub the click punch instead of a transition.</summary>
        public void BeginScrubPunch()
        {
            _scrubbing = true;
            _scrubPunch = 0f;
            _punchStarted = -1d;
            _stateDuration = 0f;

            _stateBadge.text = "Click punch";
            ScrubTo(0f);
        }

        /// <summary>Pose at normalized time <paramref name="t"/> of the scrubbed track.</summary>
        public void ScrubTo(float t)
        {
            if (!_scrubbing) return;

            t = Mathf.Clamp01(t);

            if (_scrubPunch >= 0f)
            {
                // Punch rides on top of the resting pose, so hold the state and move only the punch.
                ButtonStateMotion rest = _motion.Get(EnhancedButtonVisualState.Normal);
                _curScale = rest.scale;
                _curOffset = rest.offset;
                _curRotation = rest.rotation;
                _curTint = rest.tint;
                _curLabelTint = rest.labelTint;
                _scrubPunch = t;
                Apply();
                return;
            }

            ButtonStateMotion from = _motion.Get(_scrubFrom);
            ButtonStateMotion to = _motion.Get(_scrubTo);

            // Evaluate through the destination pose - the state being entered owns the transition.
            float eased = to.Evaluate(t);

            _curScale = Vector3.LerpUnclamped(from.scale, to.scale, eased);
            _curOffset = Vector2.LerpUnclamped(from.offset, to.offset, eased);
            _curRotation = Mathf.LerpUnclamped(from.rotation, to.rotation, eased);
            _curTint = Color.LerpUnclamped(from.tint, to.tint, eased);
            _curLabelTint = Color.LerpUnclamped(from.labelTint, to.labelTint, eased);
            Apply();
        }

        /// <summary>The curve's output at <paramref name="t"/>, so the readout can show overshoot.</summary>
        public float EasedAt(float t) =>
            _scrubPunch >= 0f ? _motion.click.Envelope(Mathf.Clamp01(t)) : _motion.Get(_scrubTo).Evaluate(t);

        /// <summary>Hand the preview back to the pointer.</summary>
        public void EndScrub()
        {
            if (!_scrubbing) return;

            _scrubbing = false;
            _scrubPunch = -1f;
            _latched = false;
            _stateBadge.text = _state.ToString();
            Resolve();
            ScrubReleased?.Invoke();
        }

        private void Resolve()
        {
            if (_latched) return;

            EnhancedButtonVisualState next =
                _pointerDown ? EnhancedButtonVisualState.Pressed :
                _pointerInside ? EnhancedButtonVisualState.Highlighted :
                EnhancedButtonVisualState.Normal;

            GoTo(next, instant: false);
        }

        private void GoTo(EnhancedButtonVisualState state, bool instant)
        {
            _state = state;
            _stateBadge.text = state.ToString();

            ButtonStateMotion target = _motion.Get(state);

            _fromScale = _curScale;
            _fromOffset = _curOffset;
            _fromRotation = _curRotation;
            _fromTint = _curTint;
            _fromLabelTint = _curLabelTint;

            _stateEase = target.ease;
            _stateDuration = instant ? 0f : target.duration;
            _stateStarted = EditorApplication.timeSinceStartup;

            if (_stateDuration > 0f) return;

            _curScale = target.scale;
            _curOffset = target.offset;
            _curRotation = target.rotation;
            _curTint = target.tint;
            _curLabelTint = target.labelTint;
            Apply();
        }

        private void Tick()
        {
            // Scrubbing is authoritative; letting the clock also write would fight the slider.
            if (_scrubbing) return;

            double now = EditorApplication.timeSinceStartup;
            _lastTick = now;

            bool dirty = false;

            if (_stateDuration > 0f)
            {
                float t = Mathf.Clamp01((float)(now - _stateStarted) / _stateDuration);
                ButtonStateMotion target = _motion.Get(_state);

                // Evaluate through the pose so a hand-drawn curve previews exactly as it will run.
                float eased = target.Evaluate(t);

                // Unclamped, so OutBack and OutElastic overshoot here exactly as they do at runtime.
                _curScale = Vector3.LerpUnclamped(_fromScale, target.scale, eased);
                _curOffset = Vector2.LerpUnclamped(_fromOffset, target.offset, eased);
                _curRotation = Mathf.LerpUnclamped(_fromRotation, target.rotation, eased);
                _curTint = Color.LerpUnclamped(_fromTint, target.tint, eased);

                if (t >= 1f) _stateDuration = 0f;
                dirty = true;
            }

            if (_punchStarted >= 0d)
            {
                ButtonPunch punch = _motion.click;
                if (!punch.enabled || punch.duration <= 0f) _punchStarted = -1d;
                else if (now - _punchStarted >= punch.duration) _punchStarted = -1d;

                dirty = true;
            }

            if (dirty) Apply();
        }

        private void Apply()
        {
            Vector3 scale = _curScale;
            float rotation = _curRotation;

            if (_scrubPunch >= 0f)
            {
                ButtonPunch punch = _motion.click;
                float envelope = punch.Envelope(_scrubPunch);
                scale += punch.scaleAmplitude * envelope;
                rotation += punch.rotationAmplitude * envelope;
            }
            else if (_punchStarted >= 0d)
            {
                ButtonPunch punch = _motion.click;
                float t = Mathf.Clamp01((float)(EditorApplication.timeSinceStartup - _punchStarted) /
                                        Mathf.Max(0.0001f, punch.duration));
                float envelope = punch.Envelope(t);
                scale += punch.scaleAmplitude * envelope;
                rotation += punch.rotationAmplitude * envelope;
            }

            _body.style.scale = new StyleScale(new Scale(new Vector2(scale.x, scale.y)));
            _body.style.rotate = new StyleRotate(new Rotate(rotation));

            // UI Toolkit's Y grows downward; uGUI's grows up. Negate so a preset that lifts a button
            // on screen also lifts it here.
            _body.style.translate = new StyleTranslate(new Translate(_curOffset.x, -_curOffset.y));

            _body.style.backgroundColor = _baseColor * _curTint;
            _label.style.color = Color.white * _curLabelTint;
        }
    }
}
