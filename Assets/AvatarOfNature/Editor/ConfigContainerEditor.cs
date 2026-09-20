using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.FPS.AvatarBoss.Configs;

namespace Unity.FPS.AvatarBoss.Configs.Editor
{
    [CustomEditor(typeof(ConfigContainerBase), true)]
    [CanEditMultipleObjects]
    public sealed class ConfigContainerEditor : UnityEditor.Editor
    {
        readonly List<ScriptableObject> m_Elements = new List<ScriptableObject>();
        readonly List<Type> m_ElementTypes = new List<Type>();
        readonly List<string> m_GroupLabels = new List<string>();
        readonly List<object> m_GroupValues = new List<object>();
        UnityEditor.Editor[] m_ChildEditors = Array.Empty<UnityEditor.Editor>();
        ConfigContainerBase m_Container;
        ConfigContainerAttribute m_Settings;
        string m_AssetPath;
        int m_SelectedTypeIndex;

        void OnEnable()
        {
            m_Container = target as ConfigContainerBase;
            if (m_Container == null)
                return;

            m_Settings = (ConfigContainerAttribute)Attribute.GetCustomAttribute(
                m_Container.GetType(), typeof(ConfigContainerAttribute), true);
            if (m_Settings == null)
                m_Settings = new ConfigContainerAttribute();

            m_AssetPath = AssetDatabase.GetAssetPath(m_Container);
            RebuildElements();
            Undo.undoRedoPerformed += RebuildElements;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= RebuildElements;
            ConfigContainerEditorUtility.DestroyCachedEditors(m_ChildEditors);
            m_ChildEditors = Array.Empty<UnityEditor.Editor>();
        }

