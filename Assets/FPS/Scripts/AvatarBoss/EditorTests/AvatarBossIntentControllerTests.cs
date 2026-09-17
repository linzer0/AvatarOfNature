using NUnit.Framework;
using UnityEngine;

namespace Unity.FPS.AvatarBoss.EditorTests
{
    public class AvatarBossIntentControllerTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var first = new AvatarBossIntentController(42);
            var second = new AvatarBossIntentController(42);
            first.SetLastPlayerPosition(new Vector3(4f, 0f, 2f));
            second.SetLastPlayerPosition(new Vector3(4f, 0f, 2f));

            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(first.TryGetNextIntent(out var a));
                Assert.IsTrue(second.TryGetNextIntent(out var b));
                Assert.AreEqual(a, b);
            }
        }

        [Test]
        public void RecentAttack_DoesNotRepeatIndefinitely()
        {
            var controller = new AvatarBossIntentController(7);
            controller.SetLastPlayerPosition(new Vector3(1f, 0f, 0f));
            AvatarBossElement previous = default;

            for (int i = 0; i < 12; i++)
            {
                Assert.IsTrue(controller.TryGetNextIntent(out var intent));
                if (i > 0)
                    Assert.AreNotEqual(previous, intent.AttackType);
                previous = intent.AttackType;
            }
        }

        [Test]
        public void RecentlyDestroyedSector_IsNeverSelectedAsSafeTarget()
        {
            var controller = new AvatarBossIntentController(11, 4, Vector3.zero, 2);
            controller.MarkSectorDestroyed(0);
            controller.SetLastPlayerPosition(new Vector3(1f, 0f, 0f));

            for (int i = 0; i < 8; i++)
            {
                Assert.IsTrue(controller.TryGetNextIntent(out var intent));
                Assert.AreNotEqual(0, intent.TargetSector);
            }
        }

        [Test]
        public void Reset_RestoresInitialSequence()
        {
            var controller = new AvatarBossIntentController(99);
            controller.SetLastPlayerPosition(new Vector3(2f, 0f, 1f));
            Assert.IsTrue(controller.TryGetNextIntent(out var beforeReset));

            controller.MarkSectorDestroyed(3);
            controller.Reset();
            controller.SetLastPlayerPosition(new Vector3(2f, 0f, 1f));
            Assert.IsTrue(controller.TryGetNextIntent(out var afterReset));

            Assert.AreEqual(beforeReset, afterReset);
            Assert.AreEqual(1, controller.LastSequenceNumber);
        }
    }
}
