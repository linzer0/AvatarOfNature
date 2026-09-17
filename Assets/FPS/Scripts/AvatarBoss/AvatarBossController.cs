using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// M1: wires boss Health damage intake into the stagger meter and triggers
    /// the weak point vulnerability window when stagger breaks.
    public class AvatarBossController : MonoBehaviour
    {
        [Header("Vulnerability Window")]
        [Tooltip("Seconds that weak points stay exposed after a stagger break")]
        public float VulnerabilityDuration = 3.5f;

        public Health BossHealth { get; private set; }
        public AvatarBossStagger Stagger { get; private set; }
        public AvatarBossWeakPoint[] WeakPoints { get; private set; }
        public AvatarBossAttackScheduler Scheduler { get; private set; }
        public AvatarBossHealingOrbs HealingOrbs { get; private set; }

        public bool PhaseTwo { get; private set; }

        /// <summary>True while a summon intermission holds the boss. Blocks stagger breaks.</summary>
        public bool SummonsActive { get; set; }

        /// <summary>Fired on a validated hit: (world position, final damage, isWeakPoint).</summary>
        public event System.Action<Vector3, float, bool> OnBossHit;

        Coroutine m_VulnerabilityRoutine;
        bool m_IsDead;
        readonly List<Damageable> m_PartDamageables = new List<Damageable>();

        public bool IsDead => m_IsDead;

        void Awake()
        {
            BossHealth = GetComponentInParent<Health>();
            if (BossHealth == null)
                BossHealth = GetComponent<Health>();

            Stagger = GetComponentInChildren<AvatarBossStagger>();
            WeakPoints = GetComponentsInChildren<AvatarBossWeakPoint>();
            Scheduler = GetComponentInChildren<AvatarBossAttackScheduler>();
            HealingOrbs = GetComponentInChildren<AvatarBossHealingOrbs>();

            if (BossHealth == null)
                Debug.LogError($"[{nameof(AvatarBossController)}] No Health found on '{name}' or its parents.", this);
            if (Stagger == null)
                Debug.LogError($"[{nameof(AvatarBossController)}] No AvatarBossStagger in children of '{name}'.", this);
            if (WeakPoints == null || WeakPoints.Length == 0)
                Debug.LogWarning($"[{nameof(AvatarBossController)}] No AvatarBossWeakPoint in children of '{name}'.", this);
        }

        void Start()
        {
            if (BossHealth != null)
            {
                BossHealth.OnDamaged += OnBossDamaged;
                BossHealth.OnDie += OnBossDie;
            }
            if (Stagger != null)
                Stagger.OnStaggerFull += OnStaggerFull;
            if (Scheduler != null)
                Scheduler.Initialize();
            if (HealingOrbs != null)
            {
                HealingOrbs.RecoveryStarted += OnRecoveryStarted;
                HealingOrbs.RecoveryFinished += OnRecoveryFinished;
            }

            SubscribePartHitFeedbacks();
        }

        void SubscribePartHitFeedbacks()
        {
            var body = transform.Find("BossBody");
            if (body != null)
            {
                var d = body.GetComponent<Damageable>();
                if (d != null) SubscribePart(d);
            }
            foreach (var weakPoint in WeakPoints)
            {
                if (weakPoint == null) continue;
                var d = weakPoint.GetComponent<Damageable>();
                if (d != null && !m_PartDamageables.Contains(d)) SubscribePart(d);
            }
        }

        void SubscribePart(Damageable d)
        {
            d.OnDamageInflicted += OnPartHit;
            m_PartDamageables.Add(d);
        }

        void OnPartHit(float damage, Damageable part)
        {
            if (m_IsDead)
                return;

            float hpAfter = BossHealth != null ? BossHealth.CurrentHealth : 0f;
            float stagger = Stagger != null ? Stagger.CurrentStagger : 0f;
            bool weakPoint = part != null && part.GetComponent<AvatarBossWeakPoint>() != null;

            if (AvatarBossDebugHUD.F10DiagnosticsEnabled)
            {
                float mult = part != null ? part.DamageMultiplier : 1f;
                float applied = damage;
                float raw = mult > 0.0001f ? damage / mult : damage;
                Debug.Log($"HIT target={(part != null ? part.name : "null")} rawDamage={raw:F1} " +
                          $"appliedDamage={applied:F1} multiplier={mult:F2} hpAfter={hpAfter:F1} stagger={stagger:F1}", part);
            }

            Vector3 pos = part != null ? part.transform.position : transform.position;
            if (part != null)
            {
                var col = part.GetComponent<Collider>();
                if (col != null)
                    pos = col.bounds.center;
            }

            OnBossHit?.Invoke(pos, damage, weakPoint);
            EventManager.Broadcast(new BossHitFeedbackEvent
            {
                Position = pos,
                Damage = damage,
                IsWeakPoint = weakPoint
            });
            EventManager.Broadcast(new CameraImpulseEvent
            {
                Strength = weakPoint ? 0.22f : 0.055f,
                Duration = weakPoint ? 0.16f : 0.08f,
                Direction = weakPoint ? Vector3.forward : Vector3.zero
            });
        }

        void OnDestroy()
        {
            if (BossHealth != null)
            {
                BossHealth.OnDamaged -= OnBossDamaged;
                BossHealth.OnDie -= OnBossDie;
            }
            if (Stagger != null)
                Stagger.OnStaggerFull -= OnStaggerFull;

            if (HealingOrbs != null)
            {
                HealingOrbs.RecoveryStarted -= OnRecoveryStarted;
                HealingOrbs.RecoveryFinished -= OnRecoveryFinished;
            }

            foreach (var d in m_PartDamageables)
                if (d != null)
                    d.OnDamageInflicted -= OnPartHit;
            m_PartDamageables.Clear();
        }

        void OnRecoveryStarted()
        {
            if (m_IsDead)
                return;

            if (Scheduler != null)
                Scheduler.Interrupt();
            if (BossHealth != null)
                BossHealth.Invincible = true;
            CloseWeakPoints();
            Debug.Log("[AvatarOfNature] RECOVERY started — destroy healing orbs.", this);
        }

        void OnRecoveryFinished()
        {
            if (m_IsDead)
                return;

            if (BossHealth != null)
                BossHealth.Invincible = false;
            CloseWeakPoints();
            if (Scheduler != null)
                Scheduler.NotifyRecoveryFinished();
            Debug.Log("[AvatarOfNature] RECOVERY finished — boss vulnerable.", this);
        }

        void CloseWeakPoints()
        {
            if (WeakPoints == null)
                return;
            foreach (var weakPoint in WeakPoints)
                if (weakPoint != null)
                    weakPoint.SetExposed(false);
        }

        void OnBossDie()
        {
            m_IsDead = true;
            var duel = GetComponent<AvatarBossDuelController>();
            if (duel != null)
                duel.DebugReset();
            if (HealingOrbs != null)
                HealingOrbs.ClearHealingOrbs();
            Debug.Log("[AvatarOfNature] Boss died.", this);
            PlayCue(AvatarBossCue.BossDeath);
            Stagger.ResetStagger();

            // damage is fully ignored after DEFEATED (no HP drop / stagger / feedback)
            BossHealth.Invincible = true;

            if (m_VulnerabilityRoutine != null)
            {
                StopCoroutine(m_VulnerabilityRoutine);
                m_VulnerabilityRoutine = null;
            }
            foreach (var weakPoint in WeakPoints)
                weakPoint.SetExposed(false);

            if (Scheduler != null)
                Scheduler.Shutdown();
        }

        void OnBossDamaged(float damage, GameObject damageSource)
        {
            if (m_IsDead)
                return;

            if (Stagger != null)
                Stagger.AddStagger(damage, damageSource);
        }

        void OnStaggerFull()
        {
            if (m_IsDead)
                return;
            EventManager.Broadcast(new CameraImpulseEvent
            {
                Strength = 0.42f,
                Duration = 0.26f,
                Direction = Vector3.back
            });
            Debug.Log("[AvatarOfNature] STAGGER BROKEN — weak points exposed!", this);
            PlayCue(AvatarBossCue.StaggerBreak);

            if (Scheduler != null)
                Scheduler.Interrupt();

            if (Stagger != null)
                Stagger.ResetStagger();

            if (m_VulnerabilityRoutine != null)
                StopCoroutine(m_VulnerabilityRoutine);
            TriggerDuelWindow(VulnerabilityDuration);
        }

        /// <summary>
        /// Opens the existing weak-point window for a duel resolution. The stagger path
        /// and the arena-duel path share the same cleanup behavior.
        /// </summary>
        public bool TriggerDuelWindow(float duration)
        {
            if (m_IsDead || WeakPoints == null || WeakPoints.Length == 0)
                return false;

            if (m_VulnerabilityRoutine != null)
                StopCoroutine(m_VulnerabilityRoutine);
            m_VulnerabilityRoutine = StartCoroutine(ExposeWeakPointsRoutine(duration));
            return true;
        }

        void PlayCue(AvatarBossCue cue)
        {
            var cues = GetComponentInChildren<AvatarBossAudioCues>();
            if (cues != null)
                cues.Play(cue);
        }

        /// <summary>Called by AvatarBossPhaseController at the end of the transition.</summary>
        public void NotifyPhase2()
        {
            if (!PhaseTwo)
                PhaseTwo = true;
        }

        /// <summary>Debug/test-only: set boss health to a ratio of max (ignored while dead).</summary>
        public void DebugSetHealth(float ratio)
        {
            if (BossHealth == null || m_IsDead)
                return;
            BossHealth.CurrentHealth = Mathf.Clamp(BossHealth.MaxHealth * ratio, 0f, BossHealth.MaxHealth);
        }

        /// <summary>Debug/test-only: fully reset the boss to a clean phase-1 state without a scene
        /// reload. Restores health/stagger/scheduler, closes weak points, clears phase 2, summons
        /// and presentation state.</summary>
        public void DebugResetBoss()
        {
            m_IsDead = false;
            PhaseTwo = false;            if (HealingOrbs != null)
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
                    if (weakPoint == null) continue;
                    weakPoint.DebugReset();
                }
            }

            if (Scheduler != null)
                Scheduler.DebugReset();

            var phase = GetComponentInChildren<AvatarBossPhaseController>();
            if (phase != null)
                phase.DebugResetPhase();

            var summons = GetComponentInChildren<AvatarBossSummonController>();
            if (summons != null)
                summons.DebugReset();

            var visual = GetComponentInChildren<AvatarBossVisualEffects>();
            if (visual != null)
                visual.DebugReset();

            var duel = GetComponent<AvatarBossDuelController>();
            if (duel != null)
                duel.DebugReset();

            Debug.Log("[AvatarOfNature] Debug reset: boss restored to phase 1.", this);
        }

        System.Collections.IEnumerator ExposeWeakPointsRoutine(float duration)
        {
            foreach (var weakPoint in WeakPoints)
            {
                // Phase 1: WeakPointA only; Phase 2: all (rule set on WeakPoint.PhaseTwoAttached)
                if (PhaseTwo || !weakPoint.PhaseTwoAttached)
                    weakPoint.SetExposed(true);
            }
            PlayCue(AvatarBossCue.WeakPointOpen);

            yield return new WaitForSeconds(duration);

            foreach (var weakPoint in WeakPoints)
                weakPoint.SetExposed(false);

            m_VulnerabilityRoutine = null;
        }
    }
}
