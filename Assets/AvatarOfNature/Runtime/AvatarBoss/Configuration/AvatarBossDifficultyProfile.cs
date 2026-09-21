using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// A named, editable balance profile stored as a sub-asset of the encounter config.
    /// Keeping profiles as assets makes each difficulty readable and independently tunable.
    /// </summary>
    public sealed class AvatarBossDifficultyProfile : ScriptableObject
    {
        [HideInInspector] public AvatarBossDifficulty Difficulty;

        [Header("Damage & survivability")]
        public float BodyDamageMultiplier;
        public float OpenBodyDamageMultiplier;
        public float BossHealthMultiplier;
        public float WeakPointDamageMultiplier;
        public float PlayerDamageMultiplier;
        [Range(0f, 1f)] public float MaxVulnerabilityDamagePercent;
        [Range(0f, 1f)] public float HighImpactWindowDamagePercent;
        [Min(0f)] public float HighImpactCooldown;
        [Min(0f)] public float StandardVulnerabilityCooldown;

        [Header("Attack pacing")]
        public float AttackCooldownMultiplier;
        public float TelegraphMultiplier;
        public float WindupMultiplier;
        [Tooltip("Difficulty-only stagger gain multiplier. Phase 2 tuning remains separate.")]
        public float DifficultyStaggerMultiplier;
        public float StaggerGainMultiplier;
        public float VulnerabilityDuration;
        public float ShockwaveSpeedMultiplier;

        [Header("Arena threats")]
        public float MeteorDamage;
        public int OrbCount;
        public float OrbHealth;
        public float OrbSpeed;
        [Range(0f, 1f)] public float HealPercent;
        public int SummonCount;
        public float SummonCooldownMultiplier;
        [Range(0f, 2f)] public float SummonHealthMultiplier = 1f;
        [Range(0f, 2f)] public float SummonDamageMultiplier = 1f;
        [Min(0f)] public float SummonAttackCooldownMultiplier = 1f;
        [Range(0f, 1f)] public float ComboFrequency;
        public float RecoveryFrequency;
        public float ComboCooldownMin;
        public float ComboCooldownMax;
        public float MeteorCooldownMin;
        public float MeteorCooldownMax;
        public int MaxConcurrentThreats;
        public int TargetCellCount;
        public bool MixedCombosEnabled;

        [Header("Phase two")]
        public float PhaseTwoTimingMultiplier;
        public float PhaseTwoTelegraphMultiplier;
        public float PhaseTwoStaggerGainMultiplier;
        public float PhaseTwoStaggerThresholdMultiplier;
        public float PhaseTwoStaggerDecayMultiplier;
        public float PhaseTwoStaggerDecayDelayMultiplier;
        public float PhaseTwoMinCooldown;
        public float PhaseTwoMinTelegraph;
        public float PhaseTwoMinWindup;
        public float PhaseTwoMinRecover;
        public float PhaseTwoMeteorDamage;
        public float PhaseTwoShockwaveSpeed;
        public float PhaseTwoVulnerabilityDuration;
        public bool PhaseTwoComboEnabled;
    }
}
