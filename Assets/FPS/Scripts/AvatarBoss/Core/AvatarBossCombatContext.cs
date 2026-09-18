using Unity.FPS.Gameplay;

namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Runtime dependency boundary for one boss encounter.
    /// Scene lookup is centralized here so attacks and orchestration code do not
    /// each discover their own arena/player instance.
    /// </summary>
    public sealed class AvatarBossCombatContext
    {
        public AvatarBossArenaController Arena { get; private set; }
        public PlayerCharacterController Player { get; private set; }

        public AvatarBossCombatContext(AvatarBossArenaController arena, PlayerCharacterController player)
        {
            Arena = arena;
            Player = player;
        }

        public void ResolveSceneReferences()
        {
            if (Arena == null)
                Arena = UnityEngine.Object.FindFirstObjectByType<AvatarBossArenaController>();
            if (Player == null)
                Player = UnityEngine.Object.FindFirstObjectByType<PlayerCharacterController>();

        }
    }
}
