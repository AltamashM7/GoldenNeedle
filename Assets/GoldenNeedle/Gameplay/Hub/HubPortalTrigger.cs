using GoldenNeedle.Gameplay.Flow;
using GoldenNeedle.Gameplay.Player;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Hub
{
    /// <summary>
    /// Polls the persistent Golden Needle player root against a scene-owned trigger volume.
    /// The volume is intentionally separate from the portal presentation prefab because the
    /// player does not need a physics collider and portal visuals should remain art-owned.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HubPortalTrigger : MonoBehaviour
    {
        [Header("Trigger volume")]
        [SerializeField] private BoxCollider triggerVolume;

        [Header("Transition")]
        [SerializeField] private bool transitionEnabled = true;
        [SerializeField] private string destinationSceneName = string.Empty;
        [SerializeField] private string destinationSpawnId = string.Empty;

        private bool _wasInside;
        private bool _transitionInFlight;
        private bool _warnedMissingVolume;
        private bool _warnedInvalidVolume;
        private bool _warnedMissingSession;
        private bool _warnedMissingFacadeRoot;
        private bool _warnedInvalidDestination;
        private bool _warnedMissingFlow;

        public BoxCollider TriggerVolume => triggerVolume;
        public bool TransitionEnabled => transitionEnabled;
        public string DestinationSceneName => destinationSceneName;
        public string DestinationSpawnId => destinationSpawnId;

        private void Awake()
        {
            ResolveTriggerVolume();
        }

        private void OnEnable()
        {
            _wasInside = false;
            _transitionInFlight = false;
        }

        private void Update()
        {
            ResolveTriggerVolume();
            if (triggerVolume == null)
            {
                WarnOnce(ref _warnedMissingVolume, "Hub portal trigger has no BoxCollider volume and will remain inert.");
                return;
            }

            if (!triggerVolume.enabled || !IsValidVolume(triggerVolume))
            {
                WarnOnce(ref _warnedInvalidVolume, "Hub portal trigger has a disabled or non-positive BoxCollider volume and will remain inert.");
                return;
            }

            var session = GoldenNeedlePlayerSession.Instance;
            if (session == null || !session.IsPersistentInstance)
            {
                _wasInside = false;
                WarnOnce(ref _warnedMissingSession, "Hub portal trigger is waiting for the persistent GoldenNeedlePlayerSession.");
                return;
            }

            var facade = session.GetComponent<GoldenNeedlePlayerFacade>();
            var playerRoot = facade == null ? null : facade.PlayerRoot;
            if (playerRoot == null)
            {
                _wasInside = false;
                WarnOnce(ref _warnedMissingFacadeRoot, "Hub portal trigger is waiting for the persistent player facade and PlayerRoot.");
                return;
            }

            var inside = IsWorldPointInside(triggerVolume, playerRoot.position);
            if (!inside)
            {
                _wasInside = false;
                _transitionInFlight = false;
                return;
            }

            if (_transitionInFlight)
            {
                var flow = GameFlowManager.Instance;
                if (flow != null && flow.IsTransitioning)
                {
                    return;
                }

                // A failed/rejected request is recoverable, but the player must leave and
                // re-enter before another request is permitted.
                _transitionInFlight = false;
            }

            if (_wasInside)
            {
                return;
            }

            // Consume the enter edge before validating the request so a bad or rejected
            // configuration cannot produce a per-frame request loop.
            _wasInside = true;
            if (!transitionEnabled)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(destinationSceneName) ||
                string.IsNullOrWhiteSpace(destinationSpawnId))
            {
                WarnOnce(
                    ref _warnedInvalidDestination,
                    "Enabled Hub portal trigger has no complete destination scene/spawn configuration.");
                return;
            }

            var gameFlow = GameFlowManager.Instance;
            if (gameFlow == null || !gameFlow.isActiveAndEnabled)
            {
                WarnOnce(ref _warnedMissingFlow, "Hub portal trigger requires an active GameFlowManager and will remain inert.");
                return;
            }

            if (gameFlow.IsTransitioning)
            {
                return;
            }

            _transitionInFlight = gameFlow.TryTransitionTo(
                destinationSceneName.Trim(),
                destinationSpawnId.Trim());
        }

        public static bool IsWorldPointInside(BoxCollider box, Vector3 worldPoint)
        {
            if (box == null)
            {
                return false;
            }

            var localPoint = box.transform.InverseTransformPoint(worldPoint) - box.center;
            var halfSize = box.size * 0.5f;
            return Mathf.Abs(localPoint.x) <= halfSize.x &&
                   Mathf.Abs(localPoint.y) <= halfSize.y &&
                   Mathf.Abs(localPoint.z) <= halfSize.z;
        }

        private void ResolveTriggerVolume()
        {
            if (triggerVolume == null)
            {
                triggerVolume = GetComponent<BoxCollider>();
            }
        }

        private static bool IsValidVolume(BoxCollider box)
        {
            return box.size.x > 0f && box.size.y > 0f && box.size.z > 0f;
        }

        private static void WarnOnce(ref bool warned, string message)
        {
            if (warned)
            {
                return;
            }

            warned = true;
            UnityEngine.Debug.LogWarning(message);
        }
    }
}
