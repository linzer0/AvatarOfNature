using UnityEngine;

namespace Unity.FPS.Game
{
    // The Game Events used across the Game.
    // Anytime there is a need for a new event, it should be added here.

    public static class Events
    {
        public static ObjectiveUpdateEvent ObjectiveUpdateEvent = new ObjectiveUpdateEvent();
        public static AllObjectivesCompletedEvent AllObjectivesCompletedEvent = new AllObjectivesCompletedEvent();
        public static GameOverEvent GameOverEvent = new GameOverEvent();
        public static PlayerDeathEvent PlayerDeathEvent = new PlayerDeathEvent();
        public static EnemyKillEvent EnemyKillEvent = new EnemyKillEvent();
        public static PickupEvent PickupEvent = new PickupEvent();
        public static AmmoPickupEvent AmmoPickupEvent = new AmmoPickupEvent();
        public static DamageEvent DamageEvent = new DamageEvent();
        public static DisplayMessageEvent DisplayMessageEvent = new DisplayMessageEvent();
    }

    public class ObjectiveUpdateEvent : GameEvent
    {
        public Objective Objective;
        public string DescriptionText;
        public string CounterText;
        public bool IsComplete;
        public string NotificationText;
    }

    public class AllObjectivesCompletedEvent : GameEvent { }

    public class GameOverEvent : GameEvent
    {
        public bool Win;
    }

    public class PlayerDeathEvent : GameEvent { }

    public class EnemyKillEvent : GameEvent
    {
        public GameObject Enemy;
        public int RemainingEnemyCount;
    }

    public class PickupEvent : GameEvent
    {
        public GameObject Pickup;
    }

    public class AmmoPickupEvent : GameEvent
    {
        public WeaponController Weapon;
    }

    public class DamageEvent : GameEvent
    {
        public GameObject Sender;
        public float DamageValue;
    }

    /// <summary>Confirmed damage feedback for presentation systems such as the HUD.</summary>
    public class BossHitFeedbackEvent : GameEvent
    {
        public Vector3 Position;
        public float Damage;
        public bool IsWeakPoint;
    }

    /// <summary>Short camera impulse emitted by combat moments that deserve weight.</summary>
    public class CameraImpulseEvent : GameEvent
    {
        public float Strength;
        public float Duration;
        public Vector3 Direction;
    }

    public class DisplayMessageEvent : GameEvent
    {
        public string Message;
        public float DelayBeforeDisplay;
    }
}
