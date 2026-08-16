using UnityEngine;

namespace Kobapps.UIExtensionsKit
{
    /// <summary>
    /// Easing curves the kit animates with. Deliberately a small, backend-agnostic set: every entry
    /// maps cleanly onto DOTween's <c>Ease</c> as well as the built-in runner, so switching backends
    /// never changes how a button feels.
    /// </summary>
    public enum UIEase
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        InSine,
        OutSine,
        InOutSine,
        InBack,
        OutBack,
        InOutBack,
        OutElastic,
        OutBounce,

        /// <summary>
        /// Use the pose's own <see cref="ButtonStateMotion.curve"/> instead of a named curve.
        /// </summary>
        /// <remarks>
        /// The named curves cover almost everything, but "almost" is not "all" — a bespoke anticipate
        /// or a two-stage settle has no entry here and never will. This lets a preset carry a hand
        /// drawn <see cref="UnityEngine.AnimationCurve"/> without the kit growing an enum entry per
        /// project.
        /// </remarks>
        Custom,
    }

    /// <summary>Evaluates <see cref="UIEase"/> curves. Pure maths — no allocation, safe off the main thread.</summary>
    public static class UIEasing
    {
        // The classic Penner overshoot constant, and its InOut variant (c1 * 1.525).
        private const float Back1 = 1.70158f;
        private const float Back2 = Back1 * 1.525f;

        /// <summary>
        /// Evaluate <paramref name="ease"/> at normalized time <paramref name="t"/>. The input is
        /// clamped to 0..1; the <b>output</b> is not — Back and Elastic intentionally overshoot
        /// outside that range, which is exactly what makes them read as bouncy.
        /// </summary>
        public static float Evaluate(UIEase ease, float t)
        {
            t = Mathf.Clamp01(t);

            switch (ease)
            {
                case UIEase.Linear: return t;

                case UIEase.InQuad: return t * t;
                case UIEase.OutQuad: return 1f - (1f - t) * (1f - t);
                case UIEase.InOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;

                case UIEase.InCubic: return t * t * t;
                case UIEase.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case UIEase.InOutCubic:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

                case UIEase.InSine: return 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
                case UIEase.OutSine: return Mathf.Sin(t * Mathf.PI * 0.5f);
                case UIEase.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

                case UIEase.InBack:
                    return (Back1 + 1f) * t * t * t - Back1 * t * t;
                case UIEase.OutBack:
                {
                    float p = t - 1f;
                    return 1f + (Back1 + 1f) * p * p * p + Back1 * p * p;
                }
                case UIEase.InOutBack:
                    return t < 0.5f
                        ? Mathf.Pow(2f * t, 2f) * ((Back2 + 1f) * 2f * t - Back2) * 0.5f
                        : (Mathf.Pow(2f * t - 2f, 2f) * ((Back2 + 1f) * (t * 2f - 2f) + Back2) + 2f) * 0.5f;

                case UIEase.OutElastic:
                {
                    if (t <= 0f) return 0f;
                    if (t >= 1f) return 1f;
                    const float period = (2f * Mathf.PI) / 3f;
                    return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * period) + 1f;
                }

                case UIEase.OutBounce: return OutBounce(t);

                default: return t;
            }
        }

        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
