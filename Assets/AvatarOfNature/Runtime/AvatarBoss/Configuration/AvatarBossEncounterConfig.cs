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
    public sealed partial class AvatarBossEncounterConfig : ConfigContainer<AvatarBossDifficultyProfile>
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

    }
}
