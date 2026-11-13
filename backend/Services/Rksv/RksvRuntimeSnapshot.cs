namespace KasseAPI_Final.Services.Rksv;

/// <summary>Effective RKSV overlay used by <see cref="IRksvEnvironmentService"/> (DB row or appsettings fallback).</summary>
public sealed record RksvRuntimeSnapshot(
    string Mode,
    string TseMode,
    string FinanzOnlineMode,
    bool ShowDemoLabel,
    bool OverlayPersisted,
    string Source,
    DateTime? UpdatedAtUtc,
    Guid? UpdatedByUserId,
    bool BypassTseInDevelopment = false)
{
    public bool IsDemoMode =>
        string.Equals(Mode, Models.RksvRuntimeConfig.ModeDemo, StringComparison.OrdinalIgnoreCase);

    public bool IsTseSimulation =>
        string.Equals(TseMode, Models.RksvRuntimeConfig.IntegrationSimulation, StringComparison.OrdinalIgnoreCase);

    public bool IsFinanzOnlineSimulation =>
        string.Equals(
            FinanzOnlineMode,
            Models.RksvRuntimeConfig.IntegrationSimulation,
            StringComparison.OrdinalIgnoreCase);

    public const string SourceDatabase = "database";
    public const string SourceAppsettings = "appsettings";
}
