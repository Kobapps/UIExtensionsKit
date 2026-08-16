using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// The motion and clip editing UI, shared by the button inspector and the Preset Library.
    /// </summary>
    /// <remarks>
    /// Both surfaces edit the same three things — which preset, the preset's motion, and the clip
    /// set — over different owners: a button in one case, a style asset in the other. Building it
    /// once means the "generate a preset asset" affordance, the inline editor and the clip
    /// diagnostics cannot drift apart, which is exactly what happened when the inspector grew them
    /// and the library kept plain property fields.
    /// </remarks>
    internal static class ButtonMotionSectionUI
    {
        /// <summary>
        /// Preset picker, plus whatever that choice implies: an editable-copy button for a built-in,
        /// a generate button when Custom has nothing assigned, or the inline motion editor when it has.
        /// </summary>
        /// <param name="host">Element the controls are added to.</param>
        /// <param name="owner">The button or style being edited.</param>
        /// <param name="presetPath">Serialized path of the <see cref="ButtonPresetKind"/> field.</param>
        /// <param name="assetPath">Serialized path of the <see cref="ButtonAnimationPreset"/> field.</param>
        /// <param name="onChanged">Raised after any edit, so a preview can refresh.</param>
        /// <param name="rebuild">Raised when the control set itself must be rebuilt.</param>
        /// <param name="onStateFocused">Raised when the user focuses a state in the motion editor.</param>
        public static void BuildTweenSection(
            VisualElement host,
            SerializedObject owner,
            string presetPath,
            string assetPath,
            Action onChanged,
            Action rebuild,
            Action<EnhancedButtonVisualState> onStateFocused = null)
        {
            host.Add(InspectorUI.Field(owner, presetPath, "Preset"));

            SerializedProperty presetProperty = owner.FindProperty(presetPath);
            var preset = (ButtonPresetKind)presetProperty.enumValueIndex;

            if (preset != ButtonPresetKind.Custom)
            {
                host.Add(InspectorUI.Muted(ButtonPresetLibrary.Describe(preset)));
                host.Add(InspectorUI.Muted(
                    "Built-in feels are compiled in. Make an editable copy to tune this one — it " +
                    "switches to Custom and points at the new asset."));

                host.Add(InspectorUI.Action($"Create editable copy of {preset}…", () =>
                    Generate(owner, presetPath, assetPath, ButtonPresetLibrary.Get(preset), preset.ToString(), onChanged, rebuild)));
                return;
            }

            host.Add(InspectorUI.Field(owner, assetPath, "Preset Asset"));

            var asset = owner.FindProperty(assetPath).objectReferenceValue as ButtonAnimationPreset;

            if (asset == null)
            {
                // Never a dead end: the warning always comes with the button that resolves it.
                host.Add(InspectorUI.Help(
                    $"Preset is Custom but nothing is assigned, so this falls back to " +
                    $"{ButtonPresetLibrary.Default}. Generate one to start tuning.",
                    HelpBoxMessageType.Warning));

                host.Add(InspectorUI.Action("Generate Preset Asset…", () =>
                    Generate(owner, presetPath, assetPath,
                        ButtonPresetLibrary.Get(ButtonPresetLibrary.Default), "ButtonAnimationPreset",
                        onChanged, rebuild)));
                return;
            }

            Foldout section = InspectorUI.Section($"Edit '{asset.name}'", true, "Kobapps.UIExtensionsKit.InlineMotion");

            var editor = new ButtonMotionEditor(new SerializedObject(asset), "m_Motion", onChanged);
            if (onStateFocused != null) editor.StateFocused += onStateFocused;

            section.Add(editor);
            host.Add(section);

            host.Add(InspectorUI.Row(
                InspectorUI.Action("Ping asset", () => EditorGUIUtility.PingObject(asset)),
                InspectorUI.Action("Reseed…", () => ReseedMenu(asset, onChanged, rebuild))));
        }

        /// <summary>
        /// Create a preset asset seeded from <paramref name="seed"/> and point the owner at it.
        /// </summary>
        public static void Generate(
            SerializedObject owner,
            string presetPath,
            string assetPath,
            ButtonMotionSet seed,
            string suggestedName,
            Action onChanged,
            Action rebuild)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "New Button Animation Preset", $"{suggestedName}Preset", "asset",
                "Where should the preset live?");

            if (string.IsNullOrEmpty(path)) return;

            var preset = ScriptableObject.CreateInstance<ButtonAnimationPreset>();
            preset.SetMotion(seed);
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            // Assign and switch to Custom, or the new asset would sit there doing nothing.
            owner.Update();
            owner.FindProperty(presetPath).enumValueIndex = (int)ButtonPresetKind.Custom;
            owner.FindProperty(assetPath).objectReferenceValue = preset;
            owner.ApplyModifiedProperties();

            onChanged?.Invoke();
            rebuild?.Invoke();
        }

        private static void ReseedMenu(ButtonAnimationPreset asset, Action onChanged, Action rebuild)
        {
            var menu = new GenericMenu();

            foreach (ButtonPresetKind kind in ButtonPresetLibrary.BuiltIn)
            {
                ButtonPresetKind captured = kind;
                menu.AddItem(new GUIContent(kind.ToString()), false, () =>
                {
                    Undo.RecordObject(asset, $"Reseed from {captured}");
                    asset.SetMotion(ButtonPresetLibrary.Get(captured));
                    EditorUtility.SetDirty(asset);

                    onChanged?.Invoke();
                    rebuild?.Invoke();
                });
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// A flat, state-labelled clip list plus the diagnostics that make clip mode debuggable.
        /// </summary>
        /// <param name="context">
        /// The GameObject the clips will play on, when there is one. Null for a style asset, which
        /// has no Animation component of its own to check.
        /// </param>
        public static void BuildClipSection(
            VisualElement host,
            SerializedObject owner,
            string clipsPath,
            GameObject context,
            Action rebuild)
        {
            host.Add(InspectorUI.Muted(
                "One legacy AnimationClip per state, played through an Animation component. State " +
                "clips run on layer 0 and the click clip on layer 1, so a click blends over the " +
                "current state instead of cancelling it."));

            host.Add(InspectorUI.Field(owner, $"{clipsPath}.normal", "Normal"));
            host.Add(InspectorUI.Field(owner, $"{clipsPath}.highlighted", "Highlighted"));
            host.Add(InspectorUI.Field(owner, $"{clipsPath}.pressed", "Pressed"));
            host.Add(InspectorUI.Field(owner, $"{clipsPath}.selected", "Selected"));
            host.Add(InspectorUI.Field(owner, $"{clipsPath}.disabled", "Disabled"));
            host.Add(InspectorUI.Field(owner, $"{clipsPath}.click", "Click (one-shot)"));
            host.Add(InspectorUI.Field(owner, $"{clipsPath}.crossFade", "Cross Fade (s)"));

            // A clip that is not marked Legacy is silently unplayable, which reads as a broken kit.
            var nonLegacy = new List<string>();
            var assigned = new List<string>();

            foreach (string field in new[] { "normal", "highlighted", "pressed", "selected", "disabled", "click" })
            {
                SerializedProperty property = owner.FindProperty($"{clipsPath}.{field}");
                if (property?.objectReferenceValue is not AnimationClip clip) continue;

                assigned.Add(clip.name);
                if (!clip.legacy) nonLegacy.Add(clip.name);
            }

            if (nonLegacy.Count > 0)
            {
                host.Add(InspectorUI.Help(
                    $"Clips are not marked Legacy: {string.Join(", ", nonLegacy)}. An Animation " +
                    "component cannot play them. Set Legacy on each clip via the Debug inspector, " +
                    "or switch to Tween mode.",
                    HelpBoxMessageType.Warning));
            }
            else if (assigned.Count == 0)
            {
                host.Add(InspectorUI.Help(
                    "No clips assigned — nothing will animate until at least one clip is set.",
                    HelpBoxMessageType.Warning));
            }

            if (context == null) return;

            if (context.GetComponent<Animation>() == null)
            {
                host.Add(InspectorUI.Help(
                    "No Animation component. Clip mode plays legacy clips through one, and this " +
                    "GameObject has none.",
                    HelpBoxMessageType.Error));

                host.Add(InspectorUI.Action("Add Animation component", () =>
                {
                    Undo.AddComponent<Animation>(context);
                    rebuild?.Invoke();
                }));
            }
        }
    }
}
