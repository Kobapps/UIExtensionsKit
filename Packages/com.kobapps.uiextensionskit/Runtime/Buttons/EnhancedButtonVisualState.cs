namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// What an <see cref="EnhancedButton"/> currently looks like. This is the kit's own resolved
    /// state, not Unity's <c>Selectable.SelectionState</c> — the two differ in one important way:
    /// <see cref="Selected"/> here means a <b>latch the game set</b> (a chosen tab, an equipped item,
    /// an active filter), not "the EventSystem happens to have focus on this".
    /// </summary>
    /// <remarks>
    /// Resolution order is fixed and always the same: Disabled beats Pressed beats Highlighted beats
    /// Selected beats Cta beats Normal. So a latched tab still shows its press and hover motion, a
    /// call to action stops pitching the moment you touch it, and a non-interactable button always
    /// reads as disabled no matter what else is true.
    /// </remarks>
    public enum EnhancedButtonVisualState
    {
        /// <summary>Idle and interactable.</summary>
        Normal = 0,

        /// <summary>Pointer is over the button, or it holds keyboard/gamepad focus.</summary>
        Highlighted = 1,

        /// <summary>Pointer is held down on the button.</summary>
        Pressed = 2,

        /// <summary>Latched on by the game via <see cref="EnhancedButton.Selected"/>.</summary>
        Selected = 3,

        /// <summary>Not interactable.</summary>
        Disabled = 4,

        /// <summary>
        /// The screen's call to action, latched on via <see cref="EnhancedButton.IsCta"/>.
        /// </summary>
        /// <remarks>
        /// Deliberately the lowest-priority state rather than the highest. A CTA button is still an
        /// ordinary button: it must hover, press and disable like any other, and the attention
        /// treatment belongs to its <i>resting</i> pose. Ranking it above Highlighted would freeze it
        /// mid-pitch the moment the player reached for it.
        /// </remarks>
        Cta = 5,
    }

    /// <summary>
    /// Which parts of a shared <see cref="EnhancedButtonStyle"/> a button ignores in favour of its
    /// own settings.
    /// </summary>
    /// <remarks>
    /// A style is normally all-or-nothing, which is what makes it predictable. But every project has
    /// the one button that should look like all the others and sound different — the destructive
    /// confirm, the premium purchase. Without this you either fork the style for a single button or
    /// abandon it entirely; both lose the shared feel that was the point.
    /// </remarks>
    [System.Flags]
    public enum ButtonStyleOverride
    {
        /// <summary>The style supplies everything.</summary>
        None = 0,

        /// <summary>Use this button's own preset and preset asset.</summary>
        Preset = 1 << 0,

        /// <summary>Use this button's own sfx ids and haptics.</summary>
        Feedback = 1 << 1,

        /// <summary>Use this button's own unscaled-time setting.</summary>
        Timing = 1 << 2,

        /// <summary>Use this button's own animation mode.</summary>
        AnimationMode = 1 << 3,

        /// <summary>Use this button's own animation clips.</summary>
        Clips = 1 << 4,

        /// <summary>Use this button's own shine settings.</summary>
        Shine = 1 << 5,
    }

    /// <summary>How an <see cref="EnhancedButton"/> drives its state motion.</summary>
    public enum ButtonAnimationMode
    {
        /// <summary>No motion from the kit. Unity's own Selectable transition still applies.</summary>
        None = 0,

        /// <summary>Tween scale/offset/rotation/tint from a preset. The default, and the only mode that needs no extra setup.</summary>
        Tween = 1,

        /// <summary>Play a legacy <see cref="UnityEngine.AnimationClip"/> per state through an <see cref="UnityEngine.Animation"/> component.</summary>
        AnimationClip = 2,
    }
}
