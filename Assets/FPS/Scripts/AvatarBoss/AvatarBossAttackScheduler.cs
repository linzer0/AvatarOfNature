using System.Collections;
using Unity.FPS.Gameplay;
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
        public float WindupTime = 0.9f;

        [Tooltip("Recovery delay after an attack before returning to Idle")]
        public float RecoverTime = 1.5f;

        [Tooltip("Grace delay before starting a new cycle after one finished normally")]
        public float AttackCooldown = 3f;

        [Tooltip("Grace delay before a new cycle after the boss was staggered")]
        public float StaggerRecoverTime = 3f;

        [Header("Phase 2 Combo (fixed: Shockwave -> ComboDelay -> Earth)")]
        public bool EnableCombo = false;
        [Range(0f, 1f)] public float ComboFrequency = 1f;
        [Tooltip("Seconds between Shockwave finishing and the chained Earth attack")]
        public float ComboDelay = 1.5f;
        [Tooltip("Min seconds that must pass between two combos")]
        public float ComboCooldownMin = 22f;
        [Tooltip("Max seconds that must pass between two combos")]
        public float ComboCooldownMax = 30f;
        [Tooltip("Mandatory idle after a combo's final attack, before the next telegraph")]
        public float ComboPostRecoverTime = 2f;

        [Header("Meteor Rain gating")]
        [Tooltip("Min seconds between two Meteor Rain attacks")]
        public float MeteorRainCooldownMin = 22f;
        [Tooltip("Max seconds between two Meteor Rain attacks")]
        public float MeteorRainCooldownMax = 30f;

        [Header("Recovery")]
        public AvatarBossHealingOrbs HealingOrbs;
        public bool EnableRecovery = true;
        [Range(0.1f, 0.95f)] public float RecoveryHealthThreshold = 0.65f;
        [Min(0f)] public float RecoveryPostDelay = 2f;

        public AvatarBossSchedulerState State { get; private set; } = AvatarBossSchedulerState.Idle;
        public AvatarBossAttack CurrentAttack { get; private set; }
        public AvatarBossIntent CurrentIntent { get; private set; }
        public bool HasCurrentIntent { get; private set; }

        /// <summary>Removes a broken arena sector from future intent selection.</summary>
        public void MarkSectorDestroyed(int sector)
        {
            m_IntentController?.MarkSectorDestroyed(sector);
        }

        /// Fired when an attack enters Execute, including the selected intent.
        public event System.Action<AvatarBossAttack, AvatarBossIntent> AttackExecuted;

        /// <summary>Number of attack cycles started (diagnostic/metric counter).</summary>
        public int AttackCount { get; private set; }
        /// <summary>Number of Shockwave->Earth combos that actually chained (metric).</summary>
        public int ComboCount { get; private set; }
        /// <summary>Number of Meteor Rain attacks that completed (metric).</summary>
        public int MeteorRainCount { get; private set; }

        AvatarBossAttack[] m_Attacks;
        AvatarBossAttack m_PendingAttack;
        Coroutine m_CycleRoutine;
        float m_NextAttackAllowedTime;
        AvatarBossElement m_LastElement;
        bool m_HasLastElement;
        bool m_Initialized;
        bool m_ComboPending;
        bool m_InComboChain;
        float m_NextComboAllowedTime;
        float m_NextMeteorRainAllowedTime;

        float m_InitWindup;
        float m_InitRecover;
        float m_InitCooldown;
        bool m_InitCombo;
        bool m_OriginalsCached;
        AvatarBossController m_Boss;
        AvatarBossShowcaseDifficultySelect m_DifficultySelect;
        AvatarBossArenaController m_Arena;
        AvatarBossIntentController m_IntentController;

        [Header("Intent integration")]
        public bool UseIntentController = true;
        public int IntentSeed = 1337;

        void Awake()
        {
            CacheOriginals();
            // fallback self-init for schedulers used without a controller
            if (GetComponentInParent<AvatarBossController>() == null)
                Initialize();
        }

        void CacheOriginals()
        {
            if (m_OriginalsCached)
                return;
            m_InitWindup = WindupTime;
            m_InitRecover = RecoverTime;
            m_InitCooldown = AttackCooldown;
            m_InitCombo = EnableCombo;
            m_OriginalsCached = true;
        }

        public void Initialize()
        {
            if (m_Initialized)
                return;
            m_Initialized = true;
            m_Boss = GetComponentInParent<AvatarBossController>();
            m_DifficultySelect = GetComponentInParent<AvatarBossShowcaseDifficultySelect>();
            if (HealingOrbs == null)
                HealingOrbs = GetComponentInParent<AvatarBossHealingOrbs>();
            m_Arena = FindFirstObjectByType<AvatarBossArenaController>();
            if (UseIntentController)
            {
                int sectorCount = m_Arena != null
                    ? m_Arena.Sectors.Count
                    : AvatarBossIntentController.DefaultSectorCount;
                Vector3 center = m_Arena != null ? m_Arena.transform.position : transform.position;
                m_IntentController = m_Arena != null
                    ? new AvatarBossIntentController(IntentSeed, m_Arena)
                    : new AvatarBossIntentController(IntentSeed, sectorCount, center);
            }
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
            if (m_DifficultySelect != null && !m_DifficultySelect.FightStarted)
            {
                if (State != AvatarBossSchedulerState.Idle)
                    SetState(AvatarBossSchedulerState.Idle);
                return;
            }
            if (!m_Initialized || m_Attacks == null)
                return;
            if (State == AvatarBossSchedulerState.Idle
                && Time.time >= m_NextAttackAllowedTime
                && m_CycleRoutine == null)
            {
                if (TryStartRecovery())
                    return;

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

        bool TryStartRecovery()
        {
            if (!EnableRecovery || HealingOrbs == null || m_Boss == null
                || m_Boss.IsDead || m_Boss.SummonsActive
                || Time.time < InitialGraceTime
                || HealingOrbs.RecoveryActive
                || m_Boss.BossHealth == null
                || m_Boss.BossHealth.GetRatio() > RecoveryHealthThreshold)
                return false;

            if (!HealingOrbs.BeginRecovery())
                return false;

            Interrupt();
            return true;
        }

        /// <summary>Called by the controller after all recovery orbs resolve.</summary>
        public void NotifyRecoveryFinished()
        {
            m_ComboPending = false;
            m_InComboChain = false;
            m_NextAttackAllowedTime = Time.time + RecoveryPostDelay;
        }

        void StartFixedCycle(AvatarBossElement element)
        {
            foreach (var attack in m_Attacks)
            {
                if (attack.Element == element)
                {
                    AttackCount++;
                    m_PendingAttack = attack;
                    if (HasCurrentIntent)
                        attack.SetIntent(CurrentIntent);
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

            // avoid picking the same element twice in a row and respect Meteor Rain gating
            bool meteorGated = Time.time < m_NextMeteorRainAllowedTime;
            AvatarBossAttack candidate = m_Attacks[Random.Range(0, m_Attacks.Length)];
            if (m_HasLastElement || meteorGated)
            {
                int guard = 0;
                while (guard++ < 16
                    && ((m_HasLastElement && candidate.Element == m_LastElement)
                        || (meteorGated && candidate.Element == AvatarBossElement.Fire)))
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

        /// <summary>Debug/test-only: interrupt whatever is running and force a specific attack
        /// into its Telegraph state. Ignored while the boss is dead.</summary>
        public void DebugForceAttack(AvatarBossElement element)
        {
            if (!m_Initialized)
                Initialize();

            var boss = GetComponentInParent<AvatarBossController>();
            if (boss != null && boss.IsDead)
            {
                Debug.LogWarning("[AvatarOfNature] DebugForceAttack ignored: boss is dead.", this);
                return;
            }

            Interrupt();
            StartFixedCycle(element);
        }

        /// <summary>Debug/test-only: force the full Shockwave -> Earth combo chain.</summary>
        public void DebugForceCombo()
        {
            if (!m_Initialized)
                Initialize();

            var boss = GetComponentInParent<AvatarBossController>();
            if (boss != null && boss.IsDead)
                return;

            EnableCombo = true; // test-only convenience: guarantee the chain resolves
            Interrupt();
            StartFixedCycle(AvatarBossElement.Shockwave);
        }

        /// <summary>Debug/test-only: restore serialized tunables, re-enable the scheduler and
        /// return the state machine to a clean Idle with a fresh initial grace window.</summary>
        public void DebugReset()
        {
            CacheOriginals();
            WindupTime = m_InitWindup;
            RecoverTime = m_InitRecover;
            AttackCooldown = m_InitCooldown;
            EnableCombo = m_InitCombo;

            Interrupt();
            enabled = true;
            m_ComboPending = false;
            m_InComboChain = false;
            m_NextComboAllowedTime = 0f;
            m_NextMeteorRainAllowedTime = 0f;
            m_HasLastElement = false;
            HasCurrentIntent = false;
            CurrentIntent = default;
            m_IntentController?.Reset();
            m_NextAttackAllowedTime = Time.time + InitialGraceTime;
            State = AvatarBossSchedulerState.Idle;
            CurrentAttack = null;
            AttackCount = 0;
            ComboCount = 0;
            MeteorRainCount = 0;
        }

        void StartNewCycle()
        {
            AttackCount++;
            m_PendingAttack = PickIntentAttack();
            m_LastElement = m_PendingAttack.Element; // recorded at cycle start so interrupted attacks still count
            m_HasLastElement = true;
            if (HasCurrentIntent)
                m_PendingAttack.SetIntent(CurrentIntent);
            SetState(AvatarBossSchedulerState.Telegraph);
            m_PendingAttack.Prepare();
            m_CycleRoutine = StartCoroutine(RunCycle(m_PendingAttack));
        }

        AvatarBossAttack PickIntentAttack()
        {
            if (m_IntentController != null)
            {
                var player = FindFirstObjectByType<PlayerCharacterController>();
                if (player != null)
                    m_IntentController.SetLastPlayerPosition(player.transform.position);

                if (m_IntentController.TryGetNextIntent(out var intent))
                {
                    CurrentIntent = intent;
                    HasCurrentIntent = true;
                    foreach (var attack in m_Attacks)
                        if (attack.Element == intent.AttackType)
                            return attack;
                }
            }

            HasCurrentIntent = false;
            return PickAttack();
        }

        IEnumerator RunCycle(AvatarBossAttack attack)
        {
            yield return new WaitForSeconds(attack.TelegraphTime);

            SetState(AvatarBossSchedulerState.Windup);
            yield return new WaitForSeconds(WindupTime);

            SetState(AvatarBossSchedulerState.Execute);
            if (HasCurrentIntent)
                AttackExecuted?.Invoke(attack, CurrentIntent);
            yield return attack.Execute();

            CleanupCurrent();

            SetState(AvatarBossSchedulerState.Recover);
            yield return new WaitForSeconds(RecoverTime);

            if (m_InComboChain)
            {
                // combo's final attack done: mandatory post-combo recover replaces the normal cooldown
                m_InComboChain = false;
                m_NextAttackAllowedTime = Time.time + ComboPostRecoverTime;
            }
            else if (EnableCombo
                && attack.Element == AvatarBossElement.Shockwave
                && Time.time >= m_NextComboAllowedTime
                && Random.value <= ComboFrequency)
            {
                m_ComboPending = true; // fixed combo: next must be Earth after ComboDelay
                m_InComboChain = true;
                ComboCount++;
                m_NextComboAllowedTime = Time.time + Random.Range(ComboCooldownMin, ComboCooldownMax);
                m_NextAttackAllowedTime = Time.time + ComboDelay;
            }
            else
            {
                m_NextAttackAllowedTime = Time.time + attack.Cooldown;
                if (attack.Element == AvatarBossElement.Fire)
                {
                    MeteorRainCount++;
                    m_NextMeteorRainAllowedTime = Time.time + Random.Range(MeteorRainCooldownMin, MeteorRainCooldownMax);
                }
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

            foreach (var attack in m_Attacks != null ? m_Attacks : System.Array.Empty<AvatarBossAttack>())
                attack.Cleanup();

            m_PendingAttack = null;
            CurrentAttack = null;
            m_ComboPending = false; // interrupted or staggered: never sandwich a combo
            m_InComboChain = false;
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
            if (newState == AvatarBossSchedulerState.Execute)
                PlayLaunchCue();
        }

        void PlayTelegraphCue()
        {
            var cues = GetComponentInParent<AvatarBossAudioCues>();
            if (cues != null)
                cues.Play(AvatarBossCue.Telegraph);
        }

        void PlayLaunchCue()
        {
            var cues = GetComponentInParent<AvatarBossAudioCues>();
            if (cues != null)
                cues.Play(AvatarBossCue.AttackLaunch);
        }
    }
}
