using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Immutable snapshot of boss state used by assertions and reporting.
    public class AvatarBossStateSnapshot
    {
        public float BossHp;
        public float BossMaxHp;
        public float PlayerHp;
        public float PlayerMaxHp;
        public float StaggerRatio;
        public bool PhaseTwo;
        public bool IsDead;
        public bool SchedulerEnabled;
        public AvatarBossSchedulerState SchedulerState;
        public AvatarBossElement SchedulerElement;
        public int WeakPointCount;
        public bool[] WeakPointsExposed;
        public string[] WeakPointNames;

        public int ExposedCount()
        {
            int n = 0;
            if (WeakPointsExposed != null)
                foreach (var e in WeakPointsExposed)
                    if (e) n++;
            return n;
        }

        public override string ToString()
        {
            return string.Format("hp={0:F0}/{1:F0} phase2={2} dead={3} sched={4}({5},{6}) stagger={7:P0} wps={8}/{9}",
                BossHp, BossMaxHp, PhaseTwo, IsDead, SchedulerEnabled, SchedulerState, SchedulerElement,
                StaggerRatio, ExposedCount(), WeakPointCount);
        }
    }

    /// Pure state assertions for the Avatar of Nature test suite.
    /// No MonoBehaviour logic: everything reads public component properties.
    public static class AvatarBossStateAssertions
    {
        public static AvatarBossStateSnapshot Capture(AvatarBossController boss, PlayerCharacterController player)
        {
            var s = new AvatarBossStateSnapshot();
            if (boss == null)
                return s;

            if (boss.BossHealth != null)
            {
                s.BossHp = boss.BossHealth.CurrentHealth;
                s.BossMaxHp = boss.BossHealth.MaxHealth;
                s.IsDead = boss.IsDead;
            }

            if (boss.Stagger != null)
                s.StaggerRatio = boss.Stagger.Ratio;

            s.PhaseTwo = boss.PhaseTwo;
            s.SchedulerEnabled = boss.Scheduler != null && boss.Scheduler.enabled;
            s.SchedulerState = boss.Scheduler != null
                ? boss.Scheduler.State
                : AvatarBossSchedulerState.Idle;
            s.SchedulerElement = boss.Scheduler != null && boss.Scheduler.CurrentAttack != null
                ? boss.Scheduler.CurrentAttack.Element
                : (AvatarBossElement)(-1);

            if (boss.WeakPoints != null)
            {
                s.WeakPointCount = boss.WeakPoints.Length;
                s.WeakPointsExposed = new bool[s.WeakPointCount];
                s.WeakPointNames = new string[s.WeakPointCount];
                for (int i = 0; i < s.WeakPointCount; i++)
                {
                    if (boss.WeakPoints[i] != null)
                    {
                        s.WeakPointsExposed[i] = boss.WeakPoints[i].IsExposed;
                        s.WeakPointNames[i] = boss.WeakPoints[i].name;
                    }
                }
            }

            if (player != null)
            {
                var playerHealth = player.GetComponent<Health>();
                if (playerHealth != null)
                {
                    s.PlayerHp = playerHealth.CurrentHealth;
                    s.PlayerMaxHp = playerHealth.MaxHealth;
                }
            }

            return s;
        }
    }
}
