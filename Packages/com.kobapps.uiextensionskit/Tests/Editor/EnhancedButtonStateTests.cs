using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// State resolution is the contract everything else hangs off — the animator, the effects and the
    /// feedback all key on it — so the precedence rules are pinned down here.
    /// </summary>
    public class EnhancedButtonStateTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        private EnhancedButton CreateButton()
        {
            var host = new GameObject("TestButton", typeof(RectTransform), typeof(Image), typeof(EnhancedButton));
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
            NativeTweenRunner.ResetState();
            ButtonFeedback.ResetState();
        }

        [Test]
        public void NewButton_StartsNormal()
        {
            Assert.AreEqual(EnhancedButtonVisualState.Normal, CreateButton().VisualState);
        }

        [Test]
        public void NotInteractable_ResolvesToDisabled()
        {
            EnhancedButton button = CreateButton();

            button.interactable = false;
            Assert.AreEqual(EnhancedButtonVisualState.Disabled, button.VisualState);

            button.interactable = true;
            Assert.AreEqual(EnhancedButtonVisualState.Normal, button.VisualState);
        }

        [Test]
        public void Latch_ResolvesToSelected()
        {
            EnhancedButton button = CreateButton();

            button.Selected = true;
            Assert.AreEqual(EnhancedButtonVisualState.Selected, button.VisualState);

            button.Selected = false;
            Assert.AreEqual(EnhancedButtonVisualState.Normal, button.VisualState);
        }

        [Test]
        public void Disabled_BeatsTheLatch()
        {
            EnhancedButton button = CreateButton();
            button.Selected = true;
            button.interactable = false;

            Assert.AreEqual(EnhancedButtonVisualState.Disabled, button.VisualState,
                "a non-interactable button must read as disabled even when latched");
        }

        [Test]
        public void SetSelected_WithoutNotify_RaisesNoFeedback()
        {
            // What a tab group needs when it clears its siblings — otherwise choosing one tab fires
            // a sound for every other tab in the row.
            EnhancedButton button = CreateButton();

            int calls = 0;
            ButtonFeedback.SfxHandler = _ => calls++;

            button.SetSelected(true, notify: false);

            Assert.AreEqual(EnhancedButtonVisualState.Selected, button.VisualState);
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void SetSelected_WithNotify_RaisesFeedbackAndEvent()
        {
            EnhancedButton button = CreateButton();

            bool eventFired = false;
            button.OnSelectedChanged.AddListener(_ => eventFired = true);

            var requests = new List<ButtonFeedbackRequest>();
            ButtonFeedback.Requested += request => requests.Add(request);

            button.SetSelected(true);

            Assert.IsTrue(eventFired);
            Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(ButtonFeedbackEvent.Select, requests[0].Event);
        }

        [Test]
        public void SetSelected_ToTheSameValue_DoesNothing()
        {
            EnhancedButton button = CreateButton();
            button.SetSelected(true);

            int calls = 0;
            button.OnSelectedChanged.AddListener(_ => calls++);
            button.SetSelected(true);

            Assert.AreEqual(0, calls);
        }

        [Test]
        public void StateChanged_FiresOnEveryTransition()
        {
            EnhancedButton button = CreateButton();

            var seen = new List<EnhancedButtonVisualState>();
            button.StateChanged += (_, state) => seen.Add(state);

            button.Selected = true;
            button.interactable = false;
            button.interactable = true;

            CollectionAssert.AreEqual(
                new[]
                {
                    EnhancedButtonVisualState.Selected,
                    EnhancedButtonVisualState.Disabled,
                    EnhancedButtonVisualState.Selected,
                },
                seen);
        }

        [Test]
        public void Style_OverridesLocalSettings()
        {
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Mechanical;

            var style = ScriptableObject.CreateInstance<EnhancedButtonStyle>();
            try
            {
                button.Style = style;

                // The style's own default is the library default, not the local Mechanical.
                Assert.AreEqual(ButtonPresetLibrary.Default, button.Preset);
            }
            finally
            {
                Object.DestroyImmediate(style);
            }
        }

        [Test]
        public void AnimationTarget_DefaultsToTheButtonsOwnRectTransform()
        {
            EnhancedButton button = CreateButton();
            Assert.AreSame(button.transform, button.AnimationTarget);
        }

        [Test]
        public void TintTarget_DefaultsToTheSelectablesTargetGraphic()
        {
            EnhancedButton button = CreateButton();
            Assert.AreSame(button.targetGraphic, button.TintTarget);
        }

        [Test]
        public void CustomPreset_WithNoAsset_FallsBackInsteadOfProducingDeadMotion()
        {
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Custom;

            ButtonMotionSet motion = button.MotionSet;
            ButtonMotionSet expected = ButtonPresetLibrary.Get(ButtonPresetLibrary.Default);

            Assert.AreEqual(expected.highlighted.scale, motion.highlighted.scale);
        }

        [Test]
        public void ClickPunch_ReturnsTheButtonToItsAuthoredScale()
        {
            // A punch that doesn't settle exactly leaves buttons permanently drifted after enough clicks.
            EnhancedButton button = CreateButton();
            button.Preset = ButtonPresetKind.Jelly;

            Vector3 original = button.transform.localScale;

            button.EditorPreviewClick();
            for (int step = 0; step < 200; step++) UITween.ManualTick(0.016f);

            Assert.AreEqual(original.x, button.transform.localScale.x, 1e-3f);
            Assert.AreEqual(original.y, button.transform.localScale.y, 1e-3f);
        }

        [Test]
        public void ClickingADisabledButton_RaisesRejectedRatherThanClicked()
        {
            // The behaviour a plain Button cannot offer: a refusal the game can hear and explain.
            EnhancedButton button = CreateButton();
            button.interactable = false;

            int clicked = 0;
            int rejected = 0;
            button.Clicked += _ => clicked++;
            button.Rejected += _ => rejected++;
            button.onClick.AddListener(() => clicked++);

            button.OnPointerClick(new PointerEventData(null));

            Assert.AreEqual(0, clicked, "a disabled button must not run its click handlers");
            Assert.AreEqual(1, rejected);
        }

        [Test]
        public void ClickingADisabledButton_RequestsTheRejectedFeedback()
        {
            EnhancedButton button = CreateButton();
            button.interactable = false;

            var events = new List<ButtonFeedbackEvent>();
            ButtonFeedback.Requested += request => events.Add(request.Event);

            button.OnPointerClick(new PointerEventData(null));

            CollectionAssert.Contains(events, ButtonFeedbackEvent.Rejected);
        }

        [Test]
        public void ClickingAnInteractableButton_RaisesClickedNotRejected()
        {
            EnhancedButton button = CreateButton();

            int clicked = 0;
            int rejected = 0;
            button.Clicked += _ => clicked++;
            button.Rejected += _ => rejected++;

            button.OnPointerClick(new PointerEventData(null));

            Assert.AreEqual(1, clicked);
            Assert.AreEqual(0, rejected);
        }

        [Test]
        public void RightClicking_IsIgnoredEntirely()
        {
            // A right-click on a disabled button should not buzz at it.
            EnhancedButton button = CreateButton();
            button.interactable = false;

            int rejected = 0;
            button.Rejected += _ => rejected++;

            button.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });

            Assert.AreEqual(0, rejected);
        }

        [Test]
        public void DebugDescribe_MentionsTheResolvedState()
        {
            EnhancedButton button = CreateButton();
            button.Selected = true;

            string description = button.DebugDescribe();

            StringAssert.Contains("Selected", description);
            StringAssert.Contains(button.name, description);
        }
    }
}
