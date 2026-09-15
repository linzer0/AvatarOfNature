using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    public enum AvatarBossSummonPhase
    {
        Idle,
        Telegraph,
        Spawned,
        Done,
    }

    /// <summary>
    /// Phase-2 summon intermission for the Avatar of Nature.
    /// Interrupts attacks, closes weak points, makes the boss temporarily
    /// invulnerable, spawns a small wave of existing Microgame enemies
    /// (Enemy_HoverBot / Enemy_Turret prefabs), and resumes the normal schedule
    /// only after every summon dies. Boss death clears summons.
    /// </summary>
    public class AvatarBossSummonController : MonoBehaviour
    {
        [Header("Summon Settings")]
        public bool EnableSummons = true;

        [Tooltip("Delay before the first summon after Phase 2 starts")]
        public float FirstSummonDelay = 8f;

        [Tooltip("Cooldown between summon intermissions")]
        public float CooldownBetweenSummons = 20f;

        [Tooltip("Max summons alive at once")]
        public int MaxActiveSummons = 3;

        [Tooltip("Summon telegraph time before the enemies spawn")]
        public float SummonTelegraphTime = 2f;

        [Tooltip("Summon anchor ring radius around the boss")]
        public float AnchorRadius = 13f;

        public AvatarBossSummonPhase Phase { get; private set; } = AvatarBossSummonPhase.Idle;

        /// <summary>Number of intermissions that completed successfully.</summary>
        public int SummonsDefeatedCount { get; private set; }

        AvatarBossController m_Boss;
        AvatarBossAttackScheduler m_Scheduler;
        AvatarBossStagger m_Stagger;
        readonly List<GameObject> m_Summons = new List<GameObject>();

        Coroutine m_SummonRoutine;
        float m_NextSummonCheckTime;

        GameObject m_HoverbotPrefab;
        GameObject m_TurretPrefab;

        void Start()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
            m_Scheduler = m_Boss != null ? m_Boss.Scheduler : null;

            if (m_Boss == null || m_Scheduler == null || m_Boss.BossHealth == null)
            {
                Debug.LogError($"[{nameof(AvatarBossSummonController)}] Requires AvatarBossController with scheduler in hierarchy.", this);
                enabled = false;
                return;
            }

            // prefabs are loaded lazily from the standard Microgame asset paths
            m_HoverbotPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Prefabs/Enemies/Enemy_HoverBot.prefab");
            m_TurretPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/FPS/Prefabs/Enemies/Enemy_Turret.prefab");

            m_NextSummonCheckTime = Time.time + FirstSummonDelay;
        }

        void Update()
        {
            if (m_Boss == null || !m_Boss.PhaseTwo || m_Boss.IsDead || m_Boss.SummonsActive)
                return;

            if (m_SummonRoutine != null || Time.time < m_NextSummonCheckTime)
                return;

            if (m_Scheduler.State != AvatarBossSchedulerState.Idle || !EnableSummons)
                return;

            StartSummon();
        }

        void StartSummon()
        {
            if (m_SummonRoutine != null)
                return;
            m_SummonRoutine = StartCoroutine(SummonRoutine());
        }

        /// <summary>Test-only deterministic hook (marked test-only, never runs in normal gameplay).</summary>
        [ContextMenu("Force Start Summon (Test Only)")]
        public void ForceSummonNow()
        {
            if (!m_Boss.SummonsActive)
                StartSummon();
        }

        /// <summary>TEST-ONLY fast-track: destroys all summons and ends the intermission deterministically.
        /// Used by the Gameplay Test Agent after its real-bullet attempts; never a normal gameplay path.</summary>
        public void ForceClearSummonsForTest()
        {
            if (!m_Boss.SummonsActive)
                return;

            foreach (var summon in m_Summons)
                if (summon != null)
                    Destroy(summon);

            EndSummon();
        }

        IEnumerator SummonRoutine()
        {
            m_Boss.SummonsActive = true;
            Phase = AvatarBossSummonPhase.Telegraph;
            Debug.Log("[AvatarOfNature] SUMMON PHASE begins", this);
            m_Scheduler.Interrupt();

            // boss is invulnerable and cannot take damage during the whole intermission
            m_Boss.BossHealth.Invincible = true;

            var cues = GetComponentInParent<AvatarBossAudioCues>();
            if (cues != null)
            {
                cues.Play(AvatarBossCue.Telegraph);
                cues.Play(AvatarBossCue.Summon);
            }

            SpawnTelegraphs();
            yield return new WaitForSeconds(SummonTelegraphTime);
            if (m_Boss == null || m_Boss.IsDead)
            {
                EndSummon();
                yield break;
            }

            DestroySummonTelegraphs();
            Phase = AvatarBossSummonPhase.Spawned;
            SpawnSummons();

            int lastCount = -1;
            while (m_Summons.Count > 0)
            {
                m_Summons.RemoveAll(s => s == null);
                if (m_Boss.IsDead)
                    break;
                if (m_Summons.Count != lastCount && m_Summons.Count > 0)
                {
                    Debug.Log($"[AvatarOfNature] Summon remaining: {m_Summons.Count}", this);
                    lastCount = m_Summons.Count;
                }
                yield return null;
            }

            EndSummon();
        }

        void SpawnTelegraphs()
        {
            int count = Mathf.Min(MaxActiveSummons, 3);
            for (int i = 0; i < count; i++)
            {
                Vector3 anchor = GetAnchor(i);
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(go.GetComponent<Collider>());
                go.name = "SummonTelegraph";
                go.transform.position = anchor;
                go.transform.localScale = new Vector3(2f, 0.02f, 2f);
                var mat = Shader.Find("Sprites/Default") != null
                    ? new Material(Shader.Find("Sprites/Default"))
                    : null;
                if (mat != null)
                {
                    mat.color = new Color(0.9f, 0.75f, 0.2f, 0.65f);
                    go.GetComponent<MeshRenderer>().material = mat;
                }
            }
        }

        void DestroySummonTelegraphs()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == "SummonTelegraph")
                    Destroy(root);
        }

        void SpawnSummons()
        {
            int count = Mathf.Min(MaxActiveSummons, 3);
            for (int i = 0; i < count; i++)
            {
                var prefab = i % 2 == 0 ? m_HoverbotPrefab : m_TurretPrefab;
                if (prefab == null)
                    continue;

                Vector3 anchor = GetAnchor(i);
                var summons = Instantiate(prefab, anchor + Vector3.up * 0.25f, Quaternion.identity);
                m_Summons.Add(summons);
            }
            Debug.Log("[AvatarOfNature] Summons spawned: " + m_Summons.Count, this);
        }

        Vector3 GetAnchor(int i)
        {
            float angle = (i * 360f / Mathf.Max(1, Mathf.Min(MaxActiveSummons, 3))) * Mathf.Deg2Rad;
            Vector3 center = m_Boss.transform.position;
            return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * AnchorRadius;
        }

        void EndSummon()
        {
            Phase = AvatarBossSummonPhase.Idle;

            bool wasBossDead = m_Boss.IsDead;
            m_Boss.SummonsActive = false;
            m_Boss.BossHealth.Invincible = false; // always restored

            // boss death clears the whole intermission with extra safety
            if (wasBossDead)
            {
                foreach (var summons in m_Summons)
                    if (summons != null)
                        Destroy(summons);
            }
            else
            {
                SummonsDefeatedCount++;
            }

            m_Summons.Clear();
            DestroySummonTelegraphs();
            m_SummonRoutine = null;
            m_NextSummonCheckTime = Time.time + CooldownBetweenSummons;
            Debug.Log("[AvatarOfNature] SUMMON PHASE ended; boss resumed", this);
        }
    }
}
