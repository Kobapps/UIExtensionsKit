using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// Plays a button's sound. Implement this over whatever audio stack the game already has —
    /// AudioKit, FMOD, Wwise, a pooled <see cref="AudioSource"/>, anything.
    /// </summary>
    /// <remarks>
    /// The kit never plays audio itself and never will: it has no opinion on mixers, buses, pooling
    /// or addressables, and any opinion it did have would be wrong for half its users. It hands over
    /// an id and the button that asked, and the game decides what that means.
    /// </remarks>
    public interface IButtonSfxAdapter
    {
        /// <summary>
        /// Play the sound registered under <paramref name="sfxId"/>. Never called with a null or
        /// empty id, and never called while <see cref="ButtonFeedback.SfxMuted"/> is set.
        /// </summary>
        /// <param name="sfxId">The id authored on the button or its style.</param>
        /// <param name="source">The button that asked. May be null when raised manually.</param>
        void PlaySfx(string sfxId, EnhancedButton source);
    }

    /// <summary>
    /// Plays a button's haptic. Implement this over the platform plugin the game uses.
    /// </summary>
    public interface IButtonHapticsAdapter
    {
        /// <summary>
        /// Play <paramref name="haptic"/>. Never called with <see cref="HapticType.None"/>, and
        /// never called while <see cref="ButtonFeedback.HapticsMuted"/> is set.
        /// </summary>
        /// <param name="haptic">Intensity, named the way platform haptics engines name them.</param>
        /// <param name="source">The button that asked. May be null when raised manually.</param>
        void PlayHaptic(HapticType haptic, EnhancedButton source);
    }

    /// <summary>
    /// Drop-in base for wiring a game's audio and haptics to the kit from a scene object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subclass it, override one or both methods, and put it on a bootstrap GameObject — it
    /// registers itself on enable and unregisters on disable, so there is no lifetime to manage and
    /// nothing to remember to tear down between scenes.
    /// </para>
    /// <para>
    /// For a game that already has a static audio façade, the one-line
    /// <see cref="ButtonFeedback.SfxHandler"/> delegate is less ceremony. Use whichever fits; they
    /// coexist.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public sealed class GameButtonFeedback : ButtonFeedbackAdapter
    /// {
    ///     public override void PlaySfx(string sfxId, EnhancedButton source) => AudioKit.Play(sfxId);
    ///     public override void PlayHaptic(HapticType haptic, EnhancedButton source) => Haptics.Play(haptic);
    /// }
    /// </code>
    /// </example>
    public abstract class ButtonFeedbackAdapter : MonoBehaviour, IButtonSfxAdapter, IButtonHapticsAdapter
    {
        [SerializeField]
        [Tooltip("Survive scene loads. Leave on for a bootstrap object that should outlive the first scene.")]
        private bool m_Persist = true;

        /// <inheritdoc/>
        public virtual void PlaySfx(string sfxId, EnhancedButton source) { }

        /// <inheritdoc/>
        public virtual void PlayHaptic(HapticType haptic, EnhancedButton source) { }

        protected virtual void OnEnable()
        {
            if (m_Persist && transform.parent == null && Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            ButtonFeedback.SfxAdapter = this;
            ButtonFeedback.HapticsAdapter = this;
        }

        protected virtual void OnDisable()
        {
            // Only stand down if we are still the active adapter; another one may have taken over.
            if (ReferenceEquals(ButtonFeedback.SfxAdapter, this)) ButtonFeedback.SfxAdapter = null;
            if (ReferenceEquals(ButtonFeedback.HapticsAdapter, this)) ButtonFeedback.HapticsAdapter = null;
        }
    }
}
