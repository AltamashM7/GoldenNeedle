using System.Collections;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Presentation
{
    /// <summary>
    /// Reusable visibility/fade behavior for an authored world-space presentation group.
    /// It changes only CanvasGroup state; the scene remains the transform/style authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldSpacePresentationGroup : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool initiallyVisible;
        [Min(0f), SerializeField] private float showDuration = 0.2f;
        [Min(0f), SerializeField] private float hideDuration = 0.2f;
        [SerializeField] private AnimationCurve showCurve = null;
        [SerializeField] private AnimationCurve hideCurve = null;
        [SerializeField] private bool blocksRaycastsWhenVisible;

        private Coroutine _fadeCoroutine;

        public CanvasGroup CanvasGroup => canvasGroup;
        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0f;

        private void Awake()
        {
            ResolveReferences();
            if (initiallyVisible)
            {
                ShowImmediate();
            }
            else
            {
                HideImmediate();
            }
        }

        private void OnDisable()
        {
            StopFade();
        }

        public void Show()
        {
            StartFade(1f, Mathf.Max(0f, showDuration), showCurve, true);
        }

        public void Hide()
        {
            StartFade(0f, Mathf.Max(0f, hideDuration), hideCurve, false);
        }

        public void ShowImmediate()
        {
            StopFade();
            ResolveReferences();
            ApplyAlpha(1f, true);
        }

        public void HideImmediate()
        {
            StopFade();
            ResolveReferences();
            ApplyAlpha(0f, false);
        }

        private void StartFade(float targetAlpha, float duration, AnimationCurve curve, bool visible)
        {
            StopFade();
            ResolveReferences();
            if (canvasGroup == null)
            {
                return;
            }

            ApplyRaycastState(visible);
            if (!isActiveAndEnabled || duration <= 0f)
            {
                ApplyAlpha(targetAlpha, visible);
                return;
            }

            _fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, duration, curve, visible));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration, AnimationCurve curve, bool visible)
        {
            var startAlpha = canvasGroup.alpha;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var eased = curve == null ? normalized : Mathf.Clamp01(curve.Evaluate(normalized));
                canvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, targetAlpha, eased);
                yield return null;
            }

            ApplyAlpha(targetAlpha, visible);
            _fadeCoroutine = null;
        }

        private void ApplyAlpha(float alpha, bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = Mathf.Clamp01(alpha);
            ApplyRaycastState(visible && canvasGroup.alpha > 0f);
        }

        private void ApplyRaycastState(bool visible)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.blocksRaycasts = visible && blocksRaycastsWhenVisible;
            canvasGroup.interactable = visible && blocksRaycastsWhenVisible;
        }

        private void StopFade()
        {
            if (_fadeCoroutine == null)
            {
                return;
            }

            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        private void ResolveReferences()
        {
            canvasGroup = canvasGroup == null ? GetComponent<CanvasGroup>() : canvasGroup;
        }
    }
}
