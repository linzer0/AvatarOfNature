using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// Fire/Meteor Rain: telegraphed impacts near the player, falling rock cubes
    /// deal area damage on landing through the shared Microgame DamageArea.
    public class AvatarBossFireAttack : AvatarBossAttack
    {
        [Header("Meteor Rain")]
        [Tooltip("Number of telegraphed impacts (6-8 recommended)")]
        public int ImpactCount = 7;
        [Tooltip("Max distance of impact positions around the player")]
        public float ImpactSpread = 7f;
        [Tooltip("Seconds between impacts (staggered bomb layout)")]
        public float ImpactInterval = 0.35f;

        [Header("Rock visuals")]
        public float FallHeight = 10f;
        public float FallTime = 0.7f;
        public float RockSize = 0.9f;

        Transform m_Player;
        AvatarBossController m_Boss;
        readonly List<GameObject> m_Telegraphs = new List<GameObject>();
        readonly List<GameObject> m_Rocks = new List<GameObject>();

        static Material s_DecalMaterial;
        static Material s_RockMaterial;

        void Awake()
        {
            m_Boss = GetComponentInParent<AvatarBossController>();
        }

        protected override AvatarBossElement ExpectedElement => AvatarBossElement.Fire;

        public override void Prepare()
        {
            if (m_Player == null)
            {
                var player = FindFirstObjectByType<PlayerCharacterController>();
                m_Player = player != null ? player.transform : null;
            }

            if (m_Player == null)
            {
                Debug.LogWarning("[AvatarOfNature] No player found for Meteor telegraph.", this);
                return;
            }

            int count = Mathf.Clamp(ImpactCount, 6, 8);
            for (int i = 0; i < count; i++)
            {
                Vector2 rnd = Random.insideUnitCircle * ImpactSpread;
                Vector3 center = i == 0
                    ? m_Player.position
                    : m_Player.position + new Vector3(rnd.x, 0f, rnd.y);

                center = SnapToGround(center + Vector3.up * 20f);

                GameObject decal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(decal.GetComponent<Collider>());
                decal.name = "MeteorTelegraph";
                decal.transform.SetPositionAndRotation(center + Vector3.up * 0.06f, Quaternion.identity);
                decal.transform.localScale = new Vector3(2.4f, 0.02f, 2.4f);

                MeshRenderer decalRenderer = decal.GetComponent<MeshRenderer>();
                Shader textureShader = Shader.Find("Sprites/Default");
                if (s_DecalMaterial == null && textureShader != null)
                {
                    s_DecalMaterial = new Material(textureShader);
                    s_DecalMaterial.color = new Color(1f, 0.4f, 0.05f, 0.65f);
                }
                if (s_DecalMaterial != null)
                    decalRenderer.material = s_DecalMaterial;

                m_Telegraphs.Add(decal);
            }
        }

        public override IEnumerator Execute()
        {
            if (m_Telegraphs.Count == 0)
                yield break;

            for (int i = 0; i < m_Telegraphs.Count; i++)
            {
                if (m_Telegraphs[i] == null)
                    continue;

                StartCoroutine(FallRock(m_Telegraphs[i].transform.position));
                yield return new WaitForSeconds(ImpactInterval);
            }

            // wait for the last rock to land
            yield return new WaitForSeconds(FallTime);
            DestroyTelegraphs();
        }

        IEnumerator FallRock(Vector3 target)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "MeteorRock";
            rock.transform.position = target + Vector3.up * FallHeight;
            rock.transform.rotation = Random.rotation;
            rock.transform.localScale = Vector3.one * RockSize;

            MeshRenderer rockRenderer = rock.GetComponent<MeshRenderer>();
            Shader textureShader = Shader.Find("Sprites/Default");
            if (s_RockMaterial == null && textureShader != null)
            {
                s_RockMaterial = new Material(textureShader);
                s_RockMaterial.color = new Color(0.8f, 0.3f, 0.1f, 1f);
            }
            if (s_RockMaterial != null)
                rockRenderer.material = s_RockMaterial;

            m_Rocks.Add(rock);

            float t = 0f;
            Vector3 start = rock.transform.position;
            while (rock != null && t < FallTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / FallTime);
                rock.transform.position = Vector3.Lerp(start, target, k * k); // accelerating fall
                rock.transform.rotation = Random.rotation;
                yield return null;
            }

            if (rock == null)
                yield break;

            rock.transform.position = target;

            // boss itself must never be hurt by its own meteor: temporary invulnerability
            Health bossHealth = m_Boss != null ? m_Boss.BossHealth : null;
            bool wasInvincible = bossHealth != null && bossHealth.Invincible;

            DamageArea area = GetComponent<DamageArea>();
            if (area == null)
                area = gameObject.AddComponent<DamageArea>();
            if (area.DamageRatioOverDistance == null || area.DamageRatioOverDistance.length == 0)
                area.DamageRatioOverDistance = AnimationCurve.Linear(0f, 1f, 1f, 0.1f);
            area.AreaOfEffectDistance = 4f;

            if (bossHealth != null && !wasInvincible)
                bossHealth.Invincible = true;

            area.InflictDamageInArea(Damage, target, Physics.AllLayers,
                QueryTriggerInteraction.Ignore, m_Boss != null ? m_Boss.gameObject : gameObject);

            if (bossHealth != null && !wasInvincible)
                bossHealth.Invincible = false;

            m_Rocks.Remove(rock);
            Destroy(rock);
        }

        void DestroyTelegraphs()
        {
            foreach (var decal in m_Telegraphs)
                if (decal != null)
                    Destroy(decal);
            m_Telegraphs.Clear();
        }

        public override void Cleanup()
        {
            DestroyTelegraphs();

            foreach (var rock in m_Rocks)
                if (rock != null)
                    Destroy(rock);
            m_Rocks.Clear();
        }

        Vector3 SnapToGround(Vector3 from)
        {
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 80f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
                return hit.point;
            return from - Vector3.up * 20f;
        }
    }
}
