using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// Inspector for <see cref="ButtonAnimationPreset"/>, with buttons that overwrite the asset from
    /// a built-in feel. Starting from Jelly and nudging it is a far better authoring experience than
    /// filling in five poses and a punch from zero.
    /// </summary>
    [CustomEditor(typeof(ButtonAnimationPreset))]
    [CanEditMultipleObjects]
    public class ButtonAnimationPresetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            // Unthemed on purpose — see InspectorUI.
            var root = new VisualElement();

            Foldout motion = InspectorUI.Section("Motion");
            motion.Add(InspectorUI.Field(serializedObject, "m_Motion"));
            motion.Add(InspectorUI.Field(serializedObject, "m_Notes", "Notes"));
            root.Add(motion);

            Foldout starters = InspectorUI.Section("Start from a built-in feel");

            var buttons = InspectorUI.Row();
            foreach (ButtonPresetKind kind in ButtonPresetLibrary.BuiltIn)
            {
                ButtonPresetKind captured = kind;
                Button button = InspectorUI.Action(kind.ToString(), () => Overwrite(captured));
                button.tooltip = ButtonPresetLibrary.Describe(kind);
                buttons.Add(button);
            }

            starters.Add(buttons);
            root.Add(starters);

            return root;
        }

        private void Overwrite(ButtonPresetKind kind)
        {
            foreach (Object each in targets)
            {
                if (!(each is ButtonAnimationPreset preset)) continue;

                Undo.RecordObject(preset, $"Apply {kind} preset");
                preset.SetMotion(ButtonPresetLibrary.Get(kind));
                EditorUtility.SetDirty(preset);
            }

            // The card's bound fields read from the serialized data, which Undo.RecordObject
            // bypassed — without this the inspector keeps showing the old numbers.
            serializedObject.Update();
        }
    }
}
