using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Samples
{
    /// <summary>
    /// A level grid where progress gates which levels can be played.
    /// </summary>
    /// <remarks>
    /// This is the scenario the <see cref="ButtonFeedbackEvent.Rejected"/> cue exists for. A locked
    /// level is <c>interactable = false</c>, so a plain <c>Button</c> would answer a tap with
    /// nothing at all and leave the player unsure the game even registered it. Here the tap still
    /// lands, plays a "nope" sound and a warning haptic, and says why it was refused.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class DemoLevels : MonoBehaviour
    {
        [SerializeField] private List<EnhancedButton> m_Levels = new List<EnhancedButton>();
        [SerializeField] private Text m_Readout;
        [SerializeField] private EnhancedButton m_UnlockNext;
        [SerializeField] private EnhancedButton m_LockAll;

        [SerializeField, Tooltip("How many levels start unlocked.")]
        private int m_Unlocked = 4;

        private void Start()
        {
            for (int i = 0; i < m_Levels.Count; i++)
            {
                if (m_Levels[i] == null) continue;

                int index = i;
                m_Levels[i].onClick.AddListener(() => SelectLevel(index));

                // A locked button still receives the click, and Rejected is where the game gets to
                // say why it refused — onClick never fires for a non-interactable button.
                m_Levels[i].Rejected += _ => Report($"Level {index + 1} is locked. Unlock it to play.");
            }

            if (m_UnlockNext != null) m_UnlockNext.onClick.AddListener(UnlockNext);
            if (m_LockAll != null) m_LockAll.onClick.AddListener(LockAll);

            ApplyLocks();
            Report("Tap a locked level — it answers instead of ignoring you.");
        }

        private void SelectLevel(int index)
        {
            for (int i = 0; i < m_Levels.Count; i++)
            {
                if (m_Levels[i] == null) continue;

                bool isSelected = i == index;
                m_Levels[i].SetSelected(isSelected, notify: isSelected);
            }

            Report($"Level {index + 1} selected.");
        }

        private void UnlockNext()
        {
            if (m_Unlocked >= m_Levels.Count)
            {
                Report("Everything is already unlocked.");
                return;
            }

            m_Unlocked++;
            ApplyLocks();
            Report($"Unlocked level {m_Unlocked} — it animated out of its Disabled pose.");
        }

        private void LockAll()
        {
            m_Unlocked = 1;
            ApplyLocks();
            Report("Locked everything past level 1.");
        }

        private void ApplyLocks()
        {
            for (int i = 0; i < m_Levels.Count; i++)
            {
                if (m_Levels[i] == null) continue;

                bool unlocked = i < m_Unlocked;
                m_Levels[i].interactable = unlocked;

                // A level that gets locked again must not stay latched, or it would read as both
                // disabled and chosen.
                if (!unlocked && m_Levels[i].Selected) m_Levels[i].SetSelected(false, notify: false);
            }
        }

        private void Report(string message)
        {
            if (m_Readout != null) m_Readout.text = message;
        }
    }
}
