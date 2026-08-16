using System;
using System.Collections.Generic;
using System.Linq;
using EditorCoreKit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// Every <see cref="EnhancedButton"/> in the open scenes, with its live state, in one list.
    /// </summary>
    /// <remarks>
    /// Selecting buttons one at a time answers "what is this button doing". This window answers the
    /// questions that only appear across a whole screen — which buttons share a style, which are
    /// disabled, which are latched, and whether the one that isn't reacting is even an
    /// EnhancedButton. The "Only problems" filter turns it into a linter for a scene's UI.
    /// </remarks>
    public sealed class EnhancedButtonDebuggerWindow : EditorWindow
    {
        private KUIWindowShell _shell;
        private VisualElement _list;

        private string _filter = string.Empty;
        private bool _onlyProblems;

        private readonly List<EnhancedButton> _buttons = new List<EnhancedButton>();
        private string _signature;

        [MenuItem("Tools/UIExtensionsKit/Enhanced Button Debugger", false, 2)]
        public static void Open()
        {
            var window = GetWindow<EnhancedButtonDebuggerWindow>();
            window.titleContent = new GUIContent("Button Debugger");
            window.minSize = new Vector2(620f, 360f);
            window.Show();
        }

        private void CreateGUI()
        {
            _shell = new KUIWindowShell("Enhanced Buttons", "Scene debugger", withSidebar: false)
                .MountInto(rootVisualElement);

            _shell.Header.Add(new KUISearchField("Filter by name…", value =>
            {
                _filter = value;
                Rebuild(force: true);
            }));

            _shell.Header.Add(new KUIToggleSwitch("Only problems", false, value =>
            {
                _onlyProblems = value;
                Rebuild(force: true);
            }));

            _shell.Header.Add(KUIButton.Secondary("Settings", UIExtensionsKitSettingsWindow.Open));

            _list = new VisualElement();
            _shell.SetContent(KUILayout.Page(_list));

            Rebuild(force: true);

            // Keeps the list honest as buttons are created, destroyed, latched or disabled.
            rootVisualElement.schedule.Execute(() => Rebuild(force: false)).Every(400);
        }

        private void Rebuild(bool force)
        {
            Scan();

            List<EnhancedButton> visible = _buttons.Where(Matches).ToList();

            // Only touch the DOM when something actually changed, or a 400ms timer would fight
            // every click and scroll in the list.
            string signature = BuildSignature(visible);
            if (!force && signature == _signature) return;

            _signature = signature;
            _list.Clear();

            if (visible.Count == 0)
            {
                _list.Add(new KUIEmptyState(
                    _buttons.Count == 0 ? "No Enhanced Buttons here" : "Nothing matches the filter",
                    _buttons.Count == 0
                        ? "Create one with GameObject ▸ UI ▸ Enhanced Button."
                        : "Clear the filter, or turn off \"Only problems\"."));
            }
            else
            {
                foreach (EnhancedButton button in visible)
                    _list.Add(BuildRow(button));
            }

            _shell.Status.Set(
                $"{visible.Count} of {_buttons.Count} shown · backend '{UITween.Active.Id}' · " +
                $"{NativeTweenRunner.ActiveCount} running",
                ButtonFeedback.HasAnyHandler ? "feedback connected" : "no feedback sink",
                ButtonFeedback.HasAnyHandler ? KUITone.Success : KUITone.Warning);
        }

        private void Scan()
        {
            _buttons.Clear();

            // Includes inactive objects: a button that never appears is exactly the kind of thing
            // someone opens this window to find.
            _buttons.AddRange(Resources.FindObjectsOfTypeAll<EnhancedButton>()
                .Where(button => button != null
                                 && button.gameObject.scene.IsValid()
                                 && !EditorUtility.IsPersistent(button)));
        }

        private bool Matches(EnhancedButton button)
        {
            if (_onlyProblems && string.IsNullOrEmpty(Problem(button))) return false;
            if (string.IsNullOrEmpty(_filter)) return true;

            return button.name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildSignature(List<EnhancedButton> visible)
        {
            var builder = new System.Text.StringBuilder(visible.Count * 32);

            foreach (EnhancedButton button in visible)
            {
                // GetHashCode rather than GetInstanceID: it identifies the object just as well for
                // change detection, and it is not deprecated on newer editors.
                builder.Append(button.GetHashCode())
                    .Append(button.VisualState)
                    .Append(button.Selected)
                    .Append(button.gameObject.activeInHierarchy)
                    .Append(Problem(button))
                    .Append(';');
            }

            return builder.ToString();
        }

        private VisualElement BuildRow(EnhancedButton button)
        {
            string style = button.Style != null ? button.Style.name : "local settings";
            string sublabel = $"{button.AnimationMode} · {button.Preset} · {style}";

            if (!button.gameObject.activeInHierarchy) sublabel += " · inactive";

            var row = new KUIListRow(button.name, () =>
                {
                    Selection.activeGameObject = button.gameObject;
                    EditorGUIUtility.PingObject(button.gameObject);
                })
                .WithSublabel(sublabel)
                .WithDot(ToneOf(button), button.DebugDescribe())
                .WithBadge(button.VisualState.ToString(), ToneOf(button));

            if (button.Selected) row.WithBadge("LATCHED", KUITone.Accent);

            string problem = Problem(button);
            if (!string.IsNullOrEmpty(problem)) row.WithBadge(problem, KUITone.Warning);

            if (Application.isPlaying)
            {
                row.WithAction(KUIButton.Ghost("Click", button.PlayClickFeedback));
                row.WithAction(KUIButton.Ghost(
                    button.Selected ? "Unlatch" : "Latch",
                    () => button.SetSelected(!button.Selected)));
            }

            return row;
        }

        private static KUITone ToneOf(EnhancedButton button)
        {
            switch (button.VisualState)
            {
                case EnhancedButtonVisualState.Disabled: return KUITone.Neutral;
                case EnhancedButtonVisualState.Pressed: return KUITone.Accent;
                case EnhancedButtonVisualState.Selected: return KUITone.Success;
                case EnhancedButtonVisualState.Highlighted: return KUITone.Accent;
                default: return KUITone.Neutral;
            }
        }

        /// <summary>The one thing most worth saying about a misconfigured button, or empty if it looks fine.</summary>
        private static string Problem(EnhancedButton button)
        {
            if (button.AnimationMode == ButtonAnimationMode.AnimationClip)
            {
                if (button.GetComponent<Animation>() == null) return "No Animation component";
                if (!button.Clips.HasAnyClip) return "No clips assigned";
            }

            if (button.AnimationTarget == null) return "Nothing to animate";

            // Scaling happens around the pivot, so an off-centre one grows the button out of a corner.
            Vector2 pivot = button.AnimationTarget.pivot;
            if (Mathf.Abs(pivot.x - 0.5f) > 0.01f || Mathf.Abs(pivot.y - 0.5f) > 0.01f)
                return "Off-centre pivot — scales from a corner";

            if (button.transition == UnityEngine.UI.Selectable.Transition.ColorTint && button.TintTarget != null)
                return "ColorTint may fight the preset";

            return string.Empty;
        }
    }
}
