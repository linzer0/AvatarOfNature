using UnityEngine;

namespace Unity.FPS.AvatarBoss.Configs.Demo
{
    public sealed class DamageAbilityPart : AbilityPart
    {
        [Min(0f)]
        [SerializeField]
        int m_Damage = 100;

        public int Damage => m_Damage;
    }
}
