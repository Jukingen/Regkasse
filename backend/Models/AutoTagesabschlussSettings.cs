using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-tenant automatic Tagesabschluss fallback stored as JSON on <see cref="CompanySettings"/>.
/// Hosted worker closes the previous Vienna business day after the configured local time
/// when the cashier did not close and the day has fiscal transactions.
/// </summary>
public sealed class AutoTagesabschlussSettings
{
    public const int DefaultHourVienna = 3;
    public const int DefaultMinuteVienna = 0;
    public const string AutomaticLateReason =
        "Automatischer Tagesabschluss (Kassierer hat nicht abgeschlossen)";
    public const string NoCashCountNote = "Kein Kassensturz";

    public bool Enabled { get; set; } = true;

    /// <summary>Europe/Vienna hour (0–23) after which yesterday is auto-closed. Default 03:00.</summary>
    [Range(0, 23)]
    public int HourVienna { get; set; } = DefaultHourVienna;

    [Range(0, 59)]
    public int MinuteVienna { get; set; } = DefaultMinuteVienna;

    /// <summary>
    /// <see cref="AutoTagesabschlussOpenOrdersPolicies"/>:
    /// Block, NotifyAndContinue, or ForceWithWarning (default).
    /// </summary>
    [MaxLength(32)]
    public string OpenOrdersPolicy { get; set; } = AutoTagesabschlussOpenOrdersPolicies.ForceWithWarning;

    /// <summary>When true, POS should prompt "Kasse zählen" before the fallback fires.</summary>
    public bool PromptCashCount { get; set; } = true;

    public static AutoTagesabschlussSettings CreateDefault() => new();

    public void Normalize()
    {
        HourVienna = Math.Clamp(HourVienna, 0, 23);
        MinuteVienna = Math.Clamp(MinuteVienna, 0, 59);
        OpenOrdersPolicy = AutoTagesabschlussOpenOrdersPolicies.Normalize(OpenOrdersPolicy);
    }

    public string FormatViennaTime() => $"{HourVienna:00}:{MinuteVienna:00}";
}
