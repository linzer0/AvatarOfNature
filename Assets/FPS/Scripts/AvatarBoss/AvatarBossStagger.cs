using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// M1: stagger meter fed from boss Health.OnDamaged. Decays after a delay.
    public class AvatarBossStagger : MonoBehaviour
    {
        [Header("Stagger")]
        [Tooltip("Meter value required for stagger break")]
        public float MaxStagger = 100f;

        [Tooltip("Stagger meter gained per point of damage dealt to any part")]
        public float StaggerGainPerDamage = 0.5f;

        [Header("Decay (does not run while the meter is full during break)")]
        public float DecayPerSecond = 5f;
        public float DecayDelay = 2f;

        public float CurrentStagger { get; private set; }
        public float Ratio => MaxStagger > 0f ? CurrentStagger / MaxStagger : 0f;
        public bool IsFull => CurrentStagger >= MaxStagger;

        public event System.Action OnStaggerFull;

        float m_LastDamageTime;
        bool m_Full;

        void Update()
        {
            if (m_Full || Time.time - m_LastDamageTime < DecayDelay)
                return;

            if (CurrentStagger > 0f)
                CurrentStagger = Mathf.Max(0f, CurrentStagger - DecayPerSecond * Time.deltaTime);
        }

        public void AddStagger(float damage, GameObject damageSource)
        {
            if (m_Full)
                return;

            m_LastDamageTime = Time.time;
            CurrentStagger = Mathf.Min(MaxStagger, CurrentStagger + damage * StaggerGainPerDamage);
            Debug.Log($"[AvatarOfNature] Stagger +{damage * StaggerGainPerDamage:F1} " +
                      $"({CurrentStagger:F0}/{MaxStagger:F0}) from {(damageSource != null ? damageSource.name : "null")}", this);

            if (IsFull)
            {
                m_Full = true;
                OnStaggerFull?.Invoke();
            }
        }

        public void ResetStagger()
        {
            CurrentStagger = 0f;
            m_Full = false;
            m_LastDamageTime = -999f;
        }
    }
}
