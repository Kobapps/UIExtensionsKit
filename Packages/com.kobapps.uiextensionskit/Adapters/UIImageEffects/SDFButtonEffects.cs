// This whole assembly only compiles when UIImageEffectsKit is installed — the asmdef defines
// UIEXTENSIONSKIT_UIIMAGEEFFECTS from a versionDefine on com.kobapps.uiimageeffectskit and then
// constrains itself to it. Nothing in the core kit references this file, so UIExtensionsKit has no
// dependency on UIImageEffectsKit; installing that package is all it takes to light this up.
using System;
using SDFImageKit;
using UnityEngine;

namespace Kobapps.UIExtensionsKit.Adapters
{
    /// <summary>When a CTA button sweeps its shine band.</summary>
    public enum SDFShineMode
    {
        /// <summary>Never.</summary>
        Off = 0,

        /// <summary>Continuously, with a pause between sweeps. The classic "press me" treatment.</summary>
        Loop = 1,

        /// <summary>One sweep each time the button is hovered or focused.</summary>
        OnHover = 2,

        /// <summary>One sweep each time the button is clicked.</summary>
        OnClick = 3,
    }

    /// <summary>The glow a button wears in one visual state.</summary>
    [Serializable]
    public struct SDFGlowState
    {
        [Tooltip("Whether the glow is visible in this state. When off, it fades to nothing rather than popping.")]
        public bool show;

        public Color color;

        [Range(0f, 6f), Tooltip("Outward reach, as a multiple of the field spread.")]
        public float width;

        [Range(0.1f, 8f), Tooltip("Falloff exponent. Above 1 tightens the halo against the edge.")]
        public float power;

        /// <summary>A hidden glow that still carries a colour, so fading to it doesn't shift hue.</summary>
        public static SDFGlowState Hidden(Color color) => new SDFGlowState
        {
            show = false,
            color = color,
            width = 0f,
            power = 1.5f,
        };

        /// <summary>A visible glow.</summary>
        public static SDFGlowState Visible(Color color, float width, float power = 1.5f) => new SDFGlowState
        {
            show = true,
            color = color,
            width = width,
            power = power,
        };
    }

    /// <summary>
    /// Drives an <see cref="SDFImage"/>'s glow and shine from an <see cref="EnhancedButton"/>'s state —
    /// a halo that comes up on hover, and a sheen that sweeps across a call-to-action.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Add it next to an EnhancedButton whose graphic is an SDFImage. Missing Glow / Shine layers are
    /// added to the effect stack automatically, so the usual setup is: swap Image for SDF Image, add
    /// this, pick colours.
    /// </para>
    /// <para>
    /// <b>Cost.</b> Moving the shine is genuinely cheap — UIImageEffectsKit updates only the material
    /// for it. Changing the <i>glow</i> is not: every edit marks the mesh dirty, because glow reach
    /// changes how far the quad must expand. That is fine for state changes, which are brief and
    /// user-driven, but a continuous pulse would rebuild the mesh every frame forever — so the pulse
    /// is rate-limited by <see cref="PulseUpdatesPerSecond"/> rather than running at full framerate.
    /// </para>
    /// </remarks>
    [AddComponentMenu("UI/Enhanced Button Effects (UIImageEffectsKit)")]
    [RequireComponent(typeof(EnhancedButton))]
    [DisallowMultipleComponent]
    public sealed class SDFButtonEffects : EnhancedButtonEffectsBehaviour
    {
        [Header("Target")]
        [SerializeField]
        [Tooltip("The SDF Image to drive. Defaults to one on this object, then the first in children.")]
        private SDFImage m_Target;

        [SerializeField]
        [Tooltip("Add missing Glow / Shine layers to the effect stack instead of doing nothing.")]
        private bool m_AutoAddEffects = true;

