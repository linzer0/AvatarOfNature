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
        [Min(0.01f)] public float ClosedBodyDamageMultiplier = 0.12f;
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

        /// <summary>
        /// Applies difficulty tuning after all boss components have initialized.
        /// Difficulty owns this value; keeping it here avoids Start() overwriting the
        /// profile with the serialized fallback.
        /// </summary>
        public void ApplyDifficultyTuning(float closedBodyDamageMultiplier, float vulnerabilityDuration)
        {
            ClosedBodyDamageMultiplier = Mathf.Max(0.01f, closedBodyDamageMultiplier);
            VulnerabilityDuration = Mathf.Max(0f, vulnerabilityDuration);
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

            m_LastResolvedIntent = intent;
            m_HasLastResolvedIntent = true;
            ResolvedSectorCount += brokenSectors;
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

            DuelWindowActive = true;
            EventManager.Broadcast(new CameraImpulseEvent
            {
                Strength = 0.18f,
                Duration = 0.24f,
                Direction = Vector3.up
            });
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
