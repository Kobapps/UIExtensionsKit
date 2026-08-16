using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// Inspector for <see cref="EnhancedButtonStyle"/>. Beyond the fields, it answers the question
    /// that matters before editing a shared asset: how many buttons in the open scenes am I about to
    /// change?
    /// </summary>
    [CustomEditor(typeof(EnhancedButtonStyle))]
    [CanEditMultipleObjects]
    public class EnhancedButtonStyleEditor : UnityEditor.Editor
    {
        private Label _usage;

        public override VisualElement CreateInspectorGUI()
        {
            // Unthemed on purpose — see InspectorUI.
            var root = new VisualElement();

            Foldout motion = InspectorUI.Section("Motion");
            motion.Add(InspectorUI.Field(serializedObject, "m_AnimationMode", "Animation Mode"));
            motion.Add(InspectorUI.Field(serializedObject, "m_Preset", "Preset"));
            motion.Add(InspectorUI.Field(serializedObject, "m_CustomPreset", "Preset Asset"));
            motion.Add(InspectorUI.Field(serializedObject, "m_UnscaledTime", "Unscaled Time"));

            var description = InspectorUI.Muted(string.Empty);
            motion.Add(description);
            root.Add(motion);

            Foldout feedback = InspectorUI.Section("Feedback");
            feedback.Add(InspectorUI.Field(serializedObject, "m_Feedback", "Sfx & Haptics"));
            root.Add(feedback);

            Foldout notes = InspectorUI.Section("Notes");
            notes.Add(InspectorUI.Field(serializedObject, "m_Notes", string.Empty));
            root.Add(notes);

            Foldout usageCard = InspectorUI.Section("Usage");
            _usage = InspectorUI.Muted(string.Empty);
            usageCard.Add(_usage);
            usageCard.Add(InspectorUI.Action("Select Them", SelectUsers));
            root.Add(usageCard);

            void Refresh()
            {
                if (target is EnhancedButtonStyle style) description.text = ButtonPresetLibrary.Describe(style.Preset);
                _usage.text = DescribeUsage();
            }

            Refresh();
            root.schedule.Execute(Refresh).Every(500);

            return root;
        }

        private EnhancedButton[] FindUsers()
        {
            if (!(target is EnhancedButtonStyle style)) return new EnhancedButton[0];

            return Resources.FindObjectsOfTypeAll<EnhancedButton>()
                .Where(button => button != null
                                 && button.Style == style
                                 && button.gameObject.scene.IsValid()
                                 && !EditorUtility.IsPersistent(button))
                .ToArray();
        }

        private string DescribeUsage()
        {
            EnhancedButton[] users = FindUsers();

            if (users.Length == 0)
                return "No buttons in the open scenes use this style yet. Assign it in a button's Shared Style field.";

            string names = string.Join(", ", users.Take(8).Select(button => button.name));
            return users.Length <= 8
                ? $"{users.Length} button(s): {names}"
                : $"{users.Length} buttons: {names}, …";
        }

        private void SelectUsers()
        {
            EnhancedButton[] users = FindUsers();
            if (users.Length == 0) return;

            Selection.objects = users.Select(button => (Object)button.gameObject).ToArray();
        }
    }
}
