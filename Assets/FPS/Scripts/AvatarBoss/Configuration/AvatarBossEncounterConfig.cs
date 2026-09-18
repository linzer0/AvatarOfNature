using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Reviewer-facing source of truth for boss balance. The component fallback
    /// remains only to keep older scenes playable until they are re-saved.
    /// </summary>
    [CreateAssetMenu(menuName = "Avatar Boss/Encounter Config", fileName = "AvatarBossEncounterConfig")]
    public sealed class AvatarBossEncounterConfig : ScriptableObject
    {
        [Header("Difficulty profiles")]
        public AvatarBossDifficultyProfile Easy = CreateEasy();
        public AvatarBossDifficultyProfile Normal = CreateNormal();
        public AvatarBossDifficultyProfile Hard = CreateHard();

        [Header("Base encounter timings")]
        public float AttackCooldown = 3f;
        public float InitialGraceTime = 4f;
        public float RecoverTime = 1.5f;
        public float WindupTime = 0.9f;
        public float RecoveryCooldown = 40f;
        public float SummonCooldown = 20f;

        public AvatarBossDifficultyProfile GetProfile(AvatarBossDifficulty difficulty)
        {
            return difficulty == AvatarBossDifficulty.Easy ? Easy :
                difficulty == AvatarBossDifficulty.Hard ? Hard : Normal;
        }

        static AvatarBossDifficultyProfile CreateEasy()
        {
            return new AvatarBossDifficultyProfile
            {
                Difficulty = AvatarBossDifficulty.Easy, BodyDamageMultiplier = 0.15f, PlayerDamageMultiplier = 0.75f,
                AttackCooldownMultiplier = 1.2f, TelegraphMultiplier = 1.25f, WindupMultiplier = 1.15f,
                DifficultyStaggerMultiplier = 1.15f, StaggerGainMultiplier = 1.15f, VulnerabilityDuration = 4.2f,
                ShockwaveSpeedMultiplier = 0.85f, MeteorDamage = 15f, OrbCount = 2, OrbHealth = 24f, OrbSpeed = 3.5f,
                HealPercent = 0.06f, SummonCount = 2, SummonCooldownMultiplier = 1.2f, ComboFrequency = 0.55f,
                RecoveryFrequency = 1.2f, ComboCooldownMin = 30f, ComboCooldownMax = 38f, MeteorCooldownMin = 30f,
                MeteorCooldownMax = 38f, MaxConcurrentThreats = 1
            };
        }

        static AvatarBossDifficultyProfile CreateNormal()
        {
            return new AvatarBossDifficultyProfile
            {
                Difficulty = AvatarBossDifficulty.Normal, BodyDamageMultiplier = 0.12f, PlayerDamageMultiplier = 1f,
                AttackCooldownMultiplier = 1f, TelegraphMultiplier = 1f, WindupMultiplier = 1f,
                DifficultyStaggerMultiplier = 1f, StaggerGainMultiplier = 1f, VulnerabilityDuration = 3.5f,
                ShockwaveSpeedMultiplier = 1f, MeteorDamage = 20f, OrbCount = 3, OrbHealth = 30f, OrbSpeed = 4.5f,
                HealPercent = 0.08f, SummonCount = 3, SummonCooldownMultiplier = 1f, ComboFrequency = 0.8f,
                RecoveryFrequency = 1f, ComboCooldownMin = 25f, ComboCooldownMax = 35f, MeteorCooldownMin = 25f,
                MeteorCooldownMax = 35f, MaxConcurrentThreats = 1
            };
        }

        static AvatarBossDifficultyProfile CreateHard()
        {
            return new AvatarBossDifficultyProfile
            {
                Difficulty = AvatarBossDifficulty.Hard, BodyDamageMultiplier = 0.08f, PlayerDamageMultiplier = 1.2f,
                AttackCooldownMultiplier = 0.85f, TelegraphMultiplier = 0.85f, WindupMultiplier = 0.85f,
                DifficultyStaggerMultiplier = 0.85f, StaggerGainMultiplier = 0.9f, VulnerabilityDuration = 3f,
                ShockwaveSpeedMultiplier = 1.1f, MeteorDamage = 24f, OrbCount = 4, OrbHealth = 42f, OrbSpeed = 5.75f,
                HealPercent = 0.10f, SummonCount = 3, SummonCooldownMultiplier = 0.85f, ComboFrequency = 1f,
                RecoveryFrequency = 0.85f, ComboCooldownMin = 18f, ComboCooldownMax = 25f, MeteorCooldownMin = 18f,
                MeteorCooldownMax = 25f, MaxConcurrentThreats = 1
            };
        }
    }
}
