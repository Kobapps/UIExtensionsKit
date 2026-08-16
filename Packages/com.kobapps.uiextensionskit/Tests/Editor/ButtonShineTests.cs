using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// The shine is one of the few things the kit times but does not draw, so the phase it hands to
    /// an effects module is the whole contract — and a sweep that never ends, or one that runs on a
    /// dead button, is exactly the sort of thing nobody notices until it ships.
    /// </summary>
    public class ButtonShineTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        private EnhancedButton CreateButton()
        {
            var host = new GameObject("ShineButton", typeof(RectTransform), typeof(Image), typeof(EnhancedButton));
            _created.Add(host);

            var button = host.GetComponent<EnhancedButton>();
            button.targetGraphic = host.GetComponent<Image>();
            return button;
        }

        [SetUp]
        public void Reset()
        {
            NativeTweenRunner.ResetState();
            ButtonFeedback.ResetState();
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (GameObject host in _created)
                if (host != null)
                    Object.DestroyImmediate(host);

            _created.Clear();
        }

        [Test]
        public void CtaResolvesToTheCtaStateButLosesToSelection()
        {
            EnhancedButton button = CreateButton();

            Assert.AreEqual(EnhancedButtonVisualState.Normal, button.VisualState);

            button.IsCta = true;
            Assert.AreEqual(EnhancedButtonVisualState.Cta, button.VisualState);

            // A chosen tab that also happens to be the CTA reads as chosen — that is the state the
            // player just acted on.
            button.Selected = true;
            Assert.AreEqual(EnhancedButtonVisualState.Selected, button.VisualState);

            button.Selected = false;
            Assert.AreEqual(EnhancedButtonVisualState.Cta, button.VisualState);

            button.interactable = false;
            Assert.AreEqual(EnhancedButtonVisualState.Disabled, button.VisualState);
        }

        [Test]
        public void EveryBuiltInPresetSuppliesAUsableShine()
        {
            foreach (ButtonPresetKind kind in ButtonPresetLibrary.BuiltIn)
            {
                ButtonShine shine = ButtonPresetLibrary.Get(kind).shine;

                Assert.Greater(shine.sweepDuration, 0f, $"{kind} sweep duration");
                Assert.Greater(shine.width, 0f, $"{kind} width");
                Assert.GreaterOrEqual(shine.interval, 0f, $"{kind} interval");
                Assert.Greater(shine.color.a, 0f, $"{kind} colour alpha");
            }
        }

        [Test]
        public void ShineOnlyRunsWhileTheButtonIsTheCallToAction()
        {
            ButtonShine shine = ButtonShine.Cta();

            Assert.IsFalse(shine.ShouldRun(EnhancedButtonVisualState.Normal, isCta: false));
            Assert.IsTrue(shine.ShouldRun(EnhancedButtonVisualState.Cta, isCta: true));

            // A sheen says "interactive". Putting one on a dead button is a lie to the player.
            Assert.IsFalse(shine.ShouldRun(EnhancedButtonVisualState.Disabled, isCta: true));
        }

        [Test]
        public void SweepAdvancesThenRestsForTheInterval()
        {
            EnhancedButton button = CreateButton();
            button.SetMotionOverride(WithShine(ButtonShine.Cta(sweepDuration: 0.5f, interval: 1f)));
            button.IsCta = true;

            // Nothing is drawn until the first interval has elapsed.
            Assert.AreEqual(-1f, button.TickShine(0.25f));

            Assert.AreEqual(0f, button.TickShine(0.8f), 0.001f, "sweep should start once the interval passes");
            Assert.AreEqual(0.5f, button.TickShine(0.25f), 0.001f, "band should be halfway across");
            Assert.AreEqual(-1f, button.TickShine(0.3f), "sweep should end rather than wrap");

            // ...and then come back round.
            Assert.AreEqual(-1f, button.TickShine(0.5f));
            Assert.AreEqual(0f, button.TickShine(0.6f), 0.001f, "the shine should repeat");
        }

        [Test]
        public void ShineStopsWhenTheButtonIsNoLongerTheCallToAction()
        {
            EnhancedButton button = CreateButton();
            button.SetMotionOverride(WithShine(ButtonShine.Cta(sweepDuration: 0.5f, interval: 0f)));
            button.IsCta = true;

            button.TickShine(0.1f);
            Assert.GreaterOrEqual(button.ShinePosition, 0f);

            button.IsCta = false;

            // The sweep in flight is allowed to finish rather than snapping off mid-band...
            button.TickShine(1f);
            Assert.AreEqual(-1f, button.ShinePosition);

            // ...and nothing starts again.
            Assert.AreEqual(-1f, button.TickShine(5f));
        }

        [Test]
        public void OneShotTriggersDoNotRepeatOnTheirOwn()
        {
            EnhancedButton button = CreateButton();

            ButtonShine shine = ButtonShine.None;
            shine.trigger = ButtonShineTrigger.OnClick;
            shine.sweepDuration = 0.4f;
            button.SetMotionOverride(WithShine(shine));

            Assert.AreEqual(-1f, button.TickShine(10f), "an idle one-shot should never start itself");

            button.PlayShineSweep();
            Assert.AreEqual(0.5f, button.TickShine(0.2f), 0.001f);
            Assert.AreEqual(-1f, button.TickShine(0.3f));
            Assert.AreEqual(-1f, button.TickShine(10f), "it should stay finished until fired again");
        }

        [Test]
        public void ADisabledButtonNeverStartsASweep()
        {
            EnhancedButton button = CreateButton();
            button.SetMotionOverride(WithShine(ButtonShine.Cta(sweepDuration: 0.4f, interval: 0f)));
            button.IsCta = true;
            button.interactable = false;

            Assert.AreEqual(-1f, button.TickShine(5f));

            button.PlayShineSweep();
            Assert.AreEqual(-1f, button.ShinePosition);
        }

        private static ButtonMotionSet WithShine(ButtonShine shine)
        {
            ButtonMotionSet motion = ButtonPresetLibrary.Get(ButtonPresetKind.Pop);
            motion.shine = shine;
            return motion;
        }
    }
}
