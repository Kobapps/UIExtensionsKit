namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// Drives a button's visuals for a visual state. Two implementations ship — a tween-driven one
    /// and a legacy-<see cref="UnityEngine.AnimationClip"/> one — and a game can add its own by
    /// implementing this and assigning it through <see cref="EnhancedButton.SetAnimator"/>.
    /// </summary>
    public interface IEnhancedButtonAnimator
    {
        /// <summary>
        /// Bind to a button and capture its authored pose. Called when the button is enabled, and
        /// again if the animator is swapped at runtime.
        /// </summary>
        void Initialize(EnhancedButton button);

        /// <summary>
        /// Move to <paramref name="state"/>. <paramref name="instant"/> means snap — used on enable
        /// and when the EventSystem forces a transition without a frame to animate in.
        /// </summary>
        void ApplyState(EnhancedButtonVisualState state, bool instant);

        /// <summary>Fire the one-shot click reaction, on top of the current state pose.</summary>
        void PlayClick();

        /// <summary>Stop everything and restore the authored pose exactly.</summary>
        void ResetToBase();

        /// <summary>Stop everything, leaving the button wherever it currently is.</summary>
        void Stop();

        /// <summary>One line describing what this animator is doing, for the inspector and debugger window.</summary>
        string DebugSummary { get; }
    }
}
