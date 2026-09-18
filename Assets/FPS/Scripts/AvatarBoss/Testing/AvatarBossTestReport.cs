using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Aggregates PASS/FAIL per scenario step and prints the final report.
    public class AvatarBossTestReport
    {
        public class Step
        {
            public string Name;
            public bool Pass;
            public string Reason;
            public float Duration;
            public AvatarBossStateSnapshot Snapshot;
        }

        readonly List<Step> m_Steps = new List<Step>();

        public IList<Step> Steps { get { return m_Steps; } }

        public void Begin()
        {
            m_Steps.Clear();
            Debug.Log("[AVATAR_BOSS_TEST] === TEST RUN START ===");
        }

        public void StepResult(string name, string detail, float stepStart, AvatarBossStateSnapshot snapshot)
        {
            var step = new Step
            {
                Name = name,
                Pass = true,
                Reason = string.IsNullOrEmpty(detail) ? "ok" : detail,
                Duration = Time.unscaledTime - stepStart,
                Snapshot = snapshot
            };
            m_Steps.Add(step);
            Debug.Log($"[AVATAR_BOSS_TEST] PASS [{name}] ({step.Duration:F2}s) {step.Reason} | {snapshot}");
        }

        public void Fail(string name, string reason, float stepStart, AvatarBossStateSnapshot snapshot)
        {
            var step = new Step
            {
                Name = name,
                Pass = false,
                Reason = reason,
                Duration = Time.unscaledTime - stepStart,
                Snapshot = snapshot
            };
            m_Steps.Add(step);
            Debug.LogError($"[AVATAR_BOSS_TEST] FAIL [{name}] ({step.Duration:F2}s) reason='{reason}' | {snapshot}");
            if (snapshot != null)
                Debug.LogWarning($"[AVATAR_BOSS_TEST] FAIL-state: {snapshot}");
        }

        public bool HasFailures()
        {
            foreach (var step in m_Steps)
                if (!step.Pass)
                    return true;
            return false;
        }

        public void Finish()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[AVATAR_BOSS_TEST] ============== FINAL REPORT ==============");
            foreach (var step in m_Steps)
            {
                string verdict = step.Pass ? "PASS" : "FAIL";
                string snap = step.Snapshot != null ? step.Snapshot.ToString() : "no-snapshot";
                string reason = step.Pass ? "" : "reason='" + step.Reason + "'";
                sb.AppendLine(string.Format("[AVATAR_BOSS_TEST] {0} [{1}] ({2:F2}s) {3} {4}",
                    verdict, step.Name, step.Duration, reason, snap));
            }
            sb.AppendLine("[AVATAR_BOSS_TEST] overall=" + (HasFailures() ? "FAIL" : "PASS"));
            sb.AppendLine("[AVATAR_BOSS_GAMEPLAY_TEST: " + (HasFailures() ? "FAIL" : "PASS") + "]");
            Debug.Log(sb.ToString());
        }
    }
}
