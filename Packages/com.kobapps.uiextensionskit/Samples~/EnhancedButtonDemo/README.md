# Enhanced Button Demo

Open **Scenes/UIExtensionsKitDemo.unity** and press Play. Nothing to build.

Five sections behind a latched nav bar:

| Section | What to try |
| --- | --- |
| **Presets** | Hover and click each one. Same component, seven different feels. |
| **States** | Latch the subject, then disable it — the readout stays on Disabled, because Disabled beats everything. Re-enable it: the latch is still there. |
| **Levels** | Tap a locked level. It answers with the `Rejected` sound and a warning haptic instead of the silence a plain `Button` gives you. Unlock one and watch it animate out of its Disabled pose. |
| **Shop** | Spend down past 100. The buy buttons disable themselves — game state sets `interactable`, and every visual and audible consequence follows from that one assignment. |
| **Pause** | Hit Pause (`Time.timeScale = 0`). The square stops. The buttons keep their bounce, because they run on unscaled time. |

The panel on the right logs every sfx and haptic request as it happens, and marks the ones the mute
toggles suppressed. That is the whole integration surface: a real game replaces the body of
`DemoFeedbackLog.Handle` with a call into its own audio and haptics, and nothing about the buttons
changes.

## About the EventSystem

The scene's EventSystem ships **without** an input module, on purpose. `StandaloneInputModule` is
inert on a project set to the new Input System, and `InputSystemUIInputModule` is a missing script on
a project that doesn't have the package — either choice would leave the demo silently unclickable
somewhere. `DemoInputBootstrap` adds whichever one fits at runtime, resolving the Input System type
by name so the sample needs no dependency on `com.unity.inputsystem`.

## Glow and shine

Not wired up here, because it would make the sample depend on
[UIImageEffectsKit](https://github.com/Kobapps/UIImageEffectsKit). To add it: install that package
(there is a one-click button in **Tools ▸ UIExtensionsKit ▸ Settings**), swap a button's `Image`
component for an `SDF Image`, and add **Enhanced Button Effects (UIImageEffectsKit)** next to it.
Missing Glow and Shine layers are added to the effect stack for you.

## Files

- `Scenes/UIExtensionsKitDemo.unity` — the demo.
- `Scripts/` — the section controllers. Small on purpose; the interesting logic lives in the kit.
- `Styles/` — two shared `EnhancedButtonStyle` assets. Edit one and every button using it changes.
