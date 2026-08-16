using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// A shared style is normally all-or-nothing. These pin down the escape hatch for the one button
    /// that must look like the rest and behave differently.
    /// </summary>
    public class ButtonStyleOverrideTests
    {
        private readonly List<Object> _created = new List<Object>();

        private EnhancedButton CreateButton()
        {
            var host = new GameObject("TestButton", typeof(RectTransform), typeof(Image), typeof(EnhancedButton));
            _created.Add(host);

            var button = host.GetComponent<EnhancedButton>();
            button.targetGraphic = host.GetComponent<Image>();
            return button;
        }

        private EnhancedButtonStyle CreateStyle(ButtonPresetKind preset, string clickSfx)
        {
            var style = ScriptableObject.CreateInstance<EnhancedButtonStyle>();
            _created.Add(style);

            var serialized = new SerializedObjectLike(style);
            serialized.SetEnum("m_Preset", (int)preset);
            serialized.SetString("m_Feedback.clickSfx", clickSfx);
            return style;
        }

        /// <summary>Tiny SerializedObject helper so the tests can set the style's private fields.</summary>
        private sealed class SerializedObjectLike
        {
            private readonly UnityEditor.SerializedObject _serialized;

            public SerializedObjectLike(Object target) => _serialized = new UnityEditor.SerializedObject(target);

            public void SetEnum(string path, int value)
            {
                _serialized.FindProperty(path).enumValueIndex = value;
                _serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            public void SetString(string path, string value)
            {
                _serialized.FindProperty(path).stringValue = value;
                _serialized.ApplyModifiedPropertiesWithoutUndo();
            }
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
            foreach (Object created in _created)
                if (created != null)
                    Object.DestroyImmediate(created);

            _created.Clear();
            ButtonFeedback.ResetState();
        }

        [Test]
        public void WithNoOverrides_TheStyleSuppliesEverything()
        {
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Mechanical;
            button.Style = CreateStyle(ButtonPresetKind.Rigid, "style_click");

            Assert.AreEqual(ButtonPresetKind.Rigid, button.Preset);
            Assert.AreEqual("style_click", button.Feedback.clickSfx);
        }

        [Test]
        public void OverridingFeedback_KeepsTheStylesLookButTheButtonsSound()
        {
            // The whole point: a destructive confirm that looks like every other button.
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Mechanical;
            button.Style = CreateStyle(ButtonPresetKind.Rigid, "style_click");
            button.Overrides = ButtonStyleOverride.Feedback;

            Assert.AreEqual(ButtonPresetKind.Rigid, button.Preset, "motion should still come from the style");
            Assert.AreEqual(ButtonFeedbackConfig.Default.clickSfx, button.Feedback.clickSfx,
                "feedback should come from the button");
        }

        [Test]
        public void OverridingPreset_KeepsTheStylesSoundButTheButtonsMotion()
        {
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Mechanical;
            button.Style = CreateStyle(ButtonPresetKind.Rigid, "style_click");
            button.Overrides = ButtonStyleOverride.Preset;

            Assert.AreEqual(ButtonPresetKind.Mechanical, button.Preset);
            Assert.AreEqual("style_click", button.Feedback.clickSfx);
        }

        [Test]
        public void OverridesCombine()
        {
            EnhancedButton button = CreateButton();
            button.Style = CreateStyle(ButtonPresetKind.Rigid, "style_click");
            button.Overrides = ButtonStyleOverride.Preset | ButtonStyleOverride.Feedback;

            Assert.IsFalse(button.UsesStyleFor(ButtonStyleOverride.Preset));
            Assert.IsFalse(button.UsesStyleFor(ButtonStyleOverride.Feedback));
            Assert.IsTrue(button.UsesStyleFor(ButtonStyleOverride.Timing), "timing was not overridden");
        }

        [Test]
        public void WithoutAStyle_OverridesAreIrrelevant()
        {
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Pop;
            button.Overrides = ButtonStyleOverride.Preset;

            Assert.AreEqual(ButtonPresetKind.Pop, button.Preset);
            Assert.IsFalse(button.UsesStyleFor(ButtonStyleOverride.Preset));
        }

        [Test]
        public void ChangingOverrides_ReresolvesTheMotion()
        {
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Rigid;
            button.Style = CreateStyle(ButtonPresetKind.Bouncy, "style_click");

            Vector3 styled = button.MotionSet.highlighted.scale;
            button.Overrides = ButtonStyleOverride.Preset;
            Vector3 overridden = button.MotionSet.highlighted.scale;

            Assert.AreNotEqual(styled, overridden, "the cached motion should have been invalidated");
            Assert.AreEqual(Vector3.one, overridden, "Rigid never scales");
        }
    }
}
