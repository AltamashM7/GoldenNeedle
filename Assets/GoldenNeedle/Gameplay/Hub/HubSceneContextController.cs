using System.Collections;
using GoldenNeedle.Gameplay.Commands;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Hub
{
    /// <summary>
    /// Scene-local Hub entry hook for the persistent production command host.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubSceneContextController : MonoBehaviour
    {
        private bool _applied;

        private void OnEnable()
        {
            StartCoroutine(ApplyWhenAvailable());
        }

        private IEnumerator ApplyWhenAvailable()
        {
            while (isActiveAndEnabled && !_applied)
            {
                var host = FindAnyObjectByType<GameplayCommandHost>();
                if (host != null)
                {
                    host.SetContext(GameplayCommandContext.Hub);
                    _applied = true;
                    yield break;
                }

                yield return null;
            }
        }
    }
}
