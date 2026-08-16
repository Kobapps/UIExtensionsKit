using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>What just happened to a button. The reason a piece of feedback is being requested.</summary>
    public enum ButtonFeedbackEvent
    {
        /// <summary>The pointer entered, or the button took focus.</summary>
        Hover = 0,

        /// <summary>The pointer went down on the button.</summary>
        Press = 1,

        /// <summary>A completed click or submit.</summary>
        Click = 2,

        /// <summary>A click on a button that isn't interactable — the "nope" buzz.</summary>
        Rejected = 3,

        /// <summary>The latched <see cref="EnhancedButton.Selected"/> state turned on.</summary>
        Select = 4,
    }

    /// <summary>
    /// Intensity of a haptic tap, named the way platform haptics engines name them. Mapping these to
    /// an actual device is the game's job — see <see cref="ButtonFeedback"/>.
    /// </summary>
    public enum HapticType
    {
        None = 0,
        Selection = 1,
        Light = 2,
        Medium = 3,
        Heavy = 4,
        Success = 5,
        Warning = 6,
        Failure = 7,
    }

    /// <summary>Which sound and haptic a button asks for, per event. Authored on a button or a shared style.</summary>
    [Serializable]
    public struct ButtonFeedbackConfig
    {
        [Tooltip("Sfx id sent when the pointer enters. Empty means no sound.")]
        public string hoverSfx;

        [Tooltip("Sfx id sent when the pointer goes down.")]
        public string pressSfx;

        [Tooltip("Sfx id sent on a completed click.")]
        public string clickSfx;

        [Tooltip("Sfx id sent when a non-interactable button is clicked.")]
        public string rejectedSfx;

        public HapticType hoverHaptic;
        public HapticType pressHaptic;
        public HapticType clickHaptic;
        public HapticType rejectedHaptic;

        /// <summary>A sensible default: a light tap on click, nothing else.</summary>
        public static ButtonFeedbackConfig Default => new ButtonFeedbackConfig
        {
            hoverSfx = string.Empty,
            pressSfx = string.Empty,
            clickSfx = "ui_click",
            rejectedSfx = string.Empty,
            hoverHaptic = HapticType.None,
            pressHaptic = HapticType.None,
            clickHaptic = HapticType.Light,
            rejectedHaptic = HapticType.Warning,
        };

        /// <summary>The sfx id for <paramref name="feedbackEvent"/>, or empty.</summary>
        public string SfxFor(ButtonFeedbackEvent feedbackEvent)
        {
            switch (feedbackEvent)
            {
                case ButtonFeedbackEvent.Hover: return hoverSfx;
                case ButtonFeedbackEvent.Press: return pressSfx;
                case ButtonFeedbackEvent.Rejected: return rejectedSfx;
                case ButtonFeedbackEvent.Select: return clickSfx;
                default: return clickSfx;
            }
        }

        /// <summary>The haptic for <paramref name="feedbackEvent"/>, or <see cref="HapticType.None"/>.</summary>
        public HapticType HapticFor(ButtonFeedbackEvent feedbackEvent)
        {
            switch (feedbackEvent)
            {
                case ButtonFeedbackEvent.Hover: return hoverHaptic;
                case ButtonFeedbackEvent.Press: return pressHaptic;
                case ButtonFeedbackEvent.Rejected: return rejectedHaptic;
                case ButtonFeedbackEvent.Select: return clickHaptic;
                default: return clickHaptic;
            }
        }
    }

    /// <summary>One request for sound and/or haptics, as handed to the game's handlers.</summary>
    public readonly struct ButtonFeedbackRequest
    {
        /// <summary>The button that asked. May be null if raised manually.</summary>
        public readonly EnhancedButton Source;

        public readonly ButtonFeedbackEvent Event;

        /// <summary>Sfx id to play, or empty. The kit never interprets this — it's whatever the game's audio stack keys on.</summary>
        public readonly string SfxId;

        public readonly HapticType Haptic;

        public ButtonFeedbackRequest(EnhancedButton source, ButtonFeedbackEvent feedbackEvent, string sfxId, HapticType haptic)
        {
            Source = source;
            Event = feedbackEvent;
            SfxId = sfxId;
            Haptic = haptic;
        }

        /// <summary>Whether this request asks for anything at all.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(SfxId) && Haptic == HapticType.None;

        public override string ToString() =>
            $"{Event}: sfx='{(string.IsNullOrEmpty(SfxId) ? "-" : SfxId)}' haptic={Haptic}";
    }

    /// <summary>Implement and register with <see cref="ButtonFeedback"/> to route button feedback to a real system.</summary>
    public interface IButtonFeedbackHandler
    {
        void Handle(in ButtonFeedbackRequest request);
    }

    /// <summary>
    /// Where button sound and haptics go. The kit raises requests here and never plays anything
    /// itself, so it stays free of any dependency on an audio or haptics package — wire this once to
    /// AudioKit, FMOD, Wwise, a native haptics plugin, or whatever the project already uses, and
    /// every button in the game is covered.
    /// </summary>
    /// <example>
    /// <code>
    /// ButtonFeedback.SfxHandler = id => AudioKit.Play(id);
    /// ButtonFeedback.HapticHandler = type => Haptics.Play(type);
    /// </code>
    /// </example>
    public static class ButtonFeedback
    {
        private static readonly List<IButtonFeedbackHandler> s_Handlers = new List<IButtonFeedbackHandler>(4);

        /// <summary>Called with the sfx id of every request that carries one. The one-liner hookup.</summary>
        public static Action<string> SfxHandler;

        /// <summary>Called with the haptic of every request that carries one.</summary>
        public static Action<HapticType> HapticHandler;

        /// <summary>
        /// The game's sound system. Set it directly, or let a <see cref="ButtonFeedbackAdapter"/>
        /// component register itself. Receives the button alongside the id, which
        /// <see cref="SfxHandler"/> cannot.
        /// </summary>
        public static IButtonSfxAdapter SfxAdapter { get; set; }

        /// <summary>The game's haptics system. See <see cref="SfxAdapter"/>.</summary>
        public static IButtonHapticsAdapter HapticsAdapter { get; set; }

        /// <summary>Raised for every request, before handlers run. Useful for logging and the sample's on-screen log.</summary>
        public static event Action<ButtonFeedbackRequest> Requested;

        /// <summary>Suppress all sfx without touching the per-button configuration.</summary>
        public static bool SfxMuted { get; set; }

        /// <summary>Suppress all haptics — the setting players actually look for.</summary>
        public static bool HapticsMuted { get; set; }

        /// <summary>How many rich handlers are registered. Shown in the inspector so a silent button is diagnosable.</summary>
        public static int HandlerCount => s_Handlers.Count;

        /// <summary>Whether anything at all is listening. A false here is why buttons are silent.</summary>
        public static bool HasAnyHandler =>
            s_Handlers.Count > 0 || SfxHandler != null || HapticHandler != null
            || SfxAdapter != null || HapticsAdapter != null;

        /// <summary>Register a handler that sees whole requests, including which button raised them.</summary>
        public static void RegisterHandler(IButtonFeedbackHandler handler)
        {
            if (handler != null && !s_Handlers.Contains(handler)) s_Handlers.Add(handler);
        }

        /// <summary>Unregister a handler. Safe to call for one that was never registered.</summary>
        public static void UnregisterHandler(IButtonFeedbackHandler handler)
        {
            if (handler != null) s_Handlers.Remove(handler);
        }

        /// <summary>Raise a request. No-op if it asks for nothing.</summary>
        public static void Play(in ButtonFeedbackRequest request)
        {
            if (request.IsEmpty) return;

            Requested?.Invoke(request);

            for (int i = 0; i < s_Handlers.Count; i++)
            {
                try
                {
                    s_Handlers[i].Handle(request);
                }
                catch (Exception exception)
                {
                    // One bad handler must not stop the others, or swallow a button's click sound.
                    Debug.LogException(exception);
                }
            }

            if (!SfxMuted && !string.IsNullOrEmpty(request.SfxId))
            {
                SfxHandler?.Invoke(request.SfxId);

                // Adapters are guarded the same way handlers are: one bad integration must not
                // silence every other button in the game.
                try
                {
                    SfxAdapter?.PlaySfx(request.SfxId, request.Source);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            if (!HapticsMuted && request.Haptic != HapticType.None)
            {
                HapticHandler?.Invoke(request.Haptic);

                try
                {
                    HapticsAdapter?.PlayHaptic(request.Haptic, request.Source);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>Raise a request built from a config. What <see cref="EnhancedButton"/> calls.</summary>
        public static void Play(EnhancedButton source, ButtonFeedbackEvent feedbackEvent, in ButtonFeedbackConfig config)
            => Play(new ButtonFeedbackRequest(
                source,
                feedbackEvent,
                config.SfxFor(feedbackEvent),
                config.HapticFor(feedbackEvent)));

        /// <summary>
        /// Drop every handler and mute flag. Runs before a new play session so "enter play mode
        /// without domain reload" doesn't keep handlers pointing at objects from the last one.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetState()
        {
            s_Handlers.Clear();
            SfxHandler = null;
            HapticHandler = null;
            SfxAdapter = null;
            HapticsAdapter = null;
            Requested = null;
            SfxMuted = false;
            HapticsMuted = false;
        }
    }
}
