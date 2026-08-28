using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

/// <summary>Tenant receipt print settings (Dankesnachricht). Source: <see cref="CompanySettings.ThankYouMessage"/>.</summary>
public sealed class ReceiptSettingsDto
{
    /// <summary>Stored custom message; null when using the default.</summary>
    public string? ThankYouMessage { get; init; }

    /// <summary>Message POS/print should show (custom or default).</summary>
    public string EffectiveThankYouMessage { get; init; } = ReceiptThankYouMessage.Default;

    public string DefaultThankYouMessage { get; init; } = ReceiptThankYouMessage.Default;

    /// <summary>Company description printed after the thank-you line (read-only on this endpoint).</summary>
    public string? CompanyDescription { get; init; }
}

public sealed class UpdateReceiptSettingsRequest
{
    [MaxLength(ReceiptThankYouMessage.MaxLength)]
    public string? ThankYouMessage { get; set; }
}
