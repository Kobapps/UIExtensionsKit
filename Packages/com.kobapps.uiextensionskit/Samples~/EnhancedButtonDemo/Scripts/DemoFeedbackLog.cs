using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Samples
{
    /// <summary>
    /// Renders every sfx and haptic request the demo's buttons make onto an on-screen label.
    /// </summary>
    /// <remarks>
    /// This is the whole integration story in one file. A real game replaces the body of
    /// <see cref="Handle"/> with a call into its audio stack and its haptics plugin, and nothing
    /// about the buttons changes. Watching the ids scroll past is also the quickest way to confirm a
    /// locked button really is asking for the rejection cue rather than staying silent.
    /// </remarks>
    [AddComponentMenu("")]
    public sealed class DemoFeedbackLog : MonoBehaviour, IButtonFeedbackHandler
    {
        [SerializeField] private Text m_Output;
        [SerializeField] private int m_MaxLines = 16;

        private readonly Queue<string> _lines = new Queue<string>();
        private readonly StringBuilder _builder = new StringBuilder(1024);
        private int _count;

        private void OnEnable()
        {
            // A game would do exactly this once, at startup, and never think about it again.
            ButtonFeedback.RegisterHandler(this);
            ButtonFeedback.SfxHandler = id => { /* AudioKit.Play(id) */ };
            ButtonFeedback.HapticHandler = type => { /* Haptics.Play(type) */ };

            Render();
        }

        private void OnDisable() => ButtonFeedback.UnregisterHandler(this);

        /// <inheritdoc/>
        public void Handle(in ButtonFeedbackRequest request)
        {
            string source = request.Source != null ? request.Source.name : "(none)";

            // Handlers run before the mutes are applied, so this shows what was asked for *and*
            // whether it actually reached the game — the distinction someone debugging a silent
            // button needs.
            string muted = string.Empty;
            if (ButtonFeedback.SfxMuted && !string.IsNullOrEmpty(request.SfxId)) muted = "  [sfx muted]";
            if (ButtonFeedback.HapticsMuted && request.Haptic != HapticType.None) muted += "  [haptics muted]";

            Append($"{++_count:000}  {source} → {request}{muted}");
        }

        /// <summary>Clear the log. Wired to the demo's Clear button.</summary>
        public void Clear()
        {
            _lines.Clear();
            _count = 0;
            Render();
        }

        private void Append(string line)
        {
            _lines.Enqueue(line);
            while (_lines.Count > Mathf.Max(1, m_MaxLines)) _lines.Dequeue();
            Render();
        }

        private void Render()
        {
            if (m_Output == null) return;

            _builder.Clear();
            _builder.AppendLine("Feedback requests — what a game routes to audio + haptics:");

            if (_lines.Count == 0) _builder.AppendLine("  …hover or click anything…");
            else
                foreach (string line in _lines)
                    _builder.Append("  ").AppendLine(line);

            m_Output.text = _builder.ToString();
        }
    }
}
