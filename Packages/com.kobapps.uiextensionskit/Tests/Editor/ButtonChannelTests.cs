using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// A button is rarely the only thing animating itself, and the failure mode when two systems
    /// disagree is not subtle: the button snaps back to wherever it was when it was first enabled.
    /// These pin down the two rules that stop that — write only what the preset uses, and give way
    /// when something else writes the same channel.
    /// </summary>
    public class ButtonChannelTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        private EnhancedButton CreateButton()
        {
            var host = new GameObject("ChannelButton", typeof(RectTransform), typeof(Image), typeof(EnhancedButton));
            _created.Add(host);

            var button = host.GetComponent<EnhancedButton>();
            button.targetGraphic = host.GetComponent<Image>();
            return button;
        }

        /// <summary>Run every tween to completion, so the assertions see settled values.</summary>
        private static void Settle()
        {
            for (int i = 0; i < 4; i++) NativeTweenRunner.Tick(1f, 1f);
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
        public void AScaleOnlyPoseClaimsOnlyScale()
        {
            ButtonStateMotion motion = ButtonStateMotion.Scaled(1.1f, 0.2f, UIEase.OutQuad);
            Assert.AreEqual(ButtonAnimationChannels.Scale, motion.UsedChannels);
        }

        [Test]
        public void AnUntouchedPoseClaimsNothing()
        {
            // A state nobody filled in is all zeroes on disk. That must read as "claims nothing"
            // rather than "scale to zero, tint to black" — otherwise a half-authored preset would
            // grab every channel on the object.
            Assert.AreEqual(ButtonAnimationChannels.None, default(ButtonStateMotion).UsedChannels);
            Assert.AreEqual(ButtonAnimationChannels.None, ButtonStateMotion.Identity.UsedChannels);
        }

        [Test]
        public void APoseClaimsEachChannelItActuallyMoves()
        {
            ButtonStateMotion motion = ButtonStateMotion.Identity
                .WithOffset(0f, -4f)
                .WithTint(Color.grey)
                .WithLabelTint(Color.red);
            motion.rotation = 3f;

            Assert.AreEqual(
                ButtonAnimationChannels.Position | ButtonAnimationChannels.Tint
                | ButtonAnimationChannels.LabelTint | ButtonAnimationChannels.Rotation,
                motion.UsedChannels);
        }

        [Test]
        public void OnlyMechanicalMovesTheButton()
        {
            // Worth knowing rather than a rule the kit enforces. Every built-in is scale and tint
            // only — which is why they coexist with a sliding panel out of the box — except
            // Mechanical, whose whole idea is a key travelling downwards under the finger. A button
            // with Mechanical and an external position animation is the one combination that needs
            // the Position channel cleared by hand.
            foreach (ButtonPresetKind kind in ButtonPresetLibrary.BuiltIn)
            {
                ButtonAnimationChannels channels = ButtonPresetLibrary.Get(kind).UsedChannels;
                bool moves = (channels & ButtonAnimationChannels.Position) != 0;

                Assert.AreEqual(kind == ButtonPresetKind.Mechanical, moves, $"{kind} position claim");
                Assert.AreEqual(
                    ButtonAnimationChannels.None,
                    channels & ButtonAnimationChannels.Rotation,
                    $"{kind} should not rotate");
            }
        }

        [Test]
        public void AButtonLeavesPositionAloneWhenItsPresetNeverMovesIt()
        {
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Pop;

            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(100f, 40f);
            button.RecaptureBasePose();
            Settle();

            // Something else — an Animator, a screen transition — slides the button.
            rect.anchoredPosition = new Vector2(220f, 40f);

            button.Selected = true;
            Settle();

            Assert.AreEqual(new Vector2(220f, 40f), rect.anchoredPosition,
                "a scale-only preset must not write position at all");
        }

        [Test]
        public void ClearingAChannelStopsTheButtonWritingIt()
        {
            EnhancedButton button = CreateButton();

            ButtonMotionSet motion = ButtonPresetLibrary.Get(ButtonPresetKind.Pop);
            motion.selected = motion.selected.WithOffset(0f, -6f);
            button.SetMotionOverride(motion);

            Assert.IsTrue(
                (button.MotionSet.UsedChannels & ButtonAnimationChannels.Position) != 0,
                "this preset does move the button, so the test is only meaningful with it cleared");

            button.AnimatedChannels = ButtonAnimationChannels.All & ~ButtonAnimationChannels.Position;

            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(50f, 12f);
            button.RecaptureBasePose();
            Settle();

            rect.anchoredPosition = new Vector2(80f, 12f);
            button.Selected = true;
            Settle();

            Assert.AreEqual(new Vector2(80f, 12f), rect.anchoredPosition);
        }

        [Test]
        public void ScaleWrittenFromOutsideIsAdoptedRatherThanStampedBack()
        {
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Pop;

            var rect = (RectTransform)button.transform;
            button.RecaptureBasePose();
            Settle();

            Assert.AreEqual(1f, rect.localScale.x, 0.001f, "normal is authored scale");

            // A parent effect shrinks the whole card to half size.
            rect.localScale = new Vector3(0.5f, 0.5f, 1f);

            // The button's own animation must ride on top of that rather than undo it. Pop's
            // Selected pose is 1.05, so the settled result is half-size and a touch bigger — the
            // number that matters is that it is nowhere near the full size it started at.
            button.Selected = true;
            Settle();

            Assert.AreEqual(0.5f * 1.05f, rect.localScale.x, 0.02f,
                "the external scale should have been adopted as the new authored pose");
        }
    }
}
