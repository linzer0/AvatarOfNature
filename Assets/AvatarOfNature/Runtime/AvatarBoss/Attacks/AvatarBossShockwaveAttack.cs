using System.Collections;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Shockwave: a directed lane pushes the player away from the boss.
    /// It is a positioning threat, not a direct-damage or arena-damage attack.
    public class AvatarBossShockwaveAttack : AvatarBossAttack
    {
        [Header("Shockwave")]
        [Tooltip("Speed of the wave front, metres per second (tuned so Execute stays in the 1.0-1.5 s window)")]
        public float WaveSpeed = 20f;

        [Tooltip("Radius at which the wave dissipates")]
        public float MaxRadius = 28f;

        [Tooltip("Half-width of the moving front in metres")]
        public float PushBandWidth = 1.2f;
        [Tooltip("Impulse in the chosen sector; adjacent sectors receive the configured fraction")]
        public float PushForce = 58f;
        [Range(0f, 1f)] public float AdjacentSectorForceMultiplier = 0.65f;
        [Tooltip("Angular padding around each sector lane")]
        [Range(0f, 0.4f)] public float LaneAngularPadding = 0.08f;
        [Tooltip("Small lift that makes the push readable without becoming a jump attack")]
        public float PushLift = 1.1f;
        [Tooltip("Difficulty-controlled physical knockback toggle. The wave telegraph remains active when disabled.")]
        public bool KnockbackEnabled = true;

        [Header("Visuals")]
        [Tooltip("Ring alpha pulse frequency (visual readability only)")]
        public float PulseSpeed = 6f;

        Transform m_Player;
        AvatarBossController m_Boss;
        AvatarBossCombatContext m_Context;
        GameObject m_Ring;
        Vector3 m_WaveOrigin;
        Vector3 m_TargetDirection = Vector3.forward;
        int m_SectorCount = 8;

        static Material s_RingMaterial;

        protected override AvatarBossElement ExpectedElement => AvatarBossElement.Shockwave;

        void Awake()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
            m_Context = m_Boss != null ? m_Boss.GetCombatContext() : null;
            m_Context?.ResolveSceneReferences();
        }

        public override void SetIntent(AvatarBossIntent intent)
        {
            base.SetIntent(intent);
            SetArenaTargetSectors(new[] { intent.TargetSector });
            m_TargetDirection = intent.TargetDirection;
            m_TargetDirection.y = 0f;
            if (m_TargetDirection.sqrMagnitude < 0.001f)
                m_TargetDirection = Vector3.forward;
            m_TargetDirection.Normalize();

            var arena = m_Context != null ? m_Context.Arena : null;
            if (arena != null)
                m_SectorCount = Mathf.Clamp(arena.SectorCount, 8, 12);
        }
        public override void Prepare()
        {
            if (m_Player == null)
            {
                m_Context?.ResolveSceneReferences();
                var player = m_Context != null ? m_Context.Player : null;
                m_Player = player != null ? player.transform : null;
            }

            if (m_Boss == null || m_Player == null)
            {
                Debug.LogWarning("[AvatarOfNature] Shockwave needs boss and player references.", this);
                return;
            }

            Vector3 center = SnapToGround(m_Boss.transform.position + Vector3.up * 20f);
            m_WaveOrigin = center + Vector3.up * 0.1f;

            m_Ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(m_Ring.GetComponent<Collider>());
            m_Ring.name = "ShockwaveTelegraph";
            m_Ring.transform.SetPositionAndRotation(m_WaveOrigin,
                Quaternion.LookRotation(m_TargetDirection, Vector3.up));
            m_Ring.transform.localScale = new Vector3(2f, 0.05f, 0f);

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
                float length = 2f + 8f * k;
                PositionWave(length);
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
                PositionWave(radius);

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
                        Vector3 toPlayer = m_Player.position - center;
                        toPlayer.y = 0f;
                        float dist = toPlayer.magnitude;
                        Vector3 playerDirection = dist > 0.001f ? toPlayer / dist : m_TargetDirection;
                        float angularOffset = Vector3.SignedAngle(m_TargetDirection, playerDirection, Vector3.up);
                        float sectorAngle = 360f / Mathf.Max(1, m_SectorCount);
                        int laneOffset = Mathf.RoundToInt(angularOffset / sectorAngle);

                        if (dist <= radius && dist > radius - PushBandWidth
                            && Mathf.Abs(angularOffset) <= sectorAngle + LaneAngularPadding * Mathf.Rad2Deg)
                        {
                            float forceMultiplier = Mathf.Abs(laneOffset) == 0
                                ? 1f
                                : Mathf.Abs(laneOffset) == 1 ? AdjacentSectorForceMultiplier : 0f;
                            if (forceMultiplier <= 0f)
                            {
                                yield return null;
                                continue;
                            }

                            Vector3 pushDirection = m_Player.position - m_Boss.transform.position;
                            pushDirection.y = 0f;
                            if (pushDirection.sqrMagnitude < 0.001f)
                                pushDirection = m_TargetDirection;
                            else
                                pushDirection.Normalize();

                            if (KnockbackEnabled)
                                controller.ApplyExternalImpulse(pushDirection * (PushForce * forceMultiplier)
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

        void PositionWave(float length)
        {
            if (m_Ring == null)
                return;
            float width = Mathf.Max(1f, length * (2f * Mathf.Tan(Mathf.Deg2Rad * (180f / m_SectorCount * 0.5f + LaneAngularPadding))));
            m_Ring.transform.SetPositionAndRotation(
                m_WaveOrigin + m_TargetDirection * (length * 0.5f),
                Quaternion.LookRotation(m_TargetDirection, Vector3.up));
            m_Ring.transform.localScale = new Vector3(width, 0.05f, length);
        }
    }
}
