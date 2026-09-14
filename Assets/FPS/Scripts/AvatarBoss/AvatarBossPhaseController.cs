using System.Collections;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Phase 2 controller for the Avatar of Nature.
    /// One-shot guarded transition: phase already changed / transition running / boss dead.
    /// Applies phase-2 tunables to existing components, never repairs designer data.
    public class AvatarBossPhaseController : MonoBehaviour
    {
        [Header("Transition")]
        [Tooltip("Health ratio that triggers Phase 2")]
        public float PhaseTwoThreshold = 0.5f;

        [Tooltip("Invulnerability seconds during the transition (always cleared at the end)")]
        public float TransitionDuration = 3f;

        [Header("Phase 2 tunables (multipliers applied to existing values)")]
        [Tooltip("Scheduler wind-up / recover / attack cooldown multiplier")]
        public float TimingMultiplier = 0.7f;
        [Tooltip("Stagger gain per damage multiplier in Phase 2 (lower = more persistent boss)")]
        public float StaggerGainMultiplier = 0.75f;
        [Tooltip("Weak point vulnerability window duration in Phase 2 (direct value)")]
        public float PhaseTwoVulnerabilityDuration = 2.2f;

        [Header("Combo (fixed Shockwave ->ComboDelay-> Earth)")]
        public bool EnableCombo = true;

        public bool PhaseTwo { get; private set; }
        public bool TransitionInProgress { get; private set; }

        AvatarBossController m_Boss;
        AvatarBossAttackScheduler m_Scheduler;
        AvatarBossStagger m_Stagger;

        bool m_SchedulerTimingsOriginal;

        void Awake()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
        }

        void Start()
        {
            if (m_Boss == null)
                m_Boss = GetComponentInParent<AvatarBossController>();

            if (m_Boss == null || m_Boss.BossHealth == null)
            {
                Debug.LogError($"[{nameof(AvatarBossPhaseController)}] Requires AvatarBossController with Health in hierarchy.", this);
                return;
            }

            m_Boss.BossHealth.OnDie += OnBossDie;
            m_Boss.BossHealth.OnDamaged += OnBossDamaged;
        }

        void OnDestroy()
        {
            if (m_Boss != null && m_Boss.BossHealth != null)
            {
                m_Boss.BossHealth.OnDie -= OnBossDie;
                m_Boss.BossHealth.OnDamaged -= OnBossDamaged;
            }
        }

        void OnBossDamaged(float damage, GameObject damageSource)
        {
            // one-shot guard: already phase 2, transition running, or boss dead
            if (m_Boss.PhaseTwo || TransitionInProgress || m_Boss.IsDead)
                return;

            if (m_Boss.BossHealth.GetRatio() <= PhaseTwoThreshold)
                StartCoroutine(TransitionRoutine());
        }

        IEnumerator TransitionRoutine()
        {
            if (TransitionInProgress || m_Boss.PhaseTwo || m_Boss.IsDead)
                yield break;

            TransitionInProgress = true;
            Debug.Log("[AvatarOfNature] PHASE 2 TRANSITION begins — boss is invulnerable.", this);

            m_Boss.BossHealth.Invincible = true;

            if (m_Scheduler == null)
                m_Scheduler = m_Boss.Scheduler;
            if (m_Scheduler != null)
                m_Scheduler.Interrupt();

            yield return new WaitForSeconds(TransitionDuration);

            // boss may have died during transition: still clear invincibility
            m_Boss.BossHealth.Invincible = false;

            if (!m_Boss.IsDead)
                ApplyPhaseTwo();

            TransitionInProgress = false;
        }

        void ApplyPhaseTwo()
        {
            m_Boss.NotifyPhase2();

            if (m_Stagger == null)
                m_Stagger = m_Boss.Stagger;
            if (m_Stagger != null)
            {
                m_Stagger.StaggerGainPerDamage *= StaggerGainMultiplier;
                m_Stagger.DecayPerSecond *= StaggerGainMultiplier;
            }

            if (m_Scheduler != null)
            {
                if (!m_SchedulerTimingsOriginal)
                {
                    m_Scheduler.WindupTime *= TimingMultiplier;
                    m_Scheduler.RecoverTime *= TimingMultiplier;
                    m_Scheduler.AttackCooldown *= TimingMultiplier;
                    m_Scheduler.EnableCombo = EnableCombo;
                    m_SchedulerTimingsOriginal = true;
                }
            }

            foreach (var weakPoint in m_Boss.WeakPoints)
                weakPoint.PhaseTwoAttached = true;

            Debug.Log("[AvatarOfNature] PHASE 2 ACTIVE — timings accelerated, combo enabled, all weak points available.", this);
        }

        void OnBossDie()
        {
            // deterministic safety: never leave invincibility enabled on death (also during transition)
            m_Boss.BossHealth.Invincible = false;
            TransitionInProgress = false;
            Debug.Log("[AvatarOfNature] Phase controller: boss death cleanup.", this);
        }

        /// <summary>Test hook: forces transition decision from current health ratio.</summary>
        public void ForceEvaluateTransition()
        {
            OnBossDamaged(0f, null);
        }
    }
}
