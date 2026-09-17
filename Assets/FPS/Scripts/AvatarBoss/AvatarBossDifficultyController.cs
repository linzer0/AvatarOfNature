using UnityEngine;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;

namespace Unity.FPS.AvatarBoss
{
    public enum AvatarBossDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    [System.Serializable]
    public struct AvatarBossDifficultyProfile
    {
        public AvatarBossDifficulty Difficulty;
        public float BodyDamageMultiplier;
        public float PlayerDamageMultiplier;
        public float AttackCooldownMultiplier;
        public float TelegraphMultiplier;
        public float WindupMultiplier;
        [Tooltip("Difficulty-only stagger gain multiplier. Phase 2 tuning remains separate.")]
        public float DifficultyStaggerMultiplier;
        public float StaggerGainMultiplier;
        public float VulnerabilityDuration;
        public float ShockwaveSpeedMultiplier;
        public float MeteorDamage;
        public int OrbCount;
        public float OrbHealth;
        public float OrbSpeed;
        public float HealPercent;
        public int SummonCount;
        public float SummonCooldownMultiplier;
        public float ComboFrequency;
        public float RecoveryFrequency;
        public float ComboCooldownMin;
        public float ComboCooldownMax;
        public float MeteorCooldownMin;
        public float MeteorCooldownMax;
        public int MaxConcurrentThreats;
    }

    public sealed class AvatarBossDifficultyController : MonoBehaviour
    {
        [Header("Selection")]
        public AvatarBossDifficulty DefaultDifficulty = AvatarBossDifficulty.Normal;
        public AvatarBossDifficulty CurrentDifficulty { get; private set; }
        public AvatarBossDifficultyProfile ActiveProfile { get; private set; }

        [Header("Profiles")]
        public AvatarBossDifficultyProfile Easy = new AvatarBossDifficultyProfile
        {
            Difficulty = AvatarBossDifficulty.Easy, BodyDamageMultiplier = 0.15f, PlayerDamageMultiplier = 0.75f,
            AttackCooldownMultiplier = 1.2f, TelegraphMultiplier = 1.25f, WindupMultiplier = 1.15f,
            DifficultyStaggerMultiplier = 1.15f,
            StaggerGainMultiplier = 1.15f, VulnerabilityDuration = 4.2f, ShockwaveSpeedMultiplier = 0.85f,
            MeteorDamage = 15f, OrbCount = 2, OrbHealth = 24f, OrbSpeed = 3.5f, HealPercent = 0.06f,
            SummonCount = 2, SummonCooldownMultiplier = 1.2f, ComboFrequency = 0.55f, RecoveryFrequency = 1.2f, ComboCooldownMin = 30f, ComboCooldownMax = 38f,
            MeteorCooldownMin = 30f, MeteorCooldownMax = 38f, MaxConcurrentThreats = 1
        };
        public AvatarBossDifficultyProfile Normal = new AvatarBossDifficultyProfile
        {
            Difficulty = AvatarBossDifficulty.Normal, BodyDamageMultiplier = 0.12f, PlayerDamageMultiplier = 1f,
            AttackCooldownMultiplier = 1f, TelegraphMultiplier = 1f, WindupMultiplier = 1f,
            DifficultyStaggerMultiplier = 1f,
            StaggerGainMultiplier = 1f, VulnerabilityDuration = 3.5f, ShockwaveSpeedMultiplier = 1f,
            MeteorDamage = 20f, OrbCount = 3, OrbHealth = 30f, OrbSpeed = 4.5f, HealPercent = 0.08f,
            SummonCount = 3, SummonCooldownMultiplier = 1f, ComboFrequency = 0.8f, RecoveryFrequency = 1f, ComboCooldownMin = 25f, ComboCooldownMax = 35f,
            MeteorCooldownMin = 25f, MeteorCooldownMax = 35f, MaxConcurrentThreats = 1
        };
        public AvatarBossDifficultyProfile Hard = new AvatarBossDifficultyProfile
        {
            Difficulty = AvatarBossDifficulty.Hard, BodyDamageMultiplier = 0.08f, PlayerDamageMultiplier = 1.2f,
            AttackCooldownMultiplier = 0.85f, TelegraphMultiplier = 0.85f, WindupMultiplier = 0.85f,
            DifficultyStaggerMultiplier = 0.85f,
            StaggerGainMultiplier = 0.9f, VulnerabilityDuration = 3f, ShockwaveSpeedMultiplier = 1.1f,
            MeteorDamage = 24f, OrbCount = 4, OrbHealth = 42f, OrbSpeed = 5.75f, HealPercent = 0.10f,
            SummonCount = 3, SummonCooldownMultiplier = 0.85f, ComboFrequency = 1f, RecoveryFrequency = 0.85f, ComboCooldownMin = 18f, ComboCooldownMax = 25f,
            MeteorCooldownMin = 18f, MeteorCooldownMax = 25f, MaxConcurrentThreats = 1
        };