        [Header("Glow")]
        [SerializeField] private bool m_DriveGlow = true;
        [SerializeField] private SDFGlowState m_Normal = SDFGlowState.Hidden(new Color(0.3f, 0.7f, 1f, 1f));
        [SerializeField] private SDFGlowState m_Highlighted = SDFGlowState.Visible(new Color(0.4f, 0.8f, 1f, 1f), 0.9f);
        [SerializeField] private SDFGlowState m_Pressed = SDFGlowState.Visible(new Color(0.5f, 0.9f, 1f, 1f), 0.5f);
        [SerializeField] private SDFGlowState m_Selected = SDFGlowState.Visible(new Color(1f, 0.85f, 0.3f, 1f), 0.8f);
        [SerializeField] private SDFGlowState m_Disabled = SDFGlowState.Hidden(new Color(0.5f, 0.5f, 0.5f, 1f));

        [SerializeField, Range(0f, 1f)] private float m_GlowFadeDuration = 0.2f;
        [SerializeField] private UIEase m_GlowEase = UIEase.OutQuad;

        [Header("Glow pulse (CTA)")]
        [SerializeField]
        [Tooltip("Breathe the glow while it is visible. Draws attention to a primary action.")]
        private bool m_Pulse;

        [SerializeField, Range(0f, 1f), Tooltip("How much of the glow's alpha the pulse removes at its dimmest.")]
        private float m_PulseAlphaAmount = 0.5f;

        [SerializeField, Range(0f, 1f), Tooltip("How much of the glow's width the pulse removes at its narrowest.")]
        private float m_PulseWidthAmount = 0f;

        [SerializeField, Range(0.1f, 4f), Tooltip("Pulses per second.")]
        private float m_PulseSpeed = 0.7f;

        [SerializeField, Range(5f, 60f)]
        [Tooltip("How often the pulse writes to the image. Each write rebuilds the mesh, so this is capped well below framerate.")]
        private float m_PulseUpdatesPerSecond = 30f;

        [Header("Shine")]
        [SerializeField] private SDFShineMode m_ShineMode = SDFShineMode.Off;
        [SerializeField, Range(0.1f, 3f), Tooltip("Seconds for one sweep across the button.")]
        private float m_ShineDuration = 0.7f;

        [SerializeField, Range(0f, 10f), Tooltip("Seconds to wait between sweeps in Loop mode.")]
        private float m_ShineInterval = 2.5f;

        [SerializeField]
        [Tooltip("Don't sweep while the button is disabled — a shine on a dead button reads as interactive.")]
        private bool m_SuppressShineWhenDisabled = true;

        private enum ShinePhase { Idle, Waiting, Sweeping }

        private EnhancedButton _button;
        private bool _hasGlow;
        private bool _hasShine;

        // Written by the state tween; the pulse modulates these on the way to the image.
        private Color _glowColor = Color.clear;
        private float _glowWidth;
        private float _glowPower = 1.5f;

        private IUITweenHandle _glowTween;

        private ShinePhase _shinePhase = ShinePhase.Idle;
        private float _shineTimer;
        private float _pulseTime;
        private float _pulseWriteTimer;
        private bool _glowVisible;
        private EnhancedButtonVisualState _state = EnhancedButtonVisualState.Normal;

        /// <inheritdoc/>
        public override string EffectsDebugSummary
        {
            get
            {
                if (m_Target == null) return "SDF effects — no SDF Image found";

                string glow = !m_DriveGlow ? "off" : $"{(_glowVisible ? "visible" : "hidden")} w={_glowWidth:0.00}";
                string shine = m_ShineMode == SDFShineMode.Off ? "off" : $"{m_ShineMode}/{_shinePhase}";
                return $"SDF effects on '{m_Target.name}' — glow: {glow}, shine: {shine}";
            }
        }

        /// <summary>How often the pulse writes to the image, in Hz. See the cost note on this class.</summary>
        public float PulseUpdatesPerSecond => m_PulseUpdatesPerSecond;

        private void Awake()
        {
            _button = GetComponent<EnhancedButton>();
            Resolve();
        }

        private void OnEnable()
        {
            // The button caches its effect modules when it is enabled. If this component was added
            // later, or enabled after the button, that cache doesn't know about us yet.
            if (_button != null) _button.RefreshEffects();
        }

        private void OnDisable()
        {
            UITween.Kill(ref _glowTween);
            _shinePhase = ShinePhase.Idle;
            ParkShine();
        }

