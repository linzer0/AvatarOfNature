using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// M1: minimal debug overlay for boss health, stagger and weak points.
    /// Disabled by default; F10 toggles it during play (never ships to players).
    public class AvatarBossDebugHUD : MonoBehaviour
    {
        /// <summary>Static gate for one-shot F10 hit diagnostics (HIT ... weakPoint=...).</summary>
        public AvatarBossController Boss;
        public bool ShowOnStart = false;

        bool m_Shown;

        void Start()
        {
            if (Boss == null)
                Boss = GetComponentInParent<AvatarBossController>();
            m_Shown = ShowOnStart;
            AvatarBossDiagnostics.F10DiagnosticsEnabled = ShowOnStart;
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f10Key.wasPressedThisFrame)
            {
                m_Shown = !m_Shown;
                AvatarBossDiagnostics.F10DiagnosticsEnabled = m_Shown;
            }
        }

        void OnGUI()
        {
            if (!m_Shown || Boss == null || Boss.BossHealth == null)
                return;

            float hp = Boss.BossHealth.CurrentHealth;
            float hpMax = Boss.BossHealth.MaxHealth;

            GUILayout.BeginArea(new Rect(10f, Screen.height - 260f, 320f, 160f), "AVATAR OF NATURE — M1 DEBUG", GUI.skin.window);
            GUILayout.Label($"State: {(Boss.BossHealth.IsCritical() ? "CRITICAL" : "Alive")}  ({hp:F0}/{hpMax:F0})");
            GUILayout.Label(string.Format("HP bar: [{0}] {1:P0}",
                new string('|', Mathf.CeilToInt(Boss.BossHealth.GetRatio() * 20f)), Boss.BossHealth.GetRatio()));

            if (Boss.Stagger != null)
            {
                GUILayout.Label(string.Format("Stagger: [{0}] {1:P0}",
                    new string('|', Mathf.CeilToInt(Boss.Stagger.Ratio * 20f)), Boss.Stagger.Ratio));
            }

            int exposed = 0;
            if (Boss.WeakPoints != null)
            {
                foreach (var wp in Boss.WeakPoints)
                    if (wp != null && wp.IsExposed)
                        exposed++;
            }

            GUILayout.Label($"Weak points exposed: {exposed}/{(Boss.WeakPoints != null ? Boss.WeakPoints.Length : 0)}");

            if (Boss.Scheduler != null)
            {
                string element = Boss.Scheduler.CurrentAttack != null
                    ? Boss.Scheduler.CurrentAttack.Element.ToString()
                    : "none";
                GUILayout.Label($"Phase 2: {(Boss.PhaseTwo ? "ACTIVE" : "off")}   Attack: {Boss.Scheduler.State}  element: {element}");
            }

            GUILayout.Label("Events: stagger/phase/weakpoint changes are logged to Console.");
            GUILayout.EndArea();
        }
    }
}
