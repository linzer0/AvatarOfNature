using System;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
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
        [Min(0.01f)] public float ClosedBodyDamageMultiplier = 0.08f;
        [Min(0.01f)] public float OpenBodyDamageMultiplier = 1f;
        [Min(0f)] public float VulnerabilityDuration = 3.5f;
        [Min(0f)] public float VulnerabilityDelayAfterCollapse = 0.15f;

        public bool DuelWindowActive { get; private set; }
        public int ResolvedSectorCount { get; private set; }

        /// Fired when an intent successfully resolves against an arena sector.
        public event Action<AvatarBossIntent> OnIntentResolved;

        Damageable m_BodyDamageable;
        float m_OriginalBodyDamageMultiplier;
        Coroutine m_VulnerabilityRoutine;
        AvatarBossIntent m_LastResolvedIntent;
        bool m_HasLastResolvedIntent;

        void Awake()
        {
            if (Boss == null)
                Boss = GetComponent<AvatarBossController>();
            if (Scheduler == null && Boss != null)
                Scheduler = Boss.Scheduler;
            if (Arena == null)
                Arena = FindFirstObjectByType<AvatarBossArenaController>();

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
            if (Arena == null)
                Arena = FindFirstObjectByType<AvatarBossArenaController>();

            if (m_BodyDamageable != null)
            {
                m_OriginalBodyDamageMultiplier = m_BodyDamageable.DamageMultiplier;
                m_BodyDamageable.DamageMultiplier = ClosedBodyDamageMultiplier;
            }

            if (Scheduler != null)
                Scheduler.AttackExecuted += OnAttackExecuted;
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
                if (target == null || !Arena.DamageSector(sectorIndex))
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

            m_LastResolvedIntent = intent;
            m_HasLastResolvedIntent = true;
            ResolvedSectorCount += brokenSectors;
            OnIntentResolved?.Invoke(intent);

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

            DuelWindowActive = true;
            if (m_BodyDamageable != null)
                m_BodyDamageable.DamageMultiplier = OpenBodyDamageMultiplier;
            Boss.TriggerDuelWindow(VulnerabilityDuration);

            if (VulnerabilityDuration > 0f)
                yield return new WaitForSeconds(VulnerabilityDuration);

            DuelWindowActive = false;
            if (m_BodyDamageable != null)
                m_BodyDamageable.DamageMultiplier = ClosedBodyDamageMultiplier;
            m_VulnerabilityRoutine = null;
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
            ResolvedSectorCount = 0;
            m_HasLastResolvedIntent = false;
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
