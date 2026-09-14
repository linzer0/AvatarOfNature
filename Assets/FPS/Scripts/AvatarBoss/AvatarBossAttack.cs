using System.Collections;
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

        /// <summary>Element this component type is designed to use. Overridden per concrete attack.</summary>
        protected virtual AvatarBossElement ExpectedElement => Element;

        /// <summary>Validation guard: designer data must match the component's expected element.
        /// Logs a clear error but never repairs data silently.</summary>
        protected virtual void Start()
        {
            if (Element != ExpectedElement)
                Debug.LogError(
                    $"[AvatarOfNature] {GetType().Name} has serialized Element '{Element}' but expects '{ExpectedElement}'. " +
                    "Fix the scene asset in the inspector; values are not repaired at runtime.", this);
        }

        /// <summary>Spawn all telegraph visuals. Called when the cycle enters the Telegraph state.</summary>
        public abstract void Prepare();

        /// <summary>Run the damage part of the attack as a coroutine.</summary>
        public abstract IEnumerator Execute();

        /// <summary>Kill all telegraphs and in-flight visuals (called on interrupt and cycle end).</summary>
        public abstract void Cleanup();
    }
}
