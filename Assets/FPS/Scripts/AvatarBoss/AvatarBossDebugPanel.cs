using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Boss Debug Panel — manual test harness for the Avatar of Nature, AvatarBossShowcase only.
    ///
    /// Disabled by default; the N key toggles it (Input System only, never legacy Input,
    /// never F8/F9/F10). Reuses the existing public boss APIs plus minimal Debug/Test-only
    /// methods; no reflection, no duplicated gameplay logic.
    /// </summary>
    public class AvatarBossDebugPanel : MonoBehaviour
    {
        [Header("Debug Panel")]
        [Tooltip("Boss controller; auto-resolved when empty.")]
        public AvatarBossController Boss;

        static readonly string[] s_RuntimePrefixes =
        {
            "EarthTelegraph", "EarthSpike",
            "MeteorTelegraph", "MeteorRock",
            "ShockwaveTelegraph", "SummonTelegraph",
            "HitImpact", "WeakPointImpact", "DamageNumber",
            "Enemy_HoverBot", "Enemy_Turret",
        };

        bool m_Shown;
        Vector2 m_Scroll;

        AvatarBossAttackScheduler m_Scheduler;
        AvatarBossStagger m_Stagger;
        AvatarBossPhaseController m_Phase;
        AvatarBossSummonController m_Summons;
        PlayerCharacterController m_Player;

        void Start()
        {
            if (SceneManager.GetActiveScene().name != "AvatarBossShowcase")
            {
                enabled = false;
                return;
            }
            ResolveReferences();
        }

        void ResolveReferences()
        {
            if (Boss == null)
                Boss = GetComponentInParent<AvatarBossController>();
            if (Boss == null)
                Boss = FindFirstObjectByType<AvatarBossController>();
            if (Boss == null)
                return;

            m_Scheduler = Boss.Scheduler;
            m_Stagger = Boss.Stagger;
            m_Phase = Boss.GetComponentInChildren<AvatarBossPhaseController>();
            m_Summons = Boss.GetComponentInChildren<AvatarBossSummonController>();
            m_Player = FindFirstObjectByType<PlayerCharacterController>();
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
                return;

            if (kb.nKey.wasPressedThisFrame)
            {
                m_Shown = !m_Shown;
                if (m_Shown)
                    ResolveReferences();
            }
        }

        void OnGUI()
        {
            if (!m_Shown || Boss == null || Boss.BossHealth == null)
                return;

            float width = 430f;
            var area = new Rect(Screen.width - width - 12f, 12f, width, Screen.height - 24f);
            GUILayout.BeginArea(area, "BOSS DEBUG PANEL  [N]", GUI.skin.window);
            m_Scroll = GUILayout.BeginScrollView(m_Scroll);

            DrawSnapshot();
            GUILayout.Space(6f);
            DrawBossStateControls();
            GUILayout.Space(6f);
            DrawAttackControls();
            GUILayout.Space(6f);
            DrawSummonControls();
            GUILayout.Space(6f);
            DrawSceneControls();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ snapshot

        void DrawSnapshot()
        {
            GUILayout.Label("SNAPSHOT", GUI.skin.box);

            var health = Boss.BossHealth;
            float hp = health.CurrentHealth;
            float hpMax = health.MaxHealth;
            float ratio = hpMax > 0f ? hp / hpMax : 0f;

            GUI.color = Boss.IsDead ? new Color(0.6f, 0.6f, 0.6f)
                : ratio <= 0.5f ? Color.yellow : Color.white;
            GUILayout.Label($"HP: {hp:F0} / {hpMax:F0}");
            GUI.color = Color.white;

            GUI.color = Boss.PhaseTwo ? new Color(1f, 0.3f, 0.25f) : Color.white;
            GUILayout.Label($"PHASE: {(Boss.PhaseTwo ? 2 : 1)}");
            GUI.color = Color.white;

            GUILayout.Label($"STATE: {StateLabel()}");
            GUILayout.Label($"ATTACK: {AttackLabel()}");

            if (m_Stagger != null)
            {
                GUI.color = m_Stagger.IsFull ? new Color(1f, 0.85f, 0.3f) : Color.white;
                GUILayout.Label($"STAGGER: {m_Stagger.CurrentStagger:F0} / {m_Stagger.MaxStagger:F0}");
                GUI.color = Color.white;
            }

            DrawWeakPointLine("WEAK POINT A", FindWeakPoint("WeakPointA"));
            DrawWeakPointLine("WEAK POINT B", FindWeakPoint("WeakPointB"));

            int summons = m_Summons != null ? m_Summons.ActiveSummonCount : 0;
            GUILayout.Label($"SUMMONS: {summons}");

            bool schedEnabled = m_Scheduler != null && m_Scheduler.enabled;
            GUI.color = schedEnabled ? Color.white : Color.yellow;
            GUILayout.Label($"SCHEDULER: {(schedEnabled ? "ACTIVE" : "OFF")}");
            GUI.color = Color.white;

            GUI.color = Boss.IsDead ? new Color(0.75f, 0.25f, 0.25f) : Color.white;
            GUILayout.Label($"DEAD: {Boss.IsDead}");
            GUI.color = Color.white;

            var playerHealth = m_Player != null ? m_Player.GetComponent<Health>() : null;
            if (playerHealth != null)
                GUILayout.Label($"PLAYER HP: {playerHealth.CurrentHealth:F0} / {playerHealth.MaxHealth:F0}");
        }

        void DrawWeakPointLine(string label, AvatarBossWeakPoint wp)
        {
            if (wp == null)
            {
                GUILayout.Label($"{label}: MISSING");
                return;
            }
            bool exposed = wp.IsExposed;
            GUI.color = exposed ? new Color(0.35f, 1f, 0.55f) : Color.white;
            GUILayout.Label($"{label}: {(exposed ? "EXPOSED" : "CLOSED")}");
            GUI.color = Color.white;
        }

        string StateLabel()
        {
            if (Boss.IsDead)
                return "DEFEATED";
            if (m_Stagger != null && m_Stagger.IsFull)
                return "STAGGERED";
            if (Boss.SummonsActive)
                return "SUMMON";
            return m_Scheduler != null ? m_Scheduler.State.ToString().ToUpperInvariant() : "—";
        }

        string AttackLabel()
        {
            return m_Scheduler != null && m_Scheduler.CurrentAttack != null
                ? m_Scheduler.CurrentAttack.Element.ToString().ToUpperInvariant()
                : "NONE";
        }

        AvatarBossWeakPoint FindWeakPoint(string name)
        {
            if (Boss.WeakPoints == null)
                return null;
            foreach (var wp in Boss.WeakPoints)
                if (wp != null && wp.name.Contains(name))
                    return wp;
            return null;
        }

        // ------------------------------------------------------------------ controls

        void DrawBossStateControls()
        {
            GUILayout.Label("BOSS STATE", GUI.skin.box);
            if (Button("RESET BOSS")) Boss.DebugResetBoss();
            if (Button("SET HP 100%")) Boss.DebugSetHealth(1f);
            if (Button("SET HP 49%")) Boss.DebugSetHealth(0.49f);
            if (Button("TRIGGER PHASE 2")) TriggerPhase2();
            if (Button("ADD STAGGER +25")) m_Stagger?.DebugAddStagger(25f);
            if (Button("FILL STAGGER")) m_Stagger?.DebugAddStagger(m_Stagger.MaxStagger);
            if (Button("EXPOSE WEAK POINTS")) SetWeakPointsExposed(true);
            if (Button("CLOSE WEAK POINTS")) SetWeakPointsExposed(false);
            if (Button("KILL BOSS")) Boss.BossHealth.Kill();
        }

        void DrawAttackControls()
        {
            GUILayout.Label("ATTACKS", GUI.skin.box);
            if (Button("FORCE EARTH TELEGRAPH")) m_Scheduler?.DebugForceAttack(AvatarBossElement.Earth);
            if (Button("FORCE SHOCKWAVE TELEGRAPH")) m_Scheduler?.DebugForceAttack(AvatarBossElement.Shockwave);
            if (Button("FORCE METEOR RAIN")) m_Scheduler?.DebugForceAttack(AvatarBossElement.Fire);
            if (Button("FORCE COMBO")) m_Scheduler?.DebugForceCombo();
            if (Button("STOP CURRENT ATTACK")) m_Scheduler?.Interrupt();
        }

        void DrawSummonControls()
        {
            GUILayout.Label("SUMMONS", GUI.skin.box);
            if (Button("START SUMMON")) m_Summons?.ForceSummonNow();
            if (Button("CLEAR SUMMONS")) m_Summons?.DebugClearSummons();
            if (Button("KILL ALL SUMMONS")) m_Summons?.DebugKillAllSummons();
            if (Button("RESUME BOSS")) m_Summons?.DebugResumeBoss();
        }

        void DrawSceneControls()
        {
            GUILayout.Label("SCENE", GUI.skin.box);
            if (Button("RESTART SHOWCASE")) RestartShowcase();
            if (Button("CLEAR RUNTIME OBJECTS")) ClearRuntimeObjects();
        }

        void TriggerPhase2()
        {
            if (m_Phase != null)
                m_Phase.ForceEvaluateTransition();
        }

        void SetWeakPointsExposed(bool exposed)
        {
            if (Boss.WeakPoints == null)
                return;
            foreach (var wp in Boss.WeakPoints)
                if (wp != null)
                    wp.SetExposed(exposed);
        }

        void RestartShowcase()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void ClearRuntimeObjects()
        {
            int removed = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root == null)
                    continue;
                foreach (var prefix in s_RuntimePrefixes)
                {
                    if (root.name.StartsWith(prefix))
                    {
                        Destroy(root);
                        removed++;
                        break;
                    }
                }
            }
            Debug.Log($"[AvatarOfNature] Debug panel cleared {removed} runtime object(s).", this);
        }

        bool Button(string label)
        {
            return GUILayout.Button(label, GUILayout.Height(26f));
        }
    }
}
