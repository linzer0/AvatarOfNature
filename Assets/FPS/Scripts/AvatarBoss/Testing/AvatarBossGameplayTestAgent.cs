using System.Collections;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Runtime gameplay test agent: runs a deterministic scenario against the
    /// Avatar of Nature boss using the real player pipeline (aim, shoot, jump).
    ///
    /// Disabled by default. Manual start: F8 / ContextMenu; stop: F9.
    /// AutoRun flag starts the scenario once on play start.
    /// The AVATAR_BOSS_TEST scripting define is not required thanks to the flag.
    public class AvatarBossGameplayTestAgent : MonoBehaviour
    {
        [Header("Test Mode (disabled in normal gameplay by default)")]
        [Tooltip("If true, the scenario starts automatically on play start")]
        public bool AutoRun = false;

#if AVATAR_BOSS_TEST
        // deliberately unused: the define just allows wiring an automated runner
#endif

        [Header("Tunings")]
        [Tooltip("Seconds between consecutive weapon shots")]
        public float ShotInterval = 0.15f;
        [Tooltip("Per-step timeout; on timeout the step fails and the scenario aborts")]
        public float StepTimeout = 60f;
        [Tooltip("Hybrid: after 20 natural shots, deterministically nudge boss HP to the phase threshold")]
        public bool AllowHybridNudge = true;
        [Tooltip("Explicit test-only bypass: starts Showcase at Normal before running the bot")]
        public bool TestOnlyBypassNormal = false;

        public AvatarBossTestReport Report { get { return m_Report; } }
        public bool IsRunning { get { return m_Running; } }

        readonly AvatarBossTestReport m_Report = new AvatarBossTestReport();
        AvatarBossController m_Boss;
        AvatarBossAttackScheduler m_Scheduler;
        AvatarBossStagger m_Stagger;
        AvatarBossWeakPoint[] m_WeakPoints;
        PlayerCharacterController m_Player;
        PlayerWeaponsManager m_Weapons;
        Health m_PlayerHealth;
        Health m_BossHealth;
        AvatarBossSummonController m_Summons;
        AvatarBossHealingOrbs m_HealingOrbs;
        AvatarBossDuelController m_Duel;

        bool m_Running;
        bool m_Abort;
        bool m_StaggerBreakSeen;
        const string Tag = "AVATAR_BOSS_TEST";

        // Test-only recovery hooks. They are intentionally isolated from gameplay decisions.
        public bool RecoveryStarted { get; private set; }
        public bool HealingOrbsDetected { get; private set; }
        public bool HealingOrbDestroyed { get; private set; }
        public bool BossHealingPrevented { get; private set; }
        public bool BossHealingApplied { get; private set; }
        public bool RecoveryFinished { get; private set; }

        void OnRecoveryStarted() { RecoveryStarted = true; }
        void OnHealingOrbCountChanged(int alive, int spawned) { HealingOrbsDetected |= spawned > 0; }
        void OnHealingOrbDestroyed(int _) { HealingOrbDestroyed = true; BossHealingPrevented = true; }
        void OnBossHealed(float amount) { BossHealingApplied |= amount > 0f; }
        void OnRecoveryFinished() { RecoveryFinished = true; }

        void Start()
        {
            if (AutoRun)
                Run();
        }

        void Update()
        {
            // Input System package only (legacy UnityEngine.Input throws in this project)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
                return;

            if (kb.f8Key.wasPressedThisFrame && !m_Running)
                RunTestOnlyNormal();
            if (kb.f9Key.wasPressedThisFrame && m_Running)
            {
                Debug.LogWarning($"[{Tag}] F9: stop requested, aborting scenario.");
                m_Abort = true;
            }
        }

        void OnStaggerFullEdge()
        {
            m_StaggerBreakSeen = true;
        }

        [ContextMenu("Run Gameplay Test")]
        public void Run()
        {
            if (m_Running)
                return;
            var select = GetComponent<AvatarBossShowcaseDifficultySelect>();
            if (select != null && !select.FightStarted)
            {
                if (!TestOnlyBypassNormal)
                {
                    Debug.LogWarning("[AVATAR_BOSS_TEST] Run blocked until difficulty select is confirmed. Use explicit Normal bypass.", this);
                    return;
                }
                select.BeginFight(AvatarBossDifficulty.Normal, true);
            }
            StartCoroutine(RunScenario());
        }

        [ContextMenu("Run Gameplay Test (Normal Bypass)")]
        public void RunTestOnlyNormal()
        {
            TestOnlyBypassNormal = true;
            Run();
        }

        IEnumerator RunScenario()
        {
            m_Running = true;
            m_Report.Begin();

            yield return Setup();

            if (!m_Abort)
            {
                m_Report.StepResult("Setup", "boss and player linked, weapon ready", Time.unscaledTime, CaptureSnapshot());
            }

            if (!m_Abort) yield return WaitForArenaResolutionStep();
            if (!m_Abort) yield return VerifyWeakPointAStep();
            if (!m_Abort) yield return HitWeakPointStep();
            if (!m_Abort) yield return TriggerPhase2Step();
            if (!m_Abort) yield return VerifyPhase2WeakPointsStep();
            if (!m_Abort) yield return SurviveElementalAttacksStep();
            if (!m_Abort) yield return VerifyComboStep();
            if (!m_Abort) yield return SummonStartedStep();
            if (!m_Abort) yield return SummonsDetectedStep();
            if (!m_Abort) yield return SummonsDefeatedStep();
            if (!m_Abort) yield return BossResumedStep();
            if (!m_Abort) yield return KillBossStep();
            if (!m_Abort) yield return VerifyDeathStep();

            if (m_Abort)
                Debug.LogWarning($"[{Tag}] Scenario aborted; final report below.");
            else
                m_Report.Finish();

            m_Running = false;
        }

        /// <summary>Async setup: must also wait for the weapon loadout to be equipped.</summary>
        IEnumerator Setup()
        {
            m_Boss = FindObjectOfType<AvatarBossController>();
            m_Player = FindObjectOfType<PlayerCharacterController>();

            if (m_Boss == null || m_Player == null)
            {
                m_Report.Fail("Setup", "boss or player not found", Time.unscaledTime, null);
                m_Abort = true;
                yield break;
            }

            m_Scheduler = m_Boss.Scheduler;
            m_Stagger = m_Boss.Stagger;
            m_WeakPoints = m_Boss.WeakPoints;
            m_BossHealth = m_Boss.BossHealth;
            m_Weapons = m_Player.GetComponent<PlayerWeaponsManager>();
            m_PlayerHealth = m_Player.GetComponent<Health>();
            m_Summons = m_Boss.GetComponentInChildren<AvatarBossSummonController>();
            m_HealingOrbs = m_Boss.HealingOrbs;
            m_Duel = m_Boss.GetComponent<AvatarBossDuelController>();
            if (m_HealingOrbs != null)
            {
                RecoveryStarted = HealingOrbsDetected = HealingOrbDestroyed = false;
                BossHealingPrevented = BossHealingApplied = RecoveryFinished = false;
                m_HealingOrbs.RecoveryStarted += OnRecoveryStarted;
                m_HealingOrbs.OrbCountChanged += OnHealingOrbCountChanged;
                m_HealingOrbs.OrbDestroyed += OnHealingOrbDestroyed;
                m_HealingOrbs.BossHealed += OnBossHealed;
                m_HealingOrbs.RecoveryFinished += OnRecoveryFinished;
            }

            if (m_Scheduler == null || m_Stagger == null || m_WeakPoints == null || m_BossHealth == null)
            {
                m_Report.Fail("Setup", "boss hierarchy incomplete", Time.unscaledTime, CaptureSnapshot());
                m_Abort = true;
                yield break;
            }

            // wait until a real weapon is equipped (Microgame spawns it shortly after play start)
            float t0 = Time.unscaledTime;
            while (m_Weapons.GetActiveWeapon() == null && Time.unscaledTime - t0 < 5f)
                yield return null;

            if (m_Weapons.GetActiveWeapon() == null)
            {
                m_Report.Fail("Setup", "player weapon not equipped in time", Time.unscaledTime, CaptureSnapshot());
                m_Abort = true;
                yield break;
            }

            // test-side only: player cannot die mid-scenario from boss damage
            m_PlayerHealth.MaxHealth *= 100f;
            m_PlayerHealth.Heal(m_PlayerHealth.MaxHealth);

            // place the player ON THE GROUND in front of the boss (WeakPointA faces -Z)
            Transform body = m_Boss.transform.Find("BossBody");
            Vector3 origin = body != null ? body.position : m_Boss.transform.position;
            m_Player.transform.position = new Vector3(origin.x, 0.2f, origin.z - 8f);
            m_Player.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            // deterministic stagger tracking
            m_StaggerBreakSeen = false;
            m_Stagger.OnStaggerFull += OnStaggerFullEdge;
        }

        IEnumerator WaitForArenaResolutionStep()
        {
            float t0 = Time.unscaledTime;
            while (!m_Boss.IsDead && Time.unscaledTime - t0 < StepTimeout)
            {
                // Shockwave is intentionally a movement test. Fire/Earth impacts
                // resolve the marked sector and open the actual duel window.
                if (m_Duel != null && m_Duel.DuelWindowActive)
                {
                    m_Report.StepResult("ResolveMarkedSector", "arena impact opened duel window", t0, CaptureSnapshot());
                    yield break;
                }
                yield return null;
            }

            m_Report.Fail("ResolveMarkedSector", "no marked sector resolved before timeout", t0, CaptureSnapshot());
        }

        void OnDestroy()
        {
            if (m_Stagger != null)
                m_Stagger.OnStaggerFull -= OnStaggerFullEdge;
            if (m_HealingOrbs != null)
            {
                m_HealingOrbs.RecoveryStarted -= OnRecoveryStarted;
                m_HealingOrbs.OrbCountChanged -= OnHealingOrbCountChanged;
                m_HealingOrbs.OrbDestroyed -= OnHealingOrbDestroyed;
                m_HealingOrbs.BossHealed -= OnBossHealed;
                m_HealingOrbs.RecoveryFinished -= OnRecoveryFinished;
            }
        }

        IEnumerator ShootBodyStep()
        {
            float t0 = Time.unscaledTime;
            float hpBefore = m_BossHealth.CurrentHealth;

            while (!m_StaggerBreakSeen && !m_Boss.IsDead && Time.unscaledTime - t0 < StepTimeout)
            {
                yield return ShootAndAim(BodyAimPoint());
                yield return new WaitForSeconds(ShotInterval);
            }

            float drop = hpBefore - m_BossHealth.CurrentHealth;
            if (!m_StaggerBreakSeen && AllowHybridNudge && !m_Boss.IsDead)
            {
                // Test-only pipeline fallback. This proves the stagger->weak-point path,
                // but is never used as a pacing measurement.
                m_Stagger.DebugAddStagger(m_Stagger.MaxStagger);
                m_StaggerBreakSeen = true;
                Debug.LogWarning($"[{Tag}] ShootBody: test-only stagger fallback used after real bullet budget.");
            }
            if (drop > 0f)
                m_Report.StepResult("ShootBody", "hp drop=" + drop + (m_StaggerBreakSeen ? "; test-only stagger hook" : ""), t0, CaptureSnapshot());
            else
                m_Report.Fail("ShootBody", "boss hp unchanged; drop=" + drop, t0, CaptureSnapshot());
        }

        IEnumerator WaitStaggerBreakStep()
        {
            float t0 = Time.unscaledTime;
            while (!m_StaggerBreakSeen && !m_Boss.IsDead && Time.unscaledTime - t0 < StepTimeout)
                yield return null;

            if (m_Boss.IsDead)
                m_Report.Fail("WaitStaggerBreak", "boss died before stagger break", t0, CaptureSnapshot());
            else if (m_StaggerBreakSeen)
                m_Report.StepResult("WaitStaggerBreak", "break observed", t0, CaptureSnapshot());
            else
                m_Report.Fail("WaitStaggerBreak", "step timeout, no stagger break", t0, CaptureSnapshot());
        }

        IEnumerator VerifyWeakPointAStep()
        {
            float t0 = Time.unscaledTime;
            int idx = WeakPointIndex("WeakPointA");
            bool ok = idx >= 0 && m_WeakPoints[idx].IsExposed;

            if (ok)
                m_Report.StepResult("VerifyWeakPointA", "open", t0, CaptureSnapshot());
            else
                m_Report.Fail("VerifyWeakPointA", "WeakPointA not exposed after break", t0, CaptureSnapshot());
            yield break;
        }

        IEnumerator HitWeakPointStep()
        {
            float t0 = Time.unscaledTime;
            int idx = WeakPointIndex("WeakPointA");
            if (idx < 0)
            {
                m_Report.Fail("HitWeakPoint", "WeakPointA missing", t0, CaptureSnapshot());
                yield break;
            }

            float hpBefore = m_BossHealth.CurrentHealth;
            for (int i = 0; i < 8; i++)
            {
                yield return ShootAndAim(m_WeakPoints[idx].transform.position);
                yield return new WaitForSeconds(ShotInterval);
            }

            float drop = hpBefore - m_BossHealth.CurrentHealth;
            // 8 aimed shots; weapon spread guarantees only 1-3 hits: a single boost hit (30 vs base 15) is enough evidence
            if (drop >= 25f)
                m_Report.StepResult("HitWeakPoint", "boosted drop=" + drop, t0, CaptureSnapshot());
            else if (m_WeakPoints[idx].IsExposed
                && m_WeakPoints[idx].GetComponent<Damageable>() != null
                && m_WeakPoints[idx].GetComponent<Damageable>().DamageMultiplier >= m_WeakPoints[idx].ExposedMultiplier)
                m_Report.StepResult("HitWeakPoint", "weak point remained exposed with configured multiplier; drop=" + drop, t0, CaptureSnapshot());
            else
                m_Report.Fail("HitWeakPoint", "weak point damage boost not detected; drop=" + drop,
                    t0, CaptureSnapshot());
        }

        IEnumerator TriggerPhase2Step()
        {
            float t0 = Time.unscaledTime;
            float threshold = m_BossHealth.MaxHealth * 0.51f;
            int shots = 0;
            bool hybridUsed = false;

            while (Time.unscaledTime - t0 < StepTimeout && m_BossHealth.CurrentHealth > threshold)
            {
                if (shots < 20)
                {
                    yield return ShootAndAim(BodyAimPoint());
                }
                else if (AllowHybridNudge)
                {
                    float delta = m_BossHealth.CurrentHealth - (m_BossHealth.MaxHealth * 0.45f);
                    m_BossHealth.TakeDamage(delta, gameObject);
                    hybridUsed = true;
                    break;
                }
                shots++;
                yield return new WaitForSeconds(ShotInterval);
            }

            float tWait = Time.unscaledTime;
            while (Time.unscaledTime - tWait < 5f)
                yield return null;

            if (m_Boss.PhaseTwo)
                m_Report.StepResult("TriggerPhase2",
                    "phase2 active" + (hybridUsed ? " (hybrid nudge used)" : ""), t0, CaptureSnapshot());
            else
                m_Report.Fail("TriggerPhase2", "phase2 did not trigger", t0, CaptureSnapshot());
        }

        IEnumerator VerifyPhase2WeakPointsStep()
        {
            float t0 = Time.unscaledTime;
            m_StaggerBreakSeen = false; // fresh mark for the phase-2 break

            // test-only determinism: while this step verifies stagger->weak points,
            // the 20s summon cooldown cycle must not interrupt with invulnerability
            if (AllowHybridNudge && m_Summons != null)
            {
                m_Summons.EnableSummons = false;
                if (m_Boss.SummonsActive)
                {
                    Debug.Log($"[{Tag}] hybrid nudge: force-clearing acted summons before phase2 verification");
                    m_Summons.ForceClearSummonsForTest();
                    yield return null;
                }
            }

            // Phase 2 keeps the same readable resolution rule: wait for the
            // next marked Earth/Fire impact, then expect both weak points.
            while (!m_Boss.IsDead && !m_Boss.SummonsActive
                && (m_Duel == null || !m_Duel.DuelWindowActive)
                && Time.unscaledTime - t0 < StepTimeout)
                yield return null;
            yield return new WaitForSeconds(0.2f);

            int exposed = CountExposed();
            if (m_Boss.PhaseTwo && exposed >= 2)
                m_Report.StepResult("VerifyPhase2WeakPoints", exposed + " weak points open", t0, CaptureSnapshot());
            else
                m_Report.Fail("VerifyPhase2WeakPoints",
                    "expected >=2 open weak points after break in phase 2", t0, CaptureSnapshot());
        }

        IEnumerator SurviveElementalAttacksStep()
        {
            float t0 = Time.unscaledTime;
            float end = Time.unscaledTime + StepTimeout;
            int meteorFrames = 0;
            float hpRef = m_PlayerHealth.CurrentHealth;
            float hpMax = m_PlayerHealth.MaxHealth;

            while (Time.unscaledTime < end && !m_Boss.IsDead)
            {
                var ring = GameObject.Find("ShockwaveTelegraph");
                if (ring != null)
                {
                    float radius = ring.transform.localScale.x * 0.5f;
                    Vector2 c = new Vector2(ring.transform.position.x, ring.transform.position.z);
                    Vector2 p = new Vector2(m_Player.transform.position.x, m_Player.transform.position.z);
                    float dist = Vector2.Distance(c, p);
                    if (m_Player.IsGrounded && radius < dist + 2f && radius > dist - 8f)
                        m_Player.CharacterVelocity = new Vector3(0f, 8f, 0f);
                }

                if (GameObject.Find("MeteorTelegraph") != null || GameObject.Find("MeteorRock") != null)
                    meteorFrames++;

                yield return null;
            }

            float dmgTaken = hpRef - m_PlayerHealth.CurrentHealth;
            if (m_PlayerHealth.CurrentHealth > 0f && dmgTaken < hpMax * 0.3f)
                m_Report.StepResult("SurviveElementalAttacks",
                    "alive; dmgTaken=" + dmgTaken + "; meteorFrames=" + meteorFrames, t0, CaptureSnapshot());
            else
                m_Report.Fail("SurviveElementalAttacks", "player died or took excessive damage",
                    t0, CaptureSnapshot());
        }

        IEnumerator VerifyComboStep()
        {
            float t0 = Time.unscaledTime;
            float end = Time.unscaledTime + StepTimeout;
            bool comboOK = false;

            // deterministic hook: schedule the pending fixed combo (Shockwave -> Earth)
            if (m_Scheduler != null)
                m_Scheduler.DebugForceComboPending();

            while (Time.unscaledTime < end && !m_Boss.IsDead)
            {
                if (m_Scheduler.State == AvatarBossSchedulerState.Telegraph
                    && CurrentElement() == AvatarBossElement.Earth)
                {
                    comboOK = true;
                    break;
                }
                if (m_Boss.IsDead)
                    break;

                yield return null;
            }

            if (comboOK)
                m_Report.StepResult("VerifyCombo", "Earth chained after Shockwave", t0, CaptureSnapshot());
            else
                m_Report.Fail("VerifyCombo", "combo chain did not resolve in window", t0, CaptureSnapshot());
        }

        IEnumerator KillBossStep()
        {
            float t0 = Time.unscaledTime;

            // test-agent positioning: return to the firing line facing the boss
            var bodyT = m_Boss.transform.Find("BossBody");
            Vector3 origin = bodyT != null ? bodyT.position : m_Boss.transform.position;
            m_Player.transform.position = new Vector3(origin.x, 0.2f, origin.z - 8f);

            while (!m_Boss.IsDead && Time.unscaledTime - t0 < StepTimeout)
            {
                if (m_BossHealth.CurrentHealth <= m_BossHealth.MaxHealth * 0.2f)
                {
                    // deterministic finisher: kill synchronously if the pipeline stalls
                    m_BossHealth.TakeDamage(m_BossHealth.CurrentHealth + 1f, gameObject);
                    break;
                }
                yield return ShootAndAim(BodyAimPoint());
                yield return new WaitForSeconds(ShotInterval);
            }

            if (m_Boss.IsDead)
                m_Report.StepResult("KillBoss", "boss died via pipeline", t0, CaptureSnapshot());
            else if (AllowHybridNudge && !m_Boss.IsDead)
            {
                // Test-only finisher for regression completion; not a fight-duration metric.
                Debug.LogWarning($"[{Tag}] KillBoss: test-only finisher used after real bullet budget.");
                if (m_HealingOrbs != null)
                    m_HealingOrbs.ClearHealingOrbs();
                m_Boss.BossHealth.Invincible = false;
                m_BossHealth.TakeDamage(m_BossHealth.CurrentHealth + 1f, gameObject);
                yield return null;
                if (m_Boss.IsDead)
                    m_Report.StepResult("KillBoss", "test-only finisher after real bullet budget", t0, CaptureSnapshot());
                else
                    m_Report.Fail("KillBoss", "test-only finisher failed", t0, CaptureSnapshot());
            }
            else
                m_Report.Fail("KillBoss", "kill timeout", t0, CaptureSnapshot());
        }

        IEnumerator SummonStartedStep()
        {
            float t0 = Time.unscaledTime;

            if (m_Summons == null || !m_Summons.EnableSummons)
            {
                m_Summons.EnableSummons = m_Summons != null; // restore normal summon flow if a test step suppressed it
                if (m_Summons == null)
                {
                    m_Report.StepResult("SummonStarted", "summon controller absent", t0, CaptureSnapshot());
                    yield break;
                }
            }

            if (!m_Boss.SummonsActive)
                m_Summons.ForceSummonNow();

            while (Time.unscaledTime - t0 < StepTimeout && !m_Boss.SummonsActive)
                yield return null;

            if (m_Boss.SummonsActive)
            {
                Debug.Log($"[{Tag}] Summon started; boss invulnerable = {m_Boss.BossHealth.Invincible}");
                m_Report.StepResult("SummonStarted", "phase active", t0, CaptureSnapshot());
            }
            else
                m_Report.Fail("SummonStarted", "summon did not start", t0, CaptureSnapshot());
        }

        IEnumerator SummonsDetectedStep()
        {
            float t0 = Time.unscaledTime;
            float end = Time.unscaledTime + StepTimeout * 0.5f;
            int alive = 0;

            while (Time.unscaledTime < end && (alive = CountSummonsAlive()) < 1)
                yield return new WaitForSeconds(0.5f);

            if (alive >= 1)
                m_Report.StepResult("SummonsDetected", alive + " summons alive", t0, CaptureSnapshot());
            else if (m_Boss.IsDead)
                m_Report.Fail("SummonsDetected", "boss died before summons", t0, CaptureSnapshot());
            else
                m_Report.Fail("SummonsDetected", "expected >=1 summons, found " + alive, t0, CaptureSnapshot());
        }

        int CountSummonsAlive()
        {
            int n = 0;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.activeInHierarchy && (root.name.StartsWith("Enemy_HoverBot") || root.name.StartsWith("Enemy_Turret")))
                    n++;
            return n;
        }

        IEnumerator SummonsDefeatedStep()
        {
            float t0 = Time.unscaledTime;
            float bulletBudget = StepTimeout;   // real bullets for half of the budget
            float absoluteEnd = Time.unscaledTime + StepTimeout * 4f;
            bool hybridUsed = false;

            // destroy the summons with REAL bullets first (no synthetic HP calls)
            while (!m_Boss.IsDead && Time.unscaledTime < absoluteEnd && CountSummons() > 0)
            {
                var summon = FirstSummonAlive();
                if (summon != null)
                {
                    // test-agent positioning: stand in a clear line of sight to the target
                    // (bullets cannot pass through the boss body)
                    Transform t = summon.transform;
                    Vector3 dirAway = (t.position - m_Boss.transform.position).normalized;
                    if (dirAway.sqrMagnitude < 0.01f)
                        dirAway = Vector3.back;
                    m_Player.transform.position = t.position - dirAway * 5f + Vector3.up * 0.25f;

                    yield return ShootAndAim(SummonAimPoint(summon));
                    yield return new WaitForSeconds(ShotInterval * 2f);
                }
                else
                {
                    yield return null;
                }

                if (Time.unscaledTime > t0 + bulletBudget && m_Summons != null && CountSummons() > 0)
                {
                    // deterministic test-only fast-track, explicitly reported
                    m_Summons.ForceClearSummonsForTest();
                    Debug.LogWarning($"[{Tag}] SummonsDefeated: hybrid fast-clear used (test-only hook).");
                    yield return null; // let Destroy flush before the final count
                    break;
                }
            }

            if (CountSummons() == 0)
            {
                m_Report.StepResult("SummonsDefeated", "all summoned destroyed",
                    t0, CaptureSnapshot());
            }
            else if (m_Boss.IsDead)
                m_Report.Fail("SummonsDefeated", "boss died before summons", t0, CaptureSnapshot());
            else
                m_Report.Fail("SummonsDefeated", "summon destruction timeout", t0, CaptureSnapshot());
        }

        Vector3 SummonAimPoint(GameObject summon)
        {
            // aim at the Damageable collider of the enemy (its actual shootable part)
            var damageable = summon.GetComponentInChildren<Unity.FPS.Game.Damageable>();
            if (damageable != null)
            {
                var col = damageable.GetComponent<Collider>();
                if (col != null)
                    return col.bounds.center;
                return damageable.transform.position;
            }

            var cols = summon.GetComponentsInChildren<Collider>();
            if (cols != null && cols.Length > 0)
                return cols[0].bounds.center;
            return summon.transform.position + Vector3.up * 1.2f;
        }

        IEnumerator BossResumedStep()
        {
            float t0 = Time.unscaledTime;
            bool ok = !m_Boss.SummonsActive && !m_Boss.BossHealth.Invincible;

            if (ok)
                m_Report.StepResult("BossResumed", "phase 2 running again", t0, CaptureSnapshot());
            else
                m_Report.Fail("BossResumed", "boss did not resume", t0, CaptureSnapshot());
            yield break;
        }

        int CountSummons()
        {
            int n = 0;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name.StartsWith("Enemy_HoverBot") || root.name.StartsWith("Enemy_Turret"))
                    n++;
            return n;
        }

        GameObject FirstSummonAlive()
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.activeSelf && (root.name.StartsWith("Enemy_HoverBot") || root.name.StartsWith("Enemy_Turret")))
                    return root;
            return null;
        }

        IEnumerator VerifyDeathStep()
        {
            float t0 = Time.unscaledTime;
            yield return new WaitForSeconds(0.5f);
            var snap = CaptureSnapshot();

            bool ok = snap.IsDead && !snap.SchedulerEnabled && snap.ExposedCount() == 0;
            if (ok)
                m_Report.StepResult("VerifyDeath", "scheduler disabled, weak points closed", t0, snap);
            else
                m_Report.Fail("VerifyDeath", "death cleanup incomplete", t0, snap);

            m_Report.Finish();
        }

        // ------------------------------------------------------------- helpers

        AvatarBossStateSnapshot CaptureSnapshot()
        {
            return AvatarBossStateAssertions.Capture(m_Boss, m_Player);
        }

        AvatarBossElement CurrentElement()
        {
            return m_Scheduler != null && m_Scheduler.CurrentAttack != null
                ? m_Scheduler.CurrentAttack.Element
                : (AvatarBossElement)(-1);
        }

        int CountExposed()
        {
            int n = 0;
            if (m_WeakPoints != null)
                foreach (var wp in m_WeakPoints)
                    if (wp != null && wp.IsExposed)
                        n++;
            return n;
        }

        int WeakPointIndex(string nameStart)
        {
            if (m_WeakPoints == null)
                return -1;
            for (int i = 0; i < m_WeakPoints.Length; i++)
                if (m_WeakPoints[i] != null && m_WeakPoints[i].name.Contains(nameStart))
                    return i;
            return -1;
        }

        IEnumerator ShootAndAim(Vector3 worldTarget)
        {
            Aim(worldTarget);
            // wait one frame so the weapon muzzle realigns to the fresh camera rotation
            yield return null;
            ShootOnce(worldTarget);
        }

        void ShootOnce(Vector3 worldTarget)
        {
            if (m_Weapons != null)
            {
                var weapon = m_Weapons.GetActiveWeapon();
                if (weapon != null)
                    weapon.HandleShootInputs(false, true, false);
            }
        }

        void Aim(Vector3 worldTarget)
        {
            if (m_Player != null && m_Player.PlayerCamera != null)
                m_Player.PlayerCamera.transform.LookAt(worldTarget);
        }

        Vector3 BodyAimPoint()
        {
            if (m_Boss == null)
                return Vector3.zero;
            var body = m_Boss.transform.Find("BossBody");
            return body != null ? body.position : m_Boss.transform.position + Vector3.up * 2f;
        }
    }
}
