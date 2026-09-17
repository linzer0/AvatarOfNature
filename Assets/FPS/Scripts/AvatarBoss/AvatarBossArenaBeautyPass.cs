using System.Collections;
using UnityEngine;
using System.Collections.Generic;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Presentation-only arena dressing. It adds a readable ritual boundary,
    /// restrained emissive color and atmosphere without changing colliders or
    /// arena gameplay state.
    /// </summary>
    public sealed class AvatarBossArenaBeautyPass : MonoBehaviour
    {
        public Color IntactColor = new Color(0.035f, 0.16f, 0.16f, 1f);
        public Color AlternateIntactColor = new Color(0.055f, 0.24f, 0.22f, 1f);
        public Color RuneColor = new Color(0.12f, 0.95f, 0.78f, 1f);
        public float RuneWidth = 0.055f;
        public int RuneSegments = 96;

        AvatarBossArenaController m_Arena;
        Transform m_DressingRoot;
        Material m_RuneMaterial;
        AvatarBossController m_Boss;
        readonly List<Transform> m_BeaconOrbs = new List<Transform>();
        readonly List<Light> m_BeaconLights = new List<Light>();

        void Start()
        {
            m_Arena = GetComponent<AvatarBossArenaController>();
            if (m_Arena == null)
                return;

            m_Boss = FindFirstObjectByType<AvatarBossController>();
            m_Arena.SectorStateChanged += OnSectorStateChanged;

            BuildAtmosphere();
            StyleSectors();
            BuildRitualBoundary();
        }

        void OnDestroy()
        {
            if (m_Arena != null)
                m_Arena.SectorStateChanged -= OnSectorStateChanged;
        }

        void OnSectorStateChanged(AvatarBossArenaSector sector,
            AvatarBossArenaSectorState previous, AvatarBossArenaSectorState current)
        {
            if (sector == null || m_DressingRoot == null)
                return;
            if (current == AvatarBossArenaSectorState.Collapsing)
                StartCoroutine(SectorPulseRoutine(sector, new Color(1f, 0.34f, 0.06f, 1f)));
            else if (current == AvatarBossArenaSectorState.Destroyed)
                StartCoroutine(SectorPulseRoutine(sector, new Color(0.35f, 1f, 0.58f, 1f)));
        }

        IEnumerator SectorPulseRoutine(AvatarBossArenaSector sector, Color color)
        {
            var go = new GameObject("SectorStatePulse");
            go.transform.SetParent(m_DressingRoot, false);
            go.transform.position = sector.transform.position + Vector3.up * 0.18f;
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 32;
            line.widthMultiplier = 0.12f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            const float duration = 0.55f;
            float time = 0f;
            while (time < duration && go != null)
            {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                float radius = Mathf.Lerp(0.45f, 2.6f, t);
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / line.positionCount;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f,
                        Mathf.Sin(angle) * radius));
                }
                Color faded = color;
                faded.a = 1f - t;
                line.startColor = faded;
                line.endColor = faded;
                yield return null;
            }
            if (go != null)
                Destroy(go);
        }

        void Update()
        {
            if (m_RuneMaterial == null)
                return;

            Color target = RuneColor;
            float intensity = 1f;
            if (m_Boss != null)
            {
                var duel = m_Boss.GetComponent<AvatarBossDuelController>();
                var scheduler = m_Boss.Scheduler;
                if (duel != null && duel.DuelWindowActive)
                {
                    target = new Color(0.35f, 1f, 0.58f, 1f);
                    intensity = 1.35f;
                }
                else if (scheduler != null
                    && (scheduler.State == AvatarBossSchedulerState.Telegraph
                        || scheduler.State == AvatarBossSchedulerState.Windup)
                    && scheduler.CurrentAttack != null)
                {
                    target = ElementColor(scheduler.CurrentAttack.Element);
                    intensity = 1.1f;
                }
            }

            float pulse = 0.72f + Mathf.Sin(Time.time * 4.5f) * 0.18f;
            m_RuneMaterial.color = new Color(target.r, target.g, target.b, 0.55f + pulse * 0.2f);
            for (int i = 0; i < m_BeaconOrbs.Count; i++)
            {
                if (m_BeaconOrbs[i] == null)
                    continue;
                float orbPulse = 0.9f + Mathf.Sin(Time.time * 5f + i * 0.7f) * 0.16f;
                m_BeaconOrbs[i].localScale = Vector3.one * (0.35f * orbPulse * intensity);
                if (i < m_BeaconLights.Count && m_BeaconLights[i] != null)
                {
                    m_BeaconLights[i].color = target;
                    m_BeaconLights[i].intensity = 1.2f * orbPulse * intensity;
                }
            }
        }

        Color ElementColor(AvatarBossElement element)
        {
            switch (element)
            {
                case AvatarBossElement.Fire:
                    return new Color(1f, 0.3f, 0.08f, 1f);
                case AvatarBossElement.Shockwave:
                    return new Color(0.35f, 0.78f, 1f, 1f);
                default:
                    return new Color(1f, 0.68f, 0.18f, 1f);
            }
        }

        void BuildAtmosphere()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.055f, 0.08f, 0.12f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.055f, 0.09f, 0.13f, 1f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;

            var fillObject = new GameObject("ArenaBeautyFillLight");
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.localPosition = new Vector3(0f, 8f, -10f);
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.38f, 0.68f, 0.72f, 1f);
            fill.intensity = 5f;
            fill.range = 24f;

            m_DressingRoot = new GameObject("ArenaBeautyDressing").transform;
            m_DressingRoot.SetParent(transform, false);
            m_RuneMaterial = new Material(Shader.Find("Sprites/Default"));
            m_RuneMaterial.color = new Color(RuneColor.r, RuneColor.g, RuneColor.b, 0.68f);
        }

        void StyleSectors()
        {
            int sectorsPerRing = Mathf.Max(1, m_Arena.SectorCount);
            for (int i = 0; i < m_Arena.Sectors.Count; i++)
            {
                var sector = m_Arena.GetSector(i);
                if (sector == null)
                    continue;

                Color color = (i / sectorsPerRing) % 2 == 0 ? IntactColor : AlternateIntactColor;
                var renderers = sector.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer == null || renderer.gameObject.name.Contains("Telegraph"))
                        continue;
                    var material = renderer.material;
                    material.color = color;
                    if (material.HasProperty("_Smoothness"))
                        material.SetFloat("_Smoothness", 0.55f);
                }
            }
        }

        void BuildRitualBoundary()
        {
            int rings = Mathf.Clamp(m_Arena.RingCount, 1, 5);
            for (int ring = 0; ring <= rings; ring++)
            {
                float radius = ring == 0
                    ? m_Arena.CenterPlatformRadius + 0.2f
                    : Mathf.Lerp(m_Arena.ArenaInnerRadius, m_Arena.ArenaRadius, (float)ring / rings);
                CreateRuneRing($"RuneRing_{ring}", radius, ring == 0 ? 0.12f : RuneWidth);
            }

            int beaconCount = Mathf.Clamp(m_Arena.SectorCount, 8, 12);
            for (int i = 0; i < beaconCount; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / beaconCount;
                Vector3 position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (m_Arena.ArenaRadius + 0.5f);
                CreateBeacon(i, position);
            }
        }

        void CreateRuneRing(string name, float radius, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(m_DressingRoot, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = RuneSegments;
            line.widthMultiplier = width;
            line.material = m_RuneMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            for (int i = 0; i < RuneSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RuneSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.08f, Mathf.Sin(angle) * radius));
            }
        }

        void CreateBeacon(int index, Vector3 position)
        {
            var root = new GameObject($"ArenaBeacon_{index:00}");
            root.transform.SetParent(m_DressingRoot, false);
            root.transform.localPosition = new Vector3(position.x, 0f, position.z);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "BeaconPillar";
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            pillar.transform.localScale = new Vector3(0.18f, 0.75f, 0.18f);
            ApplyRuneMaterial(pillar.GetComponent<MeshRenderer>());
            Destroy(pillar.GetComponent<Collider>());

            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "BeaconOrb";
            orb.transform.SetParent(root.transform, false);
            orb.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            orb.transform.localScale = Vector3.one * 0.35f;
            ApplyRuneMaterial(orb.GetComponent<MeshRenderer>());
            Destroy(orb.GetComponent<Collider>());

            var light = orb.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = RuneColor;
            light.intensity = 1.2f;
            light.range = 5f;
            m_BeaconOrbs.Add(orb.transform);
            m_BeaconLights.Add(light);
        }

        void ApplyRuneMaterial(Renderer renderer)
        {
            if (renderer == null)
                return;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = RuneColor;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", RuneColor * 2.2f);
            renderer.material = material;
        }
    }
}
