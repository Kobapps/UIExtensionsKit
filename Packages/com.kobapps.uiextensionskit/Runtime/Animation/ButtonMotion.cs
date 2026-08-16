using System;
using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// The pose a button holds in one visual state, and how long it takes to get there.
    /// </summary>
    /// <remarks>
    /// Every field is <b>relative to the authored pose</b>, never absolute: <see cref="scale"/>
    /// multiplies the scale the button has in the scene, <see cref="offset"/> adds to its anchored
    /// position, <see cref="tint"/> multiplies its graphic colour. That's what lets one preset drive
    /// buttons of different sizes, colours and layouts without knowing anything about them.
    /// </remarks>
    [Serializable]
    public struct ButtonStateMotion
    {
        [Tooltip("Multiplied onto the button's authored scale. (1,1,1) leaves it alone.")]
        public Vector3 scale;

        [Tooltip("Added to the button's authored anchored position, in pixels.")]
        public Vector2 offset;

        [Tooltip("Added to the button's authored Z rotation, in degrees.")]
        public float rotation;

        [Tooltip("Multiplied onto the tint graphic's authored colour. White leaves it alone.")]
        public Color tint;

        [Tooltip("Multiplied onto the label's authored colour. White leaves it alone.")]
        public Color labelTint;

        [Tooltip("Seconds to reach this pose. 0 snaps.")]
        public float duration;

        public UIEase ease;

        [Tooltip("Used only when Ease is Custom. Drawn in the inspector as a normal curve editor.")]
        public AnimationCurve curve;

        /// <summary>
        /// Eased progress at normalized time <paramref name="t"/>, honouring a custom curve.
        /// </summary>
        /// <remarks>
        /// Everything that animates a pose goes through here rather than calling
        /// <see cref="UIEasing.Evaluate"/> directly, so a custom curve works everywhere a named one
        /// does — animator, preview and any future backend alike.
        /// </remarks>
        public float Evaluate(float t)
        {
            if (ease != UIEase.Custom) return UIEasing.Evaluate(ease, t);

            // A Custom pose with no curve assigned would evaluate to a flat zero and freeze the
            // button at its start pose, which looks like a broken preset rather than a missing field.
            if (curve == null || curve.length == 0) return UIEasing.Evaluate(UIEase.OutQuad, t);

            return curve.Evaluate(Mathf.Clamp01(t));
        }

        /// <summary>Whether this pose needs per-frame curve evaluation rather than a named ease.</summary>
        public bool UsesCustomCurve => ease == UIEase.Custom;

        /// <summary>The authored pose, unchanged — the identity of this struct.</summary>
        public static ButtonStateMotion Identity => new ButtonStateMotion
        {
            scale = Vector3.one,
            offset = Vector2.zero,
            rotation = 0f,
            tint = Color.white,
            labelTint = Color.white,
            duration = 0.12f,
            ease = UIEase.OutQuad,
        };

        /// <summary>A uniform scale pose. The shape almost every preset entry wants.</summary>
        public static ButtonStateMotion Scaled(float uniformScale, float duration, UIEase ease)
        {
            var motion = Identity;
            motion.scale = new Vector3(uniformScale, uniformScale, 1f);
            motion.duration = duration;
            motion.ease = ease;
            return motion;
        }

        /// <summary>A non-uniform scale pose — squash and stretch.</summary>
        public static ButtonStateMotion Squashed(float x, float y, float duration, UIEase ease)
        {
            var motion = Identity;
            motion.scale = new Vector3(x, y, 1f);
            motion.duration = duration;
            motion.ease = ease;
            return motion;
        }

        /// <summary>This pose with a colour multiplier applied. Chainable onto the factories above.</summary>
        public ButtonStateMotion WithTint(Color value)
        {
            tint = value;
            return this;
        }

        /// <summary>This pose with a label colour multiplier applied.</summary>
        public ButtonStateMotion WithLabelTint(Color value)
        {
            labelTint = value;
            return this;
        }

        /// <summary>This pose with a pixel offset applied.</summary>
        public ButtonStateMotion WithOffset(float x, float y)
        {
            offset = new Vector2(x, y);
            return this;
        }

        /// <summary>
        /// Repair values that can only be mistakes. A zero scale hides the button outright and a
        /// negative duration runs the tween backwards, and neither is ever what someone meant —
        /// but a serialized struct defaults to exactly that, so a freshly-added field would
        /// otherwise make a button vanish.
        /// </summary>
        public ButtonStateMotion Sanitized()
        {
            var motion = this;
            if (motion.scale == Vector3.zero) motion.scale = Vector3.one;

            // A newly added serialized field arrives as all-zero, which would paint every label
            // transparent black. Nobody ever means that.
            if (motion.labelTint == default) motion.labelTint = Color.white;
            if (motion.scale.z == 0f) motion.scale.z = 1f;
            if (motion.duration < 0f) motion.duration = 0f;
            return motion;
        }
    }

    /// <summary>
    /// A one-shot wobble fired on click, layered <i>on top of</i> whatever state pose is current.
    /// This is the part that makes a button feel alive; the state poses alone read as merely correct.
    /// </summary>
    [Serializable]
    public struct ButtonPunch
    {
        public bool enabled;

        [Tooltip("Peak scale deviation. (0.2, -0.15, 0) stretches wide and squashes short at the peak.")]
        public Vector3 scaleAmplitude;

        [Tooltip("Peak Z rotation deviation, in degrees.")]
        public float rotationAmplitude;

        [Tooltip("Seconds for the whole wobble to play out and settle.")]
        public float duration;

        [Range(1, 8), Tooltip("How many times it swings before settling. 1 is a single clean bump.")]
        public int oscillations;

        [Range(0.1f, 6f), Tooltip("How sharply the swing decays. Higher settles sooner.")]
        public float damping;

        /// <summary>A punch that does nothing.</summary>
        public static ButtonPunch Disabled => new ButtonPunch
        {
            enabled = false,
            scaleAmplitude = Vector3.zero,
            rotationAmplitude = 0f,
            duration = 0.3f,
            oscillations = 1,
            damping = 1f,
        };

        /// <summary>A uniform scale punch — grows and settles.</summary>
        public static ButtonPunch Uniform(float amplitude, float duration, int oscillations, float damping)
            => new ButtonPunch
            {
                enabled = true,
                scaleAmplitude = new Vector3(amplitude, amplitude, 0f),
                rotationAmplitude = 0f,
                duration = duration,
                oscillations = oscillations,
                damping = damping,
            };

        /// <summary>A squash-and-stretch punch — one axis grows as the other shrinks.</summary>
        public static ButtonPunch Squash(float x, float y, float duration, int oscillations, float damping)
            => new ButtonPunch
            {
                enabled = true,
                scaleAmplitude = new Vector3(x, y, 0f),
                rotationAmplitude = 0f,
                duration = duration,
                oscillations = oscillations,
                damping = damping,
            };

        /// <summary>
        /// The wobble's shape at normalized time <paramref name="t"/>: a decaying sine that is
        /// guaranteed to be exactly 0 at both ends, so the punch always hands the button back to its
        /// state pose without a seam.
        /// </summary>
        public float Envelope(float t)
        {
            int swings = Mathf.Max(1, oscillations);
            float decay = Mathf.Pow(1f - Mathf.Clamp01(t), Mathf.Max(0.1f, damping));
            return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * swings) * decay;
        }
    }

    /// <summary>
    /// A complete button feel: one pose per visual state, plus the click punch. This is what a preset
    /// resolves to, and the only thing the animators actually consume.
    /// </summary>
    [Serializable]
    public struct ButtonMotionSet
    {
        public ButtonStateMotion normal;
        public ButtonStateMotion highlighted;
        public ButtonStateMotion pressed;
        public ButtonStateMotion selected;
        public ButtonStateMotion disabled;

        [Tooltip("Fired on click, on top of the current state pose.")]
        public ButtonPunch click;

        /// <summary>The pose for <paramref name="state"/>.</summary>
        public ButtonStateMotion Get(EnhancedButtonVisualState state)
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

        /// <summary>Replace the pose for <paramref name="state"/>. Used by the preset editor.</summary>
        public void Set(EnhancedButtonVisualState state, ButtonStateMotion motion)
        {
            switch (state)
            {
                case EnhancedButtonVisualState.Highlighted: highlighted = motion; break;
                case EnhancedButtonVisualState.Pressed: pressed = motion; break;
                case EnhancedButtonVisualState.Selected: selected = motion; break;
                case EnhancedButtonVisualState.Disabled: disabled = motion; break;
                default: normal = motion; break;
            }
        }

        /// <summary>Every pose sanitized. See <see cref="ButtonStateMotion.Sanitized"/>.</summary>
        public ButtonMotionSet Sanitized()
        {
            var set = this;
            set.normal = set.normal.Sanitized();
            set.highlighted = set.highlighted.Sanitized();
            set.pressed = set.pressed.Sanitized();
            set.selected = set.selected.Sanitized();
            set.disabled = set.disabled.Sanitized();
            if (set.click.oscillations < 1) set.click.oscillations = 1;
            if (set.click.duration < 0f) set.click.duration = 0f;
            return set;
        }
    }
}