        public override void OnInspectorGUI()
        {
            if (m_Container == null)
                return;

            serializedObject.Update();
            DrawContainerHeader();
            DrawRootProperties();
            EditorGUILayout.Space(8);

            if (serializedObject.isEditingMultipleObjects)
            {
                EditorGUILayout.HelpBox("Container elements cannot be edited in multi-object mode.", MessageType.Info);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawElementToolbar();
            DrawElements();
            DrawAddButton();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawContainerHeader()
        {
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(m_Container.GetType().Name), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Sub-assets are stored inside this config asset.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        void DrawRootProperties()
        {
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script" || iterator.propertyPath == "_elements")
                    continue;
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        void DrawElementToolbar()
        {
            if (m_GroupLabels.Count == 0)
                return;

            m_SelectedTypeIndex = Mathf.Clamp(m_SelectedTypeIndex, 0, m_GroupLabels.Count - 1);
            EditorGUILayout.LabelField("ELEMENT GROUPS", EditorStyles.miniBoldLabel);
            m_SelectedTypeIndex = GUILayout.Toolbar(m_SelectedTypeIndex, m_GroupLabels.ToArray());
            EditorGUILayout.Space(5);
        }

        void DrawElements()
        {
            EnsureEditorCapacity();
            var visibleCount = 0;

            for (var i = 0; i < m_Elements.Count; i++)
            {
                var element = m_Elements[i];
                if (element == null)
                    continue;
                if (!BelongsToSelectedGroup(element))
                    continue;

                DrawElementCard(element, i);
                visibleCount++;
            }

            if (visibleCount == 0)
                EditorGUILayout.HelpBox("No elements in this group yet.", MessageType.Info);
        }

        void DrawElementCard(ScriptableObject element, int index)
        {
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.BeginHorizontal();
            var title = ConfigContainerEditorUtility.GetDisplayName(element.GetType());
            if (m_Settings.WithCustomName)
                title += "  •  " + element.name;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            UnityEditor.Editor.CreateCachedEditor(element, null, ref m_ChildEditors[index]);
            if (m_ChildEditors[index] != null)
                m_ChildEditors[index].OnInspectorGUI();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        void DrawAddButton()
        {
            if (m_ElementTypes.Count == 0 || m_GroupLabels.Count == 0)
                return;
            if (m_Settings.MaxElementCount > 0 && m_Elements.Count >= m_Settings.MaxElementCount)
            {
                EditorGUILayout.HelpBox("Maximum element count reached.", MessageType.Info);
                return;
            }

            var selectedType = GetAddType();
            var label = "Add " + ConfigContainerEditorUtility.GetDisplayName(selectedType);
            if (GUILayout.Button(label, GUILayout.Height(26f)))
                AddElement(selectedType);
        }

        void AddElement(Type elementType)
        {
            var child = ScriptableObject.CreateInstance(elementType);
            child.name = elementType.Name;
            ApplySelectedGroupValue(child);
            AssetDatabase.AddObjectToAsset(child, m_Container);
            Undo.RegisterCreatedObjectUndo(child, "Create config element");
            Undo.RecordObject(m_Container, "Add config element");
            m_Elements.Add(child);
            SyncElements();
            Selection.activeObject = child;
        }

        void RebuildElements()
        {
            if (m_Container == null || string.IsNullOrEmpty(m_AssetPath))
                return;

            m_Elements.Clear();
            var serializedElements = m_Container.GetElementsForEditor();
            if (serializedElements != null)
            {
                foreach (var element in serializedElements)
                    if (element != null && ConfigContainerEditorUtility.IsSubAssetOf(element, m_Container, m_AssetPath))
                        m_Elements.Add(element);
            }

            var containedAssets = AssetDatabase.LoadAllAssetsAtPath(m_AssetPath);
            foreach (var containedAsset in containedAssets)
            {
                var child = containedAsset as ScriptableObject;
                if (child == null || child == m_Container || !m_Container.ElementType.IsInstanceOfType(child))
                    continue;
                if (!m_Elements.Contains(child))
                    m_Elements.Add(child);
            }

            m_ElementTypes.Clear();
            m_ElementTypes.AddRange(ConfigContainerEditorUtility.GetCreatableElementTypes(m_Container.ElementType));
            RebuildGroups();
            m_Container.SetElementsForEditor(m_Elements.ToArray());
            SyncElements(false);
        }

        void RebuildGroups()
        {
            m_GroupLabels.Clear();
            m_GroupValues.Clear();

            if (!string.IsNullOrEmpty(m_Settings.GroupByMember))
            {
                var field = GetGroupField();
                if (field != null && field.FieldType.IsEnum)
                {
                    foreach (var value in Enum.GetValues(field.FieldType))
                    {
                        m_GroupValues.Add(value);
                        m_GroupLabels.Add(ObjectNames.NicifyVariableName(value.ToString()));
                    }
                    return;
                }
            }

            if (!m_Settings.GroupByType)
            {
                m_GroupLabels.Add("ALL");
                m_GroupValues.Add(null);
                return;
            }

            foreach (var type in m_ElementTypes)
            {
                m_GroupValues.Add(type);
                m_GroupLabels.Add(ConfigContainerEditorUtility.GetDisplayName(type));
            }
        }

        FieldInfo GetGroupField()
        {
            if (m_Container == null || string.IsNullOrEmpty(m_Settings.GroupByMember))
                return null;
            return m_Container.ElementType.GetField(
                m_Settings.GroupByMember,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        bool BelongsToSelectedGroup(ScriptableObject element)
        {
            if (m_GroupValues.Count == 0)
                return true;

            var groupValue = m_GroupValues[Mathf.Clamp(m_SelectedTypeIndex, 0, m_GroupValues.Count - 1)];
            var field = GetGroupField();
            if (field != null && !(groupValue is Type))
                return Equals(field.GetValue(element), groupValue);
            return groupValue is Type type && element.GetType() == type;
        }

        Type GetAddType()
        {
            if (!string.IsNullOrEmpty(m_Settings.GroupByMember) && m_ElementTypes.Count > 0)
                return m_ElementTypes[0];
            return m_ElementTypes[Mathf.Clamp(m_SelectedTypeIndex, 0, m_ElementTypes.Count - 1)];
        }

        void ApplySelectedGroupValue(ScriptableObject element)
        {
            var field = GetGroupField();
            if (field == null || m_GroupValues.Count == 0)
                return;
            var value = m_GroupValues[Mathf.Clamp(m_SelectedTypeIndex, 0, m_GroupValues.Count - 1)];
            if (field.FieldType.IsInstanceOfType(value))
                field.SetValue(element, value);
        }

        void SyncElements(bool save = true)
        {
            m_Container.SetElementsForEditor(m_Elements.ToArray());
            if (save && !string.IsNullOrEmpty(m_AssetPath))
                ConfigContainerEditorUtility.SaveContainer(m_Container, m_AssetPath);
            serializedObject.Update();
            EnsureEditorCapacity();
        }

        void EnsureEditorCapacity()
        {
            if (m_ChildEditors.Length == m_Elements.Count)
                return;
            var oldEditors = m_ChildEditors;
            m_ChildEditors = new UnityEditor.Editor[m_Elements.Count];
            for (var i = 0; i < Mathf.Min(oldEditors.Length, m_ChildEditors.Length); i++)
                m_ChildEditors[i] = oldEditors[i];
            for (var i = m_ChildEditors.Length; i < oldEditors.Length; i++)
                if (oldEditors[i] != null)
                    DestroyImmediate(oldEditors[i]);
        }
    }
}
