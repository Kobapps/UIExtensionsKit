using NUnit.Framework;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// The built-in backend is steppable by hand, which makes it testable without waiting on frames.
    /// These cover the timing rules every animation in the kit depends on.
    /// </summary>
    public class NativeTweenTests
    {
        [SetUp]
        public void ResetRunner() => NativeTweenRunner.ResetState();

        [TearDown]
        public void CleanUp() => NativeTweenRunner.ResetState();

        [Test]
        public void Animate_SeedsStartValueImmediately()
        {
            // Without this the button would hold its previous pose for a frame before the first tick.
            float value = -1f;
            UITween.Animate(1f, UIEase.Linear, t => value = t);

            Assert.AreEqual(0f, value, 1e-4f);
        }

        [Test]
        public void Animate_ProgressesAndCompletes()
        {
            float value = -1f;
            bool completed = false;

            IUITweenHandle handle = UITween.Animate(1f, UIEase.Linear, t => value = t, onComplete: () => completed = true);
            Assert.IsTrue(handle.IsActive);

            UITween.ManualTick(0.5f);
            Assert.AreEqual(0.5f, value, 1e-3f);
            Assert.IsFalse(completed);

            UITween.ManualTick(0.6f);
            Assert.AreEqual(1f, value, 1e-4f);
            Assert.IsTrue(completed);
            Assert.IsFalse(handle.IsActive, "a finished tween should not stay active");
        }

        [Test]
        public void Animate_WithZeroDurationAndNoDelay_ResolvesWithoutATick()
        {
            float value = -1f;
            bool completed = false;

            IUITweenHandle handle = UITween.Animate(0f, UIEase.Linear, t => value = t, onComplete: () => completed = true);

            Assert.AreEqual(1f, value, 1e-4f);
            Assert.IsTrue(completed);
            Assert.IsFalse(handle.IsActive);
        }

        [Test]
        public void Animate_RespectsDelay_AndSpendsTheRemainderOnTheTween()
        {
            float value = -1f;
            UITween.Animate(1f, UIEase.Linear, t => value = t, delay: 0.5f);

            Assert.AreEqual(-1f, value, 1e-4f, "a delayed tween must not seed its start value yet");

            UITween.ManualTick(0.25f);
            Assert.AreEqual(-1f, value, 1e-4f, "still inside the delay");

            // 0.75 total: 0.5 of delay, then 0.25 of the tween — the leftover must not be discarded.
            UITween.ManualTick(0.5f);
            Assert.AreEqual(0.25f, value, 1e-3f);
        }

        [Test]
        public void Kill_WithoutComplete_StopsWithoutFiringCallbacks()
        {
            float value = -1f;
            bool completed = false;

            IUITweenHandle handle = UITween.Animate(1f, UIEase.Linear, t => value = t, onComplete: () => completed = true);
            UITween.ManualTick(0.25f);
            handle.Kill();

            UITween.ManualTick(1f);

            Assert.AreEqual(0.25f, value, 1e-3f, "killed tween should not have advanced further");
            Assert.IsFalse(completed);
            Assert.IsFalse(handle.IsActive);
        }

        [Test]
        public void Kill_WithComplete_JumpsToTheEnd()
        {
            float value = -1f;
            bool completed = false;

            IUITweenHandle handle = UITween.Animate(1f, UIEase.Linear, t => value = t, onComplete: () => completed = true);
            handle.Kill(complete: true);

            Assert.AreEqual(1f, value, 1e-4f);
            Assert.IsTrue(completed);
        }

        [Test]
        public void ActiveCount_DropsWhenTweensFinish()
        {
            UITween.Animate(1f, UIEase.Linear, _ => { });
            UITween.Animate(1f, UIEase.Linear, _ => { });
            Assert.AreEqual(2, NativeTweenRunner.ActiveCount);

            UITween.ManualTick(1.5f);
            Assert.AreEqual(0, NativeTweenRunner.ActiveCount);
        }

        [Test]
        public void Tick_ToleratesCallbacksThatStartNewTweens()
        {
            // Chaining from a callback mutates the active list mid-tick; iterating it directly would throw.
            bool chained = false;

            UITween.Animate(0.5f, UIEase.Linear, _ => { }, onComplete: () =>
            {
                UITween.Animate(0.5f, UIEase.Linear, _ => chained = true);
            });

            Assert.DoesNotThrow(() => UITween.ManualTick(0.6f));
            Assert.IsTrue(chained);
        }

        [Test]
        public void Tick_ToleratesCallbacksThatKillOtherTweens()
        {
            IUITweenHandle second = null;

            UITween.Animate(0.5f, UIEase.Linear, _ => second?.Kill());
            second = UITween.Animate(0.5f, UIEase.Linear, _ => { });

            Assert.DoesNotThrow(() => UITween.ManualTick(0.1f));
            Assert.IsFalse(second.IsActive);
        }

        [Test]
        public void OutsidePlayMode_TheActiveBackendIsAlwaysTheBuiltInOne()
        {
            // Third-party engines have no edit-mode clock, so a preview handed to one would freeze.
            Assert.AreEqual(UITween.NativeId, UITween.Active.Id);
        }
    }
}
