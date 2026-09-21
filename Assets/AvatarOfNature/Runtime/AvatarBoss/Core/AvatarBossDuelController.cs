using System;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    public enum AvatarBossVulnerabilityKind
    {
        Standard,
        HighImpact
    }

    /// <summary>
    /// Integrates boss intent, the destructible arena and the vulnerability window.
    /// Ordinary body damage is reduced until an attack resolves against its target sector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarBossDuelController : MonoBehaviour
    {
        [Header("References")]
        public AvatarBossController Boss;
        public AvatarBossArenaController Arena;
        public AvatarBossAttackScheduler Scheduler;

        [Header("Duel tuning")]
        [Min(0.01f)] public float ClosedBodyDamageMultiplier = 0.12f;
        [Min(0.01f)] public float OpenBodyDamageMultiplier = 1f;
        [Min(0f)] public float VulnerabilityDuration = 3.5f;
        [Min(0f)] public float VulnerabilityDelayAfterCollapse = 0.15f;
        [Range(0f, 1f)] public float MaxVulnerabilityDamagePercent = 0.27f;
        [Range(0f, 1f)] public float HighImpactWindowDamagePercent = 0.38f;
        [Min(0f)] public float HighImpactCooldown = 45f;
        [Min(0f)] public float StandardVulnerabilityCooldown = 25f;

        [Header("Diagnostics")]
        [Tooltip("Logs every boss damage event, vulnerability budget decision and window transition.")]
        public bool EnableDamageDiagnostics = true;

        public bool DuelWindowActive { get; private set; }
        public int ResolvedSectorCount { get; private set; }
        public float VulnerabilityDamageRemaining => m_VulnerabilityDamageRemaining;

        /// Fired when an intent successfully resolves against an arena sector.
        public event Action<AvatarBossIntent> OnIntentResolved;

        Damageable m_BodyDamageable;
        float m_OriginalBodyDamageMultiplier;
        Coroutine m_VulnerabilityRoutine;
        AvatarBossIntent m_LastResolvedIntent;
        bool m_HasLastResolvedIntent;
        AvatarBossVulnerabilityKind m_PendingWindowKind = AvatarBossVulnerabilityKind.Standard;
        float m_VulnerabilityDamageRemaining;
        bool m_DamageBudgetActive;
        bool m_LastWindowWasHighImpact;
        float m_NextHighImpactTime;
        float m_NextStandardWindowTime;
        int m_VulnerabilityWindowId;
        float m_FirstDamageTime = -1f;
        float m_TotalRawDamage;
        float m_TotalAcceptedDamage;
        float m_ClosedBodyDamage;
        int m_TotalHitCount;
        int m_VulnerabilityHitCount;
        float m_WindowRawDamage;
        float m_WindowAcceptedDamage;
        int m_WindowHitCount;
        float m_WindowOpenedAt = -1f;

        void Awake()
        {
            if (Boss == null)
                Boss = GetComponent<AvatarBossController>();
            if (Scheduler == null && Boss != null)
                Scheduler = Boss.Scheduler;
            if (Arena == null && Boss != null)
            {
                Boss.GetCombatContext().ResolveSceneReferences();
                Arena = Boss.CombatContext.Arena;
            }

            var body = transform.Find("BossBody");
            if (body != null)
                m_BodyDamageable = body.GetComponent<Damageable>();
        }

        void Start()
        {
            if (Boss == null)
                Boss = GetComponent<AvatarBossController>();
            if (Scheduler == null && Boss != null)
                Scheduler = Boss.Scheduler;
            if (Arena == null && Boss != null)
            {
                Boss.GetCombatContext().ResolveSceneReferences();
                Arena = Boss.CombatContext.Arena;
            }

            if (m_BodyDamageable != null)
            {
                m_OriginalBodyDamageMultiplier = m_BodyDamageable.DamageMultiplier;
                m_BodyDamageable.DamageMultiplier = ClosedBodyDamageMultiplier;
            }

            if (Scheduler != null)
                Scheduler.AttackExecuted += OnAttackExecuted;
        }

        /// <summary>
        /// Applies difficulty tuning after all boss components have initialized.
        /// Difficulty owns this value; keeping it here avoids Start() overwriting the
        /// profile with the serialized fallback.
        /// </summary>
        public void ApplyDifficultyTuning(float closedBodyDamageMultiplier, float vulnerabilityDuration,
            float openBodyDamageMultiplier = 1f, float maxVulnerabilityDamagePercent = 0.27f,
            float highImpactWindowDamagePercent = 0.38f, float highImpactCooldown = 45f,
            float standardVulnerabilityCooldown = 25f)
        {
            ClosedBodyDamageMultiplier = Mathf.Max(0.01f, closedBodyDamageMultiplier);
            VulnerabilityDuration = Mathf.Max(0f, vulnerabilityDuration);
            OpenBodyDamageMultiplier = Mathf.Max(0.01f, openBodyDamageMultiplier);
            MaxVulnerabilityDamagePercent = Mathf.Clamp01(maxVulnerabilityDamagePercent);
            HighImpactWindowDamagePercent = Mathf.Clamp01(highImpactWindowDamagePercent);
            HighImpactCooldown = Mathf.Max(0f, highImpactCooldown);
            StandardVulnerabilityCooldown = Mathf.Max(0f, standardVulnerabilityCooldown);
            if (m_BodyDamageable != null && !DuelWindowActive)
                m_BodyDamageable.DamageMultiplier = ClosedBodyDamageMultiplier;
        }

        void OnDestroy()
        {
            if (Scheduler != null)
                Scheduler.AttackExecuted -= OnAttackExecuted;

            if (m_BodyDamageable != null)
                m_BodyDamageable.DamageMultiplier = m_OriginalBodyDamageMultiplier;
        }

        void OnAttackExecuted(AvatarBossAttack attack, AvatarBossIntent intent)
        {
            if (Boss == null || Boss.IsDead || Arena == null)
                return;
            // Shockwave is a positioning test. It moves the player but does not spend
            // any arena durability or open the boss's damage window.
            if (attack == null || attack.Element == AvatarBossElement.Shockwave)
                return;
            var targetSectors = new List<int>();
            if (attack.ArenaTargetSectors.Count > 0)
                targetSectors.AddRange(attack.ArenaTargetSectors);
            else if (intent.TargetSector >= 0 && intent.TargetSector < Arena.Sectors.Count)
                targetSectors.Add(intent.TargetSector);

            int brokenSectors = 0;
            for (int i = 0; i < targetSectors.Count; i++)
            {
                int sectorIndex = targetSectors[i];
                var target = Arena.GetSector(sectorIndex);
                if (target == null)
                    continue;

                // A telegraphed boss impact is the player's actual puzzle
                // resolution, not a small amount of ambient arena damage. Once
                // the marked zone is hit, consume its remaining durability so
                // the arena visibly changes and the weak-point window can open
                // in the same readable boss cycle. Ordinary damage/tests still
                // use DamageSector(int) and retain the Intact -> Damaged step.
                if (!Arena.DamageSector(sectorIndex, Mathf.Max(1, target.HitPoints)))
                    continue;
                if (target.State != AvatarBossArenaSectorState.Collapsing
                    && target.State != AvatarBossArenaSectorState.Destroyed)
                    continue;

                Scheduler?.MarkSectorDestroyed(sectorIndex);
                brokenSectors++;
            }

            // A normal impact only stains/cracks cells. The duel window belongs
            // exclusively to an execution that actually breaks at least one cell.
            if (brokenSectors == 0)
                return;

            if (DuelWindowActive)
            {
                DamageLog($"WINDOW REQUEST IGNORED reason=active-window id={m_VulnerabilityWindowId} " +
                          $"brokenSectors={brokenSectors}");
                OnIntentResolved?.Invoke(intent);
                return;
            }

            m_LastResolvedIntent = intent;
            m_HasLastResolvedIntent = true;
            ResolvedSectorCount += brokenSectors;
            bool isHighImpactReady = Time.time >= m_NextHighImpactTime;
            bool isStandardReady = Time.time >= m_NextStandardWindowTime;
            bool shouldOpenHighImpact = brokenSectors >= 2 && !m_LastWindowWasHighImpact && isHighImpactReady;
            if (!shouldOpenHighImpact && !isStandardReady)
            {
                DamageLog($"WINDOW REQUEST IGNORED reason=standard-cooldown remaining={m_NextStandardWindowTime - Time.time:F1}s " +
                          $"brokenSectors={brokenSectors}");
                OnIntentResolved?.Invoke(intent);
                return;
            }
            m_PendingWindowKind = shouldOpenHighImpact
                ? AvatarBossVulnerabilityKind.HighImpact
                : AvatarBossVulnerabilityKind.Standard;
            if (brokenSectors >= 2 && !shouldOpenHighImpact && !isHighImpactReady)
            {
                DamageLog($"HIGH IMPACT DOWNGRADED reason=cooldown remaining={m_NextHighImpactTime - Time.time:F1}s");
            }
            OnIntentResolved?.Invoke(intent);
            EventManager.Broadcast(new CameraImpulseEvent
            {
                Strength = 0.28f + 0.04f * Mathf.Min(2, brokenSectors - 1),
                Duration = 0.2f,
                Direction = Vector3.down
            });

            if (m_VulnerabilityRoutine != null)
                StopCoroutine(m_VulnerabilityRoutine);
            m_VulnerabilityRoutine = StartCoroutine(OpenVulnerabilityAfterCollapse());
        }

        IEnumerator OpenVulnerabilityAfterCollapse()
        {
            float delay = (Arena != null ? Arena.CollapseDuration : 0f)
                + VulnerabilityDelayAfterCollapse;
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (Boss == null || Boss.IsDead)
                yield break;

            EventManager.Broadcast(new CameraImpulseEvent
            {
                Strength = 0.18f,
                Duration = 0.24f,
                Direction = Vector3.up
            });
            if (m_BodyDamageable != null)
                m_BodyDamageable.DamageMultiplier = OpenBodyDamageMultiplier;
            Boss.TriggerDuelWindow(VulnerabilityDuration, m_PendingWindowKind);

            if (VulnerabilityDuration > 0f)
                yield return new WaitForSeconds(VulnerabilityDuration);

            CloseVulnerabilityWindow();
            m_VulnerabilityRoutine = null;
        }

        /// <summary>Returns whether the requested window may open at the current time.</summary>
        public bool CanOpenVulnerabilityWindow(AvatarBossVulnerabilityKind kind)
        {
            if (DuelWindowActive)
                return false;
            return kind == AvatarBossVulnerabilityKind.HighImpact
                ? Time.time >= m_NextHighImpactTime
                : Time.time >= m_NextStandardWindowTime;
        }

        /// <summary>Arms the damage budget used by the next exposed weak-point window.</summary>
        public bool PrepareVulnerabilityWindow(AvatarBossVulnerabilityKind kind)
        {
            if (DuelWindowActive)
            {
                DamageLog($"WINDOW PREPARE IGNORED reason=active-window id={m_VulnerabilityWindowId} requested={kind}");
                return false;
            }

            if (Boss == null)
                Boss = GetComponent<AvatarBossController>();
            var bossHealth = Boss != null ? Boss.BossHealth : null;
            if (bossHealth == null)
                bossHealth = GetComponent<Health>() ?? GetComponentInParent<Health>();

            m_PendingWindowKind = kind;
            m_VulnerabilityWindowId++;
            m_VulnerabilityDamageRemaining = bossHealth != null
                ? bossHealth.MaxHealth * (kind == AvatarBossVulnerabilityKind.HighImpact
                    ? HighImpactWindowDamagePercent
                    : MaxVulnerabilityDamagePercent)
                : 0f;
            m_DamageBudgetActive = true;
            m_LastWindowWasHighImpact = kind == AvatarBossVulnerabilityKind.HighImpact;
            if (m_LastWindowWasHighImpact)
                m_NextHighImpactTime = Time.time + HighImpactCooldown;
            m_NextStandardWindowTime = Time.time + StandardVulnerabilityCooldown;
            m_WindowRawDamage = 0f;
            m_WindowAcceptedDamage = 0f;
            m_WindowHitCount = 0;
            m_WindowOpenedAt = Time.time;
            DuelWindowActive = true;
            if (m_BodyDamageable != null)
                m_BodyDamageable.DamageMultiplier = OpenBodyDamageMultiplier;

            DamageLog($"WINDOW OPEN id={m_VulnerabilityWindowId} kind={kind} budget={m_VulnerabilityDamageRemaining:F1} " +
                      $"duration={VulnerabilityDuration:F2}s bossHp={bossHealth?.CurrentHealth:F1}/{bossHealth?.MaxHealth:F1}");
            DamageLog($"WINDOW BASELINE id={m_VulnerabilityWindowId} kind={kind} targetDamage={m_VulnerabilityDamageRemaining:F1}");
            return true;
        }

        public void CloseVulnerabilityWindow()
        {
            if (!m_DamageBudgetActive && !DuelWindowActive)
                return;

            DamageLog($"WINDOW CLOSE id={m_VulnerabilityWindowId} kind={m_PendingWindowKind} remainingBudget={m_VulnerabilityDamageRemaining:F1} " +
                      $"bossHp={Boss?.BossHealth?.CurrentHealth:F1}/{Boss?.BossHealth?.MaxHealth:F1} " +
                      $"rawWindow={m_WindowRawDamage:F1} acceptedWindow={m_WindowAcceptedDamage:F1} " +
                      $"hits={m_WindowHitCount} windowTime={(m_WindowOpenedAt >= 0f ? Time.time - m_WindowOpenedAt : 0f):F1}s");
            m_DamageBudgetActive = false;
            m_VulnerabilityDamageRemaining = 0f;
            DuelWindowActive = false;
            m_WindowOpenedAt = -1f;
            if (m_BodyDamageable != null)
                m_BodyDamageable.DamageMultiplier = ClosedBodyDamageMultiplier;
        }

        /// <summary>Refunds overflow after Health.TakeDamage and keeps the actual HP loss within the window budget.</summary>
        public float ConsumeVulnerabilityDamage(float damage)
        {
            if (!m_DamageBudgetActive || damage <= 0f)
                return damage;

            float accepted = Mathf.Min(damage, m_VulnerabilityDamageRemaining);
            m_VulnerabilityDamageRemaining -= accepted;
            float overflow = damage - accepted;
            var bossHealth = Boss != null ? Boss.BossHealth : null;
            if (bossHealth == null)
                bossHealth = GetComponent<Health>() ?? GetComponentInParent<Health>();
            if (overflow > 0f && bossHealth != null)
                bossHealth.CurrentHealth = Mathf.Min(bossHealth.MaxHealth,
                    bossHealth.CurrentHealth + overflow);

            DamageLog($"DAMAGE BUDGET id={m_VulnerabilityWindowId} kind={m_PendingWindowKind} raw={damage:F1} accepted={accepted:F1} " +
                      $"overflow={overflow:F1} remaining={m_VulnerabilityDamageRemaining:F1} " +
                      $"bossHp={bossHealth?.CurrentHealth:F1}/{bossHealth?.MaxHealth:F1} t={Time.time:F1}s");
            return accepted;
        }

        /// <summary>Collects a compact fight-level damage sample from AvatarBossController.</summary>
        public void RecordDamageEvent(float rawDamage, float acceptedDamage, bool vulnerabilityWindow, string sourceName)
        {
            if (rawDamage <= 0f)
                return;

            if (m_FirstDamageTime < 0f)
                m_FirstDamageTime = Time.time;

            m_TotalRawDamage += rawDamage;
            m_TotalAcceptedDamage += acceptedDamage;
            m_TotalHitCount++;
            if (vulnerabilityWindow)
            {
                m_VulnerabilityHitCount++;
                m_WindowRawDamage += rawDamage;
                m_WindowAcceptedDamage += acceptedDamage;
                m_WindowHitCount++;
            }
            else
            {
                m_ClosedBodyDamage += acceptedDamage;
            }

            DamageLog($"DAMAGE SAMPLE source={sourceName} kind={(vulnerabilityWindow ? m_PendingWindowKind.ToString() : "ClosedBody")} " +
                      $"raw={rawDamage:F1} accepted={acceptedDamage:F1} totalAccepted={m_TotalAcceptedDamage:F1} " +
                      $"fightDps={m_TotalAcceptedDamage / Mathf.Max(0.1f, Time.time - m_FirstDamageTime):F1}");
        }

        /// <summary>Prints the final numbers needed to compare the playthrough with the target TTK.</summary>
        public void LogFightSummary(string result)
        {
            float fightTime = m_FirstDamageTime >= 0f ? Time.time - m_FirstDamageTime : 0f;
            float maxHealth = Boss?.BossHealth?.MaxHealth ?? 0f;
            DamageLog($"FIGHT SUMMARY result={result} duration={fightTime:F1}s maxHp={maxHealth:F1} " +
                      $"raw={m_TotalRawDamage:F1} accepted={m_TotalAcceptedDamage:F1} " +
                      $"closedBody={m_ClosedBodyDamage:F1} vulnerability={m_TotalAcceptedDamage - m_ClosedBodyDamage:F1} " +
                      $"hits={m_TotalHitCount} vulnerabilityHits={m_VulnerabilityHitCount} " +
                      $"avgAcceptedDps={m_TotalAcceptedDamage / Mathf.Max(0.1f, fightTime):F1}");
        }

        public void DamageLog(string message)
        {
            if (EnableDamageDiagnostics)
                Debug.Log($"[AvatarOfNature][BossDamage] t={Time.time:F1}s {message}", this);
        }

        /// <summary>Clears duel runtime state without rebuilding the arena.</summary>
        public void DebugReset()
        {
            if (m_VulnerabilityRoutine != null)
            {
                StopCoroutine(m_VulnerabilityRoutine);
                m_VulnerabilityRoutine = null;
            }

            DuelWindowActive = false;
            m_DamageBudgetActive = false;
            m_VulnerabilityDamageRemaining = 0f;
            ResolvedSectorCount = 0;
            m_HasLastResolvedIntent = false;
            m_LastWindowWasHighImpact = false;
            m_NextHighImpactTime = 0f;
            m_NextStandardWindowTime = 0f;
            m_FirstDamageTime = -1f;
            m_TotalRawDamage = 0f;
            m_TotalAcceptedDamage = 0f;
            m_ClosedBodyDamage = 0f;
            m_TotalHitCount = 0;
            m_VulnerabilityHitCount = 0;
            m_WindowRawDamage = 0f;
            m_WindowAcceptedDamage = 0f;
            m_WindowHitCount = 0;
            m_WindowOpenedAt = -1f;
            if (m_BodyDamageable != null)
                m_BodyDamageable.DamageMultiplier = ClosedBodyDamageMultiplier;
        }

        /// <summary>Returns the last intent that successfully changed the arena.</summary>
        public bool TryGetLastResolvedIntent(out AvatarBossIntent intent)
        {
            intent = m_LastResolvedIntent;
            return m_HasLastResolvedIntent;
        }
    }
}
