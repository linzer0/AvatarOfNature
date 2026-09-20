using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.FPS.AvatarBoss.Configs.Editor
{
    static class ConfigContainerEditorUtility
    {
        public static List<Type> GetCreatableElementTypes(Type elementType)
        {
            var types = new List<Type>();
            if (!elementType.IsAbstract && !elementType.IsInterface && typeof(ScriptableObject).IsAssignableFrom(elementType))
                types.Add(elementType);

            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (type.IsAbstract || type.IsInterface || !elementType.IsAssignableFrom(type))
                    continue;
                if (types.Contains(type))
                    continue;
                if (type.GetConstructor(Type.EmptyTypes) != null || typeof(ScriptableObject).IsAssignableFrom(type))
                    types.Add(type);
            }

            types.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return types;
        }

        public static string GetDisplayName(Type type)
        {
            return ObjectNames.NicifyVariableName(type.Name);
        }

        public static bool IsSubAssetOf(ScriptableObject element, ScriptableObject container, string assetPath)
        {
            return element != null && element != container && AssetDatabase.GetAssetPath(element) == assetPath;
        }

        public static void SaveContainer(ConfigContainerBase container, string assetPath)
        {
            EditorUtility.SetDirty(container);
            AssetDatabase.ImportAsset(assetPath);
            AssetDatabase.SaveAssets();
        }

        public static void DestroyCachedEditors(UnityEditor.Editor[] editors)
        {
            if (editors == null)
                return;
            foreach (var editor in editors)
                if (editor != null)
                    UnityEngine.Object.DestroyImmediate(editor);
        }
    }
}
