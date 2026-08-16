using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Samples
{
    /// <summary>
    /// Drives one button through every state by hand and shows what it resolves to.
    /// </summary>
    /// <remarks>
    /// The case worth trying is latching the subject and then disabling it: the readout stays on
    /// Disabled, because a non-interactable button always reads as disabled no matter what else is
    /// true. Re-enable it and the latch is still there.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class DemoStates : MonoBehaviour
    {
        [SerializeField] private EnhancedButton m_Subject;
        [SerializeField] private EnhancedButton m_ToggleInteractable;
        [SerializeField] private EnhancedButton m_ToggleLatch;
        [SerializeField] private Text m_Readout;
        [SerializeField] private Text m_InteractableLabel;
        [SerializeField] private Text m_LatchLabel;

        private void Start()
        {
            if (m_ToggleInteractable != null) m_ToggleInteractable.onClick.AddListener(ToggleInteractable);
            if (m_ToggleLatch != null) m_ToggleLatch.onClick.AddListener(ToggleLatch);
            if (m_Subject != null) m_Subject.StateChanged += (_, __) => Render();

            Render();
        }

        private void ToggleInteractable()
        {
            if (m_Subject == null) return;

            m_Subject.interactable = !m_Subject.interactable;
            Render();
        }

        private void ToggleLatch()
        {
            if (m_Subject == null) return;

            m_Subject.Selected = !m_Subject.Selected;
            Render();
        }

        private void Render()
        {
            if (m_Subject == null) return;

            if (m_Readout != null)
                m_Readout.text = $"Resolved state: <b>{m_Subject.VisualState}</b>\n" +
                                 $"interactable = {m_Subject.interactable}    latched = {m_Subject.Selected}\n\n" +
                                 "Latch it, then disable it: Disabled wins. Re-enable and the latch is still there.";

            if (m_InteractableLabel != null)
                m_InteractableLabel.text = m_Subject.interactable ? "Disable it" : "Enable it";

            if (m_LatchLabel != null)
                m_LatchLabel.text = m_Subject.Selected ? "Unlatch" : "Latch";
        }
    }
}
