using NUnit.Framework;
using UnityEngine;

namespace Unity.FPS.AvatarBoss.EditorTests
{
    /// <summary>EditMode coverage for arena state transitions and spatial lookup.</summary>
    public class AvatarBossArenaTests
    {
        GameObject m_Root;
        AvatarBossArenaController m_Controller;

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("ArenaTestRoot");
            m_Controller = m_Root.AddComponent<AvatarBossArenaController>();
            m_Controller.CreateRuntimeVisuals = false;
            m_Controller.InitializeArena();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Root);
        }

        [Test]
        public void StateTransitions_RaiseEventsAndReachDestroyed()
        {
            m_Controller.CollapseDuration = 0f;
            var events = 0;
            m_Controller.SectorStateChanged += (_, _, _) => events++;

            Assert.IsTrue(m_Controller.DamageSector(0));
            Assert.AreEqual(AvatarBossArenaSectorState.Damaged, m_Controller.GetSector(0).State);
            Assert.IsTrue(m_Controller.CollapseSector(0));
            Assert.AreEqual(AvatarBossArenaSectorState.Destroyed, m_Controller.GetSector(0).State);
            Assert.AreEqual(3, events, "Damaged, Collapsing and Destroyed must each be observable");
        }

        [Test]
        public void ResetArena_RestoresStateWithoutDestroyingSectorObjects()
        {
            var sector = m_Controller.GetSector(0);
            var sectorObject = sector.gameObject;
            m_Controller.DestroySector(0);

            m_Controller.ResetArena();

            Assert.AreSame(sectorObject, m_Controller.GetSector(0).gameObject);
            Assert.AreEqual(AvatarBossArenaSectorState.Intact, sector.State);
        }

        [Test]
        public void GetNearestSector_ReturnsClosestCenter()
        {
            var expected = m_Controller.GetSector(3);
            var query = expected.transform.position + new Vector3(0.05f, 0f, -0.05f);

            Assert.AreSame(expected, m_Controller.GetNearestSector(query));
            Assert.IsNotNull(m_Controller.GetNearestSector(new Vector3(1000f, 1000f, 1000f)),
                "The nearest registered sector is returned even outside the arena bounds");
        }

        [Test]
        public void GetSectorIndexAtWorldPosition_MapsEverySectorCenterToItsOwnSector()
        {
            for (var i = 0; i < m_Controller.Sectors.Count; i++)
            {
                var sector = m_Controller.GetSector(i);
                Assert.AreEqual(i,
                    m_Controller.GetSectorIndexAtWorldPosition(sector.transform.position),
                    $"Sector center {i} must resolve back to sector {i}");
            }
        }

        [Test]
        public void GetSectorTargetPoint_StaysInsideRequestedSector()
        {
            for (var i = 0; i < m_Controller.Sectors.Count; i++)
            {
                var point = m_Controller.GetSectorTargetPoint(i, Vector3.zero);
                Assert.AreEqual(i, m_Controller.GetSectorIndexAtWorldPosition(point),
                    $"Target point for sector {i} must resolve to that same sector");
            }
        }
    }
}
