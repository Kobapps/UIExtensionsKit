using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// An extra visual layer that follows a button's state — glow, shine, particles, a badge, an
    /// outline. A button finds every module on its own GameObject when it is enabled and drives them
    /// alongside its animator.
    /// </summary>
    /// <remarks>
    /// This is the seam that keeps optional integrations optional: the UIImageEffectsKit glow/shine
    /// driver is nothing more than an implementation of this interface living in an assembly that
    /// only compiles when that package is installed. The core kit never references it.
    /// </remarks>
    public interface IEnhancedButtonEffects
    {
        /// <summary>The button moved to a new visual state.</summary>
        void OnButtonStateChanged(EnhancedButton button, EnhancedButtonVisualState state, bool instant);

        /// <summary>The button was clicked.</summary>
        void OnButtonClicked(EnhancedButton button);

        /// <summary>One line describing what this module is doing, for the inspector and debugger window.</summary>
        string EffectsDebugSummary { get; }
    }

    /// <summary>
    /// Convenience base for effect modules. Implements the interface with no-ops so a subclass only
    /// overrides the hook it cares about.
    /// </summary>
    public abstract class EnhancedButtonEffectsBehaviour : MonoBehaviour, IEnhancedButtonEffects
    {
        /// <inheritdoc/>
        public virtual void OnButtonStateChanged(EnhancedButton button, EnhancedButtonVisualState state, bool instant) { }

        /// <inheritdoc/>
        public virtual void OnButtonClicked(EnhancedButton button) { }

        /// <inheritdoc/>
        public virtual string EffectsDebugSummary => GetType().Name;
    }
}
