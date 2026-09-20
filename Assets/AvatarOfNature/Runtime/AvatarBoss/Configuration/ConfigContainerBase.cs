using System;
using UnityEngine;

namespace Unity.FPS.AvatarBoss.Configs
{
    /// <summary>Non-generic bridge used by the universal editor.</summary>
    public abstract class ConfigContainerBase : ScriptableObject
    {
        public abstract Type ElementType { get; }
        public abstract ScriptableObject[] GetElementsForEditor();
        public abstract void SetElementsForEditor(ScriptableObject[] elements);
    }
}