        /// <summary>
        /// Re-read the effect stack and re-apply the current state.
        /// </summary>
        /// <remarks>
        /// The stack is resolved once, in <c>Awake</c>, which is enough when the component is
        /// configured in the inspector. Configure it from code and the ordering bites: adding this
        /// component runs <c>Awake</c> synchronously, so a shine mode assigned on the next line
        /// arrives after the decision to add a Shine layer has already been made — and the sheen
        /// silently never appears. Call this after any such change.
        /// </remarks>
        public void Refresh()
        {
            Resolve();
            ApplyGlowState(GlowFor(_state), instant: true);
        }

        private void Resolve()
        {
            if (m_Target == null) m_Target = GetComponent<SDFImage>();
            if (m_Target == null) m_Target = GetComponentInChildren<SDFImage>();

            if (m_Target == null)
            {
                Debug.LogWarning(
                    $"[UIExtensionsKit] '{name}' has SDFButtonEffects but no SDF Image to drive. " +
                    "Replace the button's Image with an SDF Image, or assign one.", this);
                return;
            }

            _hasGlow = m_Target.TryGetEffect<SDFGlowEffect>(out _);
            if (!_hasGlow && m_AutoAddEffects && m_DriveGlow)
            {
                // Back of the stack: an outer halo belongs behind the face, not over it.
                m_Target.AddEffect(new SDFGlowEffect { width = 0f, color = Color.clear }, front: false);
                _hasGlow = true;
            }

            _hasShine = m_Target.TryGetEffect<SDFShineEffect>(out _);
            if (!_hasShine && m_AutoAddEffects && m_ShineMode != SDFShineMode.Off)
            {
                // Front of the stack: a sheen reads as light on the surface, so it sits on top.
                m_Target.AddEffect(new SDFShineEffect { position = 0f }, front: true);
                _hasShine = true;
            }

            ParkShine();
        }

        /// <inheritdoc/>
        public override void OnButtonStateChanged(EnhancedButton button, EnhancedButtonVisualState state, bool instant)
        {
            _state = state;

            ApplyGlowState(GlowFor(state), instant);

            if (m_ShineMode == SDFShineMode.OnHover && state == EnhancedButtonVisualState.Highlighted)
                StartSweep();

            if (m_SuppressShineWhenDisabled && state == EnhancedButtonVisualState.Disabled)
            {
                _shinePhase = ShinePhase.Idle;
                ParkShine();
            }
        }

        /// <inheritdoc/>
        public override void OnButtonClicked(EnhancedButton button)
        {
            if (m_ShineMode == SDFShineMode.OnClick) StartSweep();
        }

        private SDFGlowState GlowFor(EnhancedButtonVisualState state)
        {
            switch (state)
            {
                case EnhancedButtonVisualState.Highlighted: return m_Highlighted;
                case EnhancedButtonVisualState.Pressed: return m_Pressed;
                case EnhancedButtonVisualState.Selected: return m_Selected;
                case EnhancedButtonVisualState.Disabled: return m_Disabled;
                default: return m_Normal;
            }
        }

        private void ApplyGlowState(SDFGlowState target, bool instant)
        {
            if (!m_DriveGlow || m_Target == null || !_hasGlow) return;

            _glowVisible = target.show;

            // A hidden glow keeps its colour and fades width and alpha to nothing, so turning it off
            // is a fade-out rather than a hue shift through whatever the next state happens to be.
            Color targetColor = target.color;
            if (!target.show) targetColor.a = 0f;
            float targetWidth = target.show ? target.width : 0f;
            float targetPower = target.power;

            UITween.Kill(ref _glowTween);

            if (instant || m_GlowFadeDuration <= 0f)
            {
                _glowColor = targetColor;
                _glowWidth = targetWidth;
                _glowPower = targetPower;
                WriteGlow();
                return;
            }

            Color fromColor = _glowColor;
            float fromWidth = _glowWidth;
            float fromPower = _glowPower;

            _glowTween = UITween.Animate(
                m_GlowFadeDuration,
                m_GlowEase,
                t =>
                {
                    _glowColor = Color.LerpUnclamped(fromColor, targetColor, t);
                    _glowWidth = Mathf.LerpUnclamped(fromWidth, targetWidth, t);
                    _glowPower = Mathf.LerpUnclamped(fromPower, targetPower, t);
                    WriteGlow();
                },
                unscaledTime: _button == null || _button.UseUnscaledTime);
        }

