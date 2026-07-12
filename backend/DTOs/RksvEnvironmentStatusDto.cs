using KasseAPI_Final.Services.Rksv;

namespace KasseAPI_Final.DTOs;

/// <summary>RKSV deployment environment snapshot for POS and Admin UI badges.</summary>
public sealed class RksvEnvironmentStatusDto
{
    /// <summary>Demo | Production — mirrors <c>RKSV:Mode</c> and host policy.</summary>
    public string Environment { get; init; } = "Production";

    public bool IsSimulated { get; init; }

    public bool ShowDemoLabel { get; init; }

    public string TseStatusDisplay { get; init; } = string.Empty;

    public string TseStatusBadge { get; init; } = string.Empty;

    public string EnvironmentDisplayName { get; init; } = string.Empty;

    public static RksvEnvironmentStatusDto FromService(IRksvEnvironmentService service) =>
        new()
        {
            Environment = service.IsDemoMode() ? "Demo" : "Production",
            IsSimulated = service.IsTseSimulated(),
            ShowDemoLabel = service.ShowDemoLabel(),
            TseStatusDisplay = service.GetTseStatusDisplay(),
            TseStatusBadge = service.GetTseStatusBadge(),
            EnvironmentDisplayName = service.GetEnvironmentDisplayName(),
        };
}
