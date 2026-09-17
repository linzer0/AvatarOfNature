using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>Creates and controls a circular set of destructible boss arena sectors.</summary>
    public sealed class AvatarBossArenaController : MonoBehaviour
    {
        [Header("Arena")]
        [Min(0.1f)] public float ArenaRadius = 10f;
        [Min(0.1f)] public float ArenaInnerRadius = 2.5f;
        [Min(0.05f)] public float SectorHeight = 0.25f;
        [Range(8, 12)] public int SectorCount = 8;
        [Min(0f)] public float CollapseDuration = 1f;
        public bool CreateRuntimeVisuals = true;
        [Min(0f)] public float SectorVisualLift = 0.22f;
        [Range(0.35f, 0.9f)] public float SectorArcFill = 0.48f;

        [SerializeField] List<AvatarBossArenaSector> m_Sectors = new List<AvatarBossArenaSector>();

        /// <summary>Raised after a sector changes state: sector, previous state, new state.</summary>
        public event Action<AvatarBossArenaSector, AvatarBossArenaSectorState, AvatarBossArenaSectorState> SectorStateChanged;

        /// <summary>Registered sectors in ascending index order.</summary>
        public IReadOnlyList<AvatarBossArenaSector> Sectors
        {
            get
            {
                EnsureInitialized();
                return m_Sectors;
            }
        }

        /// <summary>Creates the configured sectors, or registers the sectors already assigned in the inspector.</summary>
        public void InitializeArena()
        {
            CleanupNullSectors();
            if (m_Sectors.Count == 0)
                CreateSectors();
            else
                ReindexSectors();
        }

        /// <summary>Registers an existing sector without taking ownership of or destroying its GameObject.</summary>
        public void RegisterSector(AvatarBossArenaSector sector)
        {
            if (sector == null || m_Sectors.Contains(sector))
                return;

            m_Sectors.Add(sector);
            ReindexSectors();
        }

        /// <summary>Returns a sector by its zero-based index, or null when the index is invalid.</summary>
        public AvatarBossArenaSector GetSector(int index)
        {
            EnsureInitialized();
            if (index < 0 || index >= m_Sectors.Count)
                return null;
            return m_Sectors[index];
        }

        /// <summary>Returns the sector whose center is closest to the supplied world position.</summary>
        public AvatarBossArenaSector GetNearestSector(Vector3 worldPosition)
        {
            AvatarBossArenaSector nearest = null;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < m_Sectors.Count; i++)
            {
                var sector = m_Sectors[i];
                if (sector == null)
                    continue;

                var distance = (sector.transform.position - worldPosition).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = sector;
                }
            }

            return nearest;
        }

        /// <summary>Marks a sector as damaged and raises the state-change event.</summary>
        public bool DamageSector(int index)
        {
            return SetSectorState(index, AvatarBossArenaSectorState.Damaged);
        }

        /// <summary>Starts a sector collapse and raises events for Collapsing and Destroyed.</summary>
        public bool CollapseSector(int index)
        {
            var sector = GetSector(index);
            if (sector == null || sector.State == AvatarBossArenaSectorState.Destroyed
                || sector.State == AvatarBossArenaSectorState.Collapsing)
                return false;

            ChangeState(sector, AvatarBossArenaSectorState.Collapsing);
            sector.BeginCollapse(CollapseDuration, () =>
            {
                // The sector owns the delayed visual/state update. The controller owns
                // the corresponding event, whose previous state is known here.
                SectorStateChanged?.Invoke(sector, AvatarBossArenaSectorState.Collapsing,
                    AvatarBossArenaSectorState.Destroyed);
            });
            return true;
        }

        /// <summary>Immediately marks a sector as destroyed and raises the state-change event.</summary>
        public bool DestroySector(int index)
        {
            return SetSectorState(index, AvatarBossArenaSectorState.Destroyed);
        }

        /// <summary>Resets every registered sector to Intact without destroying its GameObjects.</summary>
        public void ResetArena()
        {
            for (var i = 0; i < m_Sectors.Count; i++)
            {
                var sector = m_Sectors[i];
                if (sector == null)
                    continue;
                sector.StopCollapse();
                ChangeState(sector, AvatarBossArenaSectorState.Intact);
            }
        }

        void Awake()
        {
            InitializeArena();
        }

        void CreateSectors()
        {
            var count = Mathf.Clamp(SectorCount, 8, 12);
            for (var i = 0; i < count; i++)
            {
                var sectorObject = new GameObject($"ArenaSector_{i:00}");
                sectorObject.transform.SetParent(transform, false);
                var angle = (i + 0.5f) * Mathf.PI * 2f / count;
                float midRadius = (ArenaInnerRadius + ArenaRadius) * 0.5f;
                sectorObject.transform.localPosition = new Vector3(Mathf.Cos(angle) * midRadius, SectorVisualLift, Mathf.Sin(angle) * midRadius);
                sectorObject.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                var sector = sectorObject.AddComponent<AvatarBossArenaSector>();
                sector.SetIndex(i);
                if (CreateRuntimeVisuals)
                    CreateSectorVisuals(sectorObject, count);
                m_Sectors.Add(sector);
            }
        }

        void CreateSectorVisuals(GameObject sectorObject, int count)
        {
            float ringDepth = Mathf.Max(0.5f, ArenaRadius - ArenaInnerRadius);
            float arcWidth = Mathf.Max(0.2f, 2f * Mathf.PI * ArenaRadius / count * SectorArcFill);

            var intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intact.name = "SectorIntactVisual";
            intact.transform.SetParent(sectorObject.transform, false);
            intact.transform.localScale = new Vector3(arcWidth, SectorHeight, ringDepth);
            intact.transform.localPosition = new Vector3(0f, -SectorHeight * 0.5f, 0f);
            SetRuntimeMaterial(intact.GetComponent<Renderer>(), new Color(0.12f, 0.28f, 0.22f, 1f));

            var damaged = GameObject.CreatePrimitive(PrimitiveType.Cube);
            damaged.name = "SectorDamagedVisual";
            damaged.transform.SetParent(sectorObject.transform, false);
            damaged.transform.localScale = new Vector3(arcWidth, SectorHeight, ringDepth);
            damaged.transform.localPosition = new Vector3(0f, -SectorHeight * 0.5f, 0f);
            SetRuntimeMaterial(damaged.GetComponent<Renderer>(), new Color(0.72f, 0.38f, 0.08f, 1f));

            var destroyed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            destroyed.name = "SectorDestroyedVisual";
            destroyed.transform.SetParent(sectorObject.transform, false);
            destroyed.transform.localScale = new Vector3(arcWidth, 0.04f, ringDepth);
            destroyed.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            SetRuntimeMaterial(destroyed.GetComponent<Renderer>(), new Color(0.18f, 0.03f, 0.02f, 1f));

            var sector = sectorObject.GetComponent<AvatarBossArenaSector>();
            sector.IntactVisual = intact;
            sector.DamagedVisual = damaged;
            sector.DestroyedVisual = destroyed;
            sector.ForceState(AvatarBossArenaSectorState.Intact);
        }

        void SetRuntimeMaterial(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader != null)
            {
                var material = new Material(shader) { color = color };
                renderer.sharedMaterial = material;
            }
        }

        void ReindexSectors()
        {
            for (var i = 0; i < m_Sectors.Count; i++)
                if (m_Sectors[i] != null)
                    m_Sectors[i].SetIndex(i);
        }

        void CleanupNullSectors()
        {
            m_Sectors.RemoveAll(sector => sector == null);
        }

        void EnsureInitialized()
        {
            if (m_Sectors.Count == 0)
                InitializeArena();
        }

        bool SetSectorState(int index, AvatarBossArenaSectorState state)
        {
            var sector = GetSector(index);
            if (sector == null)
                return false;

            if (!CanTransition(sector.State, state))
                return false;

            ChangeState(sector, state);
            return true;
        }

        bool CanTransition(AvatarBossArenaSectorState from, AvatarBossArenaSectorState to)
        {
            if (from == to)
                return false;
            if (from == AvatarBossArenaSectorState.Destroyed)
                return false;
            if (from == AvatarBossArenaSectorState.Collapsing && to != AvatarBossArenaSectorState.Destroyed)
                return false;
            if (from == AvatarBossArenaSectorState.Damaged && to == AvatarBossArenaSectorState.Intact)
                return false;
            return true;
        }

        void ChangeState(AvatarBossArenaSector sector, AvatarBossArenaSectorState state)
        {
            var previous = sector.State;
            if (previous == state)
                return;

            sector.ForceState(state);
            SectorStateChanged?.Invoke(sector, previous, state);
        }
    }
}
