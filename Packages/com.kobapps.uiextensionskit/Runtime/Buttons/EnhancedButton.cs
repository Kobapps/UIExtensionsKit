using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>Fired when a button's latched selection changes.</summary>
    [Serializable]
    public class ButtonSelectedEvent : UnityEvent<bool> { }

    /// <summary>
    /// A drop-in replacement for <see cref="Button"/> that adds the things every game ends up writing
    /// by hand: state motion from a preset, a latched Selected state, sfx and haptic hooks, and a
    /// slot for extra visual effects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It <b>is</b> a <see cref="Button"/>, so <c>onClick</c>, navigation, <c>interactable</c> and
    /// every existing listener keep working — swapping a Button for an EnhancedButton changes nothing
    /// about how the rest of the project talks to it.
    /// </para>
    /// <para>
    /// Configuration comes from one of two places, never both: an <see cref="EnhancedButtonStyle"/>
    /// asset if one is assigned, otherwise the button's own fields. That rule is what makes a
    /// project-wide restyle a single asset edit.
    /// </para>
    /// </remarks>
    [AddComponentMenu("UI/Enhanced Button", 31)]
    [DisallowMultipleComponent]
    public class EnhancedButton : Button
    {
        [Header("Style")]
        [SerializeField]
        [Tooltip("A shared style asset. When set, it supplies motion and feedback and the local settings below are ignored.")]
        private EnhancedButtonStyle m_Style;

        [SerializeField]
        [Tooltip("Parts of the style this button ignores, keeping its own settings instead. " +
                 "For the one button that should look like the rest but sound different.")]
        private ButtonStyleOverride m_Overrides = ButtonStyleOverride.None;

        [Header("Motion (used when no style is assigned)")]
        [SerializeField]
        private ButtonAnimationMode m_AnimationMode = ButtonAnimationMode.Tween;

        [SerializeField]
        [Tooltip("Which built-in feel to use. Custom defers to the preset asset below.")]
        private ButtonPresetKind m_Preset = ButtonPresetLibrary.Default;

        [SerializeField]
        [Tooltip("Used only when Preset is Custom.")]
        private ButtonAnimationPreset m_CustomPreset;

        [SerializeField]
        [Tooltip("Animate on unscaled time, so menus keep moving while the game is paused.")]
        private bool m_UnscaledTime = true;

        [Header("Targets")]
        [SerializeField]
        [Tooltip("What gets scaled/moved/rotated. Defaults to this button's own RectTransform.")]
        private RectTransform m_AnimationTarget;

        [SerializeField]
        [Tooltip("What gets tinted. Defaults to the Selectable's Target Graphic.")]
        private Graphic m_TintTarget;

        [SerializeField]
        [Tooltip("The label. Its colour is driven per state by the preset. " +
                 "Defaults to the first Graphic child that isn't the target graphic — Text or TextMeshPro.")]
        private Graphic m_LabelTarget;

        [Header("Label")]
        [SerializeField]
        [Tooltip("Optional per-state label text, e.g. Pause/Resume or Buy/Bought.")]
        private ButtonLabelTexts m_LabelTexts;

        [Header("Animation Clips (Animation Clip mode only)")]
        [SerializeField]
        private ButtonClipSet m_Clips;

        [Header("Feedback (used when no style is assigned)")]
        [SerializeField]
        private ButtonFeedbackConfig m_Feedback = ButtonFeedbackConfig.Default;

        [Header("Selection")]
        [SerializeField]
        [Tooltip("Latched selection — a chosen tab, an equipped item. Independent of EventSystem focus.")]
        private bool m_Selected;

        [Header("Events")]
        [SerializeField] private UnityEvent m_OnHoverEnter = new UnityEvent();
        [SerializeField] private UnityEvent m_OnHoverExit = new UnityEvent();
        [SerializeField] private ButtonSelectedEvent m_OnSelectedChanged = new ButtonSelectedEvent();

        private readonly List<IEnhancedButtonEffects> _effects = new List<IEnhancedButtonEffects>(2);

        private IEnhancedButtonAnimator _animator;
        private ButtonAnimationMode _animatorMode = ButtonAnimationMode.None;
        private bool _animatorIsCustom;
        private bool _animatorDirty = true;

        private ButtonMotionSet _resolvedMotion;
        private bool _motionResolved;

        private Graphic _resolvedLabel;
        private bool _labelResolved;

        private SelectionState _lastSelectionState = SelectionState.Normal;
        private EnhancedButtonVisualState _visualState = EnhancedButtonVisualState.Normal;
        private bool _ready;

        /// <summary>Raised whenever the resolved visual state changes. Useful for driving sibling visuals.</summary>
        public event Action<EnhancedButton, EnhancedButtonVisualState> StateChanged;

        /// <summary>Raised on every click, after <c>onClick</c>. Also raised for keyboard/gamepad submit.</summary>
        public event Action<EnhancedButton> Clicked;

        /// <summary>
        /// Raised when a click landed on a button that is not interactable — the refusal a plain
        /// <see cref="Button"/> answers with silence. This is where "that level is locked" and "you
        /// can't afford this" belong: the game knows why it said no, and this is the moment to say so.
        /// </summary>
        public event Action<EnhancedButton> Rejected;

        #region Resolved configuration

        /// <summary>The shared style driving this button, or null if it uses its own settings.</summary>
        public EnhancedButtonStyle Style
        {
            get => m_Style;
            set
            {
                if (m_Style == value) return;
                m_Style = value;
                InvalidateConfiguration();
            }
        }

        /// <summary>Whether the style supplies <paramref name="part"/>, or this button overrides it.</summary>
        public bool UsesStyleFor(ButtonStyleOverride part) =>
            m_Style != null && (m_Overrides & part) == 0;

        /// <summary>Which parts of the shared style this button opts out of.</summary>
        public ButtonStyleOverride Overrides
        {
            get => m_Overrides;
            set
            {
                if (m_Overrides == value) return;
                m_Overrides = value;
                InvalidateConfiguration();
            }
        }

        /// <summary>Which built-in feel this button resolves to, accounting for its style.</summary>
        public ButtonPresetKind Preset
        {
            get => UsesStyleFor(ButtonStyleOverride.Preset) ? m_Style.Preset : m_Preset;
            set
            {
                if (m_Preset == value) return;
                m_Preset = value;
                InvalidateConfiguration();
            }
        }

        /// <summary>How this button drives its visuals, accounting for its style.</summary>
        public ButtonAnimationMode AnimationMode =>
            UsesStyleFor(ButtonStyleOverride.AnimationMode) ? m_Style.AnimationMode : m_AnimationMode;

        /// <summary>Whether animations ignore <c>Time.timeScale</c>, accounting for its style.</summary>
        public bool UseUnscaledTime =>
            UsesStyleFor(ButtonStyleOverride.Timing) ? m_Style.UnscaledTime : m_UnscaledTime;

        /// <summary>The sfx and haptics this button requests, accounting for its style.</summary>
        public ButtonFeedbackConfig Feedback =>
            UsesStyleFor(ButtonStyleOverride.Feedback) ? m_Style.Feedback : m_Feedback;

        /// <summary>
        /// The clips used in <see cref="ButtonAnimationMode.AnimationClip"/> mode.
        /// </summary>
        /// <remarks>
        /// A style only supplies these when it actually has clips. Without that guard, putting a
        /// tween-oriented style on a clip-driven button would silently strip its animation.
        /// </remarks>
        public ButtonClipSet Clips =>
            UsesStyleFor(ButtonStyleOverride.Clips) && m_Style.Clips.HasAnyClip ? m_Style.Clips : m_Clips;

        /// <summary>What gets scaled, moved and rotated. Falls back to this button's own transform.</summary>
        public RectTransform AnimationTarget =>
            m_AnimationTarget != null ? m_AnimationTarget : transform as RectTransform;

        /// <summary>What gets tinted. Falls back to the Selectable's target graphic.</summary>
        public Graphic TintTarget => m_TintTarget != null ? m_TintTarget : targetGraphic;

        /// <summary>
        /// The label whose colour the preset drives. Falls back to the first Graphic child that is
        /// not the button's own background, which finds a uGUI Text or a TextMeshPro label without
        /// the kit needing to reference TextMeshPro at all.
        /// </summary>
        public Graphic LabelTarget
        {
            get
            {
                if (m_LabelTarget != null) return m_LabelTarget;
                if (_labelResolved) return _resolvedLabel;

                _labelResolved = true;
                foreach (Graphic candidate in GetComponentsInChildren<Graphic>(true))
                {
                    if (candidate == targetGraphic || candidate == m_TintTarget) continue;
                    _resolvedLabel = candidate;
                    break;
                }

                return _resolvedLabel;
            }
        }

        /// <summary>Optional per-state label text.</summary>
        public ButtonLabelTexts LabelTexts => m_LabelTexts;

        /// <summary>
        /// The motion this button resolves to. Cached, because it is read on every state change and
        /// resolving walks the style and preset asset chain.
        /// </summary>
        public ButtonMotionSet MotionSet
        {
            get
            {
                if (_motionResolved) return _resolvedMotion;
                _resolvedMotion = ResolveMotion();
                _motionResolved = true;
                return _resolvedMotion;
            }
        }

        private ButtonMotionSet ResolveMotion()
        {
            if (UsesStyleFor(ButtonStyleOverride.Preset)) return m_Style.ResolveMotion();
            if (m_Preset != ButtonPresetKind.Custom) return ButtonPresetLibrary.Get(m_Preset);

            // Set to Custom but never given an asset: fall back rather than freeze every button
            // that points at the half-finished configuration.
            if (m_CustomPreset != null) return m_CustomPreset.Motion;
            return ButtonPresetLibrary.Get(ButtonPresetLibrary.Default);
        }

        #endregion

        #region State

        /// <summary>The state the button currently looks like.</summary>
        public EnhancedButtonVisualState VisualState => _visualState;

        /// <summary>
        /// Latched selection — a chosen tab, an equipped item, an active filter. Distinct from
        /// EventSystem focus, and it survives the pointer leaving.
        /// </summary>
        public bool Selected
        {
            get => m_Selected;
            set => SetSelected(value);
        }

        /// <summary>
        /// Set the latched selection. <paramref name="notify"/> false suppresses the event and the
        /// feedback — what a tab group wants when it clears siblings, so selecting one tab doesn't
        /// fire a click sound for every other tab in the row.
        /// </summary>
        public void SetSelected(bool value, bool notify = true)
        {
            if (m_Selected == value) return;
            m_Selected = value;

            RefreshVisualState(instant: false);

            if (!notify) return;

            m_OnSelectedChanged?.Invoke(value);
            if (value) ButtonFeedback.Play(this, ButtonFeedbackEvent.Select, Feedback);
        }

        /// <summary>Fired when the pointer enters an interactable button.</summary>
        public UnityEvent OnHoverEnter => m_OnHoverEnter;

        /// <summary>Fired when the pointer leaves an interactable button.</summary>
        public UnityEvent OnHoverExit => m_OnHoverExit;

        /// <summary>Fired when <see cref="Selected"/> changes through a notifying path.</summary>
        public ButtonSelectedEvent OnSelectedChanged => m_OnSelectedChanged;

        /// <summary>
        /// Map Unity's selection state and our latch onto a visual state. Order is fixed: Disabled
        /// beats Pressed beats Highlighted beats the latch beats Normal.
        /// </summary>
        private EnhancedButtonVisualState ResolveVisualState(SelectionState state)
        {
            if (!IsInteractable()) return EnhancedButtonVisualState.Disabled;

            switch (state)
            {
                case SelectionState.Pressed:
                    return EnhancedButtonVisualState.Pressed;

                // EventSystem focus reads to a player exactly like hover, so both land on Highlighted
                // and a gamepad gets the same affordance as a mouse for free.
                case SelectionState.Highlighted:
                case SelectionState.Selected:
                    return EnhancedButtonVisualState.Highlighted;

                default:
                    return m_Selected
                        ? EnhancedButtonVisualState.Selected
                        : EnhancedButtonVisualState.Normal;
            }
        }

        /// <summary>Re-resolve and, if it changed, drive the animator and effect modules.</summary>
        private void RefreshVisualState(bool instant, bool force = false)
        {
            if (!_ready) return;

            EnhancedButtonVisualState next = ResolveVisualState(_lastSelectionState);
            if (next == _visualState && !force) return;

            _visualState = next;

            EnsureAnimator();
            _animator?.ApplyState(next, instant);
            ApplyLabelText(next);

            for (int i = 0; i < _effects.Count; i++)
                if (IsAlive(_effects[i]))
                    _effects[i].OnButtonStateChanged(this, next, instant);

            StateChanged?.Invoke(this, next);
        }

        #endregion

        #region Unity lifecycle

        protected override void OnEnable()
        {
            // Selectable.OnEnable transitions immediately; _ready is still false there, so that call
            // is ignored and we do a proper forced refresh below once our own state is set up.
            base.OnEnable();

            _lastSelectionState = currentSelectionState;
            CacheEffects();
            InvalidateConfiguration();
            _ready = true;

            RefreshVisualState(instant: true, force: true);
        }

        protected override void OnDisable()
        {
            // Hand the transform back exactly as authored. A button disabled mid-hover would
            // otherwise be saved, or re-enabled, wearing a pose nobody chose.
            _animator?.ResetToBase();
            _ready = false;

            base.OnDisable();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            _lastSelectionState = state;
            RefreshVisualState(instant);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // Only invalidate here. Rebuilding animators and writing to transforms during OnValidate
            // is not safe, so the actual refresh is left to the inspector's preview path.
            InvalidateConfiguration();
        }

        protected override void Reset()
        {
            base.Reset();

            // The kit tints through its own motion. Leaving Unity's ColorTint on as well means two
            // systems writing the same Graphic.color every frame, and the loser looks like a bug.
            transition = Transition.None;
            m_Feedback = ButtonFeedbackConfig.Default;
        }
#endif

        #endregion

        #region Pointer and submit

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if (!IsActive() || !IsInteractable()) return;

            ButtonFeedback.Play(this, ButtonFeedbackEvent.Hover, Feedback);
            m_OnHoverEnter?.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if (!IsActive() || !IsInteractable()) return;

            m_OnHoverExit?.Invoke();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (!IsActive() || !IsInteractable()) return;

            ButtonFeedback.Play(this, ButtonFeedbackEvent.Press, Feedback);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;

            // A disabled button still receives the click — the raycaster doesn't care about
            // `interactable`. Answering it with a rejection cue is far better UX than silence,
            // and it's the one thing a plain Button can't do at all.
            if (!IsActive() || !IsInteractable())
            {
                HandleRejected();
                return;
            }

            base.OnPointerClick(eventData);
            HandleClicked();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                HandleRejected();
                return;
            }

            base.OnSubmit(eventData);
            HandleClicked();
        }

        /// <summary>A click that the button refused because it is not interactable.</summary>
        private void HandleRejected()
        {
            ButtonFeedback.Play(this, ButtonFeedbackEvent.Rejected, Feedback);
            Rejected?.Invoke(this);
        }

        private void HandleClicked()
        {
            _animator?.PlayClick();

            for (int i = 0; i < _effects.Count; i++)
                if (IsAlive(_effects[i]))
                    _effects[i].OnButtonClicked(this);

            ButtonFeedback.Play(this, ButtonFeedbackEvent.Click, Feedback);
            Clicked?.Invoke(this);
        }

        #endregion

        #region Public control

        /// <summary>
        /// Play the click reaction — punch, effects and feedback — without the button having been
        /// clicked. For driving a button from a tutorial, a demo, or a keyboard shortcut that should
        /// look like the player pressed it.
        /// </summary>
        public void PlayClickFeedback() => HandleClicked();

        /// <summary>
        /// Replace the animator with your own. Survives mode changes — the button will not swap it
        /// back for a built-in one. Pass null to return to the mode-driven default.
        /// </summary>
        public void SetAnimator(IEnhancedButtonAnimator animator)
        {
            _animator?.ResetToBase();

            _animator = animator;
            _animatorIsCustom = animator != null;
            _animatorDirty = animator == null;

            _animator?.Initialize(this);
            RefreshVisualState(instant: true, force: true);
        }

        /// <summary>The animator currently driving this button. Null in <see cref="ButtonAnimationMode.None"/>.</summary>
        public IEnhancedButtonAnimator Animator => _animator;

        /// <summary>
        /// Re-read the button's authored pose. Call after deliberately moving, resizing or recolouring
        /// it at runtime — otherwise the animator keeps returning it to where it used to be.
        /// </summary>
        public void RecaptureBasePose()
        {
            if (_animator is TweenButtonAnimator tween) tween.CaptureBase();
            else _animator?.Initialize(this);

            RefreshVisualState(instant: true, force: true);
        }

        /// <summary>
        /// Re-scan for effect modules on this GameObject. Call after adding one at runtime; the
        /// button caches the list on enable and won't notice otherwise.
        /// </summary>
        public void RefreshEffects()
        {
            CacheEffects();
            RefreshVisualState(instant: true, force: true);
        }

        /// <summary>Drop cached configuration so the next read re-resolves style, preset and animator.</summary>
        public void InvalidateConfiguration()
        {
            _motionResolved = false;
            _animatorDirty = true;
            _labelResolved = false;
        }

        #endregion

        #region Internals

        /// <summary>Swap the label's text for this state, when the button defines one.</summary>
        private void ApplyLabelText(EnhancedButtonVisualState state)
        {
            if (!m_LabelTexts.enabled) return;

            string text = m_LabelTexts.Get(state);
            if (string.IsNullOrEmpty(text)) return;

            Graphic label = LabelTarget;
            if (label == null) return;

            // Text directly; anything else (TextMeshProUGUI) through its `text` property, so the kit
            // drives TMP labels without taking a dependency on TextMeshPro.
            if (label is Text uiText)
            {
                uiText.text = text;
                return;
            }

            var property = label.GetType().GetProperty("text");
            if (property != null && property.CanWrite) property.SetValue(label, text);
        }

        private void CacheEffects()
        {
            _effects.Clear();
            GetComponents(_effects);
        }

        /// <summary>
        /// Whether an effect module is still usable. Plain <c>?.</c> is not enough here: the list is
        /// interface-typed, so it compares references and sails straight past a module whose
        /// MonoBehaviour has been destroyed — Unity's null only shows through the Object cast.
        /// </summary>
        private static bool IsAlive(IEnhancedButtonEffects effects)
        {
            if (effects == null) return false;
            return !(effects is UnityEngine.Object unityObject) || unityObject != null;
        }

        private void EnsureAnimator()
        {
            if (_animatorIsCustom) return;

            ButtonAnimationMode mode = AnimationMode;
            if (_animator != null && _animatorMode == mode && !_animatorDirty) return;

            // Restore the old animator's pose before handing the transform to the new one, so the
            // replacement captures the authored values rather than a half-finished tween.
            _animator?.ResetToBase();

            switch (mode)
            {
                case ButtonAnimationMode.Tween:
                    _animator = new TweenButtonAnimator();
                    break;
                case ButtonAnimationMode.AnimationClip:
                    _animator = new ClipButtonAnimator();
                    break;
                default:
                    _animator = null;
                    break;
            }

            _animator?.Initialize(this);
            _animatorMode = mode;
            _animatorDirty = false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: drive the visuals to <paramref name="state"/> without changing what the
        /// button believes its state is. Lets the inspector show every state on demand — including
        /// outside play mode — while leaving the real state machine untouched, so ending the preview
        /// is just a matter of re-applying the truth.
        /// </summary>
        internal void EditorPreviewState(EnhancedButtonVisualState state)
        {
            // Outside play mode the enable path may not have run for this instance yet.
            if (!_ready)
            {
                CacheEffects();
                _ready = true;
            }

            EnsureAnimator();
            _animator?.ApplyState(state, false);

            for (int i = 0; i < _effects.Count; i++)
                if (IsAlive(_effects[i]))
                    _effects[i].OnButtonStateChanged(this, state, false);
        }

        /// <summary>Editor-only: fire the click reaction visually, without feedback or <c>onClick</c>.</summary>
        internal void EditorPreviewClick()
        {
            EnsureAnimator();
            _animator?.PlayClick();
        }

        /// <summary>Editor-only: end a preview and return the button to its authored pose and real state.</summary>
        internal void EditorPreviewReset()
        {
            _animator?.ResetToBase();
            RefreshVisualState(instant: true, force: true);
        }
#endif

        /// <summary>
        /// A full account of what this button resolved to and why. Shown by the inspector and the
        /// debugger window, and worth logging when a button behaves unexpectedly in a build.
        /// </summary>
        public string DebugDescribe()
        {
            var text = new StringBuilder(256);

            text.AppendLine($"EnhancedButton '{name}'");
            text.AppendLine($"  Visual state   : {_visualState} (Unity: {_lastSelectionState}, interactable: {IsInteractable()})");
            text.AppendLine($"  Latched        : {m_Selected}");
            text.AppendLine($"  Style          : {(m_Style != null ? m_Style.name : "<none — using local settings>")}");
            if (m_Style != null)
                text.AppendLine($"  Overrides      : {(m_Overrides == ButtonStyleOverride.None ? "<none — style supplies everything>" : m_Overrides.ToString())}");
            text.AppendLine($"  Mode / preset  : {AnimationMode} / {Preset}");
            text.AppendLine($"  Unscaled time  : {UseUnscaledTime}");
            text.AppendLine($"  Animator       : {(_animator != null ? _animator.DebugSummary : "<none>")}");
            text.AppendLine($"  Tween backend  : {UITween.Active.Id}");
            text.AppendLine($"  Anim target    : {(AnimationTarget != null ? AnimationTarget.name : "<none>")}");
            text.AppendLine($"  Tint target    : {(TintTarget != null ? TintTarget.name : "<none>")}");

            ButtonFeedbackConfig feedback = Feedback;
            text.AppendLine($"  Feedback       : click='{feedback.clickSfx}'/{feedback.clickHaptic}, " +
                            $"hover='{feedback.hoverSfx}'/{feedback.hoverHaptic}");
            text.AppendLine($"  Feedback sink  : {(ButtonFeedback.HasAnyHandler ? $"{ButtonFeedback.HandlerCount} handler(s) + delegates" : "NONE — feedback goes nowhere")}");

            if (_effects.Count == 0)
            {
                text.AppendLine("  Effects        : <none>");
            }
            else
            {
                text.AppendLine($"  Effects        : {_effects.Count}");
                for (int i = 0; i < _effects.Count; i++)
                    text.AppendLine($"    - {_effects[i].EffectsDebugSummary}");
            }

            return text.ToString();
        }

        #endregion
    }
}
