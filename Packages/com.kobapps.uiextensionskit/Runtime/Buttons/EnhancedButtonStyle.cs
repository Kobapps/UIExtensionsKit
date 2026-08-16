using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// A button's whole feel — motion preset, timing and feedback — saved as one asset and shared by
    /// every button that should behave alike.
    /// </summary>
    /// <remarks>
    /// This is the answer to "make it easy to reuse across the game". Author a handful of styles
    /// (Primary, Secondary, Destructive, Tab, CTA), assign one per button, and retuning how the whole
    /// game's buttons feel is a single asset edit rather than a scene-wide sweep. A button with a
    /// style ignores its own local settings entirely, so there is never a question of which one won.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "EnhancedButtonStyle",
        menuName = "UIExtensionsKit/Enhanced Button Style",
        order = 100)]
    public class EnhancedButtonStyle : ScriptableObject
    {
        [Header("Motion")]
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

        [SerializeField]
        [Tooltip("Clips used in Animation Clip mode. A style with clips supplies them to every " +
                 "button using it; a style with none leaves each button's own clips alone.")]
        private ButtonClipSet m_Clips;

        [Header("Feedback")]
        [SerializeField]
        private ButtonFeedbackConfig m_Feedback = ButtonFeedbackConfig.Default;

        [Header("Notes")]
        [SerializeField, TextArea(2, 4)]
        [Tooltip("Free notes — which buttons should use this style.")]
        private string m_Notes = string.Empty;

        /// <summary>How buttons using this style drive their visuals.</summary>
        public ButtonAnimationMode AnimationMode => m_AnimationMode;

        /// <summary>The preset kind this style selects.</summary>
        public ButtonPresetKind Preset => m_Preset;

        /// <summary>The preset asset used when <see cref="Preset"/> is <see cref="ButtonPresetKind.Custom"/>.</summary>
        public ButtonAnimationPreset CustomPreset => m_CustomPreset;

        /// <summary>Whether buttons using this style animate on unscaled time.</summary>
        public bool UnscaledTime => m_UnscaledTime;

        /// <summary>The sfx and haptics buttons using this style request.</summary>
        public ButtonFeedbackConfig Feedback => m_Feedback;

        /// <summary>
        /// Clips this style supplies in <see cref="ButtonAnimationMode.AnimationClip"/> mode.
        /// </summary>
        /// <remarks>
        /// A style with no clips deliberately supplies nothing rather than supplying emptiness —
        /// otherwise assigning a tween-oriented style to a clip-driven button would wipe its clips.
        /// </remarks>
        public ButtonClipSet Clips => m_Clips;

        /// <summary>The shine this style's preset asks for.</summary>
        public ButtonShine Shine => ResolveMotion().shine.Sanitized();

        /// <summary>Author notes shown in the inspector. Not used at runtime.</summary>
        public string Notes => m_Notes;

        /// <summary>
        /// The motion this style resolves to. Falls back to the built-in default if it is set to
        /// Custom but has no asset assigned, so a half-finished style still animates rather than
        /// leaving every button using it dead.
        /// </summary>
        public ButtonMotionSet ResolveMotion()
        {
            if (m_Preset != ButtonPresetKind.Custom) return ButtonPresetLibrary.Get(m_Preset);
            if (m_CustomPreset != null) return m_CustomPreset.Motion;
            return ButtonPresetLibrary.Get(ButtonPresetLibrary.Default);
        }

        private void Reset()
        {
            m_AnimationMode = ButtonAnimationMode.Tween;
            m_Preset = ButtonPresetLibrary.Default;
            m_UnscaledTime = true;
            m_Feedback = ButtonFeedbackConfig.Default;
            m_Notes = string.Empty;
        }
    }
}
