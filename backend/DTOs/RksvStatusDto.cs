using KasseAPI_Final.Services.Rksv;

namespace KasseAPI_Final.DTOs;

/// <summary>RKSV environment status for POS/Admin badges (<c>GET /api/rksv/status</c>).</summary>
public sealed class RksvStatusDto
{
    public bool IsSimulated { get; init; }

    public string Environment { get; init; } = "Production";

    public bool ShowDemoLabel { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string TseStatus { get; init; } = string.Empty;

    public static RksvStatusDto FromService(IRksvEnvironmentService service) =>
        new()
        {
            IsSimulated = service.IsTseSimulated(),
            Environment = service.IsDemoMode() ? "Demo" : "Production",
            ShowDemoLabel = service.ShowDemoLabel(),
            DisplayName = service.GetEnvironmentDisplayName(),
            TseStatus = service.GetTseStatusDisplay(),
        };
}
