using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Visual polish for the Avatar of Nature: builds a readable stylized
    /// silhouette (VisualRoot) from primitives, element colors, phase-2 tint,
    /// weak point glow. Gameplay colliders/health are untouched.
    public class AvatarBossVisualEffects : MonoBehaviour
    {
        [Header("Phase 2")]
        public Color PhaseTwoBodyColor = new Color(1f, 0.35f, 0.18f);

        AvatarBossController m_Boss;
        MeshRenderer m_BodyRenderer;
        GameObject m_VisualRoot;
        GameObject m_Aura;
        readonly List<GameObject> m_Cores = new List<GameObject>();
        AvatarBossWeakPoint[] m_WeakPoints;
        bool m_PhaseTwoApplied;

        readonly Color m_StoneColor = new Color(0.32f, 0.24f, 0.16f, 1f);   // dark earth-stone
        readonly Color m_GoldColor = new Color(0.85f, 0.62f, 0.16f, 1f);    // golden cores
        readonly Color m_PhaseTwoTint = new Color(1f, 0.32f, 0.14f, 1f);    // phase-2 fire accent
        readonly Color m_AuraColor = new Color(0.55f, 0.85f, 0.65f, 0.10f); // nature aura

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

            m_WeakPoints = m_Boss.WeakPoints;
            BuildSilhouette();
        }

        void BuildSilhouette()
        {
            var existing = transform.Find("VisualRoot");
            if (existing != null)
            {
                m_VisualRoot = existing.gameObject;
                return; // a VisualRoot already exists in the scene
            }

            m_VisualRoot = new GameObject("VisualRoot");
            m_VisualRoot.transform.SetParent(transform, false);

            // main husk: broad elemental torso, clearly taller and wider than the player
            AddVisual("Torso", PrimitiveType.Cube, new Vector3(0f, 3.1f, 0f),
                new Vector3(3.6f, 4.6f, 3.6f), m_StoneColor);
            // head / mask with golden crown
            AddVisual("Head", PrimitiveType.Cube, new Vector3(0f, 6.4f, 0f),
                new Vector3(2.4f, 2.2f, 2.4f), m_StoneColor);
            AddVisual("Crown", PrimitiveType.Cube, new Vector3(0f, 7.8f, 0f),
                new Vector3(2.9f, 0.4f, 2.9f), m_GoldColor);
            // shoulders and arms
            AddVisual("ShoulderL", PrimitiveType.Cube, new Vector3(-2.6f, 5.0f, 0f),
                new Vector3(1.8f, 1.6f, 2.4f), m_StoneColor);
            AddVisual("ShoulderR", PrimitiveType.Cube, new Vector3(2.6f, 5.0f, 0f),
                new Vector3(1.8f, 1.6f, 2.4f), m_StoneColor);
            AddVisual("ArmL", PrimitiveType.Cube, new Vector3(-3.4f, 3.4f, 0f),
                new Vector3(1.0f, 3.0f, 1.0f), m_StoneColor);
            AddVisual("ArmR", PrimitiveType.Cube, new Vector3(3.4f, 3.4f, 0f),
                new Vector3(1.0f, 3.0f, 1.0f), m_StoneColor);

            // elemental cores on the front face, near the weak points
            AddCore("CoreL", new Vector3(-1.15f, 3.3f, -1.8f));
            AddCore("CoreR", new Vector3(1.15f, 3.3f, -1.8f));

            // readable nature aura
            m_Aura = AddVisual("Aura", PrimitiveType.Sphere, new Vector3(0f, 3.2f, 0f),
                new Vector3(6.4f, 7.4f, 6.4f), m_AuraColor);

            // the gameplay body mesh stays invisible; gameplay collider untouched
            if (m_BodyRenderer != null)
                m_BodyRenderer.enabled = false;
        }

        GameObject AddVisual(string goName, PrimitiveType type, Vector3 localPosition,
            Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = goName + "_Visual";
            go.transform.SetParent(m_VisualRoot.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;

            var mat = Shader.Find("Sprites/Default") != null
                ? new Material(Shader.Find("Sprites/Default"))
                : new Material(go.GetComponent<MeshRenderer>().material);
            mat.color = color;
            go.GetComponent<MeshRenderer>().material = mat;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider); // visuals never take part in gameplay
            return go;
        }

        void AddCore(string coreName, Vector3 localPosition)
        {
            var go = AddVisual(coreName, PrimitiveType.Sphere, localPosition,
                Vector3.one * 0.55f, m_GoldColor);
            m_Cores.Add(go);
        }

        void Update()
        {
            if (m_Boss == null)
                return;

            // phase-2 one-way visual change
            if (m_Boss.PhaseTwo && !m_PhaseTwoApplied)
            {
                if (m_Aura != null)
                {
                    var aur = m_Aura.GetComponent<MeshRenderer>();
                    var mat = new Material(aur.material);
                    mat.color = new Color(1f, 0.3f, 0.15f, 0.16f); // hot elemental aura
                    aur.material = mat;
                }
                foreach (var core in m_Cores)
                {
                    if (core == null) continue;
                    var r = core.GetComponent<MeshRenderer>();
                    var mat = new Material(r.material);
                    mat.color = m_PhaseTwoTint;
                    r.material = mat;
                }
                Debug.Log("[AvatarOfNature] Visual feedback: PHASE TWO silhouette tint applied.", this);
                m_PhaseTwoApplied = true;
            }

            PollWeakPointColors();
        }

        void PollWeakPointColors()
        {
            if (m_WeakPoints == null)
                return;
            for (int i = 0; i < m_WeakPoints.Length; i++)
            {
                if (m_WeakPoints[i] == null)
                    continue;
                var renderer = m_WeakPoints[i].GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;
                var mat = new Material(renderer.material);
                mat.color = m_WeakPoints[i].IsExposed ? new Color(0f, 1f, 0.55f, 1f) : Color.white;
                renderer.material = mat;
            }
        }
    }
}
