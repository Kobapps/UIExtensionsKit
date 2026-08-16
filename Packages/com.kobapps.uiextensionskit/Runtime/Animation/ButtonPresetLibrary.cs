using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>Which built-in feel a button uses. <see cref="Custom"/> defers to a preset asset.</summary>
    public enum ButtonPresetKind
    {
        /// <summary>No kit motion at all.</summary>
        None = 0,

        /// <summary>Squash and stretch that wobbles on release. Chunky, cartoon, casual-game.</summary>
        Jelly = 1,

        /// <summary>Big overshoot on grow, snappy on press. Reads as energetic without deforming.</summary>
        Bouncy = 2,

        /// <summary>Linear, tiny, instant, with a physical press-down. Reads as a real key.</summary>
        Mechanical = 3,

        /// <summary>A sharp scale pop. Good for icon and toolbar buttons.</summary>
        Pop = 4,

        /// <summary>Slow and understated. For text links and secondary actions.</summary>
        Soft = 5,

        /// <summary>Colour only, no movement — for buttons inside layouts that scaling would break.</summary>
        Rigid = 6,

        /// <summary>Use the preset asset assigned on the button or its style.</summary>
        Custom = 7,
    }

    /// <summary>
    /// The built-in feels, as code rather than assets. A button is juicy the moment it is added, with
    /// no asset to create, find or accidentally leave unassigned — and because these are pure data,
    /// they cost nothing at runtime and can't drift between scenes.
    /// </summary>
    public static class ButtonPresetLibrary
    {
        /// <summary>The preset a button gets when nothing else is chosen.</summary>
        public const ButtonPresetKind Default = ButtonPresetKind.Jelly;

        private static readonly Color Dimmed = new Color(0.6f, 0.6f, 0.6f, 1f);

        // Labels need to dim harder than their background to stay legible as "unavailable" —
        // a background at 60% still reads as active if the text on it is full white.
        private static readonly Color LabelDimmed = new Color(0.65f, 0.65f, 0.7f, 0.75f);
        private static readonly Color Faded = new Color(0.65f, 0.65f, 0.65f, 0.8f);

        /// <summary>
        /// The motion set for a built-in preset. <see cref="ButtonPresetKind.Custom"/> has no built-in
        /// data and resolves to <see cref="ButtonPresetKind.None"/> here; the caller is responsible for
        /// substituting its preset asset.
        /// </summary>
        public static ButtonMotionSet Get(ButtonPresetKind kind)
        {
            switch (kind)
            {
                case ButtonPresetKind.Jelly: return Jelly();
                case ButtonPresetKind.Bouncy: return Bouncy();
                case ButtonPresetKind.Mechanical: return Mechanical();
                case ButtonPresetKind.Pop: return Pop();
                case ButtonPresetKind.Soft: return Soft();
                case ButtonPresetKind.Rigid: return Rigid();
                default: return None();
            }
        }

        /// <summary>Every kind that has built-in data, in menu order. Excludes <see cref="ButtonPresetKind.Custom"/>.</summary>
        public static readonly ButtonPresetKind[] BuiltIn =
        {
            ButtonPresetKind.None,
            ButtonPresetKind.Jelly,
            ButtonPresetKind.Bouncy,
            ButtonPresetKind.Mechanical,
            ButtonPresetKind.Pop,
            ButtonPresetKind.Soft,
            ButtonPresetKind.Rigid,
        };

        /// <summary>A one-line description of a preset, shown in the inspector under the dropdown.</summary>
        public static string Describe(ButtonPresetKind kind)
        {
            switch (kind)
            {
                case ButtonPresetKind.Jelly: return "Squash and stretch, wobbles on release. Chunky and casual.";
                case ButtonPresetKind.Bouncy: return "Overshoots on grow, snaps on press. Energetic, no deformation.";
                case ButtonPresetKind.Mechanical: return "Linear, tiny, instant, presses inward. Reads as a real key.";
                case ButtonPresetKind.Pop: return "A sharp scale pop. Good for icon and toolbar buttons.";
                case ButtonPresetKind.Soft: return "Slow and understated. Text links and secondary actions.";
                case ButtonPresetKind.Rigid: return "Colour only, no movement. Safe inside tight layouts.";
                case ButtonPresetKind.Custom: return "Driven by the assigned preset asset.";
                default: return "No motion from the kit.";
            }
        }

        private static ButtonMotionSet None() => new ButtonMotionSet
        {
            normal = ButtonStateMotion.Scaled(1f, 0f, UIEase.Linear),
            highlighted = ButtonStateMotion.Scaled(1f, 0f, UIEase.Linear),
            pressed = ButtonStateMotion.Scaled(1f, 0f, UIEase.Linear),
            selected = ButtonStateMotion.Scaled(1f, 0f, UIEase.Linear),
            disabled = ButtonStateMotion.Scaled(1f, 0f, UIEase.Linear),
            click = ButtonPunch.Disabled,
        };

        // Returns to rest on an elastic settle — the wobble is the whole point of the feel.
        private static ButtonMotionSet Jelly() => new ButtonMotionSet
        {
            normal = ButtonStateMotion.Scaled(1f, 0.45f, UIEase.OutElastic),
            highlighted = ButtonStateMotion.Scaled(1.05f, 0.25f, UIEase.OutBack),
            pressed = ButtonStateMotion.Squashed(1.12f, 0.86f, 0.09f, UIEase.OutQuad),
            selected = ButtonStateMotion.Scaled(1.06f, 0.3f, UIEase.OutBack),
            disabled = ButtonStateMotion.Scaled(1f, 0.15f, UIEase.OutQuad).WithTint(Dimmed).WithLabelTint(LabelDimmed),
            click = ButtonPunch.Squash(0.22f, -0.16f, 0.5f, 3, 1.6f),
        };

        private static ButtonMotionSet Bouncy() => new ButtonMotionSet
        {
            normal = ButtonStateMotion.Scaled(1f, 0.3f, UIEase.OutBack),
            highlighted = ButtonStateMotion.Scaled(1.1f, 0.25f, UIEase.OutBack),
            pressed = ButtonStateMotion.Scaled(0.92f, 0.08f, UIEase.OutQuad),
            selected = ButtonStateMotion.Scaled(1.08f, 0.28f, UIEase.OutBack),
            disabled = ButtonStateMotion.Scaled(0.98f, 0.15f, UIEase.OutQuad).WithTint(Dimmed).WithLabelTint(LabelDimmed),
            click = ButtonPunch.Uniform(0.18f, 0.38f, 1, 0.7f),
        };

        // No overshoot anywhere, and the press moves the button physically down rather than scaling it.
        private static ButtonMotionSet Mechanical() => new ButtonMotionSet
        {
            normal = ButtonStateMotion.Scaled(1f, 0.06f, UIEase.Linear),
            highlighted = ButtonStateMotion.Scaled(1.02f, 0.06f, UIEase.Linear)
                .WithTint(new Color(1.1f, 1.1f, 1.1f, 1f)),
            pressed = ButtonStateMotion.Scaled(0.98f, 0.04f, UIEase.Linear).WithOffset(0f, -3f),
            selected = ButtonStateMotion.Scaled(1f, 0.06f, UIEase.Linear)
                .WithTint(new Color(1.15f, 1.15f, 1.15f, 1f)),
            disabled = ButtonStateMotion.Scaled(1f, 0.08f, UIEase.Linear)
                .WithTint(new Color(0.5f, 0.5f, 0.5f, 1f)).WithLabelTint(LabelDimmed),
            click = ButtonPunch.Disabled,
        };

        private static ButtonMotionSet Pop() => new ButtonMotionSet
        {
            normal = ButtonStateMotion.Scaled(1f, 0.16f, UIEase.OutCubic),
            highlighted = ButtonStateMotion.Scaled(1.06f, 0.16f, UIEase.OutCubic),
            pressed = ButtonStateMotion.Scaled(0.9f, 0.07f, UIEase.OutQuad),
            selected = ButtonStateMotion.Scaled(1.05f, 0.18f, UIEase.OutBack),
            disabled = ButtonStateMotion.Scaled(1f, 0.15f, UIEase.OutQuad)
                .WithTint(new Color(0.55f, 0.55f, 0.55f, 1f)).WithLabelTint(LabelDimmed),
            click = ButtonPunch.Uniform(0.25f, 0.3f, 1, 0.9f),
        };

        private static ButtonMotionSet Soft() => new ButtonMotionSet
        {
            normal = ButtonStateMotion.Scaled(1f, 0.3f, UIEase.InOutSine),
            highlighted = ButtonStateMotion.Scaled(1.03f, 0.3f, UIEase.InOutSine),
            pressed = ButtonStateMotion.Scaled(0.985f, 0.15f, UIEase.InOutSine),
            selected = ButtonStateMotion.Scaled(1.02f, 0.3f, UIEase.InOutSine),
            disabled = ButtonStateMotion.Scaled(1f, 0.25f, UIEase.InOutSine).WithTint(Faded).WithLabelTint(LabelDimmed),
            click = ButtonPunch.Uniform(0.08f, 0.35f, 1, 1.2f),
        };

        private static ButtonMotionSet Rigid() => new ButtonMotionSet
        {
            normal = ButtonStateMotion.Scaled(1f, 0.08f, UIEase.OutQuad),
            highlighted = ButtonStateMotion.Scaled(1f, 0.08f, UIEase.OutQuad)
                .WithTint(new Color(1.12f, 1.12f, 1.12f, 1f)),
            pressed = ButtonStateMotion.Scaled(1f, 0.04f, UIEase.Linear)
                .WithTint(new Color(0.85f, 0.85f, 0.85f, 1f)),
            selected = ButtonStateMotion.Scaled(1f, 0.08f, UIEase.OutQuad)
                .WithTint(new Color(1.2f, 1.2f, 1.2f, 1f)),
            disabled = ButtonStateMotion.Scaled(1f, 0.08f, UIEase.OutQuad)
                .WithTint(new Color(0.5f, 0.5f, 0.5f, 1f)).WithLabelTint(LabelDimmed),
            click = ButtonPunch.Disabled,
        };
    }
}
