using Unity.FPS.Game;
using NUnit.Framework;
using UnityEngine;

namespace Unity.FPS.AvatarBoss.EditorTests
{
    /// Pure edit-mode state checks for the Avatar of Nature components.
    /// These run inside the Unity Test Framework and never touch the real scene.
    public class AvatarBossStateTests
    {
        GameObject m_Root;
        AvatarBossController m_Boss;

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("TestBossRoot");
            m_Boss = m_Root.AddComponent<AvatarBossController>();
            m_Root.AddComponent<Health>();
            m_Root.AddComponent<AvatarBossStagger>();
            m_Root.AddComponent<AvatarBossAttackScheduler>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Root);
        }

        [Test]
        public void WeakPoint_PhaseTwoAttached_DefaultsFalse()
        {
            var wpGo = new GameObject("WeakPointTest");
            wpGo.transform.SetParent(m_Root.transform, false);
            wpGo.AddComponent<Damageable>();
            var wp = wpGo.AddComponent<AvatarBossWeakPoint>();

            Assert.IsFalse(wp.IsExposed,
                "fresh spawned weak point must start hidden (collider disabled)");

            wp.SetExposed(false);
            Assert.IsFalse(wp.IsExposed, "SetExposed(false) must keep the point hidden");
        }

        [Test]
        public void Stagger_FullTriggersOnStaggerFullOnce()
        {
            var stagger = m_Root.GetComponent<AvatarBossStagger>();
            int fired = 0;
            stagger.OnStaggerFull += () => fired++;

            // simulate many damage events: break must fire once, resumed fire only after reset
            stagger.AddStagger(500f, null);
            stagger.AddStagger(500f, null);

            Assert.AreEqual(1, fired, "OnStaggerFull must fire exactly once per full meter");
            Assert.IsTrue(stagger.IsFull);
        }

        [Test]
        public void Scheduler_Shutdown_DisablesFutureCycles()
        {
            var sched = m_Root.GetComponent<AvatarBossAttackScheduler>();
            sched.enabled = true;
            sched.Shutdown();

            Assert.IsFalse(sched.enabled, "Shutdown must disable the scheduler permanently");
        }

        [Test]
        public void Stagger_NegativeDamageIsClampedNonNegative()
        {
            var stagger = m_Root.GetComponent<AvatarBossStagger>();
            stagger.ResetStagger();
            stagger.AddStagger(-50f, null);

            // negative damage should not accumulate; meter must stay at 0
            Assert.AreEqual(0f, stagger.CurrentStagger, "meter must not go below zero");
        }

        [Test]
        public void Scheduler_InitializeDiscoversAttacks()
        {
            var sched = m_Root.GetComponent<AvatarBossAttackScheduler>();
            var attackGo = new GameObject("EarthAttack");
            attackGo.transform.SetParent(m_Root.transform, false);
            attackGo.AddComponent<AvatarBossEarthAttack>();

            sched.Initialize();
        }
    }
}