        AvatarBossController m_Boss;
        AvatarBossAttackScheduler m_Scheduler;
        AvatarBossHealingOrbs m_Orbs;
        AvatarBossEarthAttack m_Earth;
        AvatarBossShockwaveAttack m_Shockwave;
        AvatarBossFireAttack m_Fire;
        AvatarBossPhaseController m_Phase;
        AvatarBossSummonController m_Summons;
        AvatarBossStagger m_Stagger;
        PlayerCharacterController m_Player;
        bool m_InitialComboEnabled;

        void Awake()
        {
            m_Boss = GetComponent<AvatarBossController>();
            m_Scheduler = GetComponent<AvatarBossAttackScheduler>();
            m_Orbs = GetComponent<AvatarBossHealingOrbs>();
            m_Earth = GetComponent<AvatarBossEarthAttack>();
            m_Shockwave = GetComponent<AvatarBossShockwaveAttack>();
            m_Fire = GetComponent<AvatarBossFireAttack>();
            m_Phase = GetComponent<AvatarBossPhaseController>();
            m_Summons = GetComponent<AvatarBossSummonController>();
            m_Stagger = GetComponent<AvatarBossStagger>();
            m_Player = FindFirstObjectByType<PlayerCharacterController>();
            m_InitialComboEnabled = m_Scheduler != null && m_Scheduler.EnableCombo;
            ApplyDifficulty(DefaultDifficulty);
        }

