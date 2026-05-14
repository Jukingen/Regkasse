namespace KasseAPI_Final.DTOs;

/// <summary>Admin FA: persisted development-mode singleton (read model).</summary>
public sealed class DevelopmentModeSettingsResponseDto
{
    public bool Enabled { get; init; }

    public bool BypassLicense { get; init; }

    public bool BypassNtpCheck { get; init; }

    public bool BypassTseCheck { get; init; }

    public bool SimulateOffline { get; init; }

    public bool ForceOnline { get; init; }

    public int ValidDays { get; init; }

    public string[] Features { get; init; } = [];

    public DateTime UpdatedAtUtc { get; init; }

    /// <summary>Updater email when <c>AspNetUsers.Id</c> matches <c>updated_by_user_id</c> (UUID string).</summary>
    public string? UpdatedBy { get; init; }
}

/// <summary>Admin FA: update development-mode singleton (no server-only response fields).</summary>
public sealed class DevelopmentModeSettingsPutRequestDto
{
    public bool Enabled { get; init; }

    public bool BypassLicense { get; init; }

    public bool BypassNtpCheck { get; init; }

    public bool BypassTseCheck { get; init; }

    public bool SimulateOffline { get; init; }

    public bool ForceOnline { get; init; }

    public int ValidDays { get; init; }

    public string[] Features { get; init; } = [];
}
