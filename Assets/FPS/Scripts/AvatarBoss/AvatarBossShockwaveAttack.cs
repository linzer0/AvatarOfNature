using System.Collections;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Shockwave: an expanding ring pushes the player away from the boss.
    /// It is a positioning threat, not a direct-damage or arena-damage attack.
    public class AvatarBossShockwaveAttack : AvatarBossAttack
    {
        [Header("Shockwave")]
        [Tooltip("Speed of the wave front, metres per second (tuned so Execute stays in the 1.0-1.5 s window)")]
        public float WaveSpeed = 20f;

        [Tooltip("Radius at which the wave dissipates")]
        public float MaxRadius = 28f;

        [Tooltip("Half-width of the band in which the player is pushed")]
        public float PushBandWidth = 1.2f;
        [Tooltip("Horizontal impulse applied away from the boss")]
        public float PushForce = 22f;
        [Tooltip("Small lift that makes the push readable without becoming a jump attack")]
        public float PushLift = 1.5f;

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
                // white pre-telegraph: bright at Telegraph phase, switches to orange at Execute
                s_RingMaterial.color = new Color(1f, 1f, 1f, 0.55f);
            }
            if (s_RingMaterial != null)
                ringRenderer.material = s_RingMaterial;

            StartCoroutine(PreTelegraphExpand());
        }

        IEnumerator PreTelegraphExpand()
        {
            // animate the ring from 0 to approximate warning radius during the telegraph phase
            float t = 0f;
            while (m_Ring != null && t < TelegraphTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / TelegraphTime);
                m_Ring.transform.localScale = new Vector3(4f + 4f * k, 4f + 4f * k, 1f); // small preview ring
                yield return null;
            }
        }

        public override IEnumerator Execute()
        {
            if (m_Ring == null || m_Player == null)
                yield break;

            // white pulse ends; the wave turns orange and expands
            if (s_RingMaterial != null)
                s_RingMaterial.color = new Color(1f, 0.7f, 0.15f, 0.85f);

            Vector3 center = m_Ring.transform.position;
            float radius = 0.5f;
            bool pushDone = false;

            while (m_Ring != null && radius < MaxRadius)
            {
                radius += WaveSpeed * Time.deltaTime;
                m_Ring.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

                // pulse alpha for readability; white flash pumping on the wave front
                if (m_Ring != null && s_RingMaterial != null)
                {
                    float a = Mathf.Lerp(0.45f, 0.9f, 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed));
                    // wavefront flashes white every pulse
                    float rhythm = Mathf.Repeat(Time.time * PulseSpeed, 1f);
                    Color baseColor = new Color(1f, 0.7f, 0.15f, a);
                    s_RingMaterial.color = Color.Lerp(baseColor, Color.white, Mathf.Pow(rhythm, 3f) * 0.6f);
                }

                if (!pushDone)
                {
                    var controller = m_Player.GetComponent<PlayerCharacterController>();
                    if (controller != null)
                    {
                        Vector2 center2D = new Vector2(center.x, center.z);
                        Vector2 player2D = new Vector2(m_Player.position.x, m_Player.position.z);
                        float dist = Vector2.Distance(center2D, player2D);

                        if (dist <= radius && dist > radius - PushBandWidth)
                        {
                            Vector3 pushDirection = m_Player.position - center;
                            pushDirection.y = 0f;
                            if (pushDirection.sqrMagnitude < 0.001f)
                                pushDirection = m_Player.forward;
                            controller.ApplyExternalImpulse(pushDirection.normalized * PushForce
                                + Vector3.up * PushLift);
                            pushDone = true;
                            // Shockwave language: force burst where the wave lands.
                            SpawnImpactEffect(m_Player.position);
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
            float maxDist = 60f;
            var hits = Physics.RaycastAll(from, Vector3.down, maxDist, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                // skip the boss's own body so the ring lands on the arena floor, not on the boss
                if (m_Boss != null && hit.collider.transform.root == m_Boss.transform.root)
                    continue;
                if (hit.distance < bestDist)
                {
                    bestDist = hit.distance;
                    best = hit;
                }
            }
            if (bestDist < float.MaxValue)
                return best.point;
            return from - Vector3.up * 20f;
        }
    }
}
