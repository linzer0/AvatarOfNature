using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Earth attack: telegraphed patches on the ground around the player,
    /// then spikes rise and deal area damage at the moment they surface.
    public class AvatarBossEarthAttack : AvatarBossAttack
    {
        [Header("Earth")]
        [Tooltip("Number of telegraphed spike positions (the first targets the player)")]
        public int SpikeCount = 5;
        [Tooltip("Max distance of spike positions around the player")]
        public float SpikeSpread = 6f;
        [Range(0f, 1f)] public float DirectTargetChance = 0.55f;
        public float DriftDistanceMin = 2.5f;
        public float DriftDistanceMax = 5.5f;
        [Tooltip("Radius inside which the spikes deal damage")]
        public float DamageRadius = 2.5f;
        [Range(1, 4)] public int TargetCellCount = 2;

        [Header("Spike visuals")]
        public float SpikeRiseTime = 0.25f;
        public float SpikeHoldTime = 0.75f;
        public float SpikeHeight = 3f;

        Transform m_Player;
        AvatarBossController m_Boss;
        AvatarBossArenaController m_Arena;
        int m_TargetSector = -1;
        readonly List<GameObject> m_Telegraphs = new List<GameObject>();
        readonly List<GameObject> m_Spikes = new List<GameObject>();
        readonly List<Vector3> m_ImpactPoints = new List<Vector3>();

        static Material s_DecalMaterial;
        static Material s_SpikeMaterial;

        protected override AvatarBossElement ExpectedElement => AvatarBossElement.Earth;

        void Awake()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
            m_Arena = FindFirstObjectByType<AvatarBossArenaController>();
        }

        public override void SetIntent(AvatarBossIntent intent)
        {
            base.SetIntent(intent);
            m_TargetSector = intent.TargetSector;
        }

        public override void Prepare()
        {
            if (m_Arena == null)
                m_Arena = FindFirstObjectByType<AvatarBossArenaController>();
            if (m_Player == null)
            {
                var player = FindFirstObjectByType<PlayerCharacterController>();
                m_Player = player != null ? player.transform : null;
            }

            if (m_Player == null)
            {
                Debug.LogWarning("[AvatarOfNature] No player found for Earth attack telegraph.", this);
                return;
            }

            m_ImpactPoints.Clear();
            if (m_Arena != null && m_TargetSector >= 0)
            {
                var targetSectors = m_Arena.GetAttackTargetSectors(m_TargetSector, TargetCellCount);
                SetArenaTargetSectors(targetSectors);
                for (int i = 0; i < targetSectors.Count; i++)
                {
                    var overlay = m_Arena.CreateSectorTelegraph(targetSectors[i],
                        new Color(0.85f, 0.58f, 0.12f, 0.55f), "EarthCellTelegraph");
                    if (overlay != null)
                        m_Telegraphs.Add(overlay);
                }

                for (int i = 0; i < SpikeCount && targetSectors.Count > 0; i++)
                {
                    int sector = targetSectors[i % targetSectors.Count];
                    m_ImpactPoints.Add(i == 0
                        ? m_Arena.GetSectorTargetPoint(sector, m_Player.position)
                        : m_Arena.GetRandomPointInSector(sector));
                }
                return;
            }

            SetArenaTargetSectors(null);
            for (int i = 0; i < SpikeCount; i++)
            {
                Vector2 rnd = Random.insideUnitCircle * SpikeSpread;
                Vector3 center = SnapToGround(m_Player.position + new Vector3(rnd.x, 20f, rnd.y));
                m_ImpactPoints.Add(center);

                GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(decal.GetComponent<Collider>());
                decal.name = "EarthTelegraph";
                decal.transform.SetPositionAndRotation(center + Vector3.up * 0.08f,
                    Quaternion.Euler(-90f, Random.value * 360f, 0f));
                decal.transform.localScale = Vector3.one * 3.5f;
                MeshRenderer decalRenderer = decal.GetComponent<MeshRenderer>();
                Shader textureShader = Shader.Find("Sprites/Default");
                if (s_DecalMaterial == null && textureShader != null)
                    s_DecalMaterial = new Material(textureShader)
                        { color = new Color(0.72f, 0.5f, 0.12f, 0.7f) };
                if (s_DecalMaterial != null)
                    decalRenderer.material = s_DecalMaterial;
                m_Telegraphs.Add(decal);
            }
        }

        public override IEnumerator Execute()
        {
            if (m_ImpactPoints.Count == 0)
                yield break;

            foreach (Vector3 impactPoint in m_ImpactPoints)
            {
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(spike.GetComponent<Collider>()); // damage applied via OverlapSphere
                spike.name = "EarthSpike";
                spike.transform.position = impactPoint - Vector3.up * SpikeHeight;
                spike.transform.rotation = Quaternion.identity;
                spike.transform.localScale = new Vector3(0.8f, SpikeHeight, 0.8f);

                MeshRenderer spikeRenderer = spike.GetComponent<MeshRenderer>();
                Shader textureShader = Shader.Find("Sprites/Default");
                if (s_SpikeMaterial == null && textureShader != null)
                {
                    s_SpikeMaterial = new Material(textureShader);
                    s_SpikeMaterial.color = new Color(0.55f, 0.35f, 0.15f, 1f);
                }
                if (s_SpikeMaterial != null)
                    spikeRenderer.material = s_SpikeMaterial;

                m_Spikes.Add(spike);
            }

            float t = 0f;
            while (t < SpikeRiseTime)
            {
                t += Time.deltaTime;
                float step = SpikeHeight * Time.deltaTime / SpikeRiseTime;
                foreach (var spike in m_Spikes)
                    if (spike != null)
                        spike.transform.position += Vector3.up * step;
                yield return null;
            }

            DealDamage();

            yield return new WaitForSeconds(SpikeHoldTime);

            DestroySpikes();
        }

        void DealDamage()
        {
            foreach (var spike in m_Spikes)
            {
                if (spike == null)
                    continue;

                Vector3 center = spike.transform.position;
                float fairnessRadius = DamageRadius + SpikeHeight * 0.5f;
                Collider[] hits = Physics.OverlapSphere(center, fairnessRadius, Physics.AllLayers,
                    QueryTriggerInteraction.Ignore);

                var damaged = new HashSet<Health>();
                foreach (var hit in hits)
                {
                    Damageable damageable = hit.GetComponent<Damageable>()
                        ?? hit.GetComponentInParent<Damageable>();
                    if (damageable == null || damageable.Health == null || damaged.Contains(damageable.Health))
                        continue;

                    // never damage the boss hierarchy with its own attack
                    if (m_Boss != null && damageable.transform.IsChildOf(m_Boss.transform))
                        continue;

                    damaged.Add(damageable.Health);
                    damageable.InflictDamage(Damage, false,
                        m_Boss != null ? m_Boss.gameObject : gameObject);
                }

                // earth impact language: dust burst at every spike
                SpawnImpactEffect(center - Vector3.up * 0.5f);
            }
        }

        void DestroySpikes()
        {
            foreach (var spike in m_Spikes)
                if (spike != null)
                    Destroy(spike);
            m_Spikes.Clear();
        }

        public override void Cleanup()
        {
            foreach (var decal in m_Telegraphs)
                if (decal != null)
                    Destroy(decal);
            m_Telegraphs.Clear();
            m_ImpactPoints.Clear();

            DestroySpikes();
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
