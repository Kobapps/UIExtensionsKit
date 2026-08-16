using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// The feedback router is the kit's only contact with a game's audio and haptics, so its routing
    /// and mute rules need to be exact — a silent button is one of the hardest things to diagnose.
    /// </summary>
    public class ButtonFeedbackTests
    {
        private sealed class RecordingHandler : IButtonFeedbackHandler
        {
            public readonly List<ButtonFeedbackRequest> Received = new List<ButtonFeedbackRequest>();
            public void Handle(in ButtonFeedbackRequest request) => Received.Add(request);
        }

        private sealed class ThrowingHandler : IButtonFeedbackHandler
        {
            public void Handle(in ButtonFeedbackRequest request) => throw new InvalidOperationException("boom");
        }

        [SetUp]
        public void Reset() => ButtonFeedback.ResetState();

        [TearDown]
        public void CleanUp() => ButtonFeedback.ResetState();

        private static ButtonFeedbackRequest Click(string sfx = "click", HapticType haptic = HapticType.Light)
            => new ButtonFeedbackRequest(null, ButtonFeedbackEvent.Click, sfx, haptic);

        [Test]
        public void Play_ReachesDelegatesAndHandlers()
        {
            var handler = new RecordingHandler();
            ButtonFeedback.RegisterHandler(handler);

            string sfx = null;
            HapticType haptic = HapticType.None;
            ButtonFeedback.SfxHandler = id => sfx = id;
            ButtonFeedback.HapticHandler = type => haptic = type;

            ButtonFeedback.Play(Click());

            Assert.AreEqual(1, handler.Received.Count);
            Assert.AreEqual("click", sfx);
            Assert.AreEqual(HapticType.Light, haptic);
        }

        [Test]
        public void Play_WithNothingToDo_IsIgnored()
        {
            int calls = 0;
            ButtonFeedback.Requested += _ => calls++;

            ButtonFeedback.Play(new ButtonFeedbackRequest(null, ButtonFeedbackEvent.Hover, string.Empty, HapticType.None));

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void SfxMuted_SilencesSoundButNotHaptics()
        {
            string sfx = null;
            HapticType haptic = HapticType.None;
            ButtonFeedback.SfxHandler = id => sfx = id;
            ButtonFeedback.HapticHandler = type => haptic = type;

            ButtonFeedback.SfxMuted = true;
            ButtonFeedback.Play(Click());

            Assert.IsNull(sfx);
            Assert.AreEqual(HapticType.Light, haptic, "muting sound must not also mute haptics");
        }

        [Test]
        public void HapticsMuted_SilencesHapticsButNotSound()
        {
            string sfx = null;
            HapticType haptic = HapticType.None;
            ButtonFeedback.SfxHandler = id => sfx = id;
            ButtonFeedback.HapticHandler = type => haptic = type;

            ButtonFeedback.HapticsMuted = true;
            ButtonFeedback.Play(Click());

            Assert.AreEqual("click", sfx);
            Assert.AreEqual(HapticType.None, haptic);
        }

        [Test]
        public void OneThrowingHandler_DoesNotStopTheOthers()
        {
            // A bad handler in one system must not swallow every button sound in the game.
            ButtonFeedback.RegisterHandler(new ThrowingHandler());
            var good = new RecordingHandler();
            ButtonFeedback.RegisterHandler(good);

            string sfx = null;
            ButtonFeedback.SfxHandler = id => sfx = id;

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                Assert.DoesNotThrow(() => ButtonFeedback.Play(Click()));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }

            Assert.AreEqual(1, good.Received.Count);
            Assert.AreEqual("click", sfx);
        }

        [Test]
        public void UnregisterHandler_StopsDelivery()
        {
            var handler = new RecordingHandler();
            ButtonFeedback.RegisterHandler(handler);
            ButtonFeedback.UnregisterHandler(handler);

            ButtonFeedback.Play(Click());

            Assert.AreEqual(0, handler.Received.Count);
        }

        [Test]
        public void RegisterHandler_IsIdempotent()
        {
            var handler = new RecordingHandler();
            ButtonFeedback.RegisterHandler(handler);
            ButtonFeedback.RegisterHandler(handler);

            ButtonFeedback.Play(Click());

            Assert.AreEqual(1, handler.Received.Count, "registering twice must not double every sound");
        }

        [Test]
        public void HasAnyHandler_ReportsWhetherFeedbackGoesAnywhere()
        {
            Assert.IsFalse(ButtonFeedback.HasAnyHandler);

            ButtonFeedback.SfxHandler = _ => { };
            Assert.IsTrue(ButtonFeedback.HasAnyHandler);
        }

        [Test]
        public void Config_MapsEventsToTheRightSfxAndHaptics()
        {
            var config = new ButtonFeedbackConfig
            {
                hoverSfx = "hover",
                pressSfx = "press",
                clickSfx = "click",
                rejectedSfx = "nope",
                hoverHaptic = HapticType.Selection,
                pressHaptic = HapticType.Light,
                clickHaptic = HapticType.Medium,
                rejectedHaptic = HapticType.Failure,
            };

            Assert.AreEqual("hover", config.SfxFor(ButtonFeedbackEvent.Hover));
            Assert.AreEqual("press", config.SfxFor(ButtonFeedbackEvent.Press));
            Assert.AreEqual("click", config.SfxFor(ButtonFeedbackEvent.Click));
            Assert.AreEqual("nope", config.SfxFor(ButtonFeedbackEvent.Rejected));

            Assert.AreEqual(HapticType.Selection, config.HapticFor(ButtonFeedbackEvent.Hover));
            Assert.AreEqual(HapticType.Light, config.HapticFor(ButtonFeedbackEvent.Press));
            Assert.AreEqual(HapticType.Medium, config.HapticFor(ButtonFeedbackEvent.Click));
            Assert.AreEqual(HapticType.Failure, config.HapticFor(ButtonFeedbackEvent.Rejected));
        }
    }
}
