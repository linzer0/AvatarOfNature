using System.Collections;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Short prototype of the boss's personal-space response.
    /// It gives the player a readable chance to leave point-blank range, then
    /// pushes them away instead of silently forbidding close combat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AvatarBossProximityThreat : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SubscribeToSceneLoads()
        {
            SceneManager.sceneLoaded -= InstallOnLoadedBosses;
            SceneManager.sceneLoaded += InstallOnLoadedBosses;
        }

        static void InstallOnLoadedBosses(Scene scene, LoadSceneMode mode)
        {
            var bosses = Object.FindObjectsByType<AvatarBossController>(FindObjectsSortMode.None);
            foreach (var boss in bosses)
                if (boss != null && boss.GetComponent<AvatarBossProximityThreat>() == null)
                    boss.gameObject.AddComponent<AvatarBossProximityThreat>();
        }

        [Header("Personal space")]
        [Min(0.5f)] public float TriggerRadius = 3.5f;
        [Min(0.5f)] public float ResetRadius = 4.25f;
        [Min(0f)] public float GraceTime = 0.45f;
        [Min(0f)] public float TelegraphTime = 0.7f;
        [Min(0f)] public float Cooldown = 4f;

        [Header("Head landing")]
        [Tooltip("Horizontal radius around the boss where landing on the head is rejected.")]
        [Min(0.5f)] public float HeadTriggerRadius = 2.4f;
        [Tooltip("Height above the boss root that counts as standing on the head.")]
        [Min(0.5f)] public float HeadTriggerHeight = 2.2f;

        [Header("Impact")]
        [Min(0.1f)] public float ImpactRadius = 2.7f;
        [Min(0f)] public float Damage = 20f;
        [Min(0f)] public float PushForce = 34f;
        [Min(0f)] public float PushLift = 0.8f;

        [Header("Prototype feedback")]
        public Color TelegraphColor = new Color(1f, 0.24f, 0.06f, 0.8f);
        public bool EnableDiagnostics = true;

        AvatarBossController m_Boss;
        AvatarBossCombatContext m_Context;
        PlayerCharacterController m_Player;
        GameObject m_Telegraph;
        Coroutine m_ThreatRoutine;
        float m_InsideTime;
        float m_CooldownRemaining;
        bool m_Armed = true;
        static Material s_TelegraphMaterial;

        void Awake()
        {
            m_Boss = GetComponent<AvatarBossController>();
            if (m_Boss == null)
                m_Boss = GetComponentInParent<AvatarBossController>();
            m_Context = m_Boss != null ? m_Boss.GetCombatContext() : null;
        }

        void Update()
        {
            if (m_Boss == null || m_Boss.IsDead)
                return;

            if (m_Context != null)
            {
                m_Context.ResolveSceneReferences();
                m_Player = m_Context.Player;
            }
            if (m_Player == null)
                return;

            float distance = HorizontalDistance(transform.position, m_Player.transform.position);
            bool inHeadZone = IsPlayerInHeadZone(distance);
            if (distance >= ResetRadius)
            {
                m_Armed = true;
                m_InsideTime = 0f;
            }

            m_CooldownRemaining = Mathf.Max(0f, m_CooldownRemaining - Time.deltaTime);
            // A player standing on the head cannot satisfy the normal horizontal
            // reset distance. Re-arm after cooldown so the boss keeps rejecting
            // the exploit instead of allowing one burst and then going quiet.
            if (inHeadZone && m_CooldownRemaining <= 0f)
                m_Armed = true;
            if (!m_Armed || m_CooldownRemaining > 0f || IsDuelWindowActive())
                return;

            if (distance <= TriggerRadius || inHeadZone)
                m_InsideTime += Time.deltaTime;
            else
                m_InsideTime = 0f;

            if (m_InsideTime >= GraceTime)
            {
                m_Armed = false;
                m_InsideTime = 0f;
                m_ThreatRoutine = StartCoroutine(PersonalSpaceBurst());
            }
        }

        IEnumerator PersonalSpaceBurst()
        {
            CreateTelegraph();
            float elapsed = 0f;
            while (elapsed < TelegraphTime)
            {
                float distance = m_Player != null
                    ? HorizontalDistance(transform.position, m_Player.transform.position)
                    : float.MaxValue;
                if (m_Player == null || (distance > TriggerRadius && !IsPlayerInHeadZone(distance)))
                {
                    CleanupTelegraph();
                    m_CooldownRemaining = 0.75f;
                    Log("CANCEL reason=player_left_personal_space");
                    m_ThreatRoutine = null;
                    yield break;
                }

                elapsed += Time.deltaTime;
                UpdateTelegraph(elapsed / Mathf.Max(0.01f, TelegraphTime));
                yield return null;
            }

            CleanupTelegraph();
            if (m_Player != null)
            {
                Vector3 fromBoss = m_Player.transform.position - transform.position;
                bool wasOnHead = IsPlayerInHeadZone(HorizontalDistance(transform.position, m_Player.transform.position));
                Vector3 pushDirection = fromBoss;
                if (pushDirection.sqrMagnitude < 0.001f)
                    pushDirection = transform.forward;
                if (wasOnHead)
                    pushDirection.y = Mathf.Max(pushDirection.y, 0.45f);
                else
                    pushDirection.y = 0f;
                pushDirection.Normalize();

                float distance = HorizontalDistance(transform.position, m_Player.transform.position);
                if (distance <= ImpactRadius || wasOnHead)
                {
                    m_Player.ApplyExternalImpulse(pushDirection * PushForce + Vector3.up * PushLift);
                    Health playerHealth = m_Player.GetComponent<Health>();
                    playerHealth?.TakeDamage(Damage, gameObject);
                    EventManager.Broadcast(new CameraImpulseEvent
                    {
                        Strength = 0.38f,
                        Duration = 0.22f,
                        Direction = pushDirection
                    });
                    Log($"IMPACT distance={distance:F2} damage={Damage:F1} push={PushForce:F1}");
                }
            }

            m_CooldownRemaining = Cooldown;
            m_ThreatRoutine = null;
        }

        bool IsDuelWindowActive()
        {
            var duel = m_Boss != null ? m_Boss.GetComponent<AvatarBossDuelController>() : null;
            return duel != null && duel.DuelWindowActive;
        }

        bool IsPlayerInHeadZone(float horizontalDistance)
        {
            if (m_Player == null || horizontalDistance > HeadTriggerRadius)
                return false;
            return m_Player.transform.position.y - transform.position.y >= HeadTriggerHeight;
        }

        void CreateTelegraph()
        {
            m_Telegraph = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            m_Telegraph.name = "BossPersonalSpaceTelegraph";
            Destroy(m_Telegraph.GetComponent<Collider>());
            if (s_TelegraphMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                    s_TelegraphMaterial = new Material(shader);
            }
            if (s_TelegraphMaterial != null)
                s_TelegraphMaterial.color = TelegraphColor;
            var renderer = m_Telegraph.GetComponent<Renderer>();
            if (renderer != null && s_TelegraphMaterial != null)
                renderer.sharedMaterial = s_TelegraphMaterial;
            UpdateTelegraph(0f);
        }

        void UpdateTelegraph(float progress)
        {
            if (m_Telegraph == null)
                return;
            m_Telegraph.transform.position = transform.position + Vector3.up * 0.06f;
            float radius = Mathf.Lerp(0.4f, ImpactRadius, Mathf.Clamp01(progress));
            m_Telegraph.transform.localScale = new Vector3(radius * 2f, 0.025f, radius * 2f);
        }

        void CleanupTelegraph()
        {
            if (m_Telegraph != null)
                Destroy(m_Telegraph);
            m_Telegraph = null;
        }

        void OnDestroy()
        {
            if (m_ThreatRoutine != null)
                StopCoroutine(m_ThreatRoutine);
            CleanupTelegraph();
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void Log(string message)
        {
            if (EnableDiagnostics)
                Debug.Log($"[AvatarOfNature] PROXIMITY {message}", this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.05f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, TriggerRadius);
            Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, ImpactRadius);
        }
    }
}
