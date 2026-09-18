using System.Collections;
using System.Collections.Generic;
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
        [Tooltip("Scheduler wind-up / recover / attack cooldown multiplier (floors enforce the exact values)")]
        public float TimingMultiplier = 0.77f;
        [Tooltip("Attack telegraph time multiplier (floors enforce the exact values)")]
        public float TelegraphMultiplier = 0.8f;
        [Tooltip("Stagger gain per damage multiplier in Phase 2 (lower = more persistent boss)")]
        public float StaggerGainMultiplier = 0.75f;
        [Tooltip("Stagger threshold multiplier in Phase 2 (must keep the break reachable before death)")]
        public float StaggerThresholdMultiplier = 0.6f;
        [Tooltip("Stagger decay speed multiplier in Phase 2")]
        public float StaggerDecayMultiplier = 1.25f;
        [Tooltip("Stagger decay delay multiplier in Phase 2")]
        public float StaggerDecayDelayMultiplier = 0.8f;
        [Tooltip("Weak point vulnerability window duration in Phase 2 (direct value)")]
        public float PhaseTwoVulnerabilityDuration = 2.5f;
        [Tooltip("Meteor Rain damage in Phase 2 (direct value)")]
        public float PhaseTwoMeteorDamage = 22f;
        [Tooltip("Shockwave wave speed in Phase 2 (direct value)")]
        public float PhaseTwoShockwaveSpeed = 24f;

        [Header("Phase 2 floors (timings never below these)")]
        public float PhaseTwoMinCooldown = 2.1f;
        public float PhaseTwoMinTelegraph = 1.3f;
        public float PhaseTwoMinWindup = 0.7f;
        public float PhaseTwoMinRecover = 1.2f;

        [Header("Combo (fixed Shockwave ->ComboDelay-> Earth)")]
        public bool EnableCombo = true;

        public bool PhaseTwo { get; private set; }
        public bool TransitionInProgress { get; private set; }

        AvatarBossController m_Boss;
        AvatarBossAttackScheduler m_Scheduler;
        AvatarBossStagger m_Stagger;
        AvatarBossAttack[] m_Attacks;
        readonly List<float> m_AttackTelegraphOriginals = new List<float>();
        readonly List<float> m_AttackCooldownOriginals = new List<float>();
        readonly List<float> m_AttackDamageOriginals = new List<float>();
        AvatarBossShockwaveAttack m_ShockwaveAttack;
        float m_ShockwaveSpeedOriginal;
        float m_BossVulnerabilityOriginal;
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

            var cues = GetComponentInParent<AvatarBossAudioCues>();
            if (cues != null)
                cues.Play(AvatarBossCue.PhaseTwo);

            if (m_Stagger == null)
                m_Stagger = m_Boss.Stagger;
            if (m_Stagger != null)
            {
                m_Stagger.StaggerGainPerDamage *= StaggerGainMultiplier;
                m_Stagger.DecayPerSecond *= StaggerDecayMultiplier;
                m_Stagger.DecayDelay *= StaggerDecayDelayMultiplier;
                m_Stagger.MaxStagger *= StaggerThresholdMultiplier;
            }

            if (m_Scheduler != null)
            {
                if (!m_SchedulerTimingsOriginal)
                {
                    m_Scheduler.WindupTime = Mathf.Max(
                        m_Scheduler.WindupTime * TimingMultiplier, PhaseTwoMinWindup);
                    m_Scheduler.RecoverTime = Mathf.Max(
                        m_Scheduler.RecoverTime * TimingMultiplier, PhaseTwoMinRecover);
                    m_Scheduler.AttackCooldown = Mathf.Max(
                        m_Scheduler.AttackCooldown * TimingMultiplier, PhaseTwoMinCooldown);

                    m_BossVulnerabilityOriginal = m_Boss.VulnerabilityDuration;
                    m_Boss.VulnerabilityDuration = PhaseTwoVulnerabilityDuration;

                    m_Attacks = m_Boss.GetComponentsInChildren<AvatarBossAttack>();
                    m_AttackTelegraphOriginals.Clear();
                    m_AttackCooldownOriginals.Clear();
                    m_AttackDamageOriginals.Clear();
                    foreach (var attack in m_Attacks)
                    {
                        m_AttackTelegraphOriginals.Add(attack.TelegraphTime);
                        m_AttackCooldownOriginals.Add(attack.Cooldown);
                        m_AttackDamageOriginals.Add(attack.Damage);
                        attack.TelegraphTime = Mathf.Max(
                            attack.TelegraphTime * TelegraphMultiplier, PhaseTwoMinTelegraph);
                        attack.Cooldown = Mathf.Max(
                            attack.Cooldown * TimingMultiplier, PhaseTwoMinCooldown);

                        if (attack is AvatarBossFireAttack)
                            attack.Damage = PhaseTwoMeteorDamage;
                        if (attack is AvatarBossShockwaveAttack shock)
                        {
                            m_ShockwaveAttack = shock;
                            m_ShockwaveSpeedOriginal = shock.WaveSpeed;
                            shock.WaveSpeed = PhaseTwoShockwaveSpeed;
                        }
                    }

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

        /// <summary>Debug/test-only: reset phase-2 state so the boss can re-enter phase 1.
        /// Stops any in-progress transition coroutine, clears invincibility and restores
        /// all phase-2 tweaked timings back to their phase-1 originals.</summary>
        public void DebugResetPhase()
        {
            StopAllCoroutines();
            PhaseTwo = false;
            TransitionInProgress = false;

            if (m_Scheduler != null)
            {
                if (m_Attacks != null && m_AttackTelegraphOriginals.Count == m_Attacks.Length)
                {
                    for (int i = 0; i < m_Attacks.Length; i++)
                    {
                        m_Attacks[i].TelegraphTime = m_AttackTelegraphOriginals[i];
                        m_Attacks[i].Cooldown = m_AttackCooldownOriginals[i];
                        m_Attacks[i].Damage = m_AttackDamageOriginals[i];
                    }
                }
                if (m_ShockwaveAttack != null)
                    m_ShockwaveAttack.WaveSpeed = m_ShockwaveSpeedOriginal;
            }

            if (m_Boss != null)
            {
                if (m_BossVulnerabilityOriginal > 0f)
                    m_Boss.VulnerabilityDuration = m_BossVulnerabilityOriginal;
                if (m_Boss.BossHealth != null)
                    m_Boss.BossHealth.Invincible = false;
            }

            m_AttackTelegraphOriginals.Clear();
            m_AttackCooldownOriginals.Clear();
            m_AttackDamageOriginals.Clear();
            m_ShockwaveAttack = null;
            m_ShockwaveSpeedOriginal = 0f;
            m_BossVulnerabilityOriginal = 0f;
            m_SchedulerTimingsOriginal = false;
        }
    }
}
