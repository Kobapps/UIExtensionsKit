using System.Collections.Generic;
using System.Linq;
using EditorCoreKit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// One place to create, try, edit and apply button feels.
    /// </summary>
    /// <remarks>
    /// Presets were previously spread across three surfaces — an enum on every button, an asset
    /// inspector, and a style asset — with no way to compare two feels or to know what a change
    /// would break. This puts every preset in the project side by side with a button you can
    /// actually press, and makes "which buttons does this affect?" a question with an answer.
    /// </remarks>
    public sealed class ButtonPresetLibraryWindow : EditorWindow
    {
        private enum EntryKind { BuiltIn, PresetAsset, Style }

        private readonly struct Entry
        {
            public readonly EntryKind Kind;
            public readonly ButtonPresetKind BuiltIn;
            public readonly Object Asset;

            public Entry(ButtonPresetKind builtIn) { Kind = EntryKind.BuiltIn; BuiltIn = builtIn; Asset = null; }
            public Entry(EntryKind kind, Object asset) { Kind = kind; BuiltIn = ButtonPresetKind.Custom; Asset = asset; }

            public string Name => Kind == EntryKind.BuiltIn ? BuiltIn.ToString() : Asset != null ? Asset.name : "<missing>";
        }

        private KUIWindowShell _shell;
        private ButtonPresetPreview _preview;
        private readonly List<Entry> _entries = new List<Entry>();
        private int _selected;
        private string _styleSignature;
        private IVisualElementScheduledItem _playback;
        private double _playStarted;
        private bool _looping;
        private bool _timelinePunch;
        private EnhancedButtonVisualState _timelineState = EnhancedButtonVisualState.Highlighted;
        private System.Action _timelineSync;

        [MenuItem("Tools/UIExtensionsKit/Preset Library", false, 3)]
        public static void Open()
        {
            var window = GetWindow<ButtonPresetLibraryWindow>();
            window.titleContent = new GUIContent("Preset Library");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        private void CreateGUI()
        {
            _shell = new KUIWindowShell("Button Presets", "Create · try · edit · apply")
                .MountInto(rootVisualElement);

            _shell.Header.Add(KUIButton.Primary("New Preset Asset…", () => CreateAsset(CurrentMotion())));
            _shell.Header.Add(KUIButton.Secondary("New Style…", CreateStyle));
            _shell.Header.Add(KUIButton.Ghost("Refresh", Rebuild));

            Rebuild();

            // An asset can be deleted while it is selected here — from the Project window, from
            // another tool, or from the Delete button below. Guarding the reads is not enough:
            // UI Toolkit keeps driving the PropertyFields still bound to the dead object, and each
            // one throws every frame. The content has to be torn down, not just skipped.
            rootVisualElement.schedule.Execute(() =>
            {
                if (_entries.Count == 0) return;

                Entry current = Current;
                if (current.Kind != EntryKind.BuiltIn && current.Asset == null) Rebuild();
            }).Every(300);
        }

        private void OnFocus()
        {
            // Assets may have been created or deleted elsewhere while this window was in the background.
            if (_shell != null) Rebuild();
        }

        private void Rebuild()
        {
            int previous = _selected;
            ScanAssets();

            _shell.Sidebar.Reset();

            _shell.Sidebar.AddGroup("Built-in");
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Kind != EntryKind.BuiltIn) continue;

                int index = i;
                _shell.Sidebar.Add(entry.Name, () => Select(index));
            }

            if (_entries.Any(e => e.Kind == EntryKind.PresetAsset))
            {
                _shell.Sidebar.AddGroup("Preset assets");
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry entry = _entries[i];
                    if (entry.Kind != EntryKind.PresetAsset) continue;

                    int index = i;
                    _shell.Sidebar.Add(entry.Name, () => Select(index));
                }
            }

            if (_entries.Any(e => e.Kind == EntryKind.Style))
            {
                _shell.Sidebar.AddGroup("Styles");
                for (int i = 0; i < _entries.Count; i++)
                {
                    Entry entry = _entries[i];
                    if (entry.Kind != EntryKind.Style) continue;

                    int index = i;
                    _shell.Sidebar.Add(entry.Name, () => Select(index));
                }
            }

            _shell.Sidebar.AddFootnote($"{_entries.Count(e => e.Kind != EntryKind.BuiltIn)} asset(s) in project");

            Select(Mathf.Clamp(previous, 0, Mathf.Max(0, _entries.Count - 1)));
        }

        private void ScanAssets()
        {
            _entries.Clear();

            foreach (ButtonPresetKind kind in ButtonPresetLibrary.BuiltIn)
                _entries.Add(new Entry(kind));

            foreach (string guid in AssetDatabase.FindAssets("t:ButtonAnimationPreset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ButtonAnimationPreset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) _entries.Add(new Entry(EntryKind.PresetAsset, asset));
            }

            foreach (string guid in AssetDatabase.FindAssets("t:EnhancedButtonStyle"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<EnhancedButtonStyle>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) _entries.Add(new Entry(EntryKind.Style, asset));
            }
        }

        private void Select(int index)
        {
            if (index < 0 || index >= _entries.Count) return;

            StopPlayback();

            _selected = index;
            _shell.Sidebar.SelectedIndex = index;
            _shell.SetContent(BuildContent);
            _shell.Status.Set(Describe(_entries[index]), KUITone.Neutral);
        }

        private Entry Current => _entries[Mathf.Clamp(_selected, 0, Mathf.Max(0, _entries.Count - 1))];

        private ButtonMotionSet CurrentMotion()
        {
            Entry entry = Current;
            switch (entry.Kind)
            {
                case EntryKind.PresetAsset: return ((ButtonAnimationPreset)entry.Asset).Motion;
                case EntryKind.Style: return ((EnhancedButtonStyle)entry.Asset).ResolveMotion();
                default: return ButtonPresetLibrary.Get(entry.BuiltIn);
            }
        }

        private static string Describe(Entry entry) =>
            entry.Kind == EntryKind.BuiltIn
                ? $"Built-in · {ButtonPresetLibrary.Describe(entry.BuiltIn)}"
                : $"{entry.Kind} · {AssetDatabase.GetAssetPath(entry.Asset)}";

        #region Content

        private VisualElement BuildContent()
        {
            if (_entries.Count == 0)
                return new KUIEmptyState("No presets", "That should be impossible — the built-ins are compiled in.");

            // Two columns, inspector-style: what you are watching on the left, what you are changing
            // on the right. Stacked vertically, tuning a duration meant scrolling away from the very
            // preview that tells you whether the change was right.
            var split = new KUISplitView(firstSize: 430f, vertical: false,
                persistenceKey: "Kobapps.UIExtensionsKit.PresetLibrarySplit");
            split.style.flexGrow = 1;

            // Build order matters: the preview card assigns _preview, which the timeline and the
            // edit card both drive.
            VisualElement preview = BuildPreviewCard();
            VisualElement timeline = BuildTimelineCard();
            VisualElement edit = BuildEditCard();
            VisualElement apply = BuildApplyCard();

            // Each column scrolls on its own, so a long motion editor never pushes the preview away.
            split.First.Add(KUILayout.Page(preview, timeline, apply));
            split.Second.Add(KUILayout.Page(edit));

            return split;
        }

        private VisualElement BuildPreviewCard()
        {
            Entry entry = Current;
            var card = new KUICard(entry.Name, "Hover it, press it, click it. This is the real curve data.");

            _preview = new ButtonPresetPreview(entry.Name);
            _preview.SetMotion(CurrentMotion());
            card.Add(_preview);

            var states = KUILayout.WrapRow();
            foreach (EnhancedButtonVisualState state in
                     (EnhancedButtonVisualState[])System.Enum.GetValues(typeof(EnhancedButtonVisualState)))
            {
                EnhancedButtonVisualState captured = state;
                states.Add(KUIButton.Secondary(state.ToString(), () => _preview.ForceState(captured)));
            }

            states.Add(KUIButton.Ghost("Follow pointer", () => _preview.ReleaseForcedState()));
            states.Add(KUIButton.Primary("Click", () => _preview.Punch()));
            card.Add(states);

            if (entry.Kind == EntryKind.BuiltIn)
                card.Add(KUIText.Muted(ButtonPresetLibrary.Describe(entry.BuiltIn)));

            return card;
        }


        /// <summary>
        /// A transport for the preview: pick a transition, scrub it by hand, or play it back.
        /// </summary>
        /// <remarks>
        /// Most button transitions are under 200ms, which is long enough to feel and far too short
        /// to inspect. Scrubbing turns "it looks a bit much" into a number — hold OutBack at t=0.6
        /// and the readout shows the curve is at 1.09, so the button is 9% oversized at its peak.
        /// </remarks>
        /// <summary>
        /// A transport for the preview: scrub the state you are editing, or the click punch.
        /// </summary>
        /// <remarks>
        /// The state comes from whatever the motion editor is focused on rather than a pair of
        /// pickers — a transition is owned by the state being entered, so choosing a state already
        /// says which animation this is. Most button animations are under 200ms: long enough to
        /// feel, far too short to inspect at speed.
        /// </remarks>
        private VisualElement BuildTimelineCard()
        {
            var card = new KUICard("Timeline", "Scrub the animation frame by frame, or play it back.");

            if (_preview == null) return card;

            var scrub = new Slider("Time", 0f, 1f) { value = 0f, showInputField = true };
            var readout = KUIText.Code(string.Empty);
            var label = KUIText.Muted(string.Empty);

            void Sync()
            {
                label.text = _timelinePunch
                    ? "Scrubbing: click punch"
                    : $"Scrubbing: into {_timelineState}";
                UpdateReadout(readout, scrub.value);
            }

            void Arm()
            {
                if (_timelinePunch) _preview.BeginScrubPunch();
                else _preview.BeginScrubTransition(EnhancedButtonVisualState.Normal, _timelineState);
            }

            void EnsureArmed()
            {
                if (!_preview.IsScrubbing) Arm();
            }

            card.Add(label);

            var punchToggle = new KUIToggleSwitch("Scrub the click punch instead", _timelinePunch, value =>
            {
                _timelinePunch = value;
                StopPlayback();

                if (_preview.IsScrubbing)
                {
                    Arm();
                    scrub.SetValueWithoutNotify(0f);
                    _preview.ScrubTo(0f);
                }

                Sync();
            });
            card.Add(punchToggle);

            scrub.RegisterValueChangedCallback(e =>
            {
                StopPlayback();
                EnsureArmed();
                _preview.ScrubTo(e.newValue);
                UpdateReadout(readout, e.newValue);
            });
            card.Add(scrub);
            card.Add(readout);

            var transport = KUILayout.WrapRow();
            transport.Add(KUIButton.Primary("Play", () =>
            {
                EnsureArmed();
                StartPlayback(scrub, readout);
            }));
            transport.Add(KUIButton.Secondary("Stop", StopPlayback));
            transport.Add(new KUIToggleSwitch("Loop", false, value => _looping = value));

            // Step by roughly one frame at 60fps, for picking apart the first few milliseconds.
            transport.Add(KUIButton.Ghost("-1 frame", () => { EnsureArmed(); Step(scrub, readout, -1f / 60f); }));
            transport.Add(KUIButton.Ghost("+1 frame", () => { EnsureArmed(); Step(scrub, readout, 1f / 60f); }));
            transport.Add(KUIButton.Ghost("Release to pointer", () =>
            {
                StopPlayback();
                _preview.EndScrub();
            }));

            card.Add(transport);
            card.Add(KUIText.Muted(
                "The preview stays clickable — hovering or pressing it hands control back from the scrubber."));

            // Configure only. Arming here would take the preview away before anyone asked.
            Sync();

            _preview.ScrubReleased += () =>
            {
                StopPlayback();
                scrub.SetValueWithoutNotify(0f);
            };

            // Focusing a state in the motion editor retargets the timeline at it.
            _timelineSync = () =>
            {
                if (_preview.IsScrubbing && !_timelinePunch)
                {
                    Arm();
                    scrub.SetValueWithoutNotify(0f);
                    _preview.ScrubTo(0f);
                }

                Sync();
            };

            return card;
        }

        private void UpdateReadout(Label readout, float t)
        {
            if (_preview == null) return;

            float seconds = _preview.ScrubDuration;
            float eased = _preview.EasedAt(t);

            string overshoot = eased > 1.0001f ? "  ← overshooting"
                : eased < -0.0001f ? "  ← undershooting"
                : string.Empty;

            readout.text =
                $"t = {t:0.000}   |   {t * seconds * 1000f:0} ms of {seconds * 1000f:0} ms   " +
                $"|   curve = {eased:0.000}{overshoot}";
        }

        private void Step(Slider scrub, Label readout, float deltaSeconds)
        {
            StopPlayback();

            float duration = Mathf.Max(0.0001f, _preview.ScrubDuration);
            float next = Mathf.Clamp01(scrub.value + deltaSeconds / duration);

            scrub.SetValueWithoutNotify(next);
            _preview.ScrubTo(next);
            UpdateReadout(readout, next);
        }

        private void StartPlayback(Slider scrub, Label readout)
        {
            StopPlayback();

            _playStarted = EditorApplication.timeSinceStartup;
            _playback = scrub.schedule.Execute(() =>
            {
                float duration = Mathf.Max(0.0001f, _preview.ScrubDuration);
                float t = (float)((EditorApplication.timeSinceStartup - _playStarted) / duration);

                if (t >= 1f)
                {
                    if (_looping)
                    {
                        _playStarted = EditorApplication.timeSinceStartup;
                        t = 0f;
                    }
                    else
                    {
                        t = 1f;
                        StopPlayback();
                    }
                }

                scrub.SetValueWithoutNotify(t);
                _preview.ScrubTo(t);
                UpdateReadout(readout, t);
            }).Every(16);
        }

        private void StopPlayback()
        {
            _playback?.Pause();
            _playback = null;
        }

        private VisualElement BuildEditCard()
        {
            Entry entry = Current;

            if (entry.Kind == EntryKind.BuiltIn)
            {
                var readOnly = new KUICard("Edit", "Built-in feels are compiled in, so they cannot be edited directly.");
                readOnly.Add(KUIText.Body(
                    "Create an asset seeded from this one, then tune it. The button using it switches to " +
                    "the Custom preset and points at the asset."));
                readOnly.Add(KUIButton.Primary($"Create asset from {entry.Name}…",
                    () => CreateAsset(ButtonPresetLibrary.Get(entry.BuiltIn))));
                return readOnly;
            }

            var card = new KUICard("Edit", "Changes are live — the preview above updates as you type.");
            var serialized = new SerializedObject(entry.Asset);

            if (entry.Kind == EntryKind.PresetAsset)
            {
                // A flat, per-state editor instead of the default nested property drawer, which
                // buries the one field you want under five collapsed foldouts.
                var motion = new ButtonMotionEditor(serialized, "m_Motion", () =>
                {
                    if (entry.Asset != null) _preview?.SetMotion(((ButtonAnimationPreset)entry.Asset).Motion);
                });

                // Editing a state should show that state, so the preview reflects what you are tuning.
                motion.StateFocused += state =>
                {
                    _timelineState = state;
                    _timelineSync?.Invoke();
                    if (_preview != null && !_preview.IsScrubbing) _preview.ForceState(state);
                };

                card.Add(motion);
                card.Add(KUIProperty.Field(serialized, "m_Notes", "Notes"));

                var seed = KUILayout.WrapRow();
                seed.Add(KUIText.Muted("Reseed from:"));
                foreach (ButtonPresetKind kind in ButtonPresetLibrary.BuiltIn)
                {
                    ButtonPresetKind captured = kind;
                    seed.Add(KUIButton.Ghost(kind.ToString(), () => Reseed(captured)));
                }

                card.Add(seed);
            }
            else
            {
                // A style gets exactly what the button inspector gets, from the same builder — the
                // whole point of a style is that it is where a feel is authored, so this is the one
                // place that must not fall back to raw property fields.
                card.Add(KUIProperty.Field(serialized, "m_AnimationMode", "Animation Mode"));

                var body = new VisualElement();
                card.Add(body);
                BuildStyleMotionBody(body, serialized, entry);

                card.Add(KUIProperty.Field(serialized, "m_UnscaledTime", "Unscaled Time"));

                var feedback = new KUISection("Sfx & Haptics", false, "Kobapps.UIExtensionsKit.StyleFeedback");
                feedback.Add(KUIProperty.Field(serialized, "m_Feedback", string.Empty));
                card.Add(feedback);

                card.Add(KUIProperty.Field(serialized, "m_Notes", "Notes"));

                // Switching a style between Tween and Clip mode changes which controls belong here.
                card.schedule.Execute(() =>
                {
                    // The asset can be deleted while it is still selected here.
                    if (entry.Asset == null) return;

                    string signature = StyleSignature(serialized);
                    if (signature == _styleSignature) return;

                    _styleSignature = signature;
                    BuildStyleMotionBody(body, serialized, entry);
                }).Every(250);
            }

            // The motion editor pushes its own changes; a style's fields are plain property fields
            // that nothing observes, so those still need a poll.
            if (entry.Kind == EntryKind.Style)
                card.schedule.Execute(() =>
                {
                    if (entry.Asset != null) _preview?.SetMotion(CurrentMotion());
                }).Every(250);

            var actions = KUILayout.Row(
                KUIButton.Secondary("Ping asset", () => EditorGUIUtility.PingObject(entry.Asset)),
                KUIButton.Secondary("Duplicate", () => Duplicate(entry)),
                KUIButton.Danger("Delete", () => Delete(entry)));

            card.Add(actions);
            return card;
        }


        /// <summary>What the style's motion controls depend on; a change means a rebuild.</summary>
        private static string StyleSignature(SerializedObject serialized)
        {
            if (serialized == null || serialized.targetObject == null) return string.Empty;

            serialized.Update();

            SerializedProperty mode = serialized.FindProperty("m_AnimationMode");
            SerializedProperty preset = serialized.FindProperty("m_Preset");
            SerializedProperty asset = serialized.FindProperty("m_CustomPreset");

            return $"{mode.enumValueIndex}/{preset.enumValueIndex}/" +
                   $"{(asset.objectReferenceValue != null ? asset.objectReferenceValue.GetInstanceID() : 0)}";
        }

        private void BuildStyleMotionBody(VisualElement host, SerializedObject serialized, Entry entry)
        {
            host.Clear();
            _styleSignature = StyleSignature(serialized);

            var mode = (ButtonAnimationMode)serialized.FindProperty("m_AnimationMode").enumValueIndex;

            switch (mode)
            {
                case ButtonAnimationMode.Tween:
                    ButtonMotionSectionUI.BuildTweenSection(
                        host, serialized, "m_Preset", "m_CustomPreset",
                        onChanged: () => _preview?.SetMotion(((EnhancedButtonStyle)entry.Asset).ResolveMotion()),
                        rebuild: () => BuildStyleMotionBody(host, serialized, entry),
                        onStateFocused: state =>
                        {
                            _timelineState = state;
                            _timelineSync?.Invoke();
                            if (_preview != null && !_preview.IsScrubbing) _preview.ForceState(state);
                        });
                    break;

                case ButtonAnimationMode.AnimationClip:
                    // No GameObject here, so no Animation component to check — a style is an asset.
                    ButtonMotionSectionUI.BuildClipSection(
                        host, serialized, "m_Clips", null,
                        () => BuildStyleMotionBody(host, serialized, entry));
                    break;

                default:
                    host.Add(KUIText.Muted(
                        "No motion from the kit. Unity's own Selectable transition still applies."));
                    break;
            }
        }

        private VisualElement BuildApplyCard()
        {
            Entry entry = Current;
            var card = new KUICard("Apply", "Push this feel onto buttons, and see which ones already use it.");

            EnhancedButton[] selection = SelectedButtons();
            card.Add(KUIText.KeyValue("Selected buttons", selection.Length.ToString()));

            EnhancedButton[] users = Users(entry);
            card.Add(KUIText.KeyValue("Already using this", users.Length.ToString()));

            var row = KUILayout.WrapRow();

            var apply = KUIButton.Primary($"Apply to {selection.Length} selected", () => ApplyToSelection(entry));
            apply.SetEnabled(selection.Length > 0);
            row.Add(apply);

            var select = KUIButton.Secondary($"Select {users.Length} user(s)", () =>
                Selection.objects = users.Select(b => (Object)b.gameObject).ToArray());
            select.SetEnabled(users.Length > 0);
            row.Add(select);

            card.Add(row);

            if (selection.Length == 0)
                card.Add(KUIText.Muted("Select one or more EnhancedButtons in a scene to enable Apply."));

            // Selection changes constantly; keep the counts honest without a manual refresh.
            card.schedule.Execute(() =>
            {
                if (SelectedButtons().Length != selection.Length) Select(_selected);
            }).Every(400);

            return card;
        }

        #endregion

        #region Actions

        private static EnhancedButton[] SelectedButtons() =>
            Selection.gameObjects
                .Select(go => go.GetComponent<EnhancedButton>())
                .Where(b => b != null)
                .ToArray();

        private static EnhancedButton[] Users(Entry entry)
        {
            EnhancedButton[] all = Resources.FindObjectsOfTypeAll<EnhancedButton>()
                .Where(b => b != null && b.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(b))
                .ToArray();

            switch (entry.Kind)
            {
                case EntryKind.Style:
                    return all.Where(b => b.Style == (EnhancedButtonStyle)entry.Asset).ToArray();

                case EntryKind.PresetAsset:
                    // A preset asset is used through the Custom kind, on the button or via its style.
                    return all.Where(b =>
                        b.Style != null
                            ? b.Style.CustomPreset == (ButtonAnimationPreset)entry.Asset
                            : b.Preset == ButtonPresetKind.Custom && UsesPreset(b, (ButtonAnimationPreset)entry.Asset))
                        .ToArray();

                default:
                    return all.Where(b => b.Style == null && b.Preset == entry.BuiltIn).ToArray();
            }
        }

        private static bool UsesPreset(EnhancedButton button, ButtonAnimationPreset preset)
        {
            var serialized = new SerializedObject(button);
            SerializedProperty property = serialized.FindProperty("m_CustomPreset");
            return property != null && property.objectReferenceValue == preset;
        }

        private void ApplyToSelection(Entry entry)
        {
            EnhancedButton[] buttons = SelectedButtons();
            if (buttons.Length == 0) return;

            Undo.RecordObjects(buttons.Cast<Object>().ToArray(), "Apply button preset");

            foreach (EnhancedButton button in buttons)
            {
                var serialized = new SerializedObject(button);

                switch (entry.Kind)
                {
                    case EntryKind.Style:
                        serialized.FindProperty("m_Style").objectReferenceValue = entry.Asset;
                        break;

                    case EntryKind.PresetAsset:
                        // A style would win over these fields, so clear it or the change does nothing.
                        serialized.FindProperty("m_Style").objectReferenceValue = null;
                        serialized.FindProperty("m_Preset").enumValueIndex = (int)ButtonPresetKind.Custom;
                        serialized.FindProperty("m_CustomPreset").objectReferenceValue = entry.Asset;
                        break;

                    default:
                        serialized.FindProperty("m_Style").objectReferenceValue = null;
                        serialized.FindProperty("m_Preset").enumValueIndex = (int)entry.BuiltIn;
                        break;
                }

                serialized.ApplyModifiedProperties();
                button.InvalidateConfiguration();
                EditorUtility.SetDirty(button);
            }

            _shell.Status.Set($"Applied {entry.Name} to {buttons.Length} button(s)", KUITone.Success);
        }

        private void CreateAsset(ButtonMotionSet motion)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Button Animation Preset", "ButtonAnimationPreset", "asset",
                "Where should the preset live?");

            if (string.IsNullOrEmpty(path)) return;

            var preset = CreateInstance<ButtonAnimationPreset>();
            preset.SetMotion(motion);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            Rebuild();
            SelectAsset(preset);
            _shell.Status.Set($"Created {preset.name}", KUITone.Success);
        }

        private void CreateStyle()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Enhanced Button Style", "EnhancedButtonStyle", "asset",
                "Where should the style live?");

            if (string.IsNullOrEmpty(path)) return;

            var style = CreateInstance<EnhancedButtonStyle>();
            AssetDatabase.CreateAsset(style, path);
            AssetDatabase.SaveAssets();

            Rebuild();
            SelectAsset(style);
            _shell.Status.Set($"Created {style.name}", KUITone.Success);
        }

        private void Reseed(ButtonPresetKind kind)
        {
            if (!(Current.Asset is ButtonAnimationPreset preset)) return;

            Undo.RecordObject(preset, $"Reseed from {kind}");
            preset.SetMotion(ButtonPresetLibrary.Get(kind));
            EditorUtility.SetDirty(preset);

            _preview?.SetMotion(preset.Motion);
            _shell.Status.Set($"Reseeded {preset.name} from {kind}", KUITone.Success);
        }

        private void Duplicate(Entry entry)
        {
            string path = AssetDatabase.GetAssetPath(entry.Asset);
            string copy = AssetDatabase.GenerateUniqueAssetPath(path);

            if (!AssetDatabase.CopyAsset(path, copy)) return;

            AssetDatabase.SaveAssets();
            Rebuild();
            SelectAsset(AssetDatabase.LoadAssetAtPath<Object>(copy));
            _shell.Status.Set($"Duplicated to {System.IO.Path.GetFileName(copy)}", KUITone.Success);
        }

        private void Delete(Entry entry)
        {
            int users = Users(entry).Length;
            string warning = users > 0
                ? $"\n\n{users} button(s) in the open scenes use it and will fall back to the default feel."
                : string.Empty;

            bool confirmed = EditorUtility.DisplayDialog(
                $"Delete {entry.Name}?",
                $"This deletes {AssetDatabase.GetAssetPath(entry.Asset)}.{warning}",
                "Delete", "Cancel");

            if (!confirmed) return;

            // Clear the content before the asset goes, so nothing is left bound to it.
            _shell.SetContent((VisualElement)null);

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(entry.Asset));
            AssetDatabase.SaveAssets();

            _selected = 0;
            Rebuild();
            _shell.Status.Set("Deleted", KUITone.Warning);
        }

        private void SelectAsset(Object asset)
        {
            int index = _entries.FindIndex(e => e.Asset == asset);
            if (index >= 0) Select(index);
        }

        #endregion
    }
}
