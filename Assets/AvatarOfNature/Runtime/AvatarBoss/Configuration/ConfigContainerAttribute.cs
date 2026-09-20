using System;

namespace Unity.FPS.AvatarBoss.Configs
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public sealed class ConfigContainerAttribute : Attribute
    {
        public bool GroupByType;
        public bool AllowDeletion = true;
        public bool AllowReorder = true;
        public bool AllowTypeChange;
        public bool WithCustomName;
        public int MaxElementCount;
        public string GroupByMember;
    }
}
