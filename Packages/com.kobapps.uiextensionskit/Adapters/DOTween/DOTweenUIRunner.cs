// This whole assembly only compiles when UIEXTENSIONSKIT_DOTWEEN is defined (see the asmdef's define
// constraint), which the user enables from Tools ▸ UIExtensionsKit ▸ Settings once DOTween is
// installed. It uses only DOTween's CORE api (DOTween.To) — never the extension "modules"
// (DOScale/DOFade/…) — so it needs no DOTween module setup and can't break on a partial install.
using System;
using DG.Tweening;
using UnityEngine;

namespace Kobapps.UIExtensionsKit.Adapters
{
    /// <summary>
    /// DOTween-backed animation runner. Registers itself on load, so selecting "dotween" as the
    /// backend in Settings is all that's needed to route every button through DOTween.
    /// </summary>
    public sealed class DOTweenUIRunner : IUITweenRunner
    {
        public string Id => UITween.DOTweenId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnLoad() => UITween.Register(new DOTweenUIRunner());

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor() => UITween.Register(new DOTweenUIRunner());
#endif

        public IUITweenHandle Animate(
            float duration,
            UIEase ease,
            Action<float> onUpdate,
            float delay = 0f,
            Action onComplete = null,
            bool unscaledTime = true)
        {
            float value = 0f;

            // DOTween applies the ease to the value itself, so `onUpdate` receives eased progress —
            // the same contract the built-in runner honours.
            Tween tween = DOTween
                .To(() => value, x => { value = x; onUpdate?.Invoke(x); }, 1f, duration)
                .SetEase(Map(ease))
                .SetUpdate(unscaledTime);

            if (delay > 0f) tween.SetDelay(delay);
            if (onComplete != null) tween.OnComplete(() => onComplete());

            return new DOTweenUIHandle(tween);
        }

        internal static Ease Map(UIEase ease)
        {
            switch (ease)
            {
                case UIEase.InQuad: return Ease.InQuad;
                case UIEase.OutQuad: return Ease.OutQuad;
                case UIEase.InOutQuad: return Ease.InOutQuad;
                case UIEase.InCubic: return Ease.InCubic;
                case UIEase.OutCubic: return Ease.OutCubic;
                case UIEase.InOutCubic: return Ease.InOutCubic;
                case UIEase.InSine: return Ease.InSine;
                case UIEase.OutSine: return Ease.OutSine;
                case UIEase.InOutSine: return Ease.InOutSine;
                case UIEase.InBack: return Ease.InBack;
                case UIEase.OutBack: return Ease.OutBack;
                case UIEase.InOutBack: return Ease.InOutBack;
                case UIEase.OutElastic: return Ease.OutElastic;
                case UIEase.OutBounce: return Ease.OutBounce;
                default: return Ease.Linear;
            }
        }
    }

    /// <summary>Wraps a DOTween <see cref="Tween"/> as a backend-agnostic handle.</summary>
    internal sealed class DOTweenUIHandle : IUITweenHandle
    {
        private readonly Tween _tween;

        internal DOTweenUIHandle(Tween tween) { _tween = tween; }

        public bool IsActive => _tween != null && _tween.IsActive();

        public void Kill(bool complete = false)
        {
            if (_tween != null && _tween.IsActive()) _tween.Kill(complete);
        }
    }
}
