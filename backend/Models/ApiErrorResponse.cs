namespace KasseAPI_Final.Models;

/// <summary>Compact API error payload with stable <see cref="Code"/> and localized <see cref="Message"/>.</summary>
public sealed class ApiErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
