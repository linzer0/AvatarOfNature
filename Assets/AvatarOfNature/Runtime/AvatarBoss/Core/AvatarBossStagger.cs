using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// M1: stagger meter fed from boss Health.OnDamaged. Decays after a delay.
    public class AvatarBossStagger : MonoBehaviour
    {
        [Header("Stagger")]
        [Tooltip("Meter value required for stagger break")]
        public float MaxStagger = 100f;

        [Tooltip("Effective stagger meter gained per post-multiplier damage point. Difficulty controller normalizes this to the 0.11 designer-scale target.")]
        public float StaggerGainPerDamage = 1.2f;

        [Header("Decay (does not run while the meter is full during break)")]
        public float DecayPerSecond = 8f;
        public float DecayDelay = 2.5f;

        public float CurrentStagger { get; private set; }
        public float Ratio => MaxStagger > 0f ? CurrentStagger / MaxStagger : 0f;
        public bool IsFull => CurrentStagger >= MaxStagger;

        public event System.Action OnStaggerFull;

        float m_LastDamageTime;
        bool m_Full;

        float m_MaxStaggerInit;
        float m_GainInit;
        float m_DecayInit;

        void Awake()
        {
            m_MaxStaggerInit = MaxStagger;
            m_GainInit = StaggerGainPerDamage;
            m_DecayInit = DecayPerSecond;
        }

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
            CurrentStagger = Mathf.Clamp(CurrentStagger + damage * StaggerGainPerDamage, 0f, MaxStagger);
            Debug.Log($"[AvatarOfNature] Stagger rawDamage={damage:F1} gainMultiplier={StaggerGainPerDamage:F2} " +
                      $"added={damage * StaggerGainPerDamage:F1} " +
                      $"meter=({CurrentStagger:F0}/{MaxStagger:F0}) " +
                      $"from {(damageSource != null ? damageSource.name : "null")}", this);

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

        /// <summary>Debug/test-only: adds raw stagger points directly to the meter.</summary>
        public void DebugAddStagger(float amount)
        {
            if (m_Full)
                return;

            m_LastDamageTime = Time.time;
            CurrentStagger = Mathf.Clamp(CurrentStagger + amount, 0f, MaxStagger);

            if (IsFull)
            {
                m_Full = true;
                OnStaggerFull?.Invoke();
            }
        }

        /// <summary>Debug/test-only: restores the serialized tunables (reversing any phase-2
        /// multipliers) and clears the meter.</summary>
        public void DebugReset()
        {
            MaxStagger = m_MaxStaggerInit;
            StaggerGainPerDamage = m_GainInit;
            DecayPerSecond = m_DecayInit;
            ResetStagger();
        }
    }
}
