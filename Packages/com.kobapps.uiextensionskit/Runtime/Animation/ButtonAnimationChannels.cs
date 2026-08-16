using System;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// The transform and colour channels a button is allowed to write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A button is rarely the only thing animating itself: a panel slides its children in, an
    /// Animator drives a shake, a layout tween nudges everything on screen. Whoever writes last
    /// wins, so a button that writes <i>every</i> channel on every frame quietly stamps out those
    /// other systems — most visibly by snapping position back to wherever the button happened to
    /// be sitting when it was first enabled.
    /// </para>
    /// <para>
    /// So the button writes as little as it can. It works out which channels its preset genuinely
    /// uses and leaves the rest alone entirely; anything it never touches belongs to whoever else
    /// wants it. Clearing a flag here narrows that further, for the case where a preset does use a
    /// channel but something else should own it anyway.
    /// </para>
    /// </remarks>
    [Flags]
    public enum ButtonAnimationChannels
    {
        /// <summary>Write nothing. The button still fires feedback and events, but never moves.</summary>
        None = 0,

        /// <summary>Local scale, multiplied against the authored scale.</summary>
        Scale = 1 << 0,

        /// <summary>Anchored position, offset from the authored position.</summary>
        Position = 1 << 1,

        /// <summary>Local Z rotation, added to the authored rotation.</summary>
        Rotation = 1 << 2,

        /// <summary>The tint target's colour, multiplied against its authored colour.</summary>
        Tint = 1 << 3,

        /// <summary>The label's colour, multiplied against its authored colour.</summary>
        LabelTint = 1 << 4,

        /// <summary>Everything the preset asks for.</summary>
        All = Scale | Position | Rotation | Tint | LabelTint,
    }
}
