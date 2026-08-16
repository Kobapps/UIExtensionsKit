using System;
using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// Optional per-state label text — Pause/Resume, Buy/Bought, Locked/Play.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every game writes this by hand: a handler that flips a label whenever some state changes,
    /// duplicated per button and easy to forget on one path. The button already knows its state, so
    /// it may as well own the text too.
    /// </para>
    /// <para>
    /// A blank entry means "leave the label alone", so filling in only Disabled is a perfectly good
    /// use of this — the other states keep whatever the label was authored with.
    /// </para>
    /// </remarks>
    [Serializable]
    public struct ButtonLabelTexts
    {
        [Tooltip("Drive the label's text from the button's state. Off leaves the label untouched.")]
        public bool enabled;

        public string normal;
        public string highlighted;
        public string pressed;

        [Tooltip("Shown while latched — 'Resume', 'Equipped', 'Selected'.")]
        public string selected;

        [Tooltip("Shown while not interactable — 'Locked', 'Sold out'.")]
        public string disabled;

        /// <summary>The text for <paramref name="state"/>, or empty to leave the label as it is.</summary>
        public string Get(EnhancedButtonVisualState state)
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

        /// <summary>Whether any state actually supplies text.</summary>
        public bool HasAnyText =>
            !string.IsNullOrEmpty(normal) || !string.IsNullOrEmpty(highlighted)
            || !string.IsNullOrEmpty(pressed) || !string.IsNullOrEmpty(selected)
            || !string.IsNullOrEmpty(disabled);
    }
}