        private void WriteGlow()
        {
            if (m_Target == null || !_hasGlow) return;

            Color color = _glowColor;
            float width = _glowWidth;

            if (m_Pulse && _glowVisible)
            {
                // 0..1, peaking at 1 — the glow never exceeds its authored strength, it only dips.
                float wave = (Mathf.Sin(_pulseTime * Mathf.PI * 2f * m_PulseSpeed) + 1f) * 0.5f;
                color.a *= Mathf.Lerp(1f - m_PulseAlphaAmount, 1f, wave);
                width *= Mathf.Lerp(1f - m_PulseWidthAmount, 1f, wave);
            }

            float power = _glowPower;
            m_Target.Modify<SDFGlowEffect>(glow =>
            {
                glow.color = color;
                glow.width = width;
                glow.power = power;
            });
        }

        private void StartSweep()
        {
            if (m_Target == null || !_hasShine || m_ShineMode == SDFShineMode.Off) return;
            if (m_SuppressShineWhenDisabled && _state == EnhancedButtonVisualState.Disabled) return;

            _shinePhase = ShinePhase.Sweeping;
            _shineTimer = 0f;
        }

        /// <summary>Move the shine band fully off the button, where it costs nothing visually.</summary>
        private void ParkShine()
        {
            if (m_Target == null || !_hasShine) return;
            m_Target.SetShinePosition(0f);
        }

        private void Update()
        {
            if (m_Target == null) return;

            bool unscaled = _button == null || _button.UseUnscaledTime;
            float deltaTime = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

            UpdatePulse(deltaTime);
            UpdateShine(deltaTime);
        }

        private void UpdatePulse(float deltaTime)
        {
            if (!m_Pulse || !m_DriveGlow || !_hasGlow || !_glowVisible) return;

            // Let the state tween own the image while it is running, rather than both writing the
            // same glow in the same frame and fighting over it.
            if (_glowTween != null && _glowTween.IsActive) return;

            _pulseTime += deltaTime;

            // Rate-limited on purpose: each write rebuilds the mesh (see the class remarks).
            _pulseWriteTimer += deltaTime;
            float interval = 1f / Mathf.Max(1f, m_PulseUpdatesPerSecond);
            if (_pulseWriteTimer < interval) return;

            _pulseWriteTimer = 0f;
            WriteGlow();
        }

        private void UpdateShine(float deltaTime)
        {
            if (!_hasShine || m_ShineMode == SDFShineMode.Off) return;

            switch (_shinePhase)
            {
                case ShinePhase.Idle:
                    // Loop mode idles only until the first sweep is scheduled.
                    if (m_ShineMode != SDFShineMode.Loop) return;
                    if (m_SuppressShineWhenDisabled && _state == EnhancedButtonVisualState.Disabled) return;
                    _shinePhase = ShinePhase.Waiting;
                    _shineTimer = m_ShineInterval;
                    return;

                case ShinePhase.Waiting:
                    _shineTimer -= deltaTime;
                    if (_shineTimer <= 0f) StartSweep();
                    return;

                case ShinePhase.Sweeping:
                    _shineTimer += deltaTime;
                    float progress = _shineTimer / Mathf.Max(0.01f, m_ShineDuration);

                    if (progress < 1f)
                    {
                        m_Target.SetShinePosition(progress);
                        return;
                    }

                    // Park at 1 — fully off the far side, and invisible until the next sweep resets to 0.
                    m_Target.SetShinePosition(1f);

                    if (m_ShineMode == SDFShineMode.Loop)
                    {
                        _shinePhase = ShinePhase.Waiting;
                        _shineTimer = m_ShineInterval;
                    }
                    else
                    {
                        _shinePhase = ShinePhase.Idle;
                    }

                    return;
            }
        }

        private void OnValidate()
        {
            if (m_ShineDuration <= 0f) m_ShineDuration = 0.1f;
            if (m_ShineInterval < 0f) m_ShineInterval = 0f;
        }
    }
}
