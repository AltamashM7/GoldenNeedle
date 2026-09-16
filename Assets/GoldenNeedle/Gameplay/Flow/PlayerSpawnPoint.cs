using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldenNeedle.Gameplay.Flow
{
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnId = "Default";

        public string SpawnId => spawnId;
        public Transform PlacementTransform => transform;
        public Pose PlacementPose => new Pose(transform.position, transform.rotation);

        private void OnValidate()
        {
            spawnId = spawnId == null ? string.Empty : spawnId.Trim();
        }

        private void OnDrawGizmos()
        {
            var previousColor = Gizmos.color;
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.color = previousColor;
        }
    }

    public static class PlayerSpawnPointResolver
    {
        public static bool TryResolve(
            Scene destinationScene,
            string requestedSpawnId,
            out PlayerSpawnPoint spawnPoint,
            out string error)
        {
            spawnPoint = null;
            error = string.Empty;

            if (!destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                error = "Destination scene is not valid and loaded.";
                return false;
            }

            var normalizedId = requestedSpawnId == null ? string.Empty : requestedSpawnId.Trim();
            if (string.IsNullOrEmpty(normalizedId))
            {
                error = "A non-empty spawn ID is required for spawn resolution.";
                return false;
            }

            var matchCount = 0;
            var roots = destinationScene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var points = roots[rootIndex].GetComponentsInChildren<PlayerSpawnPoint>(true);
                for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
                {
                    var candidate = points[pointIndex];
                    if (candidate == null ||
                        !string.Equals(candidate.SpawnId, normalizedId, System.StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matchCount++;
                    spawnPoint = candidate;
                }
            }

            if (matchCount == 1)
            {
                return true;
            }

            spawnPoint = null;
            error = matchCount == 0
                ? $"Spawn ID '{normalizedId}' was not found in destination scene '{destinationScene.name}'."
                : $"Spawn ID '{normalizedId}' is duplicated {matchCount} times in destination scene '{destinationScene.name}'.";
            return false;
        }
    }
}
