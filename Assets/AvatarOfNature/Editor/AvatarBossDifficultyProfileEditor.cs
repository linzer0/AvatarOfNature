using UnityEditor;
using UnityEngine;

namespace Unity.FPS.AvatarBoss.Editor
{
    [CustomEditor(typeof(AvatarBossDifficultyProfile))]
    public sealed class AvatarBossDifficultyProfileEditor : UnityEditor.Editor
    {
        readonly bool[] m_ShowSections = { true, false, false, false };

        public override void OnInspectorGUI()
        {
            var profile = (AvatarBossDifficultyProfile)target;
            serializedObject.Update();

            DrawHeader(profile);
            EditorGUILayout.Space(6);
            DrawSection(0, "DAMAGE & SURVIVABILITY", "How the boss and player exchange damage.", DamageColor(profile.Difficulty),
                "BodyDamageMultiplier", "OpenBodyDamageMultiplier", "BossHealthMultiplier", "WeakPointDamageMultiplier", "PlayerDamageMultiplier");
            DrawSection(1, "ATTACK PACING", "Telegraph, wind-up and stagger timing.", DamageColor(profile.Difficulty),
                "AttackCooldownMultiplier", "TelegraphMultiplier", "WindupMultiplier", "DifficultyStaggerMultiplier", "StaggerGainMultiplier", "VulnerabilityDuration", "ShockwaveSpeedMultiplier");
            DrawArenaSection(profile);
            DrawSection(3, "PHASE TWO", "The pressure curve after the first stagger.", DamageColor(profile.Difficulty),
                "PhaseTwoTimingMultiplier", "PhaseTwoTelegraphMultiplier", "PhaseTwoStaggerGainMultiplier", "PhaseTwoStaggerThresholdMultiplier", "PhaseTwoStaggerDecayMultiplier", "PhaseTwoStaggerDecayDelayMultiplier", "PhaseTwoMinCooldown", "PhaseTwoMinTelegraph", "PhaseTwoMinWindup", "PhaseTwoMinRecover", "PhaseTwoMeteorDamage", "PhaseTwoShockwaveSpeed", "PhaseTwoVulnerabilityDuration", "PhaseTwoComboEnabled");

            serializedObject.ApplyModifiedProperties();
        }

        void DrawHeader(AvatarBossDifficultyProfile profile)
        {
            var accent = DamageColor(profile.Difficulty);
            EditorGUILayout.BeginVertical("helpbox");
            var rect = EditorGUILayout.GetControlRect(false, 48f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 6f, rect.height), accent);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, normal = { textColor = accent } };
            var subtitleStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.Lerp(Color.white, accent, 0.35f) } };
            EditorGUI.LabelField(new Rect(rect.x + 14f, rect.y + 2f, rect.width - 14f, 24f), profile.name, titleStyle);
            EditorGUI.LabelField(new Rect(rect.x + 14f, rect.y + 26f, rect.width - 14f, 18f), "Avatar Boss Difficulty Profile", subtitleStyle);
            EditorGUILayout.EndVertical();
        }

        void DrawArenaSection(AvatarBossDifficultyProfile profile)
        {
            var accent = DamageColor(profile.Difficulty);
            EditorGUILayout.BeginVertical("helpbox");
            DrawSectionHeader(2, "ARENA THREATS", "Orbs, summons, combos and meteor pressure.", accent);
            if (m_ShowSections[2])
            {
                EditorGUILayout.BeginVertical("box");
                DrawProperty("MeteorDamage");
                DrawProperty("OrbCount");
                DrawProperty("OrbHealth");
                DrawProperty("OrbSpeed");
                DrawProperty("HealPercent");
                DrawProperty("SummonCount");
                DrawProperty("SummonCooldownMultiplier");
                DrawProperty("ComboFrequency");
                DrawProperty("RecoveryFrequency");
                DrawProperty("ComboCooldownMin");
                DrawProperty("ComboCooldownMax");
                DrawProperty("MeteorCooldownMin");
                DrawProperty("MeteorCooldownMax");
                DrawProperty("MaxConcurrentThreats");
                DrawProperty("TargetCellCount");
                DrawProperty("MixedCombosEnabled");
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawSection(int index, string title, string subtitle, Color accent, params string[] fields)
        {
            EditorGUILayout.BeginVertical("helpbox");
            DrawSectionHeader(index, title, subtitle, accent);
            if (m_ShowSections[index])
            {
                EditorGUILayout.BeginVertical("box");
                foreach (var field in fields)
                    DrawProperty(field);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        void DrawSectionHeader(int index, string title, string subtitle, Color accent)
        {
            var rect = EditorGUILayout.GetControlRect(false, 34f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), accent);
            var foldoutRect = new Rect(rect.x + 10f, rect.y, rect.width - 10f, 19f);
            m_ShowSections[index] = EditorGUI.Foldout(foldoutRect, m_ShowSections[index], title, true);
            EditorGUI.LabelField(new Rect(rect.x + 10f, rect.y + 17f, rect.width - 10f, 16f), subtitle, EditorStyles.miniLabel);
        }

        void DrawProperty(string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(ObjectNames.NicifyVariableName(propertyName)));
        }

        static Color DamageColor(AvatarBossDifficulty difficulty)
        {
            if (difficulty == AvatarBossDifficulty.Easy)
                return new Color(0.30f, 0.72f, 0.44f);
            if (difficulty == AvatarBossDifficulty.Hard)
                return new Color(0.92f, 0.30f, 0.28f);
            return new Color(0.30f, 0.55f, 0.95f);
        }
    }
}
