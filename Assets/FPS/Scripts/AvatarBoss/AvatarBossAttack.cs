using System.Collections;
using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    public enum AvatarBossElement
    {
        Earth,
        Wind,
        Fire,
        Shockwave,
    }

    /// Base class for boss attacks. Timing is owned by AvatarBossAttackScheduler;
    /// an attack only prepares telegraphs, executes damage and cleans itself up.
    public abstract class AvatarBossAttack : MonoBehaviour
    {
        [Header("Attack")]
        public AvatarBossElement Element = AvatarBossElement.Earth;
        [Tooltip("Seconds the telegraph is visible before the attack lands")]
        public float TelegraphTime = 2f;

        [Tooltip("Base damage applied by this attack")]
        public float Damage = 25f;

        [Tooltip("Seconds before the next attack cycle may start")]
        public float Cooldown = 6f;

        [Header("Impact VFX (reuses existing FPS prefabs, optional)")]
        [Tooltip("Prefab instantiated at every impact point; wired per attack in the scene")]
        public GameObject ImpactEffectPrefab;

        protected virtual AvatarBossElement ExpectedElement => Element;

        protected AvatarBossIntent CurrentIntent { get; private set; }
        protected bool HasIntent { get; private set; }
        readonly List<int> m_ArenaTargetSectors = new List<int>();

        /// <summary>Cells visually announced and resolved by this execution.</summary>
        public IReadOnlyList<int> ArenaTargetSectors => m_ArenaTargetSectors;

        /// <summary>Validation guard: designer data must match the component's expected element.
        /// Logs a clear error but never repairs data silently.</summary>
        protected virtual void Start()
        {
            if (Element != ExpectedElement)
                Debug.LogError(
                    $"[AvatarOfNature] {GetType().Name} has serialized Element '{Element}' but expects '{ExpectedElement}'. " +
                    "Fix the scene asset in the inspector; values are not repaired at runtime.", this);
        }

        /// <summary>Spawns the optional ImpactEffectPrefab at a position with a short lifetime.</summary>
        public void SpawnImpactEffect(Vector3 position)
        {
            if (ImpactEffectPrefab == null)
                return;

            var fx = Instantiate(ImpactEffectPrefab, position, Quaternion.identity);
            var timed = fx.GetComponent<TimedSelfDestruct>();
            if (timed == null)
                timed = fx.AddComponent<TimedSelfDestruct>();
            timed.LifeTime = 2.5f;
        }

        /// <summary>Provides the chosen boss intent to attacks that need directional telegraphing.</summary>
        public virtual void SetIntent(AvatarBossIntent intent)
        {
            CurrentIntent = intent;
            HasIntent = true;
        }

        protected void SetArenaTargetSectors(IEnumerable<int> sectors)
        {
            m_ArenaTargetSectors.Clear();
            if (sectors == null)
                return;
            foreach (int sector in sectors)
                if (!m_ArenaTargetSectors.Contains(sector))
                    m_ArenaTargetSectors.Add(sector);
        }

        /// <summary>Spawn all telegraph visuals. Called when the cycle enters the Telegraph state.</summary>
        public abstract void Prepare();

        /// <summary>Run the damage part of the attack as a coroutine.</summary>
        public abstract IEnumerator Execute();

        /// <summary>Kill all telegraphs and in-flight visuals (called on interrupt and cycle end).</summary>
        public abstract void Cleanup();
    }
}
