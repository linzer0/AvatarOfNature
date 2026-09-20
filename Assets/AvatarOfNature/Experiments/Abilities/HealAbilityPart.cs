using UnityEngine;

namespace Unity.FPS.AvatarBoss.Configs.Demo
{
    public sealed class HealAbilityPart : AbilityPart
    {
        [Min(0f)]
        [SerializeField]
        int m_HealAmount = 50;

        public int HealAmount => m_HealAmount;
    }
}
