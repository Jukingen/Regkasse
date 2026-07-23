using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin approval workflow for sensitive tenant locale / fiscal setting changes.
/// Values land on <see cref="CompanySettings"/> only after approval.
/// </summary>
[Table("tenant_settings_history")]
public class TenantSettingsHistory : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary><see cref="TenantSettingTypes"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("setting_type")]
    public string SettingType { get; set; } = string.Empty;

    [Column("old_value", TypeName = "jsonb")]
    public string? OldValue { get; set; }

    [Column("new_value", TypeName = "jsonb")]
    public string? NewValue { get; set; }

    /// <summary><see cref="TenantSettingStatuses"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = TenantSettingStatuses.Pending;

    [Required]
    [MaxLength(450)]
    [Column("requested_by")]
    public string RequestedBy { get; set; } = string.Empty;

    [MaxLength(450)]
    [Column("approved_by")]
    public string? ApprovedBy { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("effective_at")]
    public DateTime? EffectiveAt { get; set; }

    [MaxLength(1000)]
    [Column("reason")]
    public string? Reason { get; set; }

    [MaxLength(2000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TenantSettingType
{
    Currency = 0,
    Country = 1,
    Timezone = 2,
    FiscalSettings = 3,
}

public enum TenantSettingStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Reverted = 3,
}

/// <summary>Persisted <see cref="TenantSettingsHistory.SettingType"/> values (snake_case).</summary>
public static class TenantSettingTypes
{
    public const string Currency = "currency";
    public const string Country = "country";
    public const string Timezone = "timezone";
    public const string FiscalSettings = "fiscal_settings";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Currency,
        Country,
        Timezone,
        FiscalSettings,
    };

    public static string ToStorage(TenantSettingType type) =>
        type switch
        {
            TenantSettingType.Currency => Currency,
            TenantSettingType.Country => Country,
            TenantSettingType.Timezone => Timezone,
            TenantSettingType.FiscalSettings => FiscalSettings,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown setting type."),
        };

    public static bool TryParse(string? value, out TenantSettingType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case Currency:
                type = TenantSettingType.Currency;
                return true;
            case Country:
                type = TenantSettingType.Country;
                return true;
            case Timezone:
                type = TenantSettingType.Timezone;
                return true;
            case FiscalSettings:
                type = TenantSettingType.FiscalSettings;
                return true;
            default:
                return Enum.TryParse(value.Trim(), ignoreCase: true, out type)
                       && Enum.IsDefined(type);
        }
    }

    public static bool IsValid(string? value) => TryParse(value, out _);
}

/// <summary>Persisted <see cref="TenantSettingsHistory.Status"/> values (snake_case).</summary>
public static class TenantSettingStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Reverted = "reverted";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending,
        Approved,
        Rejected,
        Reverted,
    };

    public static string ToStorage(TenantSettingStatus status) =>
        status switch
        {
            TenantSettingStatus.Pending => Pending,
            TenantSettingStatus.Approved => Approved,
            TenantSettingStatus.Rejected => Rejected,
            TenantSettingStatus.Reverted => Reverted,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
        };

    public static bool TryParse(string? value, out TenantSettingStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case Pending:
                status = TenantSettingStatus.Pending;
                return true;
            case Approved:
                status = TenantSettingStatus.Approved;
                return true;
            case Rejected:
                status = TenantSettingStatus.Rejected;
                return true;
            case Reverted:
                status = TenantSettingStatus.Reverted;
                return true;
            default:
                return Enum.TryParse(value.Trim(), ignoreCase: true, out status)
                       && Enum.IsDefined(status);
        }
    }

    public static bool IsValid(string? value) => TryParse(value, out _);
}
