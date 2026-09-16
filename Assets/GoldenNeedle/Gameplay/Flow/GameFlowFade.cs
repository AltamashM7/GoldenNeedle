using System.Collections;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Flow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GameFlowFade : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [Min(0f), SerializeField] private float fadeOutSeconds = 0.25f;
        [Min(0f), SerializeField] private float fadeInSeconds = 0.25f;

        public float Alpha => ResolveCanvasGroup() == null ? 0f : canvasGroup.alpha;

        private void Awake()
        {
            ResolveCanvasGroup();
        }

        private void OnValidate()
        {
            fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
            fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
            ResolveCanvasGroup();
        }

        public IEnumerator FadeOut()
        {
            yield return FadeTo(1f, fadeOutSeconds);
        }

        public IEnumerator FadeIn()
        {
            yield return FadeTo(0f, fadeInSeconds);
        }

        public void SetFadeImmediate(float alpha)
        {
            var group = ResolveCanvasGroup();
            if (group == null)
            {
                return;
            }

            group.alpha = Mathf.Clamp01(alpha);
            group.blocksRaycasts = group.alpha > 0.0001f;
            group.interactable = false;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            var group = ResolveCanvasGroup();
            if (group == null)
            {
                yield break;
            }

            targetAlpha = Mathf.Clamp01(targetAlpha);
            var startAlpha = group.alpha;
            group.blocksRaycasts = true;
            group.interactable = false;

            if (duration <= 0f || Mathf.Approximately(startAlpha, targetAlpha))
            {
                group.alpha = targetAlpha;
                group.blocksRaycasts = targetAlpha > 0.0001f;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            group.alpha = targetAlpha;
            group.blocksRaycasts = targetAlpha > 0.0001f;
        }

        private CanvasGroup ResolveCanvasGroup()
        {
            canvasGroup = canvasGroup == null ? GetComponent<CanvasGroup>() : canvasGroup;
            return canvasGroup;
        }
    }
}
