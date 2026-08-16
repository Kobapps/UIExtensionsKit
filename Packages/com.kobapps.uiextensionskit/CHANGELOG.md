# Changelog

All notable changes to this package are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] — 2026-08-16

### Added

- **`Cta` visual state** — a second latch, for the one primary action on a screen. Sits between
  `Selected` and `Normal` in the precedence order, so a CTA still presses and hovers normally.
  `EnhancedButton.IsCta` drives it.
- **Shine in the preset** — `ButtonShine` (trigger, sweep duration, interval, width, softness,
  angle, colour) now travels with the motion set, so it is authored in the Preset Library, shared by
  every button using that preset, and previewable alongside the rest of the feel. Triggers are
  `Cta`, `Always`, `OnHover` and `OnClick`; a disabled button never shines.
  `EnhancedButton.TickShine` owns the phase, `ShinePosition` exposes it, and the UIImageEffectsKit
  adapter renders it — turn *Shine From Preset* off there for a one-off that must differ.
- **`ButtonAnimationChannels`** — the transform and colour channels a button may write, exposed as
  **Motion ▸ Writes** and on `EnhancedButton.AnimatedChannels`. For the case where a preset does use
  a channel that something else should own.
- **`SetMotionOverride` / `ClearMotionOverride`** — drive one button from a motion set built in
  code, ignoring its preset and style.
- Per-state label text now covers `Cta`.

### Fixed

- **A button no longer fights other animations on the same object.** It writes only the channels its
  preset actually uses, and when a channel changes underneath it, it adopts the new value as the
  authored pose instead of stamping it back. Adding an EnhancedButton to a button with an existing
  position animation used to reset its position; it no longer does.
- **Elastic presses no longer stick mid-animation.** The tween driver was a `HideAndDontSave`
  GameObject, which left it scene-less and so never sent `Update` — animations only advanced when
  something else forced a frame, most visibly as a press that froze until the next input. The driver
  is now injected into the PlayerLoop and owns no GameObject at all.

## [0.1.0] — 2026-08-15

First release. The package starts with one component, `EnhancedButton`, and the infrastructure the
rest of the kit will build on.

### Added

- **`EnhancedButton`** — a drop-in replacement for `UnityEngine.UI.Button`. It *is* a `Button`, so
  `onClick`, navigation, `interactable` and existing listeners keep working unchanged.
- **Visual states** — `Normal`, `Highlighted`, `Pressed`, `Selected`, `Disabled`, with a fixed
  precedence: Disabled beats Pressed beats Highlighted beats Selected beats Normal. `Selected` is a
  latch the game sets (a chosen tab, an equipped item), not EventSystem focus.
- **Motion presets** — `Jelly`, `Bouncy`, `Mechanical`, `Pop`, `Soft`, `Rigid` and `None`, built in
  as code so a button is juicy with no asset to create. `Custom` defers to a
  `ButtonAnimationPreset` asset.
- **Two animation modes** — tweened scale / offset / rotation / tint, or one legacy `AnimationClip`
  per state played through an `Animation` component.
- **Swappable tween backend** — a dependency-free built-in runner, or DOTween via an adapter
  assembly gated on `UIEXTENSIONSKIT_DOTWEEN`. The built-in runner can be stepped by hand, so
  inspector previews animate outside play mode and tests run without waiting on frames.
- **`EnhancedButtonStyle`** — a shared asset carrying preset, timing and feedback, so one edit
  restyles every button that uses it.
- **Feedback hooks** — `ButtonFeedback` routes sfx ids and haptic types to whatever the game
  already uses, with global mutes and a `Rejected` cue for clicks on disabled buttons. The refusal
  is also an `EnhancedButton.Rejected` event, so the game can explain why it said no — `onClick`
  never fires for a non-interactable button.
- **`IEnhancedButtonEffects`** — the seam extra visual layers plug into.
- **UIImageEffectsKit adapter** — `SDFButtonEffects` drives per-state glow and a sweeping shine for
  CTA buttons. Its assembly compiles only when `com.kobapps.uiimageeffectskit` is installed.
- **Feedback adapters** — `IButtonSfxAdapter` / `IButtonHapticsAdapter`, plus a
  `ButtonFeedbackAdapter` MonoBehaviour base that registers itself on enable. A game plugs its own
  audio and haptics in without the kit referencing either. The one-line `SfxHandler` delegates
  still work and coexist.
