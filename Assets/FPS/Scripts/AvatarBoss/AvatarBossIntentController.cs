using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Selects boss intentions from explicit, observable rules.
    /// It is intentionally independent of AvatarBossAttackScheduler and of any player prefab.
    public sealed class AvatarBossIntentController
    {
        public const int DefaultSectorCount = 8;

        readonly int m_Seed;
        readonly int m_SectorCount;
        readonly AvatarBossArenaController m_Arena;
        readonly Vector3 m_ArenaCenter;
        readonly int m_RecentDestroyedMemory;
        System.Random m_Random;
        readonly HashSet<int> m_DestroyedSectors = new HashSet<int>();
        readonly Queue<int> m_DestroyedSectorOrder = new Queue<int>();
        readonly Queue<AvatarBossElement> m_RecentAttacks = new Queue<AvatarBossElement>();
        Vector3 m_LastPlayerPosition;
        bool m_HasPlayerPosition;
        int m_LastTargetSector = -1;
        long m_Sequence;

        public long LastSequenceNumber => m_Sequence;
        public int SectorCount => m_SectorCount;

        public AvatarBossIntentController(
            int seed,
            int sectorCount = DefaultSectorCount,
            Vector3 arenaCenter = default,
            int recentDestroyedMemory = 3)
        {
            if (sectorCount < 2)
                throw new ArgumentOutOfRangeException(nameof(sectorCount), "At least two sectors are required.");
            if (recentDestroyedMemory < 1)
                throw new ArgumentOutOfRangeException(nameof(recentDestroyedMemory));

            m_Seed = seed;
            m_SectorCount = sectorCount;
            m_ArenaCenter = arenaCenter;
            m_RecentDestroyedMemory = recentDestroyedMemory;
            m_Random = new System.Random(seed);
        }

        public AvatarBossIntentController(
            int seed,
            AvatarBossArenaController arena,
            int recentDestroyedMemory = 3)
            : this(seed, arena != null ? arena.Sectors.Count : DefaultSectorCount,
                arena != null ? arena.transform.position : default, recentDestroyedMemory)
        {
            m_Arena = arena;
        }

        public void SetLastPlayerPosition(Vector3 position)
        {
            m_LastPlayerPosition = position;
            m_HasPlayerPosition = true;
        }

        /// Marks a sector unsafe while it remains in the bounded recent-destruction memory.
        public void MarkSectorDestroyed(int sector)
        {
            ValidateSector(sector);
            if (!m_DestroyedSectors.Add(sector))
                return;

            m_DestroyedSectorOrder.Enqueue(sector);
            while (m_DestroyedSectorOrder.Count > m_RecentDestroyedMemory)
            {
                m_DestroyedSectors.Remove(m_DestroyedSectorOrder.Dequeue());
            }
        }

        public bool IsSectorRecentlyDestroyed(int sector)
        {
            ValidateSector(sector);
            return m_DestroyedSectors.Contains(sector);
        }

        /// Returns the next decision, or false when every sector is currently unsafe.
        public bool TryGetNextIntent(out AvatarBossIntent intent)
        {
            intent = default;
            int targetSector = SelectSafeTargetSector();
            if (targetSector < 0)
                return false;

            AvatarBossIntentReason reason;
            AvatarBossExpectedResult expectedResult;
            AvatarBossElement attack = SelectAttack(targetSector, out reason, out expectedResult);
            Vector3 direction = SectorDirection(targetSector);
            m_LastTargetSector = targetSector;
            m_RecentAttacks.Enqueue(attack);
            while (m_RecentAttacks.Count > 2)
                m_RecentAttacks.Dequeue();

            m_Sequence++;
            intent = new AvatarBossIntent(
                attack, targetSector, direction, reason,
                AvatarBossTelegraphPhase.Chosen, expectedResult, m_Sequence);
            return true;
        }

        public void Reset()
        {
            m_Random = new System.Random(m_Seed);
            m_DestroyedSectors.Clear();
            m_DestroyedSectorOrder.Clear();
            m_RecentAttacks.Clear();
            m_LastPlayerPosition = default;
            m_HasPlayerPosition = false;
            m_LastTargetSector = -1;
            m_Sequence = 0;
        }

        int SelectSafeTargetSector()
        {
            var candidates = new List<int>(m_SectorCount);
            for (int sector = 0; sector < m_SectorCount; sector++)
            {
                if (!m_DestroyedSectors.Contains(sector))
                    candidates.Add(sector);
            }
            if (candidates.Count == 0)
                return -1;

            int playerSector = m_HasPlayerPosition ? SectorFromPosition(m_LastPlayerPosition) : -1;
            if (playerSector >= 0 && !m_DestroyedSectors.Contains(playerSector))
                return playerSector;
            return candidates[m_Random.Next(candidates.Count)];
        }

        AvatarBossElement SelectAttack(
            int targetSector,
            out AvatarBossIntentReason reason,
            out AvatarBossExpectedResult expectedResult)
        {
            bool playerRepositioned = m_HasPlayerPosition && targetSector != m_LastTargetSector;
            var candidates = new List<AvatarBossElement>(3);
            foreach (AvatarBossElement attack in new[]
            {
                AvatarBossElement.Earth, AvatarBossElement.Fire, AvatarBossElement.Shockwave
            })
            {
                if (!m_RecentAttacks.Contains(attack))
                    candidates.Add(attack);
            }

            // A bounded recent-history rule guarantees variety without secretly reacting to
            // player input beyond the explicitly supplied last position.
            if (candidates.Count == 0)
                candidates.AddRange(new[] { AvatarBossElement.Earth, AvatarBossElement.Fire, AvatarBossElement.Shockwave });

            AvatarBossElement selected = candidates[m_Random.Next(candidates.Count)];
            if (playerRepositioned && candidates.Contains(AvatarBossElement.Fire))
            {
                selected = AvatarBossElement.Fire;
                reason = AvatarBossIntentReason.PressurePlayerReposition;
                expectedResult = AvatarBossExpectedResult.PunishCurrentPosition;
            }
            else if (m_DestroyedSectors.Count > 0 && candidates.Contains(AvatarBossElement.Earth))
            {
                selected = AvatarBossElement.Earth;
                reason = AvatarBossIntentReason.ExploitIntactSector;
                expectedResult = AvatarBossExpectedResult.DenyIntactSector;
            }
            else if (selected == AvatarBossElement.Shockwave)
            {
                reason = AvatarBossIntentReason.ForceMovement;
                expectedResult = AvatarBossExpectedResult.ForcePlayerMovement;
            }
            else
            {
                reason = AvatarBossIntentReason.MaintainAttackVariety;
                expectedResult = AvatarBossExpectedResult.PunishCurrentPosition;
            }
            return selected;
        }

        int SectorFromPosition(Vector3 position)
        {
            if (m_Arena != null)
                return m_Arena.GetNearestSectorIndex(position);

            Vector3 offset = position - m_ArenaCenter;
            if (offset.sqrMagnitude < 0.0001f)
                return 0;
            float angle = Mathf.Atan2(offset.z, offset.x);
            int sector = Mathf.FloorToInt((angle + Mathf.PI * 2f + Mathf.PI / m_SectorCount)
                / (Mathf.PI * 2f / m_SectorCount));
            return ((sector % m_SectorCount) + m_SectorCount) % m_SectorCount;
        }

        Vector3 SectorDirection(int sector)
        {
            if (m_Arena != null)
            {
                var target = m_Arena.GetSector(sector);
                if (target != null)
                    return (target.transform.position - m_Arena.transform.position).normalized;
            }

            float angle = (sector + 0.5f) * Mathf.PI * 2f / m_SectorCount;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        void ValidateSector(int sector)
        {
            if (sector < 0 || sector >= m_SectorCount)
                throw new ArgumentOutOfRangeException(nameof(sector));
        }
    }
}
