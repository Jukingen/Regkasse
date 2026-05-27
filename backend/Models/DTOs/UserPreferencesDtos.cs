namespace KasseAPI_Final.Models.DTOs;

public sealed class UserPreferencesResponseDto
{
    public string ThemeMode { get; set; } = "system";
    public string DensityMode { get; set; } = "standard";
    public string DefaultPage { get; set; } = "/dashboard";
    public string? DateFormat { get; set; }
    public string? TimeFormat { get; set; }
    public bool ReducedAnimations { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class SaveUserPreferencesRequestDto
{
    public string? ThemeMode { get; set; }
    public string? DensityMode { get; set; }
    public string? DefaultPage { get; set; }
    public string? DateFormat { get; set; }
    public string? TimeFormat { get; set; }
    public bool? ReducedAnimations { get; set; }
}
