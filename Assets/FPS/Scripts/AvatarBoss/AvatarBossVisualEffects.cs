using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Visual polish for the Avatar of Nature: element color language, phase-2
    /// emissive body, weak point glow. Polls boss state (no event chains) so it
    /// adds zero coupling and survives death/disabled phases safely.
    public class AvatarBossVisualEffects : MonoBehaviour
    {
        [Header("Phase 2")]
        public Color PhaseTwoBodyColor = new Color(1f, 0.35f, 0.18f);
        [Tooltip("Body emissive intensity in Phase 2")]
        public float PhaseTwoEmissiveIntensity = 0.6f;

        [Header("Weak Points")]
        public Color ExposedWeakPointColor = new Color(0f, 1f, 0.55f);
        public Color HiddenWeakPointColor = Color.white;

        AvatarBossController m_Boss;
        MeshRenderer m_BodyRenderer;
        bool m_PhaseTwoApplied;

        void Start()
        {
            // only work when placed on the boss root
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
        }

        void Update()
        {
            if (m_Boss == null)
                return;

            // phase-2 one-way visual body tint (cached; the renderer is set once)
            if (m_Boss.PhaseTwo && !m_PhaseTwoApplied && m_BodyRenderer != null)
            {
                var mat = new Material(m_BodyRenderer.material);
                mat.color = PhaseTwoBodyColor;
                mat.SetColor("_EmissionColor", PhaseTwoBodyColor * 0.4f);
                mat.EnableKeyword("_EMISSION");
                m_BodyRenderer.material = mat;
                Debug.Log("[AvatarOfNature] Visual feedback: PHASE TWO body tint applied.", this);
                m_PhaseTwoApplied = true;
            }

            PollWeakPointColors();
        }

        void PollWeakPointColors()
        {
            if (m_WeakPoints == null || m_WeakPoints.Length == 0)
                return;
            for (int i = 0; i < m_WeakPoints.Length; i++)
            {
                if (m_WeakPoints[i] == null)
                    continue;
                var renderer = m_WeakPoints[i].GetComponent<MeshRenderer>();
                if (renderer == null)
                    continue;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor("_Color", m_WeakPoints[i].IsExposed ? ExposedWeakPointColor : HiddenWeakPointColor);
                renderer.SetPropertyBlock(block);
            }
        }

        AvatarBossWeakPoint[] m_WeakPoints;
    }
}
