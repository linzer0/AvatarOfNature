using System.Collections;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Shockwave: an expanding ring travels across the arena from the boss.
    /// Players avoid damage by leaving the ground (jump) when the wave passes.
    public class AvatarBossShockwaveAttack : AvatarBossAttack
    {
        [Header("Shockwave")]
        [Tooltip("Speed of the wave front, metres per second")]
        public float WaveSpeed = 12f;

        [Tooltip("Radius at which the wave dissipates")]
        public float MaxRadius = 28f;

        [Tooltip("Half-width of the band in which a grounded player takes damage")]
        public float DamageBandWidth = 1.2f;

        [Header("Visuals")]
        [Tooltip("Ring alpha pulse frequency (visual readability only)")]
        public float PulseSpeed = 6f;

        Transform m_Player;
        AvatarBossController m_Boss;
        GameObject m_Ring;

        static Material s_RingMaterial;

        protected override AvatarBossElement ExpectedElement => AvatarBossElement.Shockwave;

        void Awake()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
        }

        public override void Prepare()
        {
            if (m_Player == null)
            {
                var player = FindFirstObjectByType<PlayerCharacterController>();
                m_Player = player != null ? player.transform : null;
            }

            if (m_Boss == null || m_Player == null)
            {
                Debug.LogWarning("[AvatarOfNature] Shockwave needs boss and player references.", this);
                return;
            }

            Vector3 center = SnapToGround(m_Boss.transform.position + Vector3.up * 20f);

            m_Ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(m_Ring.GetComponent<Collider>());
            m_Ring.name = "ShockwaveTelegraph";
            m_Ring.transform.SetPositionAndRotation(center + Vector3.up * 0.1f,
                Quaternion.Euler(-90f, 0f, 0f));
            m_Ring.transform.localScale = Vector3.one * 0f;

            MeshRenderer ringRenderer = m_Ring.GetComponent<MeshRenderer>();
            Shader textureShader = Shader.Find("Sprites/Default");
            if (s_RingMaterial == null && textureShader != null)
            {
                s_RingMaterial = new Material(textureShader);
                s_RingMaterial.color = new Color(1f, 0.7f, 0.15f, 0.75f);
            }
            if (s_RingMaterial != null)
                ringRenderer.material = s_RingMaterial;
        }

        public override IEnumerator Execute()
        {
            if (m_Ring == null || m_Player == null)
                yield break;

            Vector3 center = m_Ring.transform.position;
            float radius = 0.5f;
            bool damageDone = false;

            while (m_Ring != null && radius < MaxRadius)
            {
                radius += WaveSpeed * Time.deltaTime;
                m_Ring.transform.localScale = Vector3.one * (radius * 2f);

                // pulse alpha for readability
                MeshRenderer renderer = m_Ring.GetComponent<MeshRenderer>();
                if (renderer != null && s_RingMaterial != null)
                {
                    float a = Mathf.Lerp(0.45f, 0.85f, 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed));
                    s_RingMaterial.color = new Color(1f, 0.7f, 0.15f, a);
                }

                if (!damageDone)
                {
                    var controller = m_Player.GetComponent<PlayerCharacterController>();
                    if (controller != null && controller.IsGrounded)
                    {
                        Vector2 center2D = new Vector2(center.x, center.z);
                        Vector2 player2D = new Vector2(m_Player.position.x, m_Player.position.z);
                        float dist = Vector2.Distance(center2D, player2D);

                        if (dist <= radius && dist > radius - DamageBandWidth)
                        {
                            Damageable damageable = m_Player.GetComponentInChildren<Damageable>();
                            if (damageable != null)
                            {
                                damageable.InflictDamage(Damage, false,
                                    m_Boss != null ? m_Boss.gameObject : gameObject);
                                damageDone = true;
                            }
                        }
                    }
                }

                yield return null;
            }
        }

        public override void Cleanup()
        {
            if (m_Ring != null)
            {
                Destroy(m_Ring);
                m_Ring = null;
            }
        }

        Vector3 SnapToGround(Vector3 from)
        {
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
                return hit.point;
            return from - Vector3.up * 20f;
        }
    }
}
