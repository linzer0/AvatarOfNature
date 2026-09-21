using UnityEngine;
using Unity.FPS.AvatarBoss.Configs;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Reviewer-facing source of truth for boss balance. The component fallback
    /// remains only to keep older scenes playable until they are re-saved.
    /// </summary>
    [CreateAssetMenu(menuName = "Avatar Boss/Encounter Config", fileName = "AvatarBossEncounterConfig")]
    [ConfigContainer(GroupByMember = "Difficulty", AllowDeletion = true, AllowReorder = true, WithCustomName = true)]
    public sealed class AvatarBossEncounterConfig : ConfigContainer<AvatarBossDifficultyProfile>
    {
        public AvatarBossDifficultyProfile Easy => FindProfile(AvatarBossDifficulty.Easy);
        public AvatarBossDifficultyProfile Normal => FindProfile(AvatarBossDifficulty.Normal);
        public AvatarBossDifficultyProfile Hard => FindProfile(AvatarBossDifficulty.Hard);

        [Header("Base encounter timings")]
        public float AttackCooldown = 3f;
        public float InitialGraceTime = 4f;
        public float RecoverTime = 1.5f;
        public float WindupTime = 0.9f;
        public float RecoveryCooldown = 40f;
        public float SummonCooldown = 20f;

        public AvatarBossDifficultyProfile GetProfile(AvatarBossDifficulty difficulty)
        {
            var profile = FindProfile(difficulty);
            if (profile != null)
                return profile;

            return difficulty == AvatarBossDifficulty.Easy ? CreateEasy() :
                difficulty == AvatarBossDifficulty.Hard ? CreateHard() : CreateNormal();
        }

        AvatarBossDifficultyProfile FindProfile(AvatarBossDifficulty difficulty)
        {
            if (Elements == null)
                return null;
            foreach (var profile in Elements)
                if (profile != null && profile.Difficulty == difficulty)
                    return profile;
            return null;
        }

        public static AvatarBossDifficultyProfile CreateEasy()
        {
            var profile = CreateProfile(AvatarBossDifficulty.Easy);
            profile.BodyDamageMultiplier = 0.25f; profile.OpenBodyDamageMultiplier = 1f; profile.BossHealthMultiplier = 1f; profile.WeakPointDamageMultiplier = 1.2f; profile.PlayerDamageMultiplier = 0.75f; profile.MaxVulnerabilityDamagePercent = 0.30f; profile.HighImpactWindowDamagePercent = 0.38f; profile.HighImpactCooldown = 30f; profile.StandardVulnerabilityCooldown = 15f;
            profile.AttackCooldownMultiplier = 1.2f; profile.TelegraphMultiplier = 1.25f; profile.WindupMultiplier = 1.15f;
            profile.DifficultyStaggerMultiplier = 1.15f; profile.StaggerGainMultiplier = 1.15f; profile.VulnerabilityDuration = 4.2f;
            profile.ShockwaveSpeedMultiplier = 0.85f; profile.MeteorDamage = 15f; profile.OrbCount = 2; profile.OrbHealth = 24f; profile.OrbSpeed = 3.5f;
            profile.HealPercent = 0.06f; profile.SummonCount = 2; profile.SummonCooldownMultiplier = 1.2f; profile.SummonHealthMultiplier = 0.6f; profile.SummonDamageMultiplier = 0.45f; profile.SummonAttackCooldownMultiplier = 1.5f; profile.ComboFrequency = 0.55f;
            profile.RecoveryFrequency = 1.2f; profile.ComboCooldownMin = 30f; profile.ComboCooldownMax = 38f; profile.MeteorCooldownMin = 30f;
            profile.MeteorCooldownMax = 38f; profile.MaxConcurrentThreats = 1; profile.TargetCellCount = 1; profile.MixedCombosEnabled = false;
            profile.PhaseTwoTimingMultiplier = 0.82f; profile.PhaseTwoTelegraphMultiplier = 0.9f; profile.PhaseTwoStaggerGainMultiplier = 0.75f;
            profile.PhaseTwoStaggerThresholdMultiplier = 0.7f; profile.PhaseTwoStaggerDecayMultiplier = 1.1f; profile.PhaseTwoStaggerDecayDelayMultiplier = 0.9f;
            profile.PhaseTwoMinCooldown = 2.4f; profile.PhaseTwoMinTelegraph = 1.4f; profile.PhaseTwoMinWindup = 0.75f; profile.PhaseTwoMinRecover = 1.3f;
            profile.PhaseTwoMeteorDamage = 18f; profile.PhaseTwoShockwaveSpeed = 20f; profile.PhaseTwoVulnerabilityDuration = 3f; profile.PhaseTwoComboEnabled = false;
            return profile;
        }

        public static AvatarBossDifficultyProfile CreateNormal()
        {
            var profile = CreateProfile(AvatarBossDifficulty.Normal);
            profile.BodyDamageMultiplier = 0.04f; profile.OpenBodyDamageMultiplier = 0.8f; profile.BossHealthMultiplier = 1.2f; profile.WeakPointDamageMultiplier = 0.5f; profile.PlayerDamageMultiplier = 1f; profile.MaxVulnerabilityDamagePercent = 0.08f; profile.HighImpactWindowDamagePercent = 0.18f; profile.HighImpactCooldown = 45f; profile.StandardVulnerabilityCooldown = 25f;
            profile.AttackCooldownMultiplier = 1f; profile.TelegraphMultiplier = 1f; profile.WindupMultiplier = 1f; profile.DifficultyStaggerMultiplier = 1f; profile.StaggerGainMultiplier = 1f; profile.VulnerabilityDuration = 3.5f;
            profile.ShockwaveSpeedMultiplier = 1f; profile.MeteorDamage = 20f; profile.OrbCount = 3; profile.OrbHealth = 30f; profile.OrbSpeed = 4.5f; profile.HealPercent = 0.08f;
            profile.SummonCount = 3; profile.SummonCooldownMultiplier = 1f; profile.SummonHealthMultiplier = 0.5f; profile.SummonDamageMultiplier = 0.25f; profile.SummonAttackCooldownMultiplier = 2f; profile.ComboFrequency = 0.8f; profile.RecoveryFrequency = 1f; profile.ComboCooldownMin = 25f; profile.ComboCooldownMax = 35f;
            profile.MeteorCooldownMin = 25f; profile.MeteorCooldownMax = 35f; profile.MaxConcurrentThreats = 1; profile.TargetCellCount = 2; profile.MixedCombosEnabled = false;
            profile.PhaseTwoTimingMultiplier = 0.77f; profile.PhaseTwoTelegraphMultiplier = 0.8f; profile.PhaseTwoStaggerGainMultiplier = 0.75f; profile.PhaseTwoStaggerThresholdMultiplier = 0.6f;
            profile.PhaseTwoStaggerDecayMultiplier = 1.25f; profile.PhaseTwoStaggerDecayDelayMultiplier = 0.8f; profile.PhaseTwoMinCooldown = 2.1f; profile.PhaseTwoMinTelegraph = 1.3f;
            profile.PhaseTwoMinWindup = 0.7f; profile.PhaseTwoMinRecover = 1.2f; profile.PhaseTwoMeteorDamage = 22f; profile.PhaseTwoShockwaveSpeed = 24f; profile.PhaseTwoVulnerabilityDuration = 2.5f; profile.PhaseTwoComboEnabled = true;
            return profile;
        }

        public static AvatarBossDifficultyProfile CreateHard()
        {
            var profile = CreateProfile(AvatarBossDifficulty.Hard);
            profile.BodyDamageMultiplier = 0.06f; profile.OpenBodyDamageMultiplier = 0.45f; profile.BossHealthMultiplier = 1.6f; profile.WeakPointDamageMultiplier = 0.45f; profile.PlayerDamageMultiplier = 1f; profile.MaxVulnerabilityDamagePercent = 0.15f; profile.HighImpactWindowDamagePercent = 0.24f; profile.HighImpactCooldown = 45f; profile.StandardVulnerabilityCooldown = 25f;
            profile.AttackCooldownMultiplier = 0.85f; profile.TelegraphMultiplier = 0.85f; profile.WindupMultiplier = 0.85f; profile.DifficultyStaggerMultiplier = 0.85f; profile.StaggerGainMultiplier = 0.9f; profile.VulnerabilityDuration = 2.5f;
            profile.ShockwaveSpeedMultiplier = 1.1f; profile.MeteorDamage = 24f; profile.OrbCount = 4; profile.OrbHealth = 42f; profile.OrbSpeed = 5.75f; profile.HealPercent = 0.10f;
            profile.SummonCount = 3; profile.SummonCooldownMultiplier = 0.85f; profile.SummonHealthMultiplier = 0.75f; profile.SummonDamageMultiplier = 0.6f; profile.SummonAttackCooldownMultiplier = 1.25f; profile.ComboFrequency = 1f; profile.RecoveryFrequency = 0.85f; profile.ComboCooldownMin = 18f; profile.ComboCooldownMax = 25f;
            profile.MeteorCooldownMin = 18f; profile.MeteorCooldownMax = 25f; profile.MaxConcurrentThreats = 1; profile.TargetCellCount = 3; profile.MixedCombosEnabled = true;
            profile.PhaseTwoTimingMultiplier = 0.7f; profile.PhaseTwoTelegraphMultiplier = 0.75f; profile.PhaseTwoStaggerGainMultiplier = 0.65f; profile.PhaseTwoStaggerThresholdMultiplier = 0.55f;
            profile.PhaseTwoStaggerDecayMultiplier = 1.35f; profile.PhaseTwoStaggerDecayDelayMultiplier = 0.7f; profile.PhaseTwoMinCooldown = 1.9f; profile.PhaseTwoMinTelegraph = 1.15f;
            profile.PhaseTwoMinWindup = 0.6f; profile.PhaseTwoMinRecover = 1.05f; profile.PhaseTwoMeteorDamage = 28f; profile.PhaseTwoShockwaveSpeed = 27f; profile.PhaseTwoVulnerabilityDuration = 2.1f; profile.PhaseTwoComboEnabled = true;
            return profile;
        }

        static AvatarBossDifficultyProfile CreateProfile(AvatarBossDifficulty difficulty)
        {
            var profile = ScriptableObject.CreateInstance<AvatarBossDifficultyProfile>();
            profile.Difficulty = difficulty;
            return profile;
        }
    }
}
