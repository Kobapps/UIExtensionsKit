using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kobapps.UIExtensionsKit.Tests
{
    /// <summary>
    /// Guards the pump that advances every button animation.
    /// </summary>
    /// <remarks>
    /// The pump used to be a hidden GameObject created lazily by the first tween. Setting
    /// <c>HideFlags.HideAndDontSave</c> before <c>DontDestroyOnLoad</c> could leave it belonging to
    /// no scene, and a scene-less GameObject never receives <c>Update</c> — so animations froze
    /// mid-flight and only resumed when an unrelated press created a driver that happened to land in
    /// a live scene. It also leaked one orphan per play session. These pin down the properties that
    /// make that impossible.
    /// </remarks>
    public class TweenDriverTests
    {
        private const string DriverName = "Tween Driver";

        [SetUp]
        public void Reset() => NativeTweenRunner.ResetState();

        [TearDown]
        public void CleanUp() => NativeTweenRunner.ResetState();

        private static int DriverObjectCount() =>
            Resources.FindObjectsOfTypeAll<GameObject>().Count(go => go != null && go.name.Contains(DriverName));

        [Test]
        public void StartingATween_CreatesNoGameObject()
        {
            // The regression: the pump must not depend on an object that can be orphaned, destroyed
            // or left out of a scene.
            int before = DriverObjectCount();

            UITween.Animate(1f, UIEase.OutElastic, _ => { });
            UITween.Animate(0.5f, UIEase.OutBack, _ => { });

            Assert.AreEqual(before, DriverObjectCount(),
                "starting a tween must not spawn a driver GameObject");
        }

        [Test]
        public void QueuedTweens_AdvanceOnATickWithoutStartingAnotherTween()
        {
            // The user-visible symptom was that a stalled animation only resumed once some *other*
            // animation began. A tick alone must be enough.
            float value = -1f;
            UITween.Animate(1f, UIEase.Linear, t => value = t);

            UITween.ManualTick(0.25f);
            Assert.AreEqual(0.25f, value, 1e-3f);

            UITween.ManualTick(0.25f);
            Assert.AreEqual(0.5f, value, 1e-3f);
        }

        [Test]
        public void ElasticTween_OvershootsThenSettlesExactlyOnTarget()
        {
            // Elastic is the preset that was reported stuck, and its overshoot is the reason it must
            // never be clamped on the way through.
            float value = -1f;
            bool overshot = false;

            UITween.Animate(1f, UIEase.OutElastic, t =>
            {
                value = t;
                if (t > 1.001f) overshot = true;
            });

            for (int step = 0; step < 40; step++) UITween.ManualTick(0.05f);

            Assert.IsTrue(overshot, "OutElastic should pass above 1 on its way to rest");
            Assert.AreEqual(1f, value, 1e-4f, "and must settle exactly on the target");
            Assert.AreEqual(0, NativeTweenRunner.ActiveCount, "a finished tween must be drained");
        }

        [Test]
        public void ManyQueuedTweens_AllDrainOnTicks()
        {
            // A stalled pump used to accumulate tweens that then all released at once.
            for (int i = 0; i < 8; i++) UITween.Animate(0.2f, UIEase.OutQuad, _ => { });

            Assert.AreEqual(8, NativeTweenRunner.ActiveCount);

            for (int step = 0; step < 10; step++) UITween.ManualTick(0.05f);

            Assert.AreEqual(0, NativeTweenRunner.ActiveCount);
        }
    }
}
