using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class AutoTagesabschlussSettingsDto
{
    public bool Enabled { get; set; } = true;

    [Range(0, 23)]
    public int HourVienna { get; set; } = AutoTagesabschlussSettings.DefaultHourVienna;

    [Range(0, 59)]
    public int MinuteVienna { get; set; } = AutoTagesabschlussSettings.DefaultMinuteVienna;

    [MaxLength(32)]
    public string OpenOrdersPolicy { get; set; } = AutoTagesabschlussOpenOrdersPolicies.ForceWithWarning;

    public bool PromptCashCount { get; set; } = true;

    public static AutoTagesabschlussSettingsDto From(AutoTagesabschlussSettings? settings)
    {
        var source = settings ?? AutoTagesabschlussSettings.CreateDefault();
        source.Normalize();
        return new AutoTagesabschlussSettingsDto
        {
            Enabled = source.Enabled,
            HourVienna = source.HourVienna,
            MinuteVienna = source.MinuteVienna,
            OpenOrdersPolicy = source.OpenOrdersPolicy,
            PromptCashCount = source.PromptCashCount,
        };
    }

    public AutoTagesabschlussSettings ToSettings()
    {
        var settings = new AutoTagesabschlussSettings
        {
            Enabled = Enabled,
            HourVienna = HourVienna,
            MinuteVienna = MinuteVienna,
            OpenOrdersPolicy = OpenOrdersPolicy,
            PromptCashCount = PromptCashCount,
        };
        settings.Normalize();
        return settings;
    }
}
