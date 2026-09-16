using System;
using UnityEngine;

namespace GoldenNeedle.Gameplay.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Min(0.01f), SerializeField] private float maximumHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float MaximumHealth => maximumHealth;
        public float CurrentHealth => currentHealth;
        public float NormalizedHealth => maximumHealth <= 0f ? 0f : currentHealth / maximumHealth;
        public bool IsAlive => currentHealth > 0f;

        private void Awake()
        {
            Sanitize();
        }

        private void OnValidate()
        {
            Sanitize();
        }

        public void ApplyDamage(float amount)
        {
            if (!IsFinitePositive(amount) || currentHealth <= 0f)
            {
                return;
            }

            var wasAlive = IsAlive;
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            HealthChanged?.Invoke(currentHealth, maximumHealth);
            if (wasAlive && !IsAlive)
            {
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsFinitePositive(amount) || currentHealth >= maximumHealth)
            {
                return;
            }

            currentHealth = Mathf.Min(maximumHealth, currentHealth + amount);
            HealthChanged?.Invoke(currentHealth, maximumHealth);
        }

        public void RestoreHealth()
        {
            if (Mathf.Approximately(currentHealth, maximumHealth))
            {
                return;
            }

            currentHealth = maximumHealth;
            HealthChanged?.Invoke(currentHealth, maximumHealth);
        }

        private void Sanitize()
        {
            if (float.IsNaN(maximumHealth) || float.IsInfinity(maximumHealth) || maximumHealth <= 0f)
            {
                maximumHealth = 100f;
            }

            if (float.IsNaN(currentHealth) || float.IsInfinity(currentHealth))
            {
                currentHealth = maximumHealth;
            }

            currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
