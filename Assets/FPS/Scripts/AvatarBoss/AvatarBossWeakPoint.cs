using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// M1: wraps a Damageable on a child collider and swaps its multiplier
    /// between hidden and exposed states.
    [RequireComponent(typeof(Damageable))]
    public class AvatarBossWeakPoint : MonoBehaviour
    {
        [Tooltip("Damageable.DamageMultiplier while exposed")]
        public float ExposedMultiplier = 2f;

        public bool IsExposed { get; private set; }

        Damageable m_Damageable;
        float m_Multiplier;

        void Awake()
        {
            m_Damageable = GetComponent<Damageable>();
            m_Multiplier = m_Damageable.DamageMultiplier;
            SetExposed(false);
        }

        public void SetExposed(bool exposed)
        {
            IsExposed = exposed;
            m_Damageable.DamageMultiplier = exposed ? ExposedMultiplier : m_Multiplier;

            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = exposed;
        }
    }
}
