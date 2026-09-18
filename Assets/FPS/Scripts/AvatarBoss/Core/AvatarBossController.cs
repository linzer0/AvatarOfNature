using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// M1: wires boss Health damage intake into the stagger meter and triggers
    /// the weak point vulnerability window when stagger breaks.
    public partial class AvatarBossController : MonoBehaviour
    {
        [Header("Vulnerability Window")]
        [Tooltip("Seconds that weak points stay exposed after a stagger break")]
        public float VulnerabilityDuration = 3.5f;

        [Header("Encounter References")]
        [Tooltip("Explicit arena reference. Legacy scenes may leave this empty and use the context fallback.")]
        public AvatarBossArenaController ArenaReference;
        [Tooltip("Explicit player reference. Legacy scenes may leave this empty and use the context fallback.")]
        public PlayerCharacterController PlayerReference;

        public Health BossHealth { get; private set; }
        public AvatarBossStagger Stagger { get; private set; }
        public AvatarBossWeakPoint[] WeakPoints { get; private set; }
        public AvatarBossAttackScheduler Scheduler { get; private set; }
        public AvatarBossHealingOrbs HealingOrbs { get; private set; }
        public AvatarBossCombatContext CombatContext { get; private set; }

        public bool PhaseTwo { get; private set; }

        /// <summary>True while a summon intermission holds the boss. Blocks stagger breaks.</summary>
        public bool SummonsActive { get; set; }

        /// <summary>Fired on a validated hit: (world position, final damage, isWeakPoint).</summary>
        public event System.Action<Vector3, float, bool> OnBossHit;

        Coroutine m_VulnerabilityRoutine;
        bool m_IsDead;
        readonly List<Damageable> m_PartDamageables = new List<Damageable>();

        public bool IsDead => m_IsDead;

        public AvatarBossCombatContext GetCombatContext()
        {
            if (CombatContext == null)
                CombatContext = new AvatarBossCombatContext(ArenaReference, PlayerReference);
            return CombatContext;
        }

        void Awake()
        {
            CombatContext = new AvatarBossCombatContext(ArenaReference, PlayerReference);
            CombatContext.ResolveSceneReferences();

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

            if (AvatarBossDiagnostics.F10DiagnosticsEnabled)
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
                // Never push the camera toward the target on a confirmed weak-point
                // hit: that reads as an unwanted zoom-in in first person.
                Direction = Vector3.zero
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

        /// <summary>Called by AvatarBossPhaseController at the end of the transition.</summary>
        public void NotifyPhase2()
        {
            if (!PhaseTwo)
                PhaseTwo = true;
        }

    }
}
