using UnityEngine;

namespace GoldenNeedle.Gameplay.Player
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GoldenNeedlePlayerSession : MonoBehaviour
    {
        public static GoldenNeedlePlayerSession Instance { get; private set; }
        public bool IsPersistentInstance => Instance == this;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
