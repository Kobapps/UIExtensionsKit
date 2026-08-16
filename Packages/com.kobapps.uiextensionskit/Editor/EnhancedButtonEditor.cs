using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// Inspector for <see cref="EnhancedButton"/>, built on EditorCoreKit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity's own Selectable block — interactable, transition, navigation, onClick — is embedded
    /// as-is through an <see cref="IMGUIContainer"/> rather than rebuilt. It is the part users
    /// already know, Unity keeps changing it between versions, and reimplementing its
    /// transition-dependent field switching would be a maintenance debt for no gain. Everything the
    /// kit adds below it uses stock UI Toolkit for the same reason: an inspector should look like an
    /// inspector. EditorCoreKit is reserved for the tool windows, where a themed shell belongs.
    /// </para>
    /// <para>
    /// Two sections exist purely to make buttons diagnosable: a preview strip that plays any state
    /// on demand — outside play mode as well as in it — and a live panel showing what the button
    /// actually resolved to. Between them, "why doesn't this button do anything?" is answerable
    /// without adding a log statement.
    /// </para>
    /// </remarks>
    [CustomEditor(typeof(EnhancedButton), true)]
    [CanEditMultipleObjects]
    public class EnhancedButtonEditor : ButtonEditor
    {
        private static readonly EnhancedButtonVisualState[] AllStates =
            (EnhancedButtonVisualState[])System.Enum.GetValues(typeof(EnhancedButtonVisualState));

        private bool _previewing;

        private VisualElement _motionBody;
        private Label _channelHint;
        private string _motionSignature;
        private VisualElement _warnings;
        private Label _liveState;
        private Label _resolvedTargets;
        private string _warningSignature;

        private EnhancedButton Button => target as EnhancedButton;

        protected override void OnDisable()
        {
            // Never leave a previewed pose behind. Deselecting is the natural "I'm done looking"
            // signal, and a button silently stuck at 1.12 scale would eventually get saved.
            StopPreview();

            // SelectableEditor tracks its live editors here; skipping this leaks them.
            base.OnDisable();
        }

        public override VisualElement CreateInspectorGUI()
        {
            // Deliberately unthemed. This sits in a column of Unity's own inspectors, so it uses
            // stock Foldouts, HelpBoxes and PropertyFields and inherits whatever skin is running.
            var root = new VisualElement();

            root.Add(BuildStockButtonCard());
            root.Add(BuildStyleCard());
            root.Add(BuildMotionCard());
            root.Add(BuildTargetsCard());
            root.Add(BuildFeedbackCard());
            root.Add(BuildSelectionCard());
            root.Add(BuildEventsSection());

            _warnings = new VisualElement();
            root.Add(_warnings);

            root.Add(BuildPreviewCard());
            root.Add(BuildLiveStateCard());

            RefreshDynamicParts();

            // One timer drives every live part: warnings, the resolved-target line and the state
            // readout. Polling beats wiring change callbacks for values that also move at runtime.
            root.schedule.Execute(RefreshDynamicParts).Every(200);

            return root;
        }

        #region Cards

        private VisualElement BuildStockButtonCard()
        {
            Foldout card = InspectorUI.Section("Button");

            card.Add(new IMGUIContainer(() =>
            {
                // ButtonEditor handles its own Update/Apply.
                base.OnInspectorGUI();
            }));

            return card;
        }

        private VisualElement BuildStyleCard()
        {
            Foldout card = InspectorUI.Section("Shared Style");

            card.Add(InspectorUI.Field(serializedObject, "m_Style", "Style Asset"));

            HelpBox banner = InspectorUI.Help(
                "Motion and feedback come from the style asset. The local settings below are ignored " +
                "while it is assigned — edit the asset to change every button that uses it.");

            card.Add(banner);
            banner.style.display = HasStyle ? DisplayStyle.Flex : DisplayStyle.None;

            card.Add(InspectorUI.Action("New Style Asset…", CreateStyleAsset));

            // The banner and the disabled state of the motion/feedback cards both hang off this.
            card.schedule.Execute(() => banner.style.display = HasStyle ? DisplayStyle.Flex : DisplayStyle.None)
                .Every(250);

            return card;
        }

        private VisualElement BuildMotionCard()
        {
            Foldout card = InspectorUI.Section("Motion");

            card.Add(InspectorUI.Field(serializedObject, "m_AnimationMode", "Animation Mode"));

            // Rebuilt whenever the mode, preset or preset asset changes, because each combination
            // needs a different set of controls - and a dead end with only a warning is the one
            // thing it must never show.
            _motionBody = new VisualElement();
            card.Add(_motionBody);

            card.Add(InspectorUI.Field(serializedObject, "m_UnscaledTime", "Unscaled Time"));
            card.Add(InspectorUI.Field(serializedObject, "m_Channels", "Writes"));
            _channelHint = InspectorUI.Muted(string.Empty);
            card.Add(_channelHint);

            RebuildMotionBody();
            card.schedule.Execute(() =>
            {
                card.SetEnabled(!HasStyle);
                RefreshChannelHint();

                string signature = MotionSignature();
                if (signature == _motionSignature) return;

                _motionSignature = signature;
                RebuildMotionBody();
            }).Every(250);

            return card;
        }

        /// <summary>What the motion controls depend on. A change here means the body must be rebuilt.</summary>
        private string MotionSignature()
        {
            SerializedProperty mode = serializedObject.FindProperty("m_AnimationMode");
            SerializedProperty preset = serializedObject.FindProperty("m_Preset");
            SerializedProperty asset = serializedObject.FindProperty("m_CustomPreset");

            return $"{mode.enumValueIndex}/{preset.enumValueIndex}/" +
                   $"{(asset.objectReferenceValue != null ? asset.objectReferenceValue.GetInstanceID() : 0)}";
        }

        private void RebuildMotionBody()
        {
            if (_motionBody == null) return;

            _motionBody.Clear();
            _motionSignature = MotionSignature();

            var mode = (ButtonAnimationMode)serializedObject.FindProperty("m_AnimationMode").enumValueIndex;

            switch (mode)
            {
                case ButtonAnimationMode.Tween:
                    ButtonMotionSectionUI.BuildTweenSection(
                        _motionBody, serializedObject, "m_Preset", "m_CustomPreset",
                        onChanged: () => Button?.InvalidateConfiguration(),
                        rebuild: RebuildMotionBody,
                        onStateFocused: state =>
                        {
                            if (targets.Length != 1 || Button == null) return;
                            _previewing = true;
                            Button.EditorPreviewState(state);
                        });
                    break;

                case ButtonAnimationMode.AnimationClip:
                    ButtonMotionSectionUI.BuildClipSection(
                        _motionBody, serializedObject, "m_Clips",
                        targets.Length == 1 && Button != null ? Button.gameObject : null,
                        RebuildMotionBody);
                    break;

                default:
                    _motionBody.Add(InspectorUI.Muted(
                        "No motion from the kit. Unity's own Selectable transition still applies."));
                    break;
            }
        }


        private VisualElement BuildTargetsCard()
        {
            Foldout card = InspectorUI.Section("Targets");

            card.Add(InspectorUI.Field(serializedObject, "m_AnimationTarget", "Animation Target"));
            card.Add(InspectorUI.Field(serializedObject, "m_TintTarget", "Tint Target"));

            _resolvedTargets = InspectorUI.Muted(string.Empty);
            card.Add(_resolvedTargets);

            return card;
        }

        private VisualElement BuildFeedbackCard()
        {
            Foldout card = InspectorUI.Section("Feedback");

            card.Add(InspectorUI.Field(serializedObject, "m_Feedback", "Sfx & Haptics"));

            card.schedule.Execute(() => card.SetEnabled(!HasStyle)).Every(250);
            return card;
        }

        /// <summary>
        /// The latch and the CTA flag live in their own card deliberately. They belong to this
        /// button — a chosen tab, the one primary action on this screen — never to a shared style,
        /// so unlike motion and feedback they must stay editable when a style is assigned.
        /// </summary>
        private VisualElement BuildSelectionCard()
        {
            Foldout card = InspectorUI.Section("Selection");
            card.Add(InspectorUI.Field(serializedObject, "m_Selected", "Selected (latched)"));
            card.Add(InspectorUI.Field(serializedObject, "m_Cta", "Call to action"));

            var hint = InspectorUI.Muted(string.Empty);
            card.Add(hint);
            card.schedule.Execute(() =>
            {
                var button = target as EnhancedButton;
                if (button == null) return;

                ButtonShine shine = button.Shine;
                hint.text = !button.IsCta
                    ? "The screen's primary action. Drives the Cta pose, and the preset's shine."
                    : shine.Enabled
                        ? $"Shining on the {shine.trigger} trigger, {shine.sweepDuration:0.0}s sweep. " +
                          "Edit it in the preset, under Shine."
                        : "This preset has no shine. Set one in the Preset Library, under Shine.";
            }).Every(400);

            return card;
        }

        /// <summary>Describe what the button will genuinely write, and why the rest is left alone.</summary>
        private void RefreshChannelHint()
        {
            if (_channelHint == null) return;

            var button = target as EnhancedButton;
            if (button == null) return;

            ButtonAnimationChannels used = button.MotionSet.UsedChannels;
            ButtonAnimationChannels writes = used & button.AnimatedChannels;
            ButtonAnimationChannels skipped = used & ~button.AnimatedChannels;

            string text = writes == ButtonAnimationChannels.None
                ? "This preset moves nothing, so the button writes no transform or colour at all."
                : $"Writes {writes}. Everything else is left to whatever else animates this object.";

            if (skipped != ButtonAnimationChannels.None)
                text += $" {skipped} is in the preset but switched off here.";

            _channelHint.text = text;
        }

        private VisualElement BuildEventsSection()
        {
            Foldout section = InspectorUI.Section("Extra Events", false, "Kobapps.UIExtensionsKit.Events");

            section.Add(InspectorUI.Field(serializedObject, "m_OnHoverEnter"));
            section.Add(InspectorUI.Field(serializedObject, "m_OnHoverExit"));
            section.Add(InspectorUI.Field(serializedObject, "m_OnSelectedChanged"));

            return section;
        }

        private VisualElement BuildPreviewCard()
        {
            Foldout card = InspectorUI.Section("Preview");

            if (targets.Length > 1)
            {
                card.Add(InspectorUI.Muted("Select a single button to preview."));
                return card;
            }

            var states = InspectorUI.Row();
            foreach (EnhancedButtonVisualState state in AllStates)
            {
                EnhancedButtonVisualState captured = state;
                states.Add(InspectorUI.Action(state.ToString(), () =>
                {
                    _previewing = true;
                    Button?.EditorPreviewState(captured);
                }));
            }

            card.Add(states);

            card.Add(InspectorUI.Row(
                InspectorUI.Action("Play Click", () =>
                {
                    _previewing = true;
                    Button?.EditorPreviewClick();
                }),
                InspectorUI.Action("Reset Pose", StopPreview)));

            return card;
        }

        private VisualElement BuildLiveStateCard()
        {
            Foldout card = InspectorUI.Section("Live State");

            if (targets.Length > 1)
            {
                card.Add(InspectorUI.Muted("Select a single button to inspect."));
                return card;
            }

            _liveState = InspectorUI.Code(string.Empty);
            card.Add(_liveState);

            card.Add(InspectorUI.Row(
                InspectorUI.Action("Log To Console", () =>
                {
                    if (Button != null) Debug.Log(Button.DebugDescribe(), Button);
                }),
                InspectorUI.Action("Open Debugger", EnhancedButtonDebuggerWindow.Open)));

            return card;
        }

        #endregion

        #region Live refresh

        private bool HasStyle
        {
            get
            {
                SerializedProperty style = serializedObject.FindProperty("m_Style");
                return style != null && style.objectReferenceValue != null;
            }
        }

        private void RefreshDynamicParts()
        {
            EnhancedButton button = Button;
            if (button == null) return;

            if (_resolvedTargets != null)
            {
                string animation = button.AnimationTarget != null ? button.AnimationTarget.name : "<none>";
                string tint = button.TintTarget != null ? button.TintTarget.name : "<none>";
                _resolvedTargets.text = $"Resolved: {animation} / {tint}";
            }

            if (_liveState != null) _liveState.text = button.DebugDescribe();

            RefreshWarnings(button);
        }

        private void RefreshWarnings(EnhancedButton button)
        {
            if (_warnings == null || targets.Length > 1) return;

            List<(HelpBoxMessageType type, string message)> problems = CollectProblems(button);

            // Compare the actual contents, not just the count: two different problems arriving at
            // once would otherwise leave the old banners on screen. Rebuilding only on a real change
            // is what keeps them from flickering under a 200ms timer.
            string signature = string.Join("|", problems.ConvertAll(problem => problem.message));
            if (signature == _warningSignature) return;

            _warningSignature = signature;
            _warnings.Clear();

            foreach ((HelpBoxMessageType type, string message) in problems)
                _warnings.Add(InspectorUI.Help(message, type));
        }

        private static List<(HelpBoxMessageType, string)> CollectProblems(EnhancedButton button)
        {
            var problems = new List<(HelpBoxMessageType, string)>();

            // Two systems writing Graphic.color every frame; whichever runs last wins, and it looks
            // like the preset's tint is simply broken.
            if (button.transition == Selectable.Transition.ColorTint
                && button.TintTarget != null
                && MotionTints(button.MotionSet))
            {
                problems.Add((HelpBoxMessageType.Warning,
                    "Transition is Color Tint and the preset also tints. Both write the same graphic's " +
                    "colour and will fight. Set Transition to None, or pick a preset that doesn't tint."));
            }

            // Unity scales a RectTransform around its pivot. An off-centre pivot makes every scale
            // preset grow out of a corner instead of swelling evenly — it reads as "the animation is
            // broken", and nothing else in the inspector would ever point at the pivot.
            RectTransform animationTarget = button.AnimationTarget;
            if (animationTarget != null
                && MotionScales(button.MotionSet)
                && !IsCentred(animationTarget.pivot))
            {
                problems.Add((HelpBoxMessageType.Warning,
                    $"'{animationTarget.name}' has pivot {animationTarget.pivot}, not (0.5, 0.5). Unity scales " +
                    "around the pivot, so this button will grow out of a corner rather than from its centre. " +
                    "Centre the pivot, or animate a centred child instead."));
            }

            if (button.AnimationMode == ButtonAnimationMode.AnimationClip)
            {
                if (button.GetComponent<Animation>() == null)
                    problems.Add((HelpBoxMessageType.Error, "Animation Clip mode needs an Animation component on this GameObject."));
                else if (!button.Clips.HasAnyClip)
                    problems.Add((HelpBoxMessageType.Warning, "Animation Clip mode is selected but no clips are assigned."));
            }

            if (Application.isPlaying && !ButtonFeedback.HasAnyHandler)
            {
                problems.Add((HelpBoxMessageType.Info,
                    "Nothing is listening for button feedback, so sfx and haptic ids go nowhere. Set " +
                    "ButtonFeedback.SfxHandler / HapticHandler, or register an IButtonFeedbackHandler."));
            }

            return problems;
        }

        /// <summary>Whether a pivot is close enough to the centre for scaling to look symmetrical.</summary>
        private static bool IsCentred(Vector2 pivot) =>
            Mathf.Abs(pivot.x - 0.5f) < 0.01f && Mathf.Abs(pivot.y - 0.5f) < 0.01f;

        /// <summary>Whether this motion changes scale anywhere — the only case where the pivot matters.</summary>
        private static bool MotionScales(ButtonMotionSet motion) =>
            motion.normal.scale != Vector3.one
            || motion.highlighted.scale != Vector3.one
            || motion.pressed.scale != Vector3.one
            || motion.selected.scale != Vector3.one
            || motion.disabled.scale != Vector3.one
            || (motion.click.enabled && motion.click.scaleAmplitude != Vector3.zero);

        private static bool MotionTints(ButtonMotionSet motion) =>
            motion.normal.tint != Color.white
            || motion.highlighted.tint != Color.white
            || motion.pressed.tint != Color.white
            || motion.selected.tint != Color.white
            || motion.disabled.tint != Color.white;

        #endregion

        #region Actions

        private void StopPreview()
        {
            if (!_previewing) return;
            _previewing = false;

            foreach (Object each in targets)
                if (each is EnhancedButton button)
                    button.EditorPreviewReset();
        }

        private void CreateStyleAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Enhanced Button Style", "EnhancedButtonStyle", "asset",
                "Where should the shared style live?");

            if (string.IsNullOrEmpty(path)) return;

            var style = CreateInstance<EnhancedButtonStyle>();
            AssetDatabase.CreateAsset(style, path);
            AssetDatabase.SaveAssets();

            serializedObject.Update();
            serializedObject.FindProperty("m_Style").objectReferenceValue = style;
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
    }
}
