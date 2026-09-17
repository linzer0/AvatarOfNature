using System;
using UnityEngine;

namespace Unity.FPS.AvatarBoss
{
    public enum AvatarBossIntentReason
    {
        PressurePlayerReposition,
        ExploitIntactSector,
        ForceMovement,
        MaintainAttackVariety,
    }

    public enum AvatarBossTelegraphPhase
    {
        Chosen,
        Telegraph,
        ReadyToExecute,
    }

    public enum AvatarBossExpectedResult
    {
        PunishCurrentPosition,
        DenyIntactSector,
        ForcePlayerMovement,
    }

    /// A diagnostic, self-contained description of one boss decision.
    /// This is deliberately data-only; it does not reference a player prefab or a scene object.
    [Serializable]
    public readonly struct AvatarBossIntent : IEquatable<AvatarBossIntent>
    {
        public AvatarBossElement AttackType { get; }
        public int TargetSector { get; }
        public Vector3 TargetDirection { get; }
        public AvatarBossIntentReason Reason { get; }
        public AvatarBossTelegraphPhase TelegraphPhase { get; }
        public AvatarBossExpectedResult ExpectedResult { get; }
        public long SequenceNumber { get; }

        public AvatarBossIntent(
            AvatarBossElement attackType,
            int targetSector,
            Vector3 targetDirection,
            AvatarBossIntentReason reason,
            AvatarBossTelegraphPhase telegraphPhase,
            AvatarBossExpectedResult expectedResult,
            long sequenceNumber)
        {
            AttackType = attackType;
            TargetSector = targetSector;
            TargetDirection = targetDirection;
            Reason = reason;
            TelegraphPhase = telegraphPhase;
            ExpectedResult = expectedResult;
            SequenceNumber = sequenceNumber;
        }

        public bool Equals(AvatarBossIntent other)
        {
            return AttackType == other.AttackType
                && TargetSector == other.TargetSector
                && TargetDirection == other.TargetDirection
                && Reason == other.Reason
                && TelegraphPhase == other.TelegraphPhase
                && ExpectedResult == other.ExpectedResult
                && SequenceNumber == other.SequenceNumber;
        }

        public override bool Equals(object obj) => obj is AvatarBossIntent other && Equals(other);
        public override int GetHashCode() => SequenceNumber.GetHashCode();
        public static bool operator ==(AvatarBossIntent left, AvatarBossIntent right) => left.Equals(right);
        public static bool operator !=(AvatarBossIntent left, AvatarBossIntent right) => !left.Equals(right);

        public override string ToString()
        {
            return $"#{SequenceNumber} {AttackType} sector {TargetSector} ({Reason}, {TelegraphPhase})";
        }
    }
}
