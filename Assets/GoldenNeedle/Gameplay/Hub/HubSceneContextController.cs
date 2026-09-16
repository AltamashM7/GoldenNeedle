using System.Collections;
using GoldenNeedle.Gameplay.Commands;
using GoldenNeedle.Gameplay.Player;
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
                var session = GoldenNeedlePlayerSession.Instance;
                var facade = session == null
                    ? null
                    : session.GetComponent<GoldenNeedlePlayerFacade>();
                if (host != null && facade != null)
                {
                    host.SetContext(GameplayCommandContext.Hub);
                    facade.SetAvatarAnimationAuthorityEnabled(false);
                    facade.SetAvatarPoseDriveEnabled(true);
                    facade.SetLocomotionEnabled(true);
                    _applied = true;
                    yield break;
                }

                yield return null;
            }
        }
    }
}
