namespace Unity.FPS.AvatarBoss
{
    /// <summary>
    /// Runtime-owned diagnostics state. The testing assembly may toggle this
    /// flag, while gameplay code stays independent from debug components.
    /// </summary>
    public static class AvatarBossDiagnostics
    {
        public static bool F10DiagnosticsEnabled { get; set; }
    }
}
