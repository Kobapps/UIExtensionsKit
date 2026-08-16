using System.Collections.Generic;
using UnityEngine;

namespace Kobapps.UIExtensionsKit.Samples
{
    /// <summary>
    /// Switches between the demo's sections from a row of latched nav buttons.
    /// </summary>
    /// <remarks>
    /// The nav bar is the clearest use of the latched <see cref="EnhancedButton.Selected"/> state:
    /// the current section's button stays lit while the pointer is elsewhere, which is exactly what
    /// EventSystem focus cannot express.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class DemoRouter : MonoBehaviour
    {
        [SerializeField] private List<EnhancedButton> m_NavButtons = new List<EnhancedButton>();
        [SerializeField] private List<GameObject> m_Sections = new List<GameObject>();

        private int _current = -1;

        private void Start()
        {
            for (int i = 0; i < m_NavButtons.Count; i++)
            {
                if (m_NavButtons[i] == null) continue;

                int index = i;
                m_NavButtons[i].onClick.AddListener(() => Show(index));
            }

            Show(0);
        }

        /// <summary>Show one section and latch its nav button.</summary>
        public void Show(int index)
        {
            if (index == _current) return;
            _current = index;

            for (int i = 0; i < m_Sections.Count; i++)
                if (m_Sections[i] != null)
                    m_Sections[i].SetActive(i == index);

            for (int i = 0; i < m_NavButtons.Count; i++)
            {
                if (m_NavButtons[i] == null) continue;

                // Only the incoming tab announces itself; the rest go quiet, or switching sections
                // would fire a selection sound for every button in the bar.
                bool isCurrent = i == index;
                m_NavButtons[i].SetSelected(isCurrent, notify: isCurrent);
            }
        }
    }
}
