using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// A hand-authored button feel, saved as an asset so it can be shared and tuned in one place.
    /// Only needed when none of <see cref="ButtonPresetLibrary"/>'s built-in feels fit — point a
    /// button (or a whole <see cref="EnhancedButtonStyle"/>) at one and set the preset to
    /// <see cref="ButtonPresetKind.Custom"/>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ButtonAnimationPreset",
        menuName = "UIExtensionsKit/Button Animation Preset",
        order = 101)]
    public class ButtonAnimationPreset : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Pose per visual state, plus the click punch. All values are relative to the button's authored pose.")]
        private ButtonMotionSet m_Motion = ButtonPresetLibrary.Get(ButtonPresetKind.Jelly);

        [SerializeField, TextArea(2, 4)]
        [Tooltip("Free notes — what this feel is for, which buttons should use it.")]
        private string m_Notes = string.Empty;

        /// <summary>The motion this preset describes, with impossible values repaired.</summary>
        public ButtonMotionSet Motion => m_Motion.Sanitized();

        /// <summary>Author notes shown in the inspector. Not used at runtime.</summary>
        public string Notes => m_Notes;

        /// <summary>Overwrite this preset's motion. Editor tooling uses this; runtime code should not.</summary>
        public void SetMotion(ButtonMotionSet motion) => m_Motion = motion;

        // A serialized struct defaults to all-zero, which would mean "scale to nothing". Seeding a
        // real preset on creation means a fresh asset is usable rather than invisible.
        private void Reset()
        {
            m_Motion = ButtonPresetLibrary.Get(ButtonPresetKind.Jelly);
            m_Notes = string.Empty;
        }

        private void OnValidate() => m_Motion = m_Motion.Sanitized();
    }
}
