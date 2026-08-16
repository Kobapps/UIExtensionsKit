using System.Collections.Generic;
using System.Linq;
using EditorCoreKit.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// Project-level settings for the kit: which tween backend buttons animate on, and whether the
    /// optional integrations are wired up.
    /// </summary>
    /// <remarks>
    /// The DOTween choice is a scripting define rather than a runtime setting because DOTween is
    /// usually installed as plain assets rather than a package — nothing can detect it
    /// automatically, so the adapter assembly stays uncompiled until this window turns the define
    /// on. UIImageEffectsKit <i>is</i> a package, so its define comes from a versionDefine and is
    /// shown here read-only.
    /// </remarks>
    public sealed class UIExtensionsKitSettingsWindow : EditorWindow
    {
        private const string DOTweenDefine = "UIEXTENSIONSKIT_DOTWEEN";
        private const string ImageEffectsDefine = "UIEXTENSIONSKIT_UIIMAGEEFFECTS";
        private const string BackendPrefKey = "Kobapps.UIExtensionsKit.TweenBackend";

        private KUIWindowShell _shell;

        [MenuItem("Tools/UIExtensionsKit/Settings", false, 1)]
        public static void Open()
        {
            var window = GetWindow<UIExtensionsKitSettingsWindow>();
            window.titleContent = new GUIContent("UIExtensionsKit");
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        /// <summary>
        /// Push the saved backend choice into the runtime façade. Runs on load and after every
        /// domain reload, because <see cref="UITween.BackendId"/> is a plain static that a reload wipes.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ApplySavedBackend()
        {
            string saved = EditorPrefs.GetString(BackendPrefKey, UITween.NativeId);
            if (UITween.IsAvailable(saved)) UITween.BackendId = saved;
        }

        private void CreateGUI()
        {
            _shell = new KUIWindowShell("UIExtensionsKit", "Settings", withSidebar: false)
                .MountInto(rootVisualElement);

            _shell.Header.Add(KUIButton.Secondary("Button Debugger", EnhancedButtonDebuggerWindow.Open));

            Rebuild();
        }

        private void Rebuild()
        {
            _shell.SetContent(() => KUILayout.Page(
                BuildBackendCard(),
                BuildIntegrationsCard(),
                BuildDiagnosticsCard()));

            _shell.Status.Set($"Backend '{UITween.Active.Id}'", KUITone.Neutral);
        }

        private VisualElement BuildBackendCard()
        {
            var card = new KUICard(
                "Animation Backend",
                "Which tween engine buttons animate through.");

            card.Add(KUIText.Body(
                "The built-in backend needs no dependencies and is stepped by the editor, so inspector " +
                "previews animate outside play mode. DOTween is optional."));

            List<string> ids = UITween.AvailableIds.OrderBy(id => id).ToList();
            int selected = Mathf.Max(0, ids.IndexOf(UITween.BackendId));

            card.Add(new KUISegmentedControl(ids, selected, index =>
            {
                if (index < 0 || index >= ids.Count) return;

                UITween.BackendId = ids[index];
                EditorPrefs.SetString(BackendPrefKey, ids[index]);
                _shell.Status.Set($"Backend '{UITween.Active.Id}'", KUITone.Success);
            }));

            card.Add(KUIText.KeyValue("Registered", string.Join(", ", ids)));

            bool dotweenOn = HasDefine(DOTweenDefine);
            card.Add(new KUIToggleSwitch(
                $"Compile the DOTween adapter  ({DOTweenDefine})",
                dotweenOn,
                enabled =>
                {
                    SetDefine(DOTweenDefine, enabled);

                    // The domain reloads after a define change, so rebuild rather than leave stale toggles.
                    EditorApplication.delayCall += Rebuild;
                }));

            if (dotweenOn && !UITween.IsAvailable(UITween.DOTweenId))
            {
                card.Add(KUIBanner.Warning(
                    "The define is set but the DOTween adapter has not registered. If the console shows " +
                    "errors about DG.Tweening, DOTween is not installed — turn this back off or install it."));
            }
            else if (!dotweenOn)
            {
                card.Add(KUIText.Muted(
                    "Enable only after DOTween is installed. The adapter uses DOTween's core API only, " +
                    "so it does not need the DOTween module setup."));
            }

            return card;
        }

        private VisualElement BuildIntegrationsCard()
        {
            bool imageEffects = HasDefine(ImageEffectsDefine);

            var card = new KUICard("Integrations", "Optional packages the kit lights up when present.");

            var row = KUILayout.Row(
                KUIText.FlexText("UIImageEffectsKit — glow and shine for CTA buttons"),
                new KUIBadge(imageEffects ? "Installed" : "Not installed",
                    imageEffects ? KUITone.Success : KUITone.Neutral));

            card.Add(row);

            card.Add(imageEffects
                ? KUIText.Muted(
                    "Swap a button's Image for an SDF Image, then add an \"Enhanced Button Effects " +
                    "(UIImageEffectsKit)\" component next to it. Missing Glow and Shine layers are " +
                    "added to the effect stack for you.")
                : KUIText.Muted(
                    $"Install the package and {ImageEffectsDefine} is defined automatically — there is " +
                    "nothing to switch on here."));

            if (!imageEffects)
            {
                var install = KUIButton.Primary("Install UIImageEffectsKit", InstallImageEffectsKit);
                install.SetEnabled(_installRequest == null);

                card.Add(KUILayout.Row(
                    install,
                    KUIButton.Ghost("Copy install URL", () =>
                    {
                        EditorGUIUtility.systemCopyBuffer = ImageEffectsGitUrl;
                        _shell.Status.Set("Install URL copied to clipboard", KUITone.Success);
                    })));
            }

            card.Add(KUILayout.Separator());

            card.Add(KUILayout.Row(
                KUIText.FlexText("EditorCoreKit — this window and the inspectors are built on it"),
                new KUIBadge("Required", KUITone.Accent)));

            return card;
        }

        private VisualElement BuildDiagnosticsCard()
        {
            var card = new KUICard("Diagnostics", "What the kit is doing right now.");

            var animations = KUIText.KeyValue("Running animations", string.Empty);
            var sink = KUIText.KeyValue("Feedback sink", string.Empty);

            card.Add(animations);
            card.Add(sink);
            card.Add(KUILayout.Row(
                KUIButton.Secondary("Open Button Debugger", EnhancedButtonDebuggerWindow.Open),
                KUIButton.Ghost("Refresh", Rebuild)));

            // Only the two values move, so update their labels rather than rebuilding the card —
            // which would drop the buttons out from under a click every half second.
            void Refresh()
            {
                SetValue(animations, NativeTweenRunner.ActiveCount.ToString());
                SetValue(sink, ButtonFeedback.HasAnyHandler
                    ? $"{ButtonFeedback.HandlerCount} handler(s) + delegates"
                    : "NONE — sfx and haptic ids go nowhere");
            }

            Refresh();
            card.schedule.Execute(Refresh).Every(500);

            return card;
        }

        #region Installing UIImageEffectsKit

        private const string ImageEffectsGitUrl = "https://github.com/Kobapps/UIImageEffectsKit.git";

        private AddRequest _installRequest;

        /// <summary>
        /// Add UIImageEffectsKit to the project through the Package Manager.
        /// </summary>
        /// <remarks>
        /// This edits the project manifest and forces a domain reload, so it asks first — a button
        /// that silently rewrites the manifest of whatever project happens to be open is not a
        /// button anyone wants to click by accident.
        /// </remarks>
        private void InstallImageEffectsKit()
        {
            if (_installRequest != null) return;

            bool confirmed = EditorUtility.DisplayDialog(
                "Install UIImageEffectsKit?",
                "This adds\n\n" + ImageEffectsGitUrl +
                "\n\nto the project's package manifest and reloads scripts. Requires git and network access.",
                "Install",
                "Cancel");

            if (!confirmed) return;

            _installRequest = Client.Add(ImageEffectsGitUrl);
            _shell.Status.Set("Installing UIImageEffectsKit…", KUITone.Accent);

            EditorApplication.update += TrackInstall;
            Rebuild();
        }

        private void TrackInstall()
        {
            if (_installRequest == null)
            {
                EditorApplication.update -= TrackInstall;
                return;
            }

            if (!_installRequest.IsCompleted) return;

            EditorApplication.update -= TrackInstall;

            if (_installRequest.Status == StatusCode.Success)
            {
                _shell.Status.Set(
                    $"Installed {_installRequest.Result.displayName} {_installRequest.Result.version}",
                    KUITone.Success);
            }
            else
            {
                string error = _installRequest.Error != null ? _installRequest.Error.message : "unknown error";
                Debug.LogError($"[UIExtensionsKit] Installing UIImageEffectsKit failed: {error}");
                _shell.Status.Set("Install failed — see the console", KUITone.Error);
            }

            _installRequest = null;
            Rebuild();
        }

        private void OnDisable()
        {
            // The callback outlives the window otherwise, and fires against a destroyed shell.
            EditorApplication.update -= TrackInstall;
        }

        #endregion

        /// <summary>
        /// Rewrite the value half of a <see cref="KUIText.KeyValue"/> row. The row is a key label
        /// followed by a value label, so the value is the second child.
        /// </summary>
        private static void SetValue(VisualElement keyValueRow, string value)
        {
            if (keyValueRow == null || keyValueRow.childCount < 2) return;
            if (keyValueRow[1] is Label label) label.text = value;
        }

        #region Scripting defines

        private static NamedBuildTarget CurrentTarget =>
            NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

        private static bool HasDefine(string define) => GetDefines().Contains(define);

        private static List<string> GetDefines()
        {
            PlayerSettings.GetScriptingDefineSymbols(CurrentTarget, out string[] defines);
            return defines != null ? defines.ToList() : new List<string>();
        }

        private static void SetDefine(string define, bool enabled)
        {
            List<string> defines = GetDefines();

            if (enabled)
            {
                if (defines.Contains(define)) return;
                defines.Add(define);
            }
            else if (!defines.Remove(define))
            {
                return;
            }

            // Only the active build target is touched. Editing every group would silently change
            // platforms the user isn't working on, and a define that appears on its own is worse
            // than one they have to set twice.
            PlayerSettings.SetScriptingDefineSymbols(CurrentTarget, defines.ToArray());
            AssetDatabase.SaveAssets();
        }

        #endregion
    }
}
