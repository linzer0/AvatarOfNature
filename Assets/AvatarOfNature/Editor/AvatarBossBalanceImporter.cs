using System;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.AvatarBoss.Editor
{
    /// <summary>Imports the designer-owned JSON balance sheet into the runtime ScriptableObject snapshot.</summary>
    public static class AvatarBossBalanceImporter
    {
        // JSON is the canonical designer source; Unity stores the imported snapshot in the asset.
        const string SourcePath = "Assets/AvatarOfNature/Config/AvatarBossBalance.json";
        const string ConfigPath = "Assets/AvatarOfNature/Config/Resources/AvatarBossEncounterConfig.asset";

        [MenuItem("Avatar Boss/Import balance JSON")]
        public static void Import()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath);
            var config = AssetDatabase.LoadAssetAtPath<AvatarBossEncounterConfig>(ConfigPath);
            if (source == null || config == null)
            {
                Debug.LogError($"[AvatarBossBalance] Missing source or config: {SourcePath}, {ConfigPath}");
                return;
            }

            var document = JsonUtility.FromJson<AvatarBossBalanceDocument>(source.text);
            if (document == null || document.profiles == null || document.profiles.Length == 0)
            {
                Debug.LogError("[AvatarBossBalance] JSON contains no profiles.");
                return;
            }

            if (document.baseEncounter != null)
            {
                config.AttackCooldown = document.baseEncounter.attackCooldown;
                config.InitialGraceTime = document.baseEncounter.initialGraceTime;
                config.RecoverTime = document.baseEncounter.recoverTime;
                config.WindupTime = document.baseEncounter.windupTime;
                config.RecoveryCooldown = document.baseEncounter.recoveryCooldown;
                config.SummonCooldown = document.baseEncounter.summonCooldown;
            }

            foreach (var imported in document.profiles)
            {
                if (!Enum.TryParse(imported.difficulty, true, out AvatarBossDifficulty difficulty))
                {
                    Debug.LogWarning($"[AvatarBossBalance] Unknown difficulty '{imported.difficulty}'.");
                    continue;
                }

                var profile = config.GetProfile(difficulty);
                if (profile == null)
                    continue;

                EditorJsonUtility.FromJsonOverwrite(JsonUtility.ToJson(imported), profile);
                profile.Difficulty = difficulty;
                EditorUtility.SetDirty(profile);
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ConfigPath);
            Debug.Log($"[AvatarBossBalance] Imported {document.profiles.Length} profiles from {SourcePath}.");
        }
    }

    [Serializable]
    public sealed class AvatarBossBalanceDocument
    {
        public int version;
        public AvatarBossBalanceBase baseEncounter;
        public AvatarBossBalanceProfile[] profiles;
    }

    [Serializable]
    public sealed class AvatarBossBalanceBase
    {
        public float attackCooldown;
        public float initialGraceTime;
        public float recoverTime;
        public float windupTime;
        public float recoveryCooldown;
        public float summonCooldown;
    }

    [Serializable]
    public sealed class AvatarBossBalanceProfile
    {
        public string difficulty;
        public float BodyDamageMultiplier;
        public float OpenBodyDamageMultiplier;
        public float BossHealthMultiplier;
        public float WeakPointDamageMultiplier;
        public float PlayerDamageMultiplier;
        public float MaxVulnerabilityDamagePercent;
        public float HighImpactWindowDamagePercent;
        public float AttackCooldownMultiplier;
        public float TelegraphMultiplier;
        public float WindupMultiplier;
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
        public int TargetCellCount;
        public bool MixedCombosEnabled;
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
