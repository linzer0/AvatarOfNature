using UnityEngine;

namespace Unity.FPS.AvatarBoss.Configs.Demo
{
    public abstract class AbilityPart : ScriptableObject
    {
        [SerializeField]
        string m_DisplayName;

        public string DisplayName => m_DisplayName;
    }
}
