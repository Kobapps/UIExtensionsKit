# UIExtensionsKit

UI components and utilities for uGUI. Starting with the one every game rewrites: a button that
feels good.

`EnhancedButton` **is** a `Button` — swap the component and `onClick`, navigation, `interactable`
and every existing listener keep working. What it adds is the part teams normally hand-roll per
project: state motion, a real selected state, sound and haptic hooks, and enough inspector
tooling to answer "why isn't this button reacting?" without a single log statement.

```csharp
// This is a complete, juicy button. There is no setup step.
var button = gameObject.AddComponent<EnhancedButton>();
button.Preset = ButtonPresetKind.Jelly;
button.onClick.AddListener(Buy);
```

---

## Install

Add from a git URL in the Package Manager — **Window ▸ Package Manager ▸ + ▸ Add package from git URL…**:

```
https://github.com/Kobapps/UIExtensionsKit.git?path=/Packages/com.kobapps.uiextensionskit
```

### EditorCoreKit is required for the editor tooling

The inspectors and windows are built on [EditorCoreKit](https://github.com/Kobapps/EditorCoreKit),
which UPM will **not** pull in automatically — git dependencies are not resolved transitively, so
it is declared under `relatedPackages` rather than `dependencies`. Add it yourself:

```
https://github.com/Kobapps/EditorCoreKit.git?path=Packages/com.kobapps.editorcorekit
```

The runtime does not need it. A player build contains no editor code.

### Optional

- **DOTween** — animations use a built-in, dependency-free backend by default. To route them
  through DOTween instead, install it and enable the adapter in **Tools ▸ UIExtensionsKit ▸
  Settings**, which sets the `UIEXTENSIONSKIT_DOTWEEN` define. The adapter uses only DOTween's core
  API, so it needs no DOTween module setup.
- **[UIImageEffectsKit](https://github.com/Kobapps/UIImageEffectsKit)** — unlocks glow and shine on
  CTA buttons. There is an **Install UIImageEffectsKit** button in
  **Tools ▸ UIExtensionsKit ▸ Settings** that adds it through the Package Manager for you. Nothing
  else to switch on: once the package is present `UIEXTENSIONSKIT_UIIMAGEEFFECTS` is defined
  automatically and the adapter assembly starts compiling.

## Quick start

**GameObject ▸ UI ▸ Enhanced Button** creates one, with a canvas and EventSystem if the scene has
none.

### The demo scene

Import **Enhanced Button Demo** from the package's **Samples** tab, open
`Scenes/UIExtensionsKitDemo.unity` and press Play. There is nothing to build — the scene ships
ready to run.

Six sections behind a latched nav bar:

- **Presets** — every built-in feel side by side. Hover and click each one.
- **States** — latch the subject button, then disable it. The readout stays on Disabled, because
  Disabled beats everything. Re-enable it and the latch is still there.
- **Levels** — locked levels are `interactable = false`. Tap one: it still answers, with the
  `Rejected` sound and a warning haptic, instead of the silence a plain `Button` gives you. Unlock
  one and watch it animate out of its Disabled pose.
- **Shop** — affordability sets `interactable`, and every visual and audible consequence follows
  from that one assignment. Spend down past 100 and the buy buttons disable themselves.
- **Pause** — sets `Time.timeScale = 0`. The square stops; the buttons keep their bounce, because
  they run on unscaled time. The sfx and haptics toggles are latched buttons wired straight to
  `ButtonFeedback.SfxMuted` / `HapticsMuted`.
- **Effects** — CTA buttons on shaped sprites (banner, pill, star, gem, circle) wearing per-state
  glow and a sweeping shine. The locked one shows the glow dropping out and the sheen stopping.

A panel on the right logs every sfx and haptic request as it happens, and marks the ones the mutes
suppressed.

Two things in the scene are deliberately *absent* so the one sample stays universal:

> The EventSystem ships **without** an input module. A bootstrap component adds
> `InputSystemUIInputModule` or `StandaloneInputModule` at runtime, whichever matches the project —
> baking either one in would leave the sample unclickable on projects using the other.

> The Effects section's buttons are plain `Image`s. If UIImageEffectsKit is installed, `DemoEffects`
> upgrades them to `SDF Image` + **Enhanced Button Effects** at runtime and the glow and shine come
> alive; if it isn't, they stay ordinary buttons and the section says so. Baking `SDFImage` into the
> scene would make it a missing script — and take the whole scene down — for anyone without the
> package.

The sample's sprites are plain white shapes with an anti-aliased alpha silhouette, tinted per button.
That is what the glow and shine need: UIImageEffectsKit builds its distance field from the sprite's
alpha, so the silhouette *is* the effect.

---

## The five things it adds

### 1. Motion, from a preset

Seven built-in feels, as code — there is no asset to create, find, or leave unassigned.

| Preset | What it feels like |
| --- | --- |
| `Jelly` | Squash and stretch, wobbles on release. Chunky and casual. |
| `Bouncy` | Overshoots on grow, snaps on press. Energetic, no deformation. |
| `Mechanical` | Linear, tiny, instant, presses inward. Reads as a real key. |
| `Pop` | A sharp scale pop. Good for icon and toolbar buttons. |
| `Soft` | Slow and understated. Text links and secondary actions. |
| `Rigid` | Colour only, no movement. Safe inside tight layouts. |
| `None` | No motion from the kit. |

Every value in a preset is **relative to the pose the button was authored with** — `scale`
multiplies, `offset` adds, `tint` multiplies. That is what lets one preset drive buttons of any
size, colour or anchoring.

> **Centre your pivots.** Unity scales a RectTransform around its pivot, so a button pivoted at a
> corner grows out of that corner instead of swelling evenly — the animation looks lopsided and
> nothing obviously points at the cause. Unity's own UI templates are centred already; hand-built
> layouts often aren't. The inspector and the debugger window both flag this when a scaling preset
> meets an off-centre pivot.

Need something else? You never have to leave the button's inspector. Pick any built-in and hit
**Create editable copy** — it makes the asset, switches the button to `Custom` and points it at the
new preset. Choose `Custom` with nothing assigned and you get a **Generate Preset Asset** button
rather than a dead end. Once an asset is assigned, the full motion editor appears **inline in the
inspector**:

- **One state** — flat sliders with sensible ranges, tint and label tint, and a drawn thumbnail of
  the easing curve, so `OutBack` is a shape you can see overshoot rather than a word you hope is right.
- **All states** — every state as a row (scale, duration, ease, tint) for the questions that only
  make sense across states: is Pressed faster than Highlighted, do the durations form a rhythm.
- Reset state, copy from another state, even out durations, and a live preview.

**Tools ▸ UIExtensionsKit ▸ Preset Library** is the same editor over every preset and style in the
project at once, with a button you can hover, press and click to judge a feel without entering play
mode — plus duplicate, reseed, apply-to-selection and "which buttons use this?".

It also has a **timeline**: choose any state-to-state transition (or the click punch), then scrub it,
step a frame at a time, or play it back on loop. The readout shows normalized time, elapsed
milliseconds, and the curve's own value — so an overshoot becomes a number rather than an
impression. A 90ms press is long enough to feel and far too short to inspect at speed.

> A transition belongs to the state being **entered**: leaving Highlighted for Normal uses *Normal's*
> duration and ease. Set a state's timing to control how buttons arrive at it.

**Prefer hand-authored clips?** Set Animation Mode to `AnimationClip` and the inspector switches to a
flat, state-labelled clip list with a cross-fade control. If the GameObject has no `Animation`
component there is a button to add one, and clips that aren't marked Legacy — which an Animation
component silently cannot play — are called out by name. State clips play on layer 0 and the click clip on layer 1, so
a click blends over the current state instead of cancelling it.

### 2. States, including a real Selected

Unity gives you normal / highlighted / pressed / disabled. The one that is always missing is a
**latch** — this tab is the chosen one, this item is equipped, this filter is active.

```csharp
tabButton.SetSelected(true);              // announces itself: fires the event and the feedback
otherTab.SetSelected(false, notify: false); // goes quiet — what a tab group wants for siblings
```

There is a second latch for the one button on the screen that matters most:

```csharp
playButton.IsCta = true;                  // the primary action — gets the Cta pose and the shine
```

Resolution order is fixed and never surprises:

```
Disabled  >  Pressed  >  Highlighted  >  Selected (latch)  >  Cta  >  Normal
```

So a latched tab still shows its hover and press motion, a CTA still presses like everything else,
and a non-interactable button always reads as disabled no matter what else is true.

### 3. Sfx and haptics that route anywhere

The kit never plays a sound. It raises a request; the game decides what that means. Wire it once:

```csharp
ButtonFeedback.SfxHandler    = id   => AudioKit.Play(id);
ButtonFeedback.HapticHandler = type => Haptics.Play(type);
```

Every button in the game is now covered. Per-button (or per-style) you author which id and which
haptic goes with hover, press, click and **rejected** — the cue for clicking a button that isn't
interactable, which a plain `Button` answers with silence.

That refusal is also an event, so the game can say *why* it said no:

```csharp
buyButton.Rejected += _ => Toast("Not enough coins.");
```

`onClick` never runs for a non-interactable button, which is exactly why `Rejected` exists.

For richer routing, implement `IButtonFeedbackHandler` and `ButtonFeedback.RegisterHandler` it; it
receives whole requests including which button raised them. `SfxMuted` and `HapticsMuted` are the
global switches players actually look for.

### 4. Glow and shine for CTAs

With UIImageEffectsKit installed, add **Enhanced Button Effects (UIImageEffectsKit)** next to a
button whose graphic is an `SDF Image`. Missing Glow and Shine layers are added to the effect stack
for you.

- **Glow** follows the button's state — a halo that comes up on hover, changes colour when latched,
  and drops out when disabled.
- **Shine** sweeps across the button. By default its trigger, timing, width, angle and colour come
  from the **preset**, so it is authored once in the Preset Library and shared by every button using
  that preset — set `IsCta` and it runs. Turn *Shine From Preset* off for a one-off that has to
  differ from its preset, and the component's own Loop / OnHover / OnClick modes take over.
- **Pulse** breathes the glow for a primary action.

The split is deliberate: the kit owns the sweep's *timing* and the adapter only draws it. That is
why the shine is editable in the Preset Library alongside the rest of the feel, and why a disabled
button never shines whatever the trigger says — a sheen reads as "interactive", and putting one on a
dead button is a lie to the player.

One honest note about cost: moving the shine is cheap — UIImageEffectsKit updates only the material.
Changing the *glow* is not; every edit marks the mesh dirty, because glow reach changes how far the
quad must expand. That is fine for state changes, which are brief and user-driven, but a continuous
pulse would rebuild the mesh every frame forever, so the pulse is rate-limited (30 Hz by default)
rather than running at full framerate.

### 5. Reuse, through Style assets

Create a **UIExtensionsKit ▸ Enhanced Button Style**, assign it to a button's **Shared Style**, and
the button takes its motion and feedback from the asset — its own settings are ignored entirely, so
there is never a question of which one won.

Author a handful (Primary, Secondary, Destructive, Tab, CTA) and retuning how the whole game's
buttons feel is one asset edit. The style's inspector tells you how many buttons in the open scenes
it drives, and will select them for you.

### Sharing a button with your own animations

Adding an EnhancedButton to a button that something else already animates used to fight it — the
button would snap back to wherever it sat when it was enabled. It no longer does, for two reasons:

- **It writes only what its preset uses.** Every built-in except `Mechanical` is scale and tint
  only, so an Animator sliding the button in, or a panel tween moving it, is untouched.
- **It gives way when something else writes.** If a channel changed since the button last wrote it,
  the new value becomes the authored pose and the button's motion rides on top, rather than undoing
  it.

For the remaining case — a preset that *does* use a channel you want to own — clear it under
**Motion ▸ Writes**:

```csharp
button.AnimatedChannels &= ~ButtonAnimationChannels.Position;   // an Animator drives position
```

The inspector spells out what the button will actually write, and so does the debugger window.

---

## Debugging

Three things exist purely so a misbehaving button is diagnosable:

**The inspector's Preview strip** plays any state on demand — outside play mode as well as in it.
Deselecting the object restores the authored pose.

**The inspector's Live State panel** prints what the button actually resolved to: its state, its
style, which preset won, the active tween backend, the resolved animation and tint targets, and
whether anything is even listening for feedback.

**Tools ▸ UIExtensionsKit ▸ Enhanced Button Debugger** lists every EnhancedButton in the open scenes
with its live state, including inactive ones. The **Only problems** filter turns it into a linter —
clip mode with no clips, nothing to animate, a `ColorTint` transition that will fight the preset's
tint.

`button.DebugDescribe()` returns the same report as a string, worth logging from a build.

---

## Swapping the tween backend

Everything animates through `UITween`, so the engine underneath is replaceable:

```csharp
UITween.BackendId = UITween.DOTweenId;   // or your own
```

Implement `IUITweenRunner` and `UITween.Register` it to plug in something else entirely. Outside
play mode the built-in runner is always used — third-party engines have no edit-mode clock, and a
tween handed to one there would strand the button mid-pose.

## Requirements

Unity 6000.0+, `com.unity.ugui`. EditorCoreKit for the editor tooling. DOTween and UIImageEffectsKit
are optional.

## Licence

MIT — see [LICENSE.md](LICENSE.md).
