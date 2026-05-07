namespace KasseAPI_Final.DTOs;

/// <summary>Dual-language RKSV export disclaimer for admin UI and clients.</summary>
public sealed class RksvExportDisclaimerResponseDto
{
    public string De { get; set; } = string.Empty;
    public string En { get; set; } = string.Empty;
}
