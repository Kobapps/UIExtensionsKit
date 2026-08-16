using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Samples
{
    /// <summary>
    /// A pause overlay that really does stop the game clock, plus the audio and haptic mutes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The point of this section is <c>Time.timeScale = 0</c>. Buttons default to unscaled time, so a
    /// paused game still has a responsive menu — the failure this avoids is the classic one where
    /// pausing freezes every button animation and the UI feels broken exactly when the player is
    /// looking at it. Turn Unscaled Time off on a button and pause to see the difference.
    /// </para>
    /// <para>
    /// The mute toggles are latched buttons wired straight to <see cref="ButtonFeedback"/>'s global
    /// switches — the settings players actually look for, in about six lines.
    /// </para>
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class DemoPause : MonoBehaviour
    {
        [SerializeField] private EnhancedButton m_PauseToggle;
        [SerializeField] private Text m_PauseLabel;
        [SerializeField] private EnhancedButton m_SfxToggle;
        [SerializeField] private Text m_SfxLabel;
        [SerializeField] private EnhancedButton m_HapticsToggle;
        [SerializeField] private Text m_HapticsLabel;
        [SerializeField] private Text m_Readout;
        [SerializeField] private RectTransform m_Spinner;

        [SerializeField, Tooltip("Degrees per second. Uses SCALED time, so it stops when paused.")]
        private float m_SpinSpeed = 90f;

        private void Start()
        {
            if (m_PauseToggle != null) m_PauseToggle.onClick.AddListener(TogglePause);
            if (m_SfxToggle != null) m_SfxToggle.onClick.AddListener(ToggleSfx);
            if (m_HapticsToggle != null) m_HapticsToggle.onClick.AddListener(ToggleHaptics);

            Refresh();
        }

        private void OnDisable()
        {
            // Leaving this section with the game frozen would break every other section.
            Time.timeScale = 1f;

            if (m_PauseToggle != null) m_PauseToggle.SetSelected(false, notify: false);
            Refresh();
        }

        private void Update()
        {
            // Deliberately scaled: this is the "game", and it must visibly stop while the buttons
            // above it keep animating.
            if (m_Spinner != null) m_Spinner.Rotate(0f, 0f, -m_SpinSpeed * Time.deltaTime);
        }

        private void TogglePause()
        {
            bool pausing = Time.timeScale > 0f;
            Time.timeScale = pausing ? 0f : 1f;

            if (m_PauseToggle != null) m_PauseToggle.SetSelected(pausing, notify: false);
            Refresh();
        }

        private void ToggleSfx()
        {
            ButtonFeedback.SfxMuted = !ButtonFeedback.SfxMuted;
            if (m_SfxToggle != null) m_SfxToggle.SetSelected(ButtonFeedback.SfxMuted, notify: false);
            Refresh();
        }

        private void ToggleHaptics()
        {
            ButtonFeedback.HapticsMuted = !ButtonFeedback.HapticsMuted;
            if (m_HapticsToggle != null) m_HapticsToggle.SetSelected(ButtonFeedback.HapticsMuted, notify: false);
            Refresh();
        }

        private void Refresh()
        {
            bool paused = Mathf.Approximately(Time.timeScale, 0f);

            if (m_PauseLabel != null) m_PauseLabel.text = paused ? "Resume" : "Pause";
            if (m_SfxLabel != null) m_SfxLabel.text = ButtonFeedback.SfxMuted ? "Sfx: off" : "Sfx: on";
            if (m_HapticsLabel != null) m_HapticsLabel.text = ButtonFeedback.HapticsMuted ? "Haptics: off" : "Haptics: on";

            if (m_Readout == null) return;

            m_Readout.text = paused
                ? "timeScale = 0. The square stopped — but these buttons still bounce, because they run on unscaled time."
                : "timeScale = 1. Hit Pause, then hover these buttons: the square freezes, the buttons don't.";
        }
    }
}
