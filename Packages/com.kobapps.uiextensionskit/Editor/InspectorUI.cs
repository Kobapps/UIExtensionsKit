using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// Plain UI Toolkit widgets for the component inspectors.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The editor <b>windows</b> are built on EditorCoreKit, which is right for them — a tool window
    /// is its own surface and benefits from a consistent shell. An inspector is not: it sits in a
    /// column of Unity's own inspectors, and a themed card stack in the middle of that reads as a
    /// foreign body no matter how good the theme is.
    /// </para>
    /// <para>
    /// So the inspectors use stock <see cref="Foldout"/>, <see cref="HelpBox"/> and
    /// <see cref="PropertyField"/> and inherit whatever skin the user is running. This also keeps the
    /// shared motion editor free of EditorCoreKit, so it can be embedded in either surface.
    /// </para>
    /// </remarks>
    internal static class InspectorUI
    {
        /// <summary>A collapsible group with a bold header, matching Unity's own inspector sections.</summary>
        public static Foldout Section(string title, bool expanded = true, string persistenceKey = null)
        {
            var foldout = new Foldout { text = title, value = expanded };
            foldout.style.marginTop = 4;

            if (!string.IsNullOrEmpty(persistenceKey))
            {
                foldout.value = EditorPrefs.GetBool(persistenceKey, expanded);
                foldout.RegisterValueChangedCallback(e =>
                {
                    // Only the foldout's own toggle should persist; children bubble change events too.
                    if (e.target == foldout) EditorPrefs.SetBool(persistenceKey, e.newValue);
                });
            }

            return foldout;
        }

        /// <summary>A bold section title for groups that should not collapse.</summary>
        public static Label Header(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 6;
            label.style.marginBottom = 2;
            return label;
        }

        /// <summary>Dimmed helper copy.</summary>
        public static Label Muted(string text)
        {
            var label = new Label(text) { style = { whiteSpace = WhiteSpace.Normal } };
            label.style.opacity = 0.7f;
            label.style.marginBottom = 2;
            return label;
        }

        /// <summary>Unity's own help box, so warnings look like every other warning in the editor.</summary>
        public static HelpBox Help(string message, HelpBoxMessageType type = HelpBoxMessageType.Info) =>
            new HelpBox(message, type);

        /// <summary>A horizontal row that wraps, for button strips.</summary>
        public static VisualElement Row(params VisualElement[] children)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 2 },
            };

            foreach (VisualElement child in children)
                if (child != null)
                    row.Add(child);

            return row;
        }

        /// <summary>A button with a little breathing room, since stock buttons sit flush.</summary>
        public static Button Action(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.marginRight = 4;
            return button;
        }

        /// <summary>
        /// A bound field for one property path, or a visible complaint when the path is wrong.
        /// </summary>
        public static VisualElement Field(SerializedObject serialized, string path, string label = null)
        {
            SerializedProperty property = serialized.FindProperty(path);

            // A mistyped path otherwise renders as nothing at all, which is far harder to spot.
            if (property == null) return Muted($"Property '{path}' was not found.");

            var field = label != null ? new PropertyField(property, label) : new PropertyField(property);
            field.Bind(serialized);
            return field;
        }

        /// <summary>Monospaced, selectable block for diagnostics people will want to copy.</summary>
        public static Label Code(string text)
        {
            var label = new Label(text)
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    marginTop = 4,
                    marginBottom = 4,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 4, paddingBottom = 4,
                    backgroundColor = new Color(0f, 0f, 0f, 0.15f),
                },
            };

            label.selection.isSelectable = true;
            return label;
        }
    }
}
