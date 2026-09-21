using System;
using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Recovery-phase healing orb coordinator. The component owns orb lifetime,
    /// safe spawn validation, movement, damage routing and one-shot healing.
    /// Boss/controller/scheduler integration is intentionally kept outside this
    /// foundation so the recovery priority rules can be wired in one place.
    /// </summary>
    public class AvatarBossHealingOrbs : MonoBehaviour
    {
        [Header("References")]
        public AvatarBossController Boss;
        public Transform ArenaCenter;
        public Transform OrbTarget;
        public Material OrbMaterial;
        public Material TrailMaterial;

        [Header("Recovery")]
        [Min(1)] public int OrbCount = 3;
        [Min(1f)] public float OrbHealth = 30f;
        [Range(0f, 1f)] public float HealPercentOfMaxHealth = 0.08f;
        [Min(0f)] public float TimeBeforeLaunch = 1.5f;
        [Min(0.1f)] public float OrbSpeed = 4.5f;
        [Min(0.1f)] public float SpawnRingRadius = 13f;
        [Min(0f)] public float SpawnHeight = 1.4f;
        [Min(0.1f)] public float OrbRadius = 0.45f;
        [Range(1f, 2f)] public float VisualScaleMultiplier = 1.6f;
        public bool HealEnabledInPhaseTwo = true;
        [Min(0f)] public float CameraClearDistance = 2.5f;
        [Min(0f)] public float RecoveryCooldown = 40f;

        [Header("Safety")]
        public LayerMask BlockingLayers = Physics.DefaultRaycastLayers;
        [Min(0.1f)] public float SpawnSampleDistance = 2.5f;
        [Min(0.1f)] public float BossClearance = 2.2f;
        [Min(0.1f)] public float PlayerClearance = 1.6f;

        [Header("Debug colors")]
        public Color OrbColor = new Color(0.2f, 1f, 0.55f, 1f);
        public Color TrailColor = new Color(0.1f, 0.9f, 1f, 0.8f);

        public int ActiveOrbCount => m_Orbs.Count;
        public int SpawnedOrbCount { get; private set; }
        public int DestroyedOrbCount { get; private set; }
        public int ReachedBossCount { get; private set; }
        public float HealingReceived { get; private set; }
        public bool RecoveryActive { get; private set; }
        public float LastRecoveryStartedAt { get; private set; } = -999f;

        public event Action<int, int> OrbCountChanged;
        public event Action RecoveryStarted;
        public event Action<int> OrbDestroyed;
        public event Action<float> BossHealed;
        public event Action OrbReachedWithoutHealing;
        public event Action RecoveryFinished;

        readonly List<OrbState> m_Orbs = new List<OrbState>();
        Coroutine m_RecoveryRoutine;
        float m_NextRecoveryAllowedTime;
        int m_RecoveryToken;

        void Awake()
        {
            if (Boss == null)
                Boss = GetComponentInParent<AvatarBossController>();
            if (OrbTarget == null && Boss != null)
                OrbTarget = Boss.transform;
            if (ArenaCenter == null)
                ArenaCenter = transform;
        }

        void OnDestroy()
        {
            ClearHealingOrbs();
        }

        public bool CanStartRecovery()
        {
            return Boss != null
                && Boss.BossHealth != null
                && !Boss.IsDead
                && Boss.PhaseTwo
                && !Boss.SummonsActive
                && Time.time >= m_NextRecoveryAllowedTime;
        }

        /// <summary>Starts a recovery phase and spawns the configured orb count.</summary>
        public bool BeginRecovery()
        {
            if (!CanStartRecovery() || RecoveryActive)
                return false;

            m_NextRecoveryAllowedTime = Time.time + RecoveryCooldown;
            LastRecoveryStartedAt = Time.time;
            RecoveryActive = true;
            RecoveryStarted?.Invoke();
            SpawnHealingOrbs();
            m_RecoveryRoutine = StartCoroutine(RecoveryRoutine(++m_RecoveryToken));
            return true;
        }

        /// <summary>Debug/test-only alias for BeginRecovery.</summary>
        public bool ForceRecovery()
        {
            if (RecoveryActive)
                return false;
            m_NextRecoveryAllowedTime = 0f;
            return BeginRecovery();
        }

        /// <summary>Debug/test-only spawn hook. Does not heal and does not start the cooldown.</summary>
        public int SpawnHealingOrbs()
        {
            if (Boss == null)
                return 0;

            ClearOrbObjects();
            for (var i = 0; i < OrbCount; i++)
            {
                if (!TryGetSafeSpawnPosition(i, out var position))
                    continue;

                var orb = CreateOrb(position, i);
                m_Orbs.Add(orb);
                SpawnedOrbCount++;
            }

            OrbCountChanged?.Invoke(ActiveOrbCount, SpawnedOrbCount);
            return ActiveOrbCount;
        }

        /// <summary>Debug/test-only clear hook. Cleared orbs can never heal.</summary>
        public void ClearHealingOrbs()
        {
            if (m_RecoveryRoutine != null)
            {
                StopCoroutine(m_RecoveryRoutine);
                m_RecoveryRoutine = null;
            }

            ClearOrbObjects();
            RecoveryActive = false;
            OrbCountChanged?.Invoke(0, SpawnedOrbCount);
        }

        void ClearOrbObjects()
        {
            for (var i = m_Orbs.Count - 1; i >= 0; i--)
            {
                if (m_Orbs[i].Root != null)
                    Destroy(m_Orbs[i].Root);
            }

            m_Orbs.Clear();
        }

        IEnumerator RecoveryRoutine(int token)
        {
            yield return new WaitForSeconds(TimeBeforeLaunch);

            if (token != m_RecoveryToken || !RecoveryActive)
                yield break;

            var snapshot = new List<OrbState>(m_Orbs);
            foreach (var orb in snapshot)
            {
                if (orb.Root != null && !orb.Resolved)
                    orb.MoveRoutine = StartCoroutine(MoveOrbToBoss(orb, token));
            }

            while (RecoveryActive && token == m_RecoveryToken && m_Orbs.Count > 0)
                yield return null;

            FinishRecovery(token);
        }

        IEnumerator MoveOrbToBoss(OrbState orb, int token)
        {
            while (orb.Root != null && !orb.Resolved && RecoveryActive && token == m_RecoveryToken)
            {
                if (Boss == null || Boss.IsDead || OrbTarget == null)
                {
                    ResolveOrb(orb, false);
                    yield break;
                }

                var target = OrbTarget.position;
                orb.Root.transform.position = Vector3.MoveTowards(
                    orb.Root.transform.position, target, OrbSpeed * Time.deltaTime);
                UpdateTrail(orb);

                if (Vector3.Distance(orb.Root.transform.position, target) <= OrbRadius * VisualScaleMultiplier + 0.15f)
                {
                    ResolveOrb(orb, true);
                    yield break;
                }

                yield return null;
            }
        }

        OrbState CreateOrb(Vector3 position, int index)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = $"HealingOrb_{index + 1}";
            root.transform.position = position;
            root.transform.localScale = Vector3.one * (OrbRadius * 2f * VisualScaleMultiplier);

            var renderer = root.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = OrbMaterial != null
                    ? OrbMaterial
                    : CreateFallbackMaterial(OrbColor);
                var emission = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(emission);
                emission.SetColor("_EmissionColor", OrbColor * 2.5f);
                renderer.SetPropertyBlock(emission);
            }

            var collider = root.GetComponent<SphereCollider>();
            if (collider != null)
                collider.radius = 0.5f / VisualScaleMultiplier;

            var glow = root.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = OrbColor;
            glow.range = 2.5f;
            glow.intensity = 1.2f;

            var health = root.AddComponent<Health>();
            health.MaxHealth = OrbHealth;

            var damageable = root.AddComponent<Damageable>();
            damageable.DamageMultiplier = 1f;

            var trail = root.AddComponent<LineRenderer>();
            trail.positionCount = 2;
            trail.startWidth = 0.08f;
            trail.endWidth = 0.018f;
            trail.startColor = TrailColor;
            trail.endColor = new Color(TrailColor.r, TrailColor.g, TrailColor.b, 0f);
            trail.material = TrailMaterial != null
                ? TrailMaterial
                : CreateFallbackMaterial(TrailColor);

            var orb = new OrbState
            {
                Root = root,
                Health = health,
                Damageable = damageable,
                Trail = trail,
                Index = index
            };
            health.OnDie += () => { SpawnBurst(root.transform.position, new Color(0.4f, 1f, 0.7f), 18); ResolveOrb(orb, false); };
            damageable.OnDamageInflicted += (_, __) => { SpawnBurst(root.transform.position, Color.white, 6); OrbCountChanged?.Invoke(ActiveOrbCount, SpawnedOrbCount); };
            SpawnBurst(position, OrbColor, 10);
            UpdateTrail(orb);
            return orb;
        }

        void ResolveOrb(OrbState orb, bool reachedBoss)
        {
            if (orb == null || orb.Resolved)
                return;

            orb.Resolved = true;
            if (orb.MoveRoutine != null)
                StopCoroutine(orb.MoveRoutine);

            m_Orbs.Remove(orb);
            if (reachedBoss && Boss != null && Boss.BossHealth != null && !Boss.IsDead
                && Boss.PhaseTwo && HealEnabledInPhaseTwo)
            {
                var amount = Boss.BossHealth.MaxHealth * HealPercentOfMaxHealth;
                Boss.BossHealth.Heal(amount);
                HealingReceived += amount;
                ReachedBossCount++;
                BossHealed?.Invoke(amount);
            }
            else if (reachedBoss && Boss != null && Boss.PhaseTwo && !HealEnabledInPhaseTwo)
            {
                OrbReachedWithoutHealing?.Invoke();
                SpawnBurst(OrbTarget != null ? OrbTarget.position : transform.position, new Color(1f, 0.35f, 0.2f), 12);
            }
            else
            {
                DestroyedOrbCount++;
                OrbDestroyed?.Invoke(orb.Index);
            }

            if (orb.Root != null)
                Destroy(orb.Root);

            OrbCountChanged?.Invoke(ActiveOrbCount, SpawnedOrbCount);
            if (RecoveryActive && m_Orbs.Count == 0)
                FinishRecovery(m_RecoveryToken);
        }

        void FinishRecovery(int token)
        {
            if (!RecoveryActive || token != m_RecoveryToken)
                return;

            RecoveryActive = false;
            if (m_RecoveryRoutine != null)
            {
                StopCoroutine(m_RecoveryRoutine);
                m_RecoveryRoutine = null;
            }

            ClearHealingOrbs();
            RecoveryFinished?.Invoke();
        }

        bool TryGetSafeSpawnPosition(int index, out Vector3 result)
        {
            var center = ArenaCenter != null ? ArenaCenter.position : transform.position;
            var player = FindObjectOfType<PlayerCharacterController>();
            var camera = Camera.main;

            for (var attempt = 0; attempt < 12; attempt++)
            {
                var angle = ((index + attempt * 0.37f) / Mathf.Max(1, OrbCount)) * Mathf.PI * 2f;
                var candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SpawnRingRadius;
                candidate.y += SpawnHeight;

                if (!NavMesh.SamplePosition(candidate, out var hit, SpawnSampleDistance, NavMesh.AllAreas))
                    continue;
                var position = hit.position + Vector3.up * SpawnHeight;

                if (Boss != null && Vector3.Distance(position, Boss.transform.position) < BossClearance)
                    continue;
                if (player != null && Vector3.Distance(position, player.transform.position) < PlayerClearance)
                    continue;
                if (camera != null && Vector3.Distance(position, camera.transform.position) < CameraClearDistance)
                    continue;
                if (Physics.CheckSphere(position, OrbRadius, BlockingLayers, QueryTriggerInteraction.Ignore))
                    continue;

                result = position;
                return true;
            }

            result = default;
            return false;
        }

        void UpdateTrail(OrbState orb)
        {
            if (orb?.Trail == null || OrbTarget == null || orb.Root == null)
                return;
            orb.Trail.SetPosition(0, orb.Root.transform.position);
            orb.Trail.SetPosition(1, OrbTarget.position);
        }

        void SpawnBurst(Vector3 position, Color color, int count)
        {
            var go = new GameObject("HealingOrbFeedback");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.35f;
            main.startLifetime = 0.45f;
            main.startSpeed = 2.2f;
            main.startSize = 0.08f;
            main.startColor = color;
            main.maxParticles = count;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;
            ps.Play();
            Destroy(go, 1.1f);
        }

        static Material CreateFallbackMaterial(Color color)
        {
            var shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            var material = new Material(shader) { color = color };
            return material;
        }

        sealed class OrbState
        {
            public int Index;
            public GameObject Root;
            public Health Health;
            public Damageable Damageable;
            public LineRenderer Trail;
            public Coroutine MoveRoutine;
            public bool Resolved;
        }
    }
}
