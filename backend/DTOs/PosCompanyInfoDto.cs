using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

/// <summary>
/// RKSV §8 company header for POS receipts (read-only). Source: <see cref="Models.CompanySettings"/>.
/// Also includes working hours for smart Tagesabschluss reminders and schedule display.
/// Working hours must never gate POS orders or payments.
/// </summary>
public sealed class PosCompanyInfoDto
{
    public string CompanyName { get; init; } = string.Empty;
    public string CompanyAddress { get; init; } = string.Empty;
    public string TaxNumber { get; init; } = string.Empty;
    public string? ReceiptFooter { get; init; }

    /// <summary>IANA time zone for local closing times (default Europe/Vienna).</summary>
    public string TimeZone { get; init; } = "Europe/Vienna";

    /// <summary>Restaurant working hours + reminder lead time for POS Tagesabschluss banner.</summary>
    public WorkingHoursDto WorkingHours { get; init; } = WorkingHoursDto.From(null);
}
