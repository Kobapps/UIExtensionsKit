using System;
using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>When a button's shine band sweeps across it.</summary>
    public enum ButtonShineTrigger
    {
        /// <summary>Never.</summary>
        Off = 0,

        /// <summary>Only while the button is the call to action — see <see cref="EnhancedButton.IsCta"/>.</summary>
        Cta = 1,

        /// <summary>Continuously, whatever the state. Use sparingly; every button shining is no button shining.</summary>
        Always = 2,

        /// <summary>One sweep each time the button is hovered or focused.</summary>
        OnHover = 3,

        /// <summary>One sweep each time the button is clicked.</summary>
        OnClick = 4,
    }

    /// <summary>
    /// A sheen that travels across a button, carried by the preset so it is authored and reused
    /// alongside the rest of the feel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The kit owns the <i>intent and the timing</i>; it does not draw anything. An effects module —
    /// today the UIImageEffectsKit adapter — reads <see cref="EnhancedButton.ShinePosition"/> and
    /// renders the band. That split is deliberate: a shine authored on the adapter component would
    /// be per-button and invisible to the Preset Library, which is exactly the drift that made
    /// styles worth having in the first place.
    /// </para>
    /// <para>
    /// Because it lives in the motion set, editing a preset's shine changes every button using that
    /// preset, and the Preset Library can preview and tune it like any other part of the feel.
    /// </para>
    /// </remarks>
    [Serializable]
    public struct ButtonShine
    {
        [Tooltip("When the sweep runs. Cta ties it to the button's call-to-action latch.")]
        public ButtonShineTrigger trigger;

        [Range(0.1f, 3f), Tooltip("Seconds for one sweep to cross the button.")]
        public float sweepDuration;

        [Range(0f, 10f), Tooltip("Seconds of rest between sweeps, for the repeating triggers.")]
        public float interval;

        [Range(0.02f, 1f), Tooltip("Width of the band as a fraction of its travel.")]
        public float width;

        [Range(0f, 1f), Tooltip("Edge softness. 0 is a hard stripe, 1 a soft gradient.")]
        public float softness;

        [Range(0f, 360f), Tooltip("Sweep direction, in degrees.")]
        public float angle;

        [Tooltip("Band colour. Its alpha is the overall strength.")]
        public Color color;

        /// <summary>Whether this shine does anything at all.</summary>
        public bool Enabled => trigger != ButtonShineTrigger.Off;

        /// <summary>Whether this trigger repeats rather than firing once per interaction.</summary>
        public bool Repeats => trigger == ButtonShineTrigger.Cta || trigger == ButtonShineTrigger.Always;

        /// <summary>No shine, but with sane values so enabling it in the inspector behaves.</summary>
        public static ButtonShine None => new ButtonShine
        {
            trigger = ButtonShineTrigger.Off,
            sweepDuration = 0.7f,
            interval = 2.5f,
            width = 0.15f,
            softness = 0.6f,
            angle = 45f,
            color = new Color(1f, 1f, 1f, 0.8f),
        };

        /// <summary>A looping call-to-action sheen — the standard "press me" treatment.</summary>
        public static ButtonShine Cta(float sweepDuration = 0.7f, float interval = 2.5f)
        {
            ButtonShine shine = None;
            shine.trigger = ButtonShineTrigger.Cta;
            shine.sweepDuration = sweepDuration;
            shine.interval = interval;
            return shine;
        }

        /// <summary>
        /// Whether this shine should be running given the button's current situation.
        /// </summary>
        /// <remarks>
        /// A disabled button never shines, whatever the trigger says: a sheen reads as "interactive",
        /// and putting one on a dead button is an outright lie to the player.
        /// </remarks>
        public bool ShouldRun(EnhancedButtonVisualState state, bool isCta)
        {
            if (!Enabled || state == EnhancedButtonVisualState.Disabled) return false;

            switch (trigger)
            {
                case ButtonShineTrigger.Always: return true;
                case ButtonShineTrigger.Cta: return isCta;
                default: return false;
            }
        }

        /// <summary>Repair values a zeroed serialized struct would otherwise carry.</summary>
        public ButtonShine Sanitized()
        {
            ButtonShine shine = this;

            if (shine.sweepDuration <= 0f) shine.sweepDuration = 0.7f;
            if (shine.interval < 0f) shine.interval = 0f;
            if (shine.width <= 0f) shine.width = 0.15f;
            if (shine.color == default) shine.color = new Color(1f, 1f, 1f, 0.8f);

            return shine;
        }
    }
}
