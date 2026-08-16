using System;
using NUnit.Framework;
using UnityEngine;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// The easing curves are the one piece of pure maths in the kit, and everything visual rides on
    /// them, so they are worth pinning down exactly.
    /// </summary>
    public class UIEasingTests
    {
        private static readonly UIEase[] AllEases = (UIEase[])Enum.GetValues(typeof(UIEase));

        [Test]
        public void EveryEase_StartsAtZeroAndEndsAtOne()
        {
            foreach (UIEase ease in AllEases)
            {
                Assert.AreEqual(0f, UIEasing.Evaluate(ease, 0f), 1e-4f, $"{ease} should start at 0");
                Assert.AreEqual(1f, UIEasing.Evaluate(ease, 1f), 1e-4f, $"{ease} should end at 1");
            }
        }

        [Test]
        public void EveryEase_ClampsInputOutsideZeroToOne()
        {
            foreach (UIEase ease in AllEases)
            {
                Assert.AreEqual(0f, UIEasing.Evaluate(ease, -5f), 1e-4f, $"{ease} should clamp below 0");
                Assert.AreEqual(1f, UIEasing.Evaluate(ease, 5f), 1e-4f, $"{ease} should clamp above 1");
            }
        }

        [Test]
        public void EveryEase_ProducesFiniteValues()
        {
            foreach (UIEase ease in AllEases)
            {
                for (int step = 0; step <= 20; step++)
                {
                    float value = UIEasing.Evaluate(ease, step / 20f);
                    Assert.IsFalse(float.IsNaN(value) || float.IsInfinity(value), $"{ease} produced {value}");
                }
            }
        }

        [Test]
        public void Linear_IsIdentity()
        {
            for (int step = 0; step <= 10; step++)
            {
                float t = step / 10f;
                Assert.AreEqual(t, UIEasing.Evaluate(UIEase.Linear, t), 1e-5f);
            }
        }

        [Test]
        public void OutBack_Overshoots_BecauseThatIsThePoint()
        {
            // If this ever clamps to 1, Bouncy and Jelly quietly lose their character.
            bool overshot = false;
            for (int step = 0; step <= 100; step++)
                if (UIEasing.Evaluate(UIEase.OutBack, step / 100f) > 1.001f)
                    overshot = true;

            Assert.IsTrue(overshot, "OutBack must exceed 1 somewhere in its range");
        }

        [Test]
        public void OutQuad_IsMonotonic()
        {
            float previous = -1f;
            for (int step = 0; step <= 100; step++)
            {
                float value = UIEasing.Evaluate(UIEase.OutQuad, step / 100f);
                Assert.GreaterOrEqual(value, previous, "OutQuad must never move backwards");
                previous = value;
            }
        }

        [Test]
        public void Punch_Envelope_ReturnsToZeroAtBothEnds()
        {
            // A punch that doesn't settle at exactly 0 leaves the button permanently offset.
            foreach (int oscillations in new[] { 1, 2, 3, 5 })
            {
                var punch = ButtonPunch.Uniform(0.2f, 0.4f, oscillations, 1.5f);
                Assert.AreEqual(0f, punch.Envelope(0f), 1e-4f, $"start, {oscillations} oscillations");
                Assert.AreEqual(0f, punch.Envelope(1f), 1e-4f, $"end, {oscillations} oscillations");
            }
        }

        [Test]
        public void Punch_Envelope_PeaksWithinAmplitude()
        {
            var punch = ButtonPunch.Uniform(0.2f, 0.4f, 1, 1f);

            for (int step = 0; step <= 100; step++)
                Assert.LessOrEqual(Mathf.Abs(punch.Envelope(step / 100f)), 1.0001f);
        }
    }
}
