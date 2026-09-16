using System.Collections;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Presentation
{
    /// <summary>
    /// Small authored camera-pose adapter. The scene owns poseAnchor and the Inspector owns FOV.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PresentationCameraRig : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform poseAnchor;
        [SerializeField] private float fieldOfView = 60f;
        [SerializeField] private bool snapOnStart = true;
        [Min(0f), SerializeField] private float transitionSeconds;
        [SerializeField] private AnimationCurve easing = null;

        private Coroutine _transitionCoroutine;

        private void Start()
        {
            ResolveReferences();
            if (snapOnStart)
            {
                ApplyPose();
            }
        }

        public void ApplyPose()
        {
            ResolveReferences();
            if (targetCamera == null || poseAnchor == null)
            {
                return;
            }

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            targetCamera.fieldOfView = fieldOfView;
            if (!Application.isPlaying || transitionSeconds <= 0f)
            {
                targetCamera.transform.SetPositionAndRotation(
                    poseAnchor.position,
                    poseAnchor.rotation);
                return;
            }

            _transitionCoroutine = StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            var startPosition = targetCamera.transform.position;
            var startRotation = targetCamera.transform.rotation;
            var elapsed = 0f;
            while (elapsed < transitionSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / transitionSeconds);
                var eased = easing == null ? normalized : Mathf.Clamp01(easing.Evaluate(normalized));
                targetCamera.transform.position = Vector3.LerpUnclamped(
                    startPosition,
                    poseAnchor.position,
                    eased);
                targetCamera.transform.rotation = Quaternion.SlerpUnclamped(
                    startRotation,
                    poseAnchor.rotation,
                    eased);
                yield return null;
            }

            targetCamera.transform.SetPositionAndRotation(
                poseAnchor.position,
                poseAnchor.rotation);
            _transitionCoroutine = null;
        }

        private void ResolveReferences()
        {
            targetCamera = targetCamera == null ? GetComponent<Camera>() : targetCamera;
        }
    }
}
