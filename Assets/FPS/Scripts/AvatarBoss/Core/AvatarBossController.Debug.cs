using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    public partial class AvatarBossController
    {
        /// <summary>Debug/test-only: set boss health to a ratio of max (ignored while dead).</summary>
        public void DebugSetHealth(float ratio)
        {
            if (BossHealth == null || m_IsDead)
                return;
            BossHealth.CurrentHealth = Mathf.Clamp(BossHealth.MaxHealth * ratio, 0f, BossHealth.MaxHealth);
        }

        /// <summary>
        /// Debug/test-only reset to a clean phase-1 state without a scene reload.
        /// Production gameplay never calls this path.
        /// </summary>
        public void DebugResetBoss()
        {
            m_IsDead = false;
            PhaseTwo = false;

            if (HealingOrbs != null)
                HealingOrbs.ClearHealingOrbs();

            SummonsActive = false;

            if (m_VulnerabilityRoutine != null)
            {
                StopCoroutine(m_VulnerabilityRoutine);
                m_VulnerabilityRoutine = null;
            }

            if (BossHealth != null)
                BossHealth.ResetHealth();
            if (Stagger != null)
                Stagger.DebugReset();

            if (WeakPoints != null)
            {
                foreach (var weakPoint in WeakPoints)
                {
                    if (weakPoint != null)
                        weakPoint.DebugReset();
                }
            }

            Scheduler?.DebugReset();
            GetComponentInChildren<AvatarBossPhaseController>()?.DebugResetPhase();
            GetComponentInChildren<AvatarBossSummonController>()?.DebugReset();
            GetComponentInChildren<AvatarBossVisualEffects>()?.DebugReset();
            GetComponent<AvatarBossDuelController>()?.DebugReset();

            Debug.Log("[AvatarOfNature] Debug reset: boss restored to phase 1.", this);
        }
    }
}
