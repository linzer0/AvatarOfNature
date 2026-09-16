using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Difficulty free-run driver for AvatarBossShowcase.
    ///
    /// Plays the boss with the real player pipeline (aim + shoot + dodge) and
    /// WITHOUT any of the regression test hooks:
    ///   - no ForceClearSummonsForTest (summons are killed with real bullets),
    ///   - no hybrid HP nudge,
    ///   - no KillBoss finisher,
    ///   - player health is NOT inflated.
    ///
    /// This is an automated approximation of manual play (a deterministic bot that
    /// dodges telegraphs), NOT a substitute for a human blind test. It exists to
    /// produce comparable fight-time / damage-taken / death metrics across a balance
    /// pass.
    /// </summary>
    public class AvatarBossFreeRunAgent : MonoBehaviour
    {
        [Header("Free-run")]
        [Tooltip("If true, the free-run starts automatically on play start")]
        public bool AutoRun = false;

        [Tooltip("Hard ceiling on the run length (seconds)")]
        public float RunTimeout = 480f;

        [Tooltip("Seconds between consecutive weapon shots")]
        public float ShotInterval = 0.15f;

        [Tooltip("Distance from an area telegraph at which the bot starts dodging")]
        public float DodgeRadius = 3.5f;

        [Tooltip("Human-like reaction latency before the bot responds to a telegraph (seconds)")]
        public float ReactionLatency = 0.3f;

        [Tooltip("Body-only mode: never target exposed weak points (used for the body-gating check)")]
        public bool BodyOnlyMode = false;

        const string Tag = "AVATAR_BOSS_FREE_RUN";

        public bool IsRunning { get; private set; }

        public bool BossDead => m_Boss != null && m_Boss.IsDead;
        public bool PlayerDead => m_Player != null && m_Player.IsDead;
        public float FightTime => m_EndTime > 0f ? m_EndTime - m_FightStart : 0f;
        public float TimeToFirstStagger => m_FirstStaggerTime >= 0f ? m_FirstStaggerTime - m_FightStart : -1f;
        public float TimeToPhaseTwo => m_PhaseTwoTime >= 0f ? m_PhaseTwoTime - m_FightStart : -1f;
        public float PlayerDamageTaken => m_PlayerDamageTaken;
        public int Deaths => m_Deaths;
        public int TotalHits => m_TotalHits;
        public int UnfairHits => m_UnfairHits;
        public int Attacks => m_Scheduler != null ? m_Scheduler.AttackCount : -1;
        public int Combos => m_Scheduler != null ? m_Scheduler.ComboCount : -1;
        public int Meteors => m_Scheduler != null ? m_Scheduler.MeteorRainCount : -1;
        public int SummonPhases => m_SummonPhases;
        public int StaggerBreaks => m_StaggerBreaks;
        public float BodyDamageByBoss => m_BodyDamageByBoss;
        public float WeakPointDamageByBoss => m_WeakPointDamageByBoss;

        AvatarBossController m_Boss;
        AvatarBossAttackScheduler m_Scheduler;
        AvatarBossStagger m_Stagger;
        AvatarBossWeakPoint[] m_WeakPoints;
        PlayerCharacterController m_Player;
        PlayerWeaponsManager m_Weapons;
        Health m_PlayerHealth;
        Health m_BossHealth;
        AvatarBossSummonController m_Summons;

        float m_FightStart;
        float m_FirstStaggerTime = -1f;
        float m_PhaseTwoTime = -1f;
        float m_EndTime;
        float m_PlayerDamageTaken;
        int m_Deaths;
        int m_TotalHits;
        int m_UnfairHits;
        int m_SummonPhases;
        int m_StaggerBreaks;
        float m_BodyDamageByBoss;
        float m_WeakPointDamageByBoss;
        bool m_WasSummonsActive;
        float m_LastTelegraphSeenTime = -999f;
        float m_TelegraphBeganAt = -1f;
        float m_NextShotTime;
        float m_NextHopTime;
        Vector3 m_PendingTarget;
        bool m_HasPendingShot;

        void Start()
        {
            if (AutoRun)
                Run();
        }

        [ContextMenu("Run Free-run")]
        public void Run()
        {
            if (IsRunning)
                return;
            StartCoroutine(FreeRun());
        }

        IEnumerator FreeRun()
        {
            IsRunning = true;
            yield return Setup();

            if (m_Boss == null || m_Player == null)
            {
                Debug.LogError($"[{Tag}] Setup failed (boss/player missing); aborting.");
                IsRunning = false;
                yield break;
            }

            m_FightStart = Time.unscaledTime;
            Debug.Log($"[{Tag}] FREE-RUN START playerHp={m_PlayerHealth.MaxHealth} bossHp={m_BossHealth.MaxHealth} bodyOnly={BodyOnlyMode}");

            while (!m_Boss.IsDead && !m_Player.IsDead && Time.unscaledTime - m_FightStart < RunTimeout)
            {
                UpdateDodging();
                UpdateShooting();
                TrackMilestones();
                yield return null;
            }

            m_EndTime = Time.unscaledTime;
            float fightTime = m_EndTime - m_FightStart;
            string staggerStr = m_FirstStaggerTime >= 0f ? (m_FirstStaggerTime - m_FightStart).ToString("F1") : "never";
            string phase2Str = m_PhaseTwoTime >= 0f ? (m_PhaseTwoTime - m_FightStart).ToString("F1") : "never";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[{Tag}] ============== FREE-RUN RESULT ==============");
            sb.AppendLine($"[{Tag}] bossDead={m_Boss.IsDead} playerDead={m_Player.IsDead}");
            sb.AppendLine($"[{Tag}] fightTime={fightTime:F1}s");
            sb.AppendLine($"[{Tag}] timeToFirstStagger={staggerStr}");
            sb.AppendLine($"[{Tag}] timeToPhase2={phase2Str}");
            int attacks = m_Scheduler != null ? m_Scheduler.AttackCount : -1;
            int combos = m_Scheduler != null ? m_Scheduler.ComboCount : -1;
            int meteors = m_Scheduler != null ? m_Scheduler.MeteorRainCount : -1;
            int summonsDefeated = m_Summons != null ? m_Summons.SummonsDefeatedCount : -1;

            sb.AppendLine($"[{Tag}] playerDamageTaken={m_PlayerDamageTaken:F1} totalHits={m_TotalHits} unfairHits={m_UnfairHits}");
            sb.AppendLine($"[{Tag}] deaths={m_Deaths}");
            sb.AppendLine($"[{Tag}] staggerBreaks={m_StaggerBreaks} bodyDmg={m_BodyDamageByBoss:F1} weakPointDmg={m_WeakPointDamageByBoss:F1}");
            sb.AppendLine($"[{Tag}] attacks={attacks} combos={combos} meteors={meteors}");
            sb.AppendLine($"[{Tag}] summonPhases={m_SummonPhases} summonsDefeated={summonsDefeated}");
            Debug.Log(sb.ToString());

            WriteResultFile(sb.ToString());

            IsRunning = false;
        }

        void WriteResultFile(string summary)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.dataPath, "..", "FreeRunResults.log");
                System.IO.File.AppendAllText(path, summary + "\n");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[{Tag}] failed to write result file: {e.Message}");
            }
        }

        IEnumerator Setup()
        {
            m_Boss = FindFirstObjectByType<AvatarBossController>();
            m_Player = FindFirstObjectByType<PlayerCharacterController>();

            if (m_Boss == null || m_Player == null)
                yield break;

            m_Scheduler = m_Boss.Scheduler;
            m_Stagger = m_Boss.Stagger;
            m_WeakPoints = m_Boss.WeakPoints;
            m_BossHealth = m_Boss.BossHealth;
            m_Weapons = m_Player.GetComponent<PlayerWeaponsManager>();
            m_PlayerHealth = m_Player.GetComponent<Health>();
            m_Summons = m_Boss.GetComponentInChildren<AvatarBossSummonController>();

            if (m_PlayerHealth != null)
            {
                m_PlayerHealth.OnDamaged += OnPlayerDamaged;
                m_PlayerHealth.OnDie += OnPlayerDie;
            }
            if (m_Stagger != null)
                m_Stagger.OnStaggerFull += OnStaggerFull;
            if (m_Boss != null)
                m_Boss.OnBossHit += OnBossHitSplit;

            // wait for the weapon loadout to equip
            float t0 = Time.unscaledTime;
            while (m_Weapons != null && m_Weapons.GetActiveWeapon() == null && Time.unscaledTime - t0 < 5f)
                yield return null;

            // place the player on the ground in front of the boss (firing line)
            Transform body = m_Boss.transform.Find("BossBody");
            Vector3 origin = body != null ? body.position : m_Boss.transform.position;
            m_Player.transform.position = new Vector3(origin.x, 0.2f, origin.z - 8f);
            m_Player.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        void OnDestroy()
        {
            if (m_PlayerHealth != null)
            {
                m_PlayerHealth.OnDamaged -= OnPlayerDamaged;
                m_PlayerHealth.OnDie -= OnPlayerDie;
            }
            if (m_Stagger != null)
                m_Stagger.OnStaggerFull -= OnStaggerFull;
            if (m_Boss != null)
                m_Boss.OnBossHit -= OnBossHitSplit;
        }

        void OnPlayerDamaged(float damage, GameObject source)
        {
            m_PlayerDamageTaken += damage;
            m_TotalHits++;

            bool shockwaveActive = FindRoot("ShockwaveTelegraph") != null;
            bool airborne = m_Player != null && !m_Player.IsGrounded;
            bool recentTelegraph = Time.time - m_LastTelegraphSeenTime < 1.5f;

            // unfair heuristic: damage with no visible telegraph, or shockwave while airborne
            if (!recentTelegraph || (shockwaveActive && airborne))
            {
                m_UnfairHits++;
                Debug.Log($"[{Tag}] possible unfair hit dmg={damage:F1} source={(source != null ? source.name : "null")} airborne={airborne} shockwave={shockwaveActive}");
            }
        }

        void OnPlayerDie()
        {
            m_Deaths++;
            Debug.Log($"[{Tag}] PLAYER DIED (death #{m_Deaths})");
        }

        void OnStaggerFull()
        {
            m_StaggerBreaks++;
            if (m_FirstStaggerTime < 0f)
                m_FirstStaggerTime = Time.unscaledTime;
        }

        /// <summary>Test/лаб hook: splits boss HP loss into body vs weak point damage.</summary>
        void OnBossHitSplit(Vector3 position, float damage, bool isWeakPoint)
        {
            if (isWeakPoint)
                m_WeakPointDamageByBoss += damage;
            else
                m_BodyDamageByBoss += damage;
        }

        void TrackMilestones()
        {
            if (m_PhaseTwoTime < 0f && m_Boss.PhaseTwo)
                m_PhaseTwoTime = Time.unscaledTime;

            if (m_Boss.SummonsActive != m_WasSummonsActive)
            {
                m_WasSummonsActive = m_Boss.SummonsActive;
                if (m_WasSummonsActive)
                    m_SummonPhases++;
            }
        }

        void UpdateShooting()
        {
            if (m_Weapons == null || m_Weapons.GetActiveWeapon() == null)
                return;

            if (m_HasPendingShot)
            {
                ShootOnce();
                m_HasPendingShot = false;
            }
            else if (Time.unscaledTime >= m_NextShotTime)
            {
                Vector3 target = ChooseTarget();
                Aim(target);
                m_PendingTarget = target;
                m_HasPendingShot = true;
                m_NextShotTime = Time.unscaledTime + ShotInterval;
            }
        }

        Vector3 ChooseTarget()
        {
            if (m_Boss.SummonsActive)
            {
                var summon = FirstSummonAlive();
                if (summon != null)
                    return SummonAimPoint(summon);
            }

            if (!BodyOnlyMode)
            {
                foreach (var wp in m_WeakPoints)
                {
                    if (wp != null && wp.IsExposed)
                        return wp.transform.position;
                }
            }

            var body = m_Boss.transform.Find("BossBody");
            return body != null ? body.position : m_Boss.transform.position + Vector3.up * 2f;
        }

        void Aim(Vector3 worldTarget)
        {
            if (m_Player == null)
                return;

            // the player controller re-owns the camera pitch every frame, but never the yaw,
            // so we rotate the player transform itself to face the target horizontally
            Vector3 dir = worldTarget - m_Player.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                m_Player.transform.rotation = Quaternion.LookRotation(dir);

            if (m_Player.PlayerCamera != null)
                m_Player.PlayerCamera.transform.LookAt(worldTarget);
        }

        void ShootOnce()
        {
            if (m_Weapons == null)
                return;
            var weapon = m_Weapons.GetActiveWeapon();
            if (weapon != null)
                weapon.HandleShootInputs(false, true, false);
        }

        void UpdateDodging()
        {
            if (m_Player == null)
                return;

            GameObject shockwave = null;
            bool anyTelegraph = false;
            var areaTelegraphs = new List<Vector3>();

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root == null || !root.activeInHierarchy)
                    continue;
                string n = root.name;
                if (n == "ShockwaveTelegraph")
                {
                    shockwave = root;
                }
                else if (n == "MeteorTelegraph" || n == "EarthTelegraph")
                {
                    anyTelegraph = true;
                    areaTelegraphs.Add(root.transform.position);
                }
                else if (n == "MeteorRock" || n == "SummonTelegraph")
                    anyTelegraph = true;
            }

            if (anyTelegraph)
            {
                if (m_TelegraphBeganAt < 0f)
                    m_TelegraphBeganAt = Time.time;
                m_LastTelegraphSeenTime = Time.time;
            }
            else
            {
                m_TelegraphBeganAt = -1f;
            }

            // human-like latency: only respond once the telegraph has been visible long enough
            bool canReact = m_TelegraphBeganAt < 0f || (Time.time - m_TelegraphBeganAt) >= ReactionLatency;

            // shockwave: hop over the wave front while grounded
            if (canReact && shockwave != null && m_Player.IsGrounded && Time.unscaledTime >= m_NextHopTime)
            {
                float radius = shockwave.transform.localScale.x * 0.5f;
                float dist = HorizontalDistance(shockwave.transform.position, m_Player.transform.position);
                if (dist < radius + 3f && dist > radius - 9f)
                {
                    m_Player.transform.position += Vector3.up * 2.5f;
                    m_NextHopTime = Time.unscaledTime + 0.8f;
                }
            }

            // area attacks: flee the combined danger of all nearby telegraphs
            if (canReact && areaTelegraphs.Count > 0)
            {
                Vector3 player = m_Player.transform.position;
                Vector3 flee = Vector3.zero;
                foreach (var t in areaTelegraphs)
                {
                    float d = HorizontalDistance(t, player);
                    if (d < DodgeRadius + 2f)
                    {
                        Vector3 away = player - t;
                        away.y = 0f;
                        if (away.sqrMagnitude < 0.001f)
                            away = Vector3.back;
                        away = away.normalized;
                        flee += away * (1f - d / (DodgeRadius + 2f));
                    }
                }
                if (flee.sqrMagnitude > 0.001f)
                {
                    flee.y = 0f;
                    Vector3 newPos = player + flee.normalized * Mathf.Min(flee.magnitude * 2f + 2f, 8f);
                    newPos.y = player.y;
                    m_Player.transform.position = newPos;
                }
            }
        }

        float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        GameObject FindRoot(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root != null && root.name == name)
                    return root;
            return null;
        }

        GameObject FirstSummonAlive()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root != null && root.activeSelf
                    && (root.name.StartsWith("Enemy_HoverBot") || root.name.StartsWith("Enemy_Turret")))
                    return root;
            return null;
        }

        Vector3 SummonAimPoint(GameObject summon)
        {
            var damageable = summon.GetComponentInChildren<Damageable>();
            if (damageable != null)
            {
                var col = damageable.GetComponent<Collider>();
                if (col != null)
                    return col.bounds.center;
                return damageable.transform.position;
            }
            return summon.transform.position + Vector3.up * 1.2f;
        }
    }
}
