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

        [Header("Hitbox")]
        [Tooltip("Normalizes BossBody to a capsule at runtime so the active arena cannot keep a stale box collider.")]
        public bool NormalizeBodyHitbox = true;
        [Min(0.01f)] public float BodyHitboxRadius = 0.575f;
        [Min(0.01f)] public float BodyHitboxHeight = 1.25f;

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
            ConfigureBodyHitbox();
            ConfigureArenaProjectileBlockers();

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

        void ConfigureBodyHitbox()
        {
            if (!NormalizeBodyHitbox)
                return;

            var body = transform.Find("BossBody");
            if (body == null)
            {
                Debug.LogWarning($"[AvatarOfNature][BossHitbox] BossBody was not found on '{name}' in scene '{gameObject.scene.name}'.", this);
                return;
            }

            var capsule = body.GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = body.gameObject.AddComponent<CapsuleCollider>();

            capsule.center = Vector3.zero;
            capsule.radius = BodyHitboxRadius;
            capsule.height = Mathf.Max(BodyHitboxHeight, BodyHitboxRadius * 2f);
            capsule.direction = 1;
            capsule.isTrigger = false;
            capsule.enabled = true;

            ConfigureUpperBodyHitbox(body);
            ConfigureArmHitboxes(body);

            // The old box is the source of the inconsistent top/bottom hit results.
            // Keep it in the scene for backwards compatibility, but never let physics use it.
            var legacyBox = body.GetComponent<BoxCollider>();
            if (legacyBox != null)
                legacyBox.enabled = false;

            Debug.Log($"[AvatarOfNature][BossHitbox] scene={gameObject.scene.name} " +
                      $"boss={name} body={body.name} collider=CapsuleCollider " +
                      $"radius={capsule.radius:F3} height={capsule.height:F3} " +
                      $"worldSize={capsule.bounds.size}", this);
        }

        void ConfigureUpperBodyHitbox(Transform body)
        {
            var capsules = body.GetComponents<CapsuleCollider>();
            CapsuleCollider upper = capsules.Length > 1 ? capsules[1] : body.gameObject.AddComponent<CapsuleCollider>();

            // VisualRoot's head reaches well above the torso collider. This second
            // capsule covers the head/shoulders while still sharing BossBody's
            // Damageable and damage multiplier.
            upper.center = new Vector3(0f, 1.05f, 0f);
            upper.radius = 0.40f;
            upper.height = 1.10f;
            upper.direction = 1;
            upper.isTrigger = false;
            upper.enabled = true;

            Debug.Log($"[AvatarOfNature][BossHitbox] upperBody=CapsuleCollider " +
                      $"center={upper.bounds.center} worldSize={upper.bounds.size}", body);
        }

        void ConfigureArmHitboxes(Transform body)
        {
            var boxes = body.GetComponents<BoxCollider>();
            var left = FindOrAddArmBox(body, boxes, "left");
            var right = FindOrAddArmBox(body, boxes, "right");

            ConfigureArmBox(left, new Vector3(-0.68f, 0.50f, 0f));
            ConfigureArmBox(right, new Vector3(0.68f, 0.50f, 0f));

            Debug.Log($"[AvatarOfNature][BossHitbox] arms=BoxCollider " +
                      $"leftWorld={left.bounds.size} rightWorld={right.bounds.size}", body);
        }

        BoxCollider FindOrAddArmBox(Transform body, BoxCollider[] existing, string side)
        {
            // The first BoxCollider is the legacy torso collider. Reuse later
            // components when domain reload/runtime setup is repeated.
            int desiredIndex = side == "left" ? 1 : 2;
            if (existing.Length > desiredIndex)
                return existing[desiredIndex];
            return body.gameObject.AddComponent<BoxCollider>();
        }

        void ConfigureArmBox(BoxCollider box, Vector3 localCenter)
        {
            box.center = localCenter;
            box.size = new Vector3(1.04f, 0.86f, 0.24f);
            box.isTrigger = false;
            box.enabled = true;
        }

        void ConfigureArenaProjectileBlockers()
        {
            // BossCenterPlatform is authored with a very wide non-uniform scale.
            // A CapsuleCollider on that object becomes a tall rounded volume and
            // intercepts shots in front of the boss, leaving only the upper body
            // apparently hittable. Keep the platform physical, but make its
            // collider match the visible flat platform.
            var platform = GameObject.Find("BossCenterPlatform");
            if (platform == null)
                return;

            var legacyCapsule = platform.GetComponent<CapsuleCollider>();
            if (legacyCapsule != null)
                legacyCapsule.enabled = false;

            var box = platform.GetComponent<BoxCollider>();
            if (box == null)
                box = platform.AddComponent<BoxCollider>();

            box.center = Vector3.zero;
            box.size = new Vector3(1f, 2f, 1f);
            box.isTrigger = false;
            box.enabled = true;

            Debug.Log($"[AvatarOfNature][BossHitbox] platform={platform.name} " +
                      $"legacyCapsule={(legacyCapsule != null ? "disabled" : "none")} " +
                      $"collider=BoxCollider worldSize={box.bounds.size}", platform);
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
                Debug.Log($"HIT target={(part != null ? part.name : "null")} " +
                          $"weakPoint={weakPoint} collider={(part != null ? part.GetComponent<Collider>()?.GetType().Name : "null")} " +
                          $"rawDamage={raw:F1} appliedDamage={applied:F1} multiplier={mult:F2} " +
                          $"hpAfter={hpAfter:F1} stagger={stagger:F1}", part);
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
