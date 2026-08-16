using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// Pumps <see cref="NativeTweenRunner"/> during play mode. Created on demand the first time the
    /// built-in backend starts an animation, and hidden from the hierarchy — nothing about it is
    /// worth authoring, and it must not be saved into a scene.
    /// </summary>
    /// <remarks>
    /// Outside play mode there is no driver; the editor steps the runner itself via
    /// <see cref="UITween.ManualTick"/> so inspector previews still animate.
    /// </remarks>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class UITweenDriver : MonoBehaviour
    {
        private static UITweenDriver s_Instance;

        internal static void Ensure()
        {
            if (s_Instance != null || !Application.isPlaying) return;

            var host = new GameObject("[UIExtensionsKit Tween Driver]")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            DontDestroyOnLoad(host);
            s_Instance = host.AddComponent<UITweenDriver>();
        }

        private void Update() => NativeTweenRunner.Tick(Time.deltaTime, Time.unscaledDeltaTime);

        private void OnDestroy()
        {
            if (s_Instance == this) s_Instance = null;
        }
    }
}
