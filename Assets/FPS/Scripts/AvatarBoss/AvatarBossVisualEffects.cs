using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Presentation pass: readable, lit, color-separated silhouette (VisualRoot)
    /// from primitives: distinct materials per part, gold emissive crown/cores,
    /// elemental cores, weak point halos + world markers, warm rim light,
    /// phase-2 aura tint, hit / stagger flashes. Gameplay untouched.
    public class AvatarBossVisualEffects : MonoBehaviour
    {
        [Header("Phase 2")]
        public Color PhaseTwoBodyColor = new Color(1f, 0.35f, 0.18f);

        AvatarBossController m_Boss;
        MeshRenderer m_BodyRenderer;
        GameObject m_VisualRoot;
        GameObject m_Aura;
        Light m_RimLight;
        readonly List<GameObject> m_Cores = new List<GameObject>();

        struct WeakPointMarker
        {
            public AvatarBossWeakPoint Point;
            public GameObject Marker;
            public float Flash;
        }

        readonly List<WeakPointMarker> m_Markers = new List<WeakPointMarker>();
        readonly List<FloatingMarker> m_FloatingMarkers = new List<FloatingMarker>();
        readonly Dictionary<string, Color> m_BaseColors = new Dictionary<string, Color>();
        bool m_PhaseTwoApplied;
        bool m_Dead;
        bool m_Built;
        float m_LastMarkerSpawn;

        struct FloatingMarker
        {
            public GameObject Go;
            public TextMesh Label;
            public Vector3 StartPos;
            public float Time;
            public float Life;
        }

        readonly Color m_TorsoColor = new Color(0.18f, 0.28f, 0.24f, 1f);   // deep nature stone
        readonly Color m_HeadColor = new Color(0.09f, 0.13f, 0.14f, 1f);   // dark ceremonial mask
        readonly Color m_CrownColor = new Color(0.92f, 0.72f, 0.18f, 1f);  // emissive gold
        readonly Color m_ShoulderColor = new Color(0.50f, 0.36f, 0.20f, 1f); // bronze
        readonly Color m_ArmColor = new Color(0.32f, 0.42f, 0.30f, 1f);    // lighter stone
        readonly Color m_AuraColor = new Color(0.55f, 0.85f, 0.65f, 0.10f); // nature aura
        readonly Color m_PhaseTwoTint = new Color(1f, 0.32f, 0.14f, 1f);   // phase-2 fire accent

        void Start()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
            if (m_Boss == null)
            {
                enabled = false;
                return;
            }

            var body = transform.Find("BossBody");
            if (body != null)
                m_BodyRenderer = body.GetComponent<MeshRenderer>();

            var gate = m_Boss.GetComponent<AvatarBossShowcaseDifficultySelect>();
            if (gate != null && !gate.FightStarted)
                gate.FightStartedEvent += BuildDeferred;
            else
                BuildSilhouette();

            // presentation-only reaction subscriptions (precise per-part hit feedback)
            m_Boss.OnBossHit += OnBossHit;
            if (m_Boss.BossHealth != null)
                m_Boss.BossHealth.OnDie += OnBossDie;
            if (m_Boss.Stagger != null)
                m_Boss.Stagger.OnStaggerFull += OnStaggerBreak;

        }

        void OnDestroy()
        {
            if (m_Boss != null)
            {
                var gate = m_Boss.GetComponent<AvatarBossShowcaseDifficultySelect>();
                if (gate != null)
                    gate.FightStartedEvent -= BuildDeferred;
                m_Boss.OnBossHit -= OnBossHit;
                if (m_Boss.BossHealth != null)
                    m_Boss.BossHealth.OnDie -= OnBossDie;
            }
        }

        /// <summary>On death all aura/VFX shut off; silhouette stays readable.</summary>
        void OnBossDie()
        {
            m_Dead = true;
            if (m_Aura != null)
                m_Aura.SetActive(false);
            foreach (var marker in m_Markers)
                if (marker.Marker != null)
                    marker.Marker.SetActive(false);
            if (m_RimLight != null)
                m_RimLight.intensity = 0f;
            foreach (var fm in m_FloatingMarkers)
                if (fm.Go != null)
                    Object.Destroy(fm.Go);
            m_FloatingMarkers.Clear();
        }

        /// <summary>Debug/test-only: restore presentation to the phase-1 baseline after a boss reset.</summary>
        public void DebugReset()
        {
            m_Dead = false;
            m_PhaseTwoApplied = false;
            m_HitFlash = 0f;
            m_StaggerFlash = 0f;

            if (m_Aura != null)
            {
                m_Aura.SetActive(false);
                var auraRenderer = m_Aura.GetComponent<MeshRenderer>();
                if (auraRenderer != null)
                {
                    var mat = new Material(auraRenderer.material);
                    mat.color = m_AuraColor;
                    auraRenderer.material = mat;
                }
            }

            foreach (var core in m_Cores)
            {
                if (core == null) continue;
                var r = core.GetComponent<MeshRenderer>();
                if (r == null) continue;
                var mat = new Material(r.material);
                mat.color = m_CrownColor;
                mat.SetColor("_EmissionColor", m_CrownColor * 1.6f);
                r.material = mat;
            }

            if (m_RimLight != null)
            {
                m_RimLight.intensity = 5f;
                m_RimLight.color = new Color(1f, 0.85f, 0.55f);
            }

            foreach (var fm in m_FloatingMarkers)
                if (fm.Go != null)
                    Object.Destroy(fm.Go);
            m_FloatingMarkers.Clear();

            if (m_VisualRoot != null)
            {
                foreach (Transform child in m_VisualRoot.transform)
                {
                    if (child == null || !child.name.EndsWith("_Visual"))
                        continue;
                    if (m_Aura != null && child == m_Aura.transform)
                        continue;
                    var r = child.GetComponent<MeshRenderer>();
                    if (r == null)
                        continue;
                    Color baseColor;
                    if (m_BaseColors.TryGetValue(child.name, out baseColor))
                    {
                        var mat = new Material(r.material);
                        mat.color = baseColor;
                        r.material = mat;
                    }
                }
            }
        }


        GameObject AddVisual(string goName, PrimitiveType type, Vector3 localPosition,
            Vector3 scale, Color color, bool emissive = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = goName + "_Visual";
            go.transform.SetParent(m_VisualRoot.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<MeshRenderer>();
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat;
            if (litShader != null)
            {
                mat = new Material(litShader);
                mat.SetFloat("_Smoothness", 0.25f);
                // A restrained fill emission keeps the silhouette readable in the
                // arena's deep teal lighting. Important accents use a stronger value.
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * (emissive ? 1.6f : 0.22f));
            }
            else
            {
                mat = new Material(renderer.material);
            }
            mat.color = color;
            renderer.material = mat;
            m_BaseColors[go.name] = color;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider); // visuals never take part in gameplay
            return go;
        }

        void AddCore(string coreName, Vector3 localPosition)
        {
            var go = AddVisual(coreName, PrimitiveType.Sphere, localPosition,
                Vector3.one * 0.55f, m_CrownColor, emissive: true);
            m_Cores.Add(go);
        }

        void BuildSilhouette()
        {
            var existing = transform.Find("VisualRoot");
            if (existing != null)
            {
                m_VisualRoot = existing.gameObject;
                BuildWeakPointMarkers();
                m_Built = true;
                return; // a VisualRoot already exists in the scene
            }

            m_VisualRoot = new GameObject("VisualRoot");
            m_VisualRoot.transform.SetParent(transform, false);

            // main husk: broad elemental torso, clearly taller and wider than the player
            AddVisual("Torso", PrimitiveType.Cube, new Vector3(0f, 3.1f, 0f),
                new Vector3(3.6f, 4.6f, 3.6f), m_TorsoColor);
            // head / mask with golden crown
            AddVisual("Head", PrimitiveType.Cube, new Vector3(0f, 6.4f, 0f),
                new Vector3(2.4f, 2.2f, 2.4f), m_HeadColor);
            AddVisual("Crown", PrimitiveType.Cube, new Vector3(0f, 7.8f, 0f),
                new Vector3(2.9f, 0.4f, 2.9f), m_CrownColor, emissive: true);
            // shoulders and arms
            AddVisual("ShoulderL", PrimitiveType.Cube, new Vector3(-2.6f, 5.0f, 0f),
                new Vector3(1.8f, 1.6f, 2.4f), m_ShoulderColor);
            AddVisual("ShoulderR", PrimitiveType.Cube, new Vector3(2.6f, 5.0f, 0f),
                new Vector3(1.8f, 1.6f, 2.4f), m_ShoulderColor);
            AddVisual("ArmL", PrimitiveType.Cube, new Vector3(-3.4f, 3.4f, 0f),
                new Vector3(1.0f, 3.0f, 1.0f), m_ArmColor);
            AddVisual("ArmR", PrimitiveType.Cube, new Vector3(3.4f, 3.4f, 0f),
                new Vector3(1.0f, 3.0f, 1.0f), m_ArmColor);

            // elemental cores on the front face, near the weak points
            AddCore("CoreL", new Vector3(-1.15f, 3.3f, -1.8f));
            AddCore("CoreR", new Vector3(1.15f, 3.3f, -1.8f));

            // translucent nature aura (unlit so it reads as a glow, not a solid).
            // Compact shell hugging the silhouette — never encloses the whole boss.
            // Hidden in Phase 1, activated in Phase 2, disabled on death.
            var auraShader = Shader.Find("Sprites/Default");
            m_Aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_Aura.name = "Aura_Visual";
            m_Aura.transform.SetParent(m_VisualRoot.transform, false);
            m_Aura.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            m_Aura.transform.localScale = new Vector3(4.2f, 5.2f, 4.2f);
            var auraRenderer = m_Aura.GetComponent<MeshRenderer>();
            var auraMat = new Material(auraShader);
            auraMat.color = m_AuraColor;
            auraRenderer.material = auraMat;
            auraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_BaseColors["Aura_Visual"] = m_AuraColor;
            var auraCollider = m_Aura.GetComponent<Collider>();
            if (auraCollider != null)
                Destroy(auraCollider);
            m_Aura.SetActive(false); // Phase 1: aura hidden

            // warm rim light standing over the boss (presentation only)
            var lightGo = new GameObject("BossRimLight");
            lightGo.transform.SetParent(m_VisualRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 14f, 8f);
            m_RimLight = lightGo.AddComponent<Light>();
            m_RimLight.type = LightType.Spot;
            m_RimLight.color = new Color(1f, 0.85f, 0.55f);
            m_RimLight.intensity = 5f;
            m_RimLight.range = 45f;
            m_RimLight.spotAngle = 70f;
            m_RimLight.shadows = LightShadows.Soft;
            lightGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, -0.9f, -1f));

            // the gameplay body mesh stays invisible; gameplay collider untouched
            if (m_BodyRenderer != null)
                m_BodyRenderer.enabled = false;

            BuildWeakPointMarkers();
            m_Built = true;
        }

        void BuildWeakPointMarkers()
        {
            if (m_Boss.WeakPoints == null)
                return;
            foreach (var wp in m_Boss.WeakPoints)
            {
                if (wp == null) continue;

                // gameplay collider must not keep a permanently visible renderer;
                // the halo marker below is the only visual, shown while exposed
                var wpRenderer = wp.GetComponent<MeshRenderer>();
                if (wpRenderer != null)
                    wpRenderer.enabled = false;

                var marker = new GameObject("WeakPointMarker");
                marker.transform.SetParent(wp.transform, false);

                var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                halo.name = "WeakPointHalo";
                halo.transform.SetParent(marker.transform, false);
                var haloRenderer = halo.GetComponent<MeshRenderer>();
                var haloMat = new Material(Shader.Find("Sprites/Default"));
                haloMat.color = new Color(0.2f, 1f, 0.55f, 0.3f);
                haloRenderer.material = haloMat;
                haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var haloCol = halo.GetComponent<Collider>();
                if (haloCol != null)
                    Destroy(haloCol);

                // halo surrounds the weak point collider
                var bounds = wp.GetComponent<Collider>() != null
                    ? wp.GetComponent<Collider>().bounds.size
                    : Vector3.one;
                if (bounds == Vector3.zero) bounds = Vector3.one;
                halo.transform.localScale = new Vector3(Mathf.Max(0.9f, bounds.x * 1.6f),
                    Mathf.Max(0.9f, bounds.y * 1.6f), Mathf.Max(0.9f, bounds.z * 1.6f));

                marker.SetActive(false);
                m_Markers.Add(new WeakPointMarker { Point = wp, Marker = marker });
            }
        }

        void BuildDeferred()
        {
            if (!m_Built)
                StartCoroutine(BuildDeferredRoutine());
        }

        System.Collections.IEnumerator BuildDeferredRoutine()
        {
            yield return null;
            BuildSilhouette();
        }

        void Update()
        {
            if (m_Boss == null)
                return;

            // dead: aura/VFX stay off, no new feedback (weak points already closed)
            if (m_Dead)
                return;

            // hit flash: brief white pulse on body hits, green pulse on weak points
            if (m_HitFlash > 0f)
            {
                m_HitFlash -= Time.deltaTime;
                Color flash = m_HitFlashIsWeakPoint
                    ? new Color(0.3f, 1f, 0.7f, 1f)
                    : new Color(0.4f, 0.72f, 0.92f, 1f);
                ApplySilhouetteFlash(flash, Mathf.Clamp01(m_HitFlash / 0.16f) * 0.7f);
            }

            // stagger burst: full-body golden pulse at break
            if (m_StaggerFlash > 0f)
            {
                m_StaggerFlash -= Time.deltaTime;
                ApplySilhouetteFlash(new Color(1f, 0.85f, 0.3f, 1f), Mathf.Clamp01(m_StaggerFlash / 0.5f));
            }

            // phase-2 one-way visual change: aura turns on + compact red glow
            if (m_Boss.PhaseTwo && !m_PhaseTwoApplied)
            {
                if (m_Aura != null)
                {
                    var aur = m_Aura.GetComponent<MeshRenderer>();
                    var mat = new Material(aur.material);
                    mat.color = new Color(1f, 0.3f, 0.15f, 0.14f); // hot elemental aura
                    aur.material = mat;
                    m_Aura.SetActive(true);
                }
                foreach (var core in m_Cores)
                {
                    if (core == null) continue;
                    var r = core.GetComponent<MeshRenderer>();
                    var mat = new Material(r.material);
                    mat.color = m_PhaseTwoTint;
                    mat.SetColor("_EmissionColor", m_PhaseTwoTint * 1.8f);
                    r.material = mat;
                }
                if (m_RimLight != null)
                    m_RimLight.color = new Color(1f, 0.45f, 0.2f);
                m_PhaseTwoApplied = true;
            }

            UpdateWeakPointMarkers();
            UpdateFloatingMarkers();
        }

        /// <summary>Floating damage numbers: rise + fade once per damage event.</summary>
        void UpdateFloatingMarkers()
        {
            for (int i = m_FloatingMarkers.Count - 1; i >= 0; i--)
            {
                var fm = m_FloatingMarkers[i];
                if (fm.Go == null)
                {
                    m_FloatingMarkers.RemoveAt(i);
                    continue;
                }
                float t = fm.Time + Time.deltaTime;
                fm = new FloatingMarker { Go = fm.Go, Label = fm.Label, StartPos = fm.StartPos, Time = t, Life = fm.Life };
                m_FloatingMarkers[i] = fm;
                if (t >= fm.Life)
                {
                    Object.Destroy(fm.Go);
                    m_FloatingMarkers.RemoveAt(i);
                    continue;
                }
                fm.Go.transform.position = fm.StartPos + Vector3.up * (t * 1.4f);
                var cam = Camera.main;
                if (cam != null)
                    fm.Go.transform.rotation = Quaternion.LookRotation(fm.Go.transform.position - cam.transform.position);
                float k = 1f - t / fm.Life;
                if (fm.Label != null)
                    fm.Label.color = new Color(fm.Label.color.r, fm.Label.color.g, fm.Label.color.b, k);
            }
        }

        /// <summary>Markers visible only while the window is open; label bills to camera; hit flash.</summary>
        void UpdateWeakPointMarkers()
        {
            if (m_Markers.Count == 0)
                return;
            var cam = Camera.main;
            for (int i = m_Markers.Count - 1; i >= 0; i--)
            {
                var m = m_Markers[i];
                if (m.Point == null || m.Marker == null)
                {
                    m_Markers.RemoveAt(i);
                    continue;
                }
                bool show = m.Point.IsExposed;
                bool wasVisible = m.Marker.activeSelf;
                if (m.Marker.activeSelf != show)
                    m.Marker.SetActive(show);
                if (show && !wasVisible)
                    SpawnExposureBurst(m.Point.transform.position);
                if (!show) continue;

                // impact flash: halo pulses white
                float pulse = 0f;
                if (m.Flash > 0f)
                {
                    float f = m.Flash - Time.deltaTime;
                    m = new WeakPointMarker { Point = m.Point, Marker = m.Marker, Flash = f };
                    m_Markers[i] = m;
                    pulse = Mathf.Clamp01(f / 0.2f);
                }
                var halo = m.Marker.transform.Find("WeakPointHalo");
                if (halo != null)
                {
                    var r = halo.GetComponent<MeshRenderer>();
                    var mat = r.material;
                    float wave = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
                    mat.color = Color.Lerp(new Color(0.2f, 1f, 0.55f, 0.3f), new Color(1f, 1f, 0.8f, 0.8f),
                        Mathf.Max(wave, pulse));
                }

            }
        }

        void OnBossHit(Vector3 pos, float damage, bool weakPoint)
        {
            if (m_Dead) // no new hit feedback after DEFEATED
                return;

            m_HitFlash = weakPoint ? 0.22f : 0.16f;
            m_HitFlashIsWeakPoint = weakPoint;

            // impact flash on any currently exposed weak point markers
            for (int i = 0; i < m_Markers.Count; i++)
                if (m_Markers[i].Point != null && m_Markers[i].Point.IsExposed)
                    m_Markers[i] = new WeakPointMarker { Point = m_Markers[i].Point, Marker = m_Markers[i].Marker, Flash = 0.2f };

            // Body hits get a crisp armor reaction, but no damage number: a normal
            // number here falsely suggests that the player is piercing the shell.
            if (!weakPoint)
            {
                SpawnArmorDeflection(pos);
                return;
            }

            // floating damage marker (throttled against machine-gun tick spam)
            if (Time.time - m_LastMarkerSpawn < 0.08f)
                return;
            m_LastMarkerSpawn = Time.time;

            SpawnImpact(pos, weakPoint);

            var markerGo = new GameObject("DamageNumber");
            markerGo.transform.position = pos + Vector3.up * (weakPoint ? 0.9f : 0.6f);
            var label = markerGo.AddComponent<TextMesh>();
            label.fontSize = weakPoint ? 26 : 18;
            label.anchor = TextAnchor.MiddleCenter;
            label.text = weakPoint ? $"WEAK POINT -{damage:F0}" : $"-{damage:F0}";
            var color = weakPoint ? new Color(0.35f, 1f, 0.5f, 1f) : new Color(1f, 0.95f, 0.6f, 1f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            markerGo.GetComponent<MeshRenderer>().material = label.font.material;
            label.color = color;
            m_FloatingMarkers.Add(new FloatingMarker
            {
                Go = markerGo,
                Label = label,
                StartPos = markerGo.transform.position,
                Time = 0f,
                Life = weakPoint ? 1.1f : 0.8f
            });
        }

        /// <summary>Brief impact flash sphere at the exact hit point.</summary>
        void SpawnImpact(Vector3 position, bool weakPoint)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = weakPoint ? "WeakPointImpact" : "HitImpact";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (weakPoint ? 0.55f : 0.35f);
            var r = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = weakPoint ? new Color(0.3f, 1f, 0.6f, 0.9f) : new Color(1f, 0.9f, 0.5f, 0.9f);
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);
            Object.Destroy(go, 0.22f);
        }

        void SpawnArmorDeflection(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ArmorDeflection";
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.58f, 0.08f, 0.58f);
            var renderer = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0.45f, 0.8f, 1f, 0.8f);
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            Object.Destroy(go, 0.12f);
        }

        void SpawnExposureBurst(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "WeakPointExposureBurst";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.32f;
            var renderer = go.GetComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = new Color(0.35f, 1f, 0.55f, 0.95f);
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
            Object.Destroy(go, 0.35f);
        }

        void OnStaggerBreak()
        {
            m_StaggerFlash = 0.5f;
        }

        float m_HitFlash;
        float m_StaggerFlash;
        bool m_HitFlashIsWeakPoint;

        /// <summary>Overlays a color on every solid silhouette piece briefly. k=0..1.</summary>
        void ApplySilhouetteFlash(Color flash, float k)
        {
            if (m_VisualRoot == null)
                return;
            foreach (Transform child in m_VisualRoot.transform)
            {
                if (child == null || !child.name.EndsWith("_Visual"))
                    continue;
                if (child == m_Aura.transform) // aura keeps its own subtle color
                    continue;
                var r = child.GetComponent<MeshRenderer>();
                if (r == null)
                    continue;
                Color baseColor;
                if (!m_BaseColors.TryGetValue(child.name, out baseColor))
                    baseColor = m_TorsoColor;
                var mat = new Material(r.material);
                mat.color = Color.Lerp(baseColor, flash, k);
                r.material = mat;
            }
        }
    }
}