- **Per-button style overrides** — `ButtonStyleOverride` lets one button keep a shared style but
  supply its own preset, feedback, timing or animation mode. For the destructive confirm that
  should look like every other button and sound different.
- **Per-state labels** — presets carry a `labelTint` so the label dims with (or against) the
  background, and `ButtonLabelTexts` swaps the text per state: Pause/Resume, Buy/Bought,
  Locked/Play. Works with uGUI `Text` and TextMeshPro without the kit referencing TMP.
- **Custom animation curves** — `UIEase.Custom` plus an `AnimationCurve` on each pose, for the
  bespoke anticipate or two-stage settle no named ease covers. Named eases still run natively on
  the selected backend.
- **Preset Library window** (`Tools ▸ UIExtensionsKit ▸ Preset Library`) — every built-in feel and
  every preset/style asset in one place, each with a button you can actually hover, press and click
  to judge it, driven by the real curve data and without entering play mode. Create, duplicate,
  reseed from a built-in, delete, apply to the current selection, and see which buttons already use it.
- **Native component inspectors** — the button, style and preset inspectors use stock Foldouts,
  HelpBoxes and PropertyFields rather than EditorCoreKit. An inspector sits in a column of Unity's
  own inspectors, where a themed card stack reads as a foreign body however good the theme is.
  EditorCoreKit is reserved for the tool windows, where a shell belongs — and this keeps the shared
  motion editor embeddable in either surface.
- **Two-column Preset Library** — preview and timeline on the left, the motion editor on the right,
  with a draggable divider that remembers where it was left and a scroll per column. Stacked
  vertically, tuning a duration scrolled away the very preview that tells you whether it was right.
- **Timeline scrubber** in the Preset Library — scrub the state you are editing, or the click punch,
  then drag through it frame by frame, step ±1 frame, or play it back with looping.
  A readout gives normalized time, elapsed milliseconds of the total, and the curve's own output, so
  an overshoot stops being "looks a bit much" and becomes a number: hold Bouncy's Normal→Highlighted
  at t=0.58 and the curve reads 1.100, putting the button 11% oversized against its 10% target.
  Most button transitions are under 200ms — long enough to feel, far too short to inspect.
- **Motion editor** — replaces the default nested property drawer (five collapsed foldouts of eight
  raw float boxes) with two views. *One state* gives flat sliders with sensible ranges, colour
  fields, and a drawn thumbnail of the selected easing curve so a named ease is a shape rather than
  a word. *All states* lays every state out as a row — scale, duration, ease, tint — which is the
  view for the questions that only make sense across states: is Pressed faster than Highlighted, do
  the durations form a rhythm, is one state still on a default nobody chose. Plus reset-state,
  copy-from-state, even-out-durations, and a live preview that updates on the same frame as the edit.
  A transition belongs to the state being *entered*, and the all-states view says so.
- **Off-centre pivot warning** — the inspector and the debugger window flag a button whose animation
  target has a non-centred pivot while its preset scales. Unity scales around the pivot, so such a
  button grows out of a corner instead of swelling evenly, and nothing else would point at why.
- **Editor tooling**, built on EditorCoreKit — an inspector with a state preview strip and a live
  resolved-state panel, an `EnhancedButtonStyle` inspector that reports which buttons a change will
  affect, a scene-wide **Enhanced Button Debugger** window with an "only problems" filter, and a
  settings window for the tween backend and integrations.
- **Sample** — *Enhanced Button Demo*: one ready-to-play scene, no builder to run. Six sections
  behind a latched nav bar (preset comparison, a state playground, a level grid where locked levels
  answer a tap, a shop gated on affordability, a pause overlay proving buttons keep animating at
  `timeScale = 0`, and shaped CTA buttons wearing glow and shine), plus a live log of every sfx and
  haptic request. Two things are deliberately absent from the scene so one sample serves everyone:
  the EventSystem has no input module and a bootstrap adds the matching one at runtime, and the
  Effects buttons are plain `Image`s that upgrade themselves to `SDFImage` only when
  UIImageEffectsKit is present. Baking either in would break the scene for half its audience.

[0.1.0]: https://github.com/Kobapps/UIExtensionsKit/releases/tag/v0.1.0
