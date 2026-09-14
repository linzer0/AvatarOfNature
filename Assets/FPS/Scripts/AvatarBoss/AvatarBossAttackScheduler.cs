using System.Collections;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    public enum AvatarBossSchedulerState
    {
        Idle,
        Telegraph,
        Windup,
        Execute,
        Recover,
    }

    /// M2: explicit state machine driver for boss attacks.
    public class AvatarBossAttackScheduler : MonoBehaviour
    {
        [Header("Tunings")]
        [Tooltip("Initial grace period: the first attack cycle starts no earlier than this (seconds after play start)")]
        public float InitialGraceTime = 4f;

        [Header("Timings")]
        [Tooltip("Wind-up delay between telegraph and the attack landing")]
        public float WindupTime = 1f;

        [Tooltip("Recovery delay after an attack before returning to Idle")]
        public float RecoverTime = 1f;

        [Tooltip("Grace delay before starting a new cycle after one finished normally")]
        public float AttackCooldown = 3f;

        [Tooltip("Grace delay before a new cycle after the boss was staggered")]
        public float StaggerRecoverTime = 3f;

        [Header("Phase 2 Combo (fixed: Shockwave -> ComboDelay -> Earth)")]
        public bool EnableCombo = false;
        [Tooltip("Seconds between Shockwave finishing and the chained Earth attack")]
        public float ComboDelay = 1.5f;

        public AvatarBossSchedulerState State { get; private set; } = AvatarBossSchedulerState.Idle;
        public AvatarBossAttack CurrentAttack { get; private set; }

        AvatarBossAttack[] m_Attacks;
        AvatarBossAttack m_PendingAttack;
        Coroutine m_CycleRoutine;
        float m_NextAttackAllowedTime;
        AvatarBossElement m_LastElement;
        bool m_HasLastElement;
        bool m_Initialized;
        bool m_ComboPending;

        void Awake()
        {
            // fallback self-init for schedulers used without a controller
            if (GetComponentInParent<AvatarBossController>() == null)
                Initialize();
        }

        public void Initialize()
        {
            if (m_Initialized)
                return;
            m_Initialized = true;
            m_NextAttackAllowedTime = Time.time + InitialGraceTime;
            m_Attacks = GetComponentsInChildren<AvatarBossAttack>();
            if (m_Attacks.Length == 0)
                Debug.LogWarning("[AvatarOfNature] No AvatarBossAttack components in boss hierarchy.", this);
        }

        /// <summary>Stops all attack activity permanently (e.g. boss death).</summary>
        public void Shutdown()
        {
            Interrupt();
            enabled = false;
        }

        void Update()
        {
            if (!m_Initialized || m_Attacks == null)
                return;
            if (State == AvatarBossSchedulerState.Idle
                && Time.time >= m_NextAttackAllowedTime
                && m_CycleRoutine == null)
            {
                if (m_ComboPending)
                {
                    m_ComboPending = false;
                    StartFixedCycle(AvatarBossElement.Earth);
                }
                else
                {
                    StartNewCycle();
                }
            }
        }

        void StartFixedCycle(AvatarBossElement element)
        {
            foreach (var attack in m_Attacks)
            {
                if (attack.Element == element)
                {
                    m_PendingAttack = attack;
                    m_LastElement = element;
                    m_HasLastElement = true;
                    SetState(AvatarBossSchedulerState.Telegraph);
                    attack.Prepare();
                    m_CycleRoutine = StartCoroutine(RunCycle(attack));
                    return;
                }
            }
            Debug.LogWarning($"[AvatarOfNature] Combo target element '{element}' not found in boss hierarchy.", this);
        }

        AvatarBossAttack PickAttack()
        {
            if (m_Attacks.Length == 1)
                return m_Attacks[0];

            // avoid picking the same element twice in a row
            AvatarBossAttack candidate = m_Attacks[Random.Range(0, m_Attacks.Length)];
            if (m_HasLastElement)
            {
                int guard = 0;
                while (candidate.Element == m_LastElement && guard++ < 16)
                    candidate = m_Attacks[Random.Range(0, m_Attacks.Length)];
            }
            return candidate;
        }

        /// <summary>Test-only hook: deterministically arms the next fixed combo cycle. Does not alter designer data.</summary>
        public void DebugForceComboPending()
        {
            m_ComboPending = true;
            m_NextAttackAllowedTime = Time.time;
        }

        void StartNewCycle()
        {
            m_PendingAttack = PickAttack();
            m_LastElement = m_PendingAttack.Element; // recorded at cycle start so interrupted attacks still count
            m_HasLastElement = true;
            SetState(AvatarBossSchedulerState.Telegraph);
            m_PendingAttack.Prepare();
            m_CycleRoutine = StartCoroutine(RunCycle(m_PendingAttack));
        }

        IEnumerator RunCycle(AvatarBossAttack attack)
        {
            yield return new WaitForSeconds(attack.TelegraphTime);

            SetState(AvatarBossSchedulerState.Windup);
            yield return new WaitForSeconds(WindupTime);

            SetState(AvatarBossSchedulerState.Execute);
            yield return attack.Execute();

            CleanupCurrent();

            SetState(AvatarBossSchedulerState.Recover);
            yield return new WaitForSeconds(RecoverTime);

            if (EnableCombo && attack.Element == AvatarBossElement.Shockwave)
            {
                m_ComboPending = true; // fixed combo: next must be Earth after ComboDelay
                m_NextAttackAllowedTime = Time.time + ComboDelay;
            }
            else
            {
                m_NextAttackAllowedTime = Time.time + attack.Cooldown;
            }
            m_CycleRoutine = null;
            SetState(AvatarBossSchedulerState.Idle);
        }

        /// <summary>Called by AvatarBossController when the stagger meter breaks.</summary>
        public void Interrupt()
        {
            if (State != AvatarBossSchedulerState.Idle)
            {
                Debug.Log($"[AvatarOfNature] Attack interrupted during {State}", this);
            }

            if (m_CycleRoutine != null)
            {
                StopCoroutine(m_CycleRoutine);
                m_CycleRoutine = null;
            }

            foreach (var attack in m_Attacks)
                attack.Cleanup();

            m_PendingAttack = null;
            CurrentAttack = null;
            m_ComboPending = false; // interrupted or staggered: never sandwich a combo
            SetState(AvatarBossSchedulerState.Idle);
            m_NextAttackAllowedTime = Time.time + StaggerRecoverTime;
        }

        void CleanupCurrent()
        {
            if (m_PendingAttack != null)
            {
                m_PendingAttack.Cleanup();
                m_PendingAttack = null;
            }
        }

        void SetState(AvatarBossSchedulerState newState)
        {
            State = newState;
            CurrentAttack = m_PendingAttack;
            Debug.Log($"[AvatarOfNature] Scheduler state -> {newState} (element={CurrentAttack?.Element.ToString() ?? "none"})", this);

            if (newState == AvatarBossSchedulerState.Telegraph)
                PlayTelegraphCue();
        }

        void PlayTelegraphCue()
        {
            var cues = GetComponentInParent<AvatarBossAudioCues>();
            if (cues != null)
                cues.Play(AvatarBossCue.Telegraph);
        }
    }
}
