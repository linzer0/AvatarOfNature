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
        [Tooltip("Shared balance asset. If empty, the legacy inline profiles below keep older scenes playable.")]
        public AvatarBossEncounterConfig EncounterConfig;
        public AvatarBossDifficulty CurrentDifficulty { get; private set; }
        public AvatarBossDifficultyProfile ActiveProfile { get; private set; }

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
            if (EncounterConfig == null)
                EncounterConfig = Resources.Load<AvatarBossEncounterConfig>("AvatarBossEncounterConfig");
            var context = GetComponentInParent<AvatarBossController>()?.GetCombatContext();
            context?.ResolveSceneReferences();
            m_Player = context != null ? context.Player : null;
            m_InitialComboEnabled = m_Scheduler != null && m_Scheduler.EnableCombo;
            ApplyDifficulty(DefaultDifficulty);
        }

        public void ApplyDifficulty(AvatarBossDifficulty difficulty)
        {
            CurrentDifficulty = difficulty;
            var legacyProfile = difficulty == AvatarBossDifficulty.Easy
                ? AvatarBossEncounterConfig.CreateEasy()
                : difficulty == AvatarBossDifficulty.Hard
                    ? AvatarBossEncounterConfig.CreateHard()
                    : AvatarBossEncounterConfig.CreateNormal();
            ActiveProfile = EncounterConfig != null ? EncounterConfig.GetProfile(difficulty) : legacyProfile;

            float baseAttackCooldown = EncounterConfig != null ? EncounterConfig.AttackCooldown : 3f;
            float baseInitialGrace = EncounterConfig != null ? EncounterConfig.InitialGraceTime : 4f;
            float baseRecover = EncounterConfig != null ? EncounterConfig.RecoverTime : 1.5f;
            float baseWindup = EncounterConfig != null ? EncounterConfig.WindupTime : 0.9f;
            float baseRecoveryCooldown = EncounterConfig != null ? EncounterConfig.RecoveryCooldown : 40f;
            float baseSummonCooldown = EncounterConfig != null ? EncounterConfig.SummonCooldown : 20f;

            if (m_Scheduler != null)
            {
                m_Scheduler.ComboFrequency = ActiveProfile.ComboFrequency;
                m_Scheduler.UseMixedCombos = difficulty == AvatarBossDifficulty.Hard;
                m_Scheduler.EnableCombo = difficulty == AvatarBossDifficulty.Hard || m_InitialComboEnabled;
                m_Scheduler.AttackCooldown = baseAttackCooldown * ActiveProfile.AttackCooldownMultiplier;
                m_Scheduler.InitialGraceTime = baseInitialGrace * ActiveProfile.AttackCooldownMultiplier;
                m_Scheduler.RecoverTime = baseRecover * ActiveProfile.AttackCooldownMultiplier;
                m_Scheduler.WindupTime = Mathf.Max(0.6f, baseWindup * ActiveProfile.WindupMultiplier);
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
            if (m_Orbs != null) { m_Orbs.OrbCount = ActiveProfile.OrbCount; m_Orbs.OrbHealth = ActiveProfile.OrbHealth; m_Orbs.OrbSpeed = ActiveProfile.OrbSpeed; m_Orbs.HealPercentOfMaxHealth = ActiveProfile.HealPercent; m_Orbs.RecoveryCooldown = baseRecoveryCooldown * ActiveProfile.RecoveryFrequency; }
            if (m_Summons != null) { m_Summons.MaxActiveSummons = ActiveProfile.SummonCount; m_Summons.CooldownBetweenSummons = baseSummonCooldown * ActiveProfile.SummonCooldownMultiplier; }

            var body = transform.Find("BossBody");
            var bodyDamageable = body != null ? body.GetComponent<Damageable>() : null;
            if (bodyDamageable != null)
                bodyDamageable.DamageMultiplier = ActiveProfile.BodyDamageMultiplier;

            if (m_Player == null)
            {
                var context = GetComponentInParent<AvatarBossController>()?.GetCombatContext();
                context?.ResolveSceneReferences();
                m_Player = context != null ? context.Player : null;
            }
            var playerDamageable = m_Player != null ? m_Player.GetComponent<Damageable>() : null;
            if (playerDamageable != null)
                playerDamageable.DamageMultiplier = ActiveProfile.PlayerDamageMultiplier;
        }
    }
}
