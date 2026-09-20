using UnityEngine;

namespace Unity.FPS.Game
{
    public class Damageable : MonoBehaviour
    {
        [Tooltip("Multiplier to apply to the received damage")]
        public float DamageMultiplier = 1f;

        [Tooltip("When several Damageable colliders overlap, higher priority wins within the same hierarchy root.")]
        public int HitPriority;

        [Range(0, 1)] [Tooltip("Multiplier to apply to self damage")]
        public float SensibilityToSelfdamage = 0.5f;

        /// <summary>Optional diagnostic/feedback hook: fires after damage is applied, with the final damage and the part hit.</summary>
        public System.Action<float, Damageable> OnDamageInflicted;

        public Health Health { get; private set; }

        void Awake()
        {
            // find the health component either at the same level, or higher in the hierarchy
            Health = GetComponent<Health>();
            if (!Health)
            {
                Health = GetComponentInParent<Health>();
            }
        }

        public void InflictDamage(float damage, bool isExplosionDamage, GameObject damageSource)
        {
            if (Health)
            {
                var totalDamage = damage;

                // skip the crit multiplier if it's from an explosion
                if (!isExplosionDamage)
                {
                    totalDamage *= DamageMultiplier;
                }

                // potentially reduce damages if inflicted by self
                if (Health.gameObject == damageSource)
                {
                    totalDamage *= SensibilityToSelfdamage;
                }

                // apply the damages
                Health.TakeDamage(totalDamage, damageSource);

                OnDamageInflicted?.Invoke(totalDamage, this);
            }
        }
    }
}
