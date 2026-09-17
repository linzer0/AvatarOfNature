using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>Creates and controls a circular set of destructible boss arena sectors.</summary>
    public sealed class AvatarBossArenaController : MonoBehaviour
    {
        [Header("Arena")]
        [Min(0.1f)] public float ArenaRadius = 18f;
        [Min(0.1f)] public float ArenaInnerRadius = 4f;
        [Min(0.05f)] public float SectorHeight = 0.25f;
        [Range(8, 12)] public int SectorCount = 8;
        [Range(2, 5)] public int RingCount = 3;
        [Min(0f)] public float RingGap = 0.18f;
        [Min(0f)] public float CollapseDuration = 1f;
        public bool CreateRuntimeVisuals = true;
        public bool CreateCenterPlatform = true;
        [Min(0.1f)] public float CenterPlatformRadius = 3.7f;
        [Min(0f)] public float SectorVisualLift = 0.22f;
        [Range(0.35f, 0.95f)] public float SectorArcFill = 0.86f;

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

        /// <summary>Returns the flat list index of the sector nearest to a world position.</summary>
        public int GetNearestSectorIndex(Vector3 worldPosition)
        {
            var sector = GetNearestSector(worldPosition);
            return sector == null ? -1 : sector.Index;
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
            var rings = Mathf.Clamp(RingCount, 2, 5);
            if (CreateCenterPlatform)
                CreateCenterPlatformVisual();

            for (var ring = 0; ring < rings; ring++)
            {
                float ringStart = Mathf.Lerp(ArenaInnerRadius, ArenaRadius, (float)ring / rings);
                float ringEnd = Mathf.Lerp(ArenaInnerRadius, ArenaRadius, (float)(ring + 1) / rings);
                float midRadius = (ringStart + ringEnd) * 0.5f;
                for (var angular = 0; angular < count; angular++)
                {
                    var index = ring * count + angular;
                    var sectorObject = new GameObject($"ArenaSector_R{ring}_S{angular:00}");
                    sectorObject.transform.SetParent(transform, false);
                    var angle = (angular + 0.5f) * Mathf.PI * 2f / count;
                    sectorObject.transform.localPosition = new Vector3(Mathf.Cos(angle) * midRadius, SectorVisualLift, Mathf.Sin(angle) * midRadius);
                    sectorObject.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                    var sector = sectorObject.AddComponent<AvatarBossArenaSector>();
                    sector.SetIndex(index);
                    if (CreateRuntimeVisuals)
                        CreateSectorVisuals(sectorObject, count, ring, ringStart, ringEnd);
                    m_Sectors.Add(sector);
                }
            }
        }

        void CreateCenterPlatformVisual()
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            platform.name = "BossCenterPlatform";
            platform.transform.SetParent(transform, false);
            platform.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            platform.transform.localScale = new Vector3(CenterPlatformRadius * 2f, 0.2f, CenterPlatformRadius * 2f);
            SetRuntimeMaterial(platform.GetComponent<Renderer>(), new Color(0.035f, 0.06f, 0.08f, 1f));
        }

        void CreateSectorVisuals(GameObject sectorObject, int count, int ring, float ringStart, float ringEnd)
        {
            float ringDepth = Mathf.Max(0.5f, ringEnd - ringStart - RingGap);
            float arcWidth = Mathf.Max(0.2f, 2f * Mathf.PI * ringEnd / count * SectorArcFill);

            var intact = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intact.name = "SectorIntactVisual";
            intact.transform.SetParent(sectorObject.transform, false);
            intact.transform.localScale = new Vector3(arcWidth, SectorHeight, ringDepth);
            intact.transform.localPosition = new Vector3(0f, -SectorHeight * 0.5f, 0f);
            SetRuntimeMaterial(intact.GetComponent<Renderer>(), ring % 2 == 0
                ? new Color(0.10f, 0.26f, 0.20f, 1f)
                : new Color(0.14f, 0.34f, 0.27f, 1f));

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