        public void ApplyDifficulty(AvatarBossDifficulty difficulty)
        {
            CurrentDifficulty = difficulty;
            ActiveProfile = difficulty == AvatarBossDifficulty.Easy ? Easy :
                difficulty == AvatarBossDifficulty.Hard ? Hard : Normal;

            if (m_Scheduler != null)
            {
                m_Scheduler.ComboFrequency = ActiveProfile.ComboFrequency;
                m_Scheduler.UseMixedCombos = difficulty == AvatarBossDifficulty.Hard;
                m_Scheduler.EnableCombo = difficulty == AvatarBossDifficulty.Hard || m_InitialComboEnabled;
                m_Scheduler.AttackCooldown = 3f * ActiveProfile.AttackCooldownMultiplier;
                m_Scheduler.InitialGraceTime = 4f * ActiveProfile.AttackCooldownMultiplier;
                m_Scheduler.RecoverTime = 1.5f * ActiveProfile.AttackCooldownMultiplier;
                m_Scheduler.WindupTime = Mathf.Max(0.6f, 0.9f * ActiveProfile.WindupMultiplier);
                m_Scheduler.ComboCooldownMin = ActiveProfile.ComboCooldownMin;
                m_Scheduler.ComboCooldownMax = ActiveProfile.ComboCooldownMax;
                m_Scheduler.MeteorRainCooldownMin = ActiveProfile.MeteorCooldownMin;
                m_Scheduler.MeteorRainCooldownMax = ActiveProfile.MeteorCooldownMax;
            }

            int arenaTargetCells = difficulty == AvatarBossDifficulty.Easy ? 1 :
                difficulty == AvatarBossDifficulty.Hard ? 3 : 2;
            if (m_Earth != null)
            {
                m_Earth.TelegraphTime = 1.6f * ActiveProfile.TelegraphMultiplier;
                m_Earth.Cooldown = 2.7f * ActiveProfile.AttackCooldownMultiplier;
                m_Earth.TargetCellCount = arenaTargetCells;
            }
            if (m_Shockwave != null) { m_Shockwave.TelegraphTime = 1.6f * ActiveProfile.TelegraphMultiplier; m_Shockwave.Cooldown = 2.7f * ActiveProfile.AttackCooldownMultiplier; m_Shockwave.WaveSpeed = 22f * ActiveProfile.ShockwaveSpeedMultiplier; }
            if (m_Fire != null)
            {
                m_Fire.TelegraphTime = 1.8f * ActiveProfile.TelegraphMultiplier;
                m_Fire.Cooldown = 2.7f * ActiveProfile.AttackCooldownMultiplier;
                m_Fire.Damage = ActiveProfile.MeteorDamage;
                m_Fire.TargetCellCount = arenaTargetCells;
            }
            if (m_Boss != null) m_Boss.VulnerabilityDuration = ActiveProfile.VulnerabilityDuration;
            var duel = GetComponent<AvatarBossDuelController>();
            if (duel != null)
                duel.ApplyDifficultyTuning(ActiveProfile.BodyDamageMultiplier, ActiveProfile.VulnerabilityDuration);
            if (m_Stagger != null)
            {
                m_Stagger.MaxStagger = 100f;
                // Health.OnDamaged supplies post-DamageMultiplier values. Normalize
                // back to the Normal BossBody scale before applying the requested
                // designer multipliers, so Hard's lower HP damage does not make
                // stagger unreachable. The 1.2 base is the effective equivalent
                // of the requested 0.11 designer-scale target in this project.
                const float normalBodyMultiplier = 0.12f;
                m_Stagger.StaggerGainPerDamage = 1.2f * ActiveProfile.DifficultyStaggerMultiplier
                    * normalBodyMultiplier / Mathf.Max(0.01f, ActiveProfile.BodyDamageMultiplier);
            }
            if (m_Phase != null) { m_Phase.PhaseTwoVulnerabilityDuration = ActiveProfile.VulnerabilityDuration * 0.72f; m_Phase.StaggerGainMultiplier = ActiveProfile.StaggerGainMultiplier; }
            if (m_Orbs != null) { m_Orbs.OrbCount = ActiveProfile.OrbCount; m_Orbs.OrbHealth = ActiveProfile.OrbHealth; m_Orbs.OrbSpeed = ActiveProfile.OrbSpeed; m_Orbs.HealPercentOfMaxHealth = ActiveProfile.HealPercent; m_Orbs.RecoveryCooldown = 40f * ActiveProfile.RecoveryFrequency; }
            if (m_Summons != null) { m_Summons.MaxActiveSummons = ActiveProfile.SummonCount; m_Summons.CooldownBetweenSummons = 20f * ActiveProfile.SummonCooldownMultiplier; }

            var body = transform.Find("BossBody");
            var bodyDamageable = body != null ? body.GetComponent<Damageable>() : null;
            if (bodyDamageable != null)
                bodyDamageable.DamageMultiplier = ActiveProfile.BodyDamageMultiplier;

            if (m_Player == null)
                m_Player = FindFirstObjectByType<PlayerCharacterController>();
            var playerDamageable = m_Player != null ? m_Player.GetComponent<Damageable>() : null;
            if (playerDamageable != null)
                playerDamageable.DamageMultiplier = ActiveProfile.PlayerDamageMultiplier;
        }
    }
}
