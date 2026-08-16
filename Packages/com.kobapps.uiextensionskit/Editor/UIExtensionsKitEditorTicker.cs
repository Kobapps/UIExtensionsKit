using UnityEditor;
using UnityEditorInternal;

namespace Kobapps.UIExtensionsKit.Editor
{
    /// <summary>
    /// Steps the built-in tween backend outside play mode, so inspector previews actually animate
    /// instead of snapping. In play mode the runtime driver owns the clock and this stays out of
    /// the way — ticking from both would double every animation's speed.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIExtensionsKitEditorTicker
    {
        private static double s_LastTime;

        static UIExtensionsKitEditorTicker()
        {
            s_LastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(now - s_LastTime);
            s_LastTime = now;

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (NativeTweenRunner.ActiveCount == 0) return;

            // Editor update ticks are irregular — a domain reload or a modal dialog can leave a gap
            // of seconds, which would teleport every running preview straight to its end.
            if (deltaTime <= 0f || deltaTime > 0.25f) return;

            UITween.ManualTick(deltaTime);

            // Nothing else knows the transform moved, so ask for the repaint ourselves. This covers
            // scene views, the game view and the inspector in one call.
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
