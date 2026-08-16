using NUnit.Framework;
using UnityEngine;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// Guards the built-in feels against the failure that serialized structs invite: a zeroed field
    /// that scales a button to nothing or runs a tween backwards.
    /// </summary>
    public class ButtonPresetTests
    {
        [Test]
        public void EveryBuiltInPreset_HasUsableMotionInEveryState()
        {
            foreach (ButtonPresetKind kind in ButtonPresetLibrary.BuiltIn)
            {
                ButtonMotionSet motion = ButtonPresetLibrary.Get(kind);

                foreach (EnhancedButtonVisualState state in new[]
                         {
                             EnhancedButtonVisualState.Normal,
                             EnhancedButtonVisualState.Highlighted,
                             EnhancedButtonVisualState.Pressed,
                             EnhancedButtonVisualState.Selected,
                             EnhancedButtonVisualState.Disabled,
                         })
                {
                    ButtonStateMotion pose = motion.Get(state);

                    Assert.AreNotEqual(Vector3.zero, pose.scale, $"{kind}.{state} would scale the button away");
                    Assert.GreaterOrEqual(pose.duration, 0f, $"{kind}.{state} has a negative duration");
                    Assert.Less(pose.duration, 2f, $"{kind}.{state} is implausibly slow for a button");
                }
            }
        }

        [Test]
        public void EveryBuiltInPreset_HasASensiblePunch()
        {
            foreach (ButtonPresetKind kind in ButtonPresetLibrary.BuiltIn)
            {
                ButtonPunch punch = ButtonPresetLibrary.Get(kind).click;

                Assert.GreaterOrEqual(punch.oscillations, 1, $"{kind} punch has no oscillations");
                Assert.GreaterOrEqual(punch.duration, 0f, $"{kind} punch has a negative duration");
            }
        }

        [Test]
        public void None_LeavesEveryStateAtTheAuthoredPose()
        {
            ButtonMotionSet motion = ButtonPresetLibrary.Get(ButtonPresetKind.None);

            Assert.AreEqual(Vector3.one, motion.highlighted.scale);
            Assert.AreEqual(Vector3.one, motion.pressed.scale);
            Assert.AreEqual(Color.white, motion.pressed.tint);
            Assert.IsFalse(motion.click.enabled);
        }

        [Test]
        public void Jelly_SquashesOnPress()
        {
            // The defining trait: wider than it is tall while held down.
            ButtonStateMotion pressed = ButtonPresetLibrary.Get(ButtonPresetKind.Jelly).pressed;

            Assert.Greater(pressed.scale.x, 1f);
            Assert.Less(pressed.scale.y, 1f);
        }

        [Test]
        public void Mechanical_MovesTheButtonDownInsteadOfScalingIt()
        {
            ButtonStateMotion pressed = ButtonPresetLibrary.Get(ButtonPresetKind.Mechanical).pressed;

            Assert.Less(pressed.offset.y, 0f, "a mechanical press should physically depress");
            Assert.IsFalse(ButtonPresetLibrary.Get(ButtonPresetKind.Mechanical).click.enabled);
        }

        [Test]
        public void Rigid_NeverMoves()
        {
            ButtonMotionSet motion = ButtonPresetLibrary.Get(ButtonPresetKind.Rigid);

            foreach (EnhancedButtonVisualState state in new[]
                     {
                         EnhancedButtonVisualState.Normal,
                         EnhancedButtonVisualState.Highlighted,
                         EnhancedButtonVisualState.Pressed,
                         EnhancedButtonVisualState.Selected,
                         EnhancedButtonVisualState.Disabled,
                     })
            {
                ButtonStateMotion pose = motion.Get(state);
                Assert.AreEqual(Vector3.one, pose.scale, $"Rigid.{state} must not scale");
                Assert.AreEqual(Vector2.zero, pose.offset, $"Rigid.{state} must not move");
                Assert.AreEqual(0f, pose.rotation, $"Rigid.{state} must not rotate");
            }
        }

        [Test]
        public void Describe_CoversEveryKind()
        {
            foreach (ButtonPresetKind kind in (ButtonPresetKind[])System.Enum.GetValues(typeof(ButtonPresetKind)))
                Assert.IsNotEmpty(ButtonPresetLibrary.Describe(kind), $"{kind} has no description");
        }

        [Test]
        public void Sanitize_RepairsAZeroedStruct()
        {
            // Exactly what Unity hands back for a newly added serialized field.
            var zeroed = default(ButtonMotionSet);
            ButtonMotionSet repaired = zeroed.Sanitized();

            Assert.AreEqual(Vector3.one, repaired.normal.scale);
            Assert.AreEqual(Vector3.one, repaired.pressed.scale);
            Assert.GreaterOrEqual(repaired.click.oscillations, 1);
        }

        [Test]
        public void Sanitize_RepairsANegativeDuration()
        {
            var motion = ButtonStateMotion.Identity;
            motion.duration = -1f;

            Assert.AreEqual(0f, motion.Sanitized().duration);
        }

        [Test]
        public void MotionSet_GetAndSet_RoundTrip()
        {
            var motion = ButtonPresetLibrary.Get(ButtonPresetKind.Pop);
            var replacement = ButtonStateMotion.Scaled(1.5f, 0.3f, UIEase.OutBounce);

            motion.Set(EnhancedButtonVisualState.Highlighted, replacement);

            Assert.AreEqual(replacement.scale, motion.Get(EnhancedButtonVisualState.Highlighted).scale);
            Assert.AreEqual(UIEase.OutBounce, motion.Get(EnhancedButtonVisualState.Highlighted).ease);
        }
    }
}
