using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models.DTOs;

public sealed class SessionSettingsDto
{
    public int TimeoutMinutes { get; set; } = 30;
    public int WarningMinutes { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}

public sealed class UpdateSessionSettingsRequest
{
    [Range(5, 480)]
    public int TimeoutMinutes { get; set; } = 30;

    [Range(1, 10)]
    public int WarningMinutes { get; set; } = 1;

    public bool Enabled { get; set; } = true;
}
