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

        [Tooltip("Damageable.DamageMultiplier while the window is closed (collider is disabled, kept as a serialized safety floor)")]
        public float ClosedDamageMultiplier = 0.12f;

        [Tooltip("Priority over the overlapping BossBody collider while this weak point is exposed.")]
        public int HitPriority = 100;

        [Tooltip("True = weak point available only from Phase 2 (WeakPointB)")]
        public bool PhaseTwoAttached = false;

        public bool IsExposed { get; private set; }

        Damageable m_Damageable;
        float m_Multiplier;
        bool m_PhaseTwoAttachedInit;

        void Awake()
        {
            m_PhaseTwoAttachedInit = PhaseTwoAttached;
            m_Damageable = GetComponent<Damageable>();
            m_Multiplier = m_Damageable.DamageMultiplier;
            m_Damageable.HitPriority = HitPriority;
            SetExposed(false);
        }

        public void SetExposed(bool exposed)
        {
            IsExposed = exposed;
            if (m_Damageable == null)
            {
                // lazy init: Awake may not run in edit-mode tools/tests
                m_Damageable = GetComponent<Damageable>();
                m_Multiplier = m_Damageable != null ? m_Damageable.DamageMultiplier : 1f;
            }
            if (m_Damageable != null)
            {
                m_Damageable.HitPriority = HitPriority;
                m_Damageable.DamageMultiplier = exposed ? ExposedMultiplier : ClosedDamageMultiplier;
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = exposed;

            // weak point VFX only show while the window is open
            var renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.enabled = exposed;
        }

        /// <summary>Debug/test-only: restore the designer PhaseTwoAttached value and close the point.</summary>
        public void DebugReset()
        {
            PhaseTwoAttached = m_PhaseTwoAttachedInit;
            SetExposed(false);
        }
    }
}
