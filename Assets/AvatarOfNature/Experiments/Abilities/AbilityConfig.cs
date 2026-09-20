using UnityEngine;
using Unity.FPS.AvatarBoss.Configs;

namespace Unity.FPS.AvatarBoss.Configs.Demo
{
    [CreateAssetMenu(menuName = "Avatar Boss/Ability Config", fileName = "AbilityConfig")]
    [ConfigContainer(GroupByType = true, AllowDeletion = true, AllowReorder = true, WithCustomName = true)]
    public sealed class AbilityConfig : ConfigContainer<AbilityPart>
    {
        [SerializeField]
        string m_AbilityId = "fireball";

        public string AbilityId => m_AbilityId;
    }
}
