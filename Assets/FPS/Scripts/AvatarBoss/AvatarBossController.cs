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
        public float VulnerabilityDuration = 3f;

        public Health BossHealth { get; private set; }
        public AvatarBossStagger Stagger { get; private set; }
        public AvatarBossWeakPoint[] WeakPoints { get; private set; }
        public AvatarBossAttackScheduler Scheduler { get; private set; }

        public bool PhaseTwo { get; private set; }

        Coroutine m_VulnerabilityRoutine;
        bool m_IsDead;

        public bool IsDead => m_IsDead;

        void Awake()
        {
            BossHealth = GetComponentInParent<Health>();
            if (BossHealth == null)
                BossHealth = GetComponent<Health>();

            Stagger = GetComponentInChildren<AvatarBossStagger>();
            WeakPoints = GetComponentsInChildren<AvatarBossWeakPoint>();
            Scheduler = GetComponentInChildren<AvatarBossAttackScheduler>();

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
        }

        void OnBossDie()
        {
            m_IsDead = true;
            Debug.Log("[AvatarOfNature] Boss died.", this);
            Stagger.ResetStagger();

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
            Debug.Log("[AvatarOfNature] STAGGER BROKEN — weak points exposed!", this);

            if (Scheduler != null)
                Scheduler.Interrupt();

            if (Stagger != null)
                Stagger.ResetStagger();

            if (m_VulnerabilityRoutine != null)
                StopCoroutine(m_VulnerabilityRoutine);
            m_VulnerabilityRoutine = StartCoroutine(ExposeWeakPointsRoutine());
        }

        /// <summary>Called by AvatarBossPhaseController at the end of the transition.</summary>
        public void NotifyPhase2()
        {
            if (!PhaseTwo)
                PhaseTwo = true;
        }

        System.Collections.IEnumerator ExposeWeakPointsRoutine()
        {
            foreach (var weakPoint in WeakPoints)
            {
                // Phase 1: WeakPointA only; Phase 2: all (rule set on WeakPoint.PhaseTwoAttached)
                if (PhaseTwo || !weakPoint.PhaseTwoAttached)
                    weakPoint.SetExposed(true);
            }

            yield return new WaitForSeconds(VulnerabilityDuration);

            foreach (var weakPoint in WeakPoints)
                weakPoint.SetExposed(false);

            m_VulnerabilityRoutine = null;
        }
    }
}
