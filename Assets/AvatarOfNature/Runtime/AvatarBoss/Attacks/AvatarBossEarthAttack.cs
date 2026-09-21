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
        [Tooltip("Horizontal gameplay radius around each spike impact point")]
        public float DamageRadius = 2.5f;
        [Range(1, 4)] public int TargetCellCount = 1;

        [Header("Spike visuals")]
        public float SpikeRiseTime = 0.25f;
        public float SpikeHoldTime = 0.75f;
        public float SpikeHeight = 3f;

        Transform m_Player;
        AvatarBossController m_Boss;
        AvatarBossCombatContext m_Context;
        AvatarBossArenaController m_Arena;
        int m_TargetSector = -1;
        readonly List<GameObject> m_Telegraphs = new List<GameObject>();
        readonly List<GameObject> m_TelegraphPool = new List<GameObject>();
        readonly List<GameObject> m_Spikes = new List<GameObject>();
        readonly List<GameObject> m_SpikePool = new List<GameObject>();
        readonly List<Vector3> m_ImpactPoints = new List<Vector3>();

        static Material s_DecalMaterial;
        static Material s_SpikeMaterial;
        static Shader s_SpriteShader;

        protected override AvatarBossElement ExpectedElement => AvatarBossElement.Earth;

        void Awake()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
            m_Context = m_Boss != null ? m_Boss.GetCombatContext() : null;
            m_Context?.ResolveSceneReferences();
            m_Arena = m_Context != null ? m_Context.Arena : null;
        }

        public override void SetIntent(AvatarBossIntent intent)
        {
            base.SetIntent(intent);
            m_TargetSector = intent.TargetSector;
        }

        public override void Prepare()
        {
            if (m_Arena == null)
            {
                m_Context?.ResolveSceneReferences();
                m_Arena = m_Context != null ? m_Context.Arena : null;
            }
            if (m_Player == null)
            {
                m_Context?.ResolveSceneReferences();
                var player = m_Context != null ? m_Context.Player : null;
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

                GameObject decal = AcquirePrimitive(m_TelegraphPool, PrimitiveType.Quad, "EarthTelegraph");
                decal.name = "EarthTelegraph";
                decal.transform.SetPositionAndRotation(center + Vector3.up * 0.08f,
                    Quaternion.Euler(-90f, Random.value * 360f, 0f));
                decal.transform.localScale = Vector3.one * 3.5f;
                MeshRenderer decalRenderer = decal.GetComponent<MeshRenderer>();
                if (s_DecalMaterial == null && SpriteShader != null)
                    s_DecalMaterial = new Material(SpriteShader)
                        { color = new Color(0.72f, 0.5f, 0.12f, 0.7f) };
                if (s_DecalMaterial != null)
                    decalRenderer.sharedMaterial = s_DecalMaterial;
                m_Telegraphs.Add(decal);
            }
        }

        public override IEnumerator Execute()
        {
            if (m_ImpactPoints.Count == 0)
                yield break;

            foreach (Vector3 impactPoint in m_ImpactPoints)
            {
                GameObject spike = AcquirePrimitive(m_SpikePool, PrimitiveType.Cube, "EarthSpike");
                spike.name = "EarthSpike";
                spike.transform.position = impactPoint - Vector3.up * SpikeHeight;
                spike.transform.rotation = Quaternion.identity;
                spike.transform.localScale = new Vector3(0.8f, SpikeHeight, 0.8f);

                MeshRenderer spikeRenderer = spike.GetComponent<MeshRenderer>();
                if (s_SpikeMaterial == null && SpriteShader != null)
                {
                    s_SpikeMaterial = new Material(SpriteShader);
                    s_SpikeMaterial.color = new Color(0.55f, 0.35f, 0.15f, 1f);
                }
                if (s_SpikeMaterial != null)
                    spikeRenderer.sharedMaterial = s_SpikeMaterial;

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
            // Damage callbacks can interrupt/cleanup an attack (for example when
            // the player dies or a stagger interrupts the scheduler). Iterate over
            // a stable snapshot so the visual impact pass cannot throw while the
            // live pool list is being released.
            var activeSpikes = m_Spikes.ToArray();
            foreach (var spike in activeSpikes)
            {
                if (spike == null)
                    continue;

                // The spike transform is the impact-point center after the rise.
                // Do not add half the visual spike height here: that inflated the
                // gameplay footprint and made Earth hit well outside its telegraph.
                Vector3 center = spike.transform.position;
                Collider[] hits = Physics.OverlapSphere(center, Mathf.Max(0f, DamageRadius), Physics.AllLayers,
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
                    ReleasePrimitive(m_SpikePool, spike);
            m_Spikes.Clear();
        }

        public override void Cleanup()
        {
            foreach (var decal in m_Telegraphs)
                if (decal != null)
                    ReleasePrimitive(m_TelegraphPool, decal);
            m_Telegraphs.Clear();
            m_ImpactPoints.Clear();

            DestroySpikes();
        }

        static Shader SpriteShader => s_SpriteShader != null
            ? s_SpriteShader
            : s_SpriteShader = Shader.Find("Sprites/Default");

        GameObject AcquirePrimitive(List<GameObject> pool, PrimitiveType type, string objectName)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var pooled = pool[i];
                if (pooled != null && !pooled.activeSelf)
                {
                    pooled.SetActive(true);
                    return pooled;
                }
            }

            var created = GameObject.CreatePrimitive(type);
            created.name = objectName;
            var collider = created.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            pool.Add(created);
            return created;
        }

        void ReleasePrimitive(List<GameObject> pool, GameObject item)
        {
            if (item == null)
                return;
            item.SetActive(false);
            if (!pool.Contains(item))
                pool.Add(item);
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
