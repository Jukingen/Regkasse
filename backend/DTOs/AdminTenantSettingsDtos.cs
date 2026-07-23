using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class RequestTenantSettingsChangeDto
{
    [Required]
    public Guid TenantId { get; set; }

    /// <summary>currency | country | timezone | fiscal_settings</summary>
    [Required]
    [MaxLength(32)]
    public string SettingType { get; set; } = string.Empty;

    /// <summary>Scalar string (currency/country/timezone) or fiscal settings object.</summary>
    [Required]
    public JsonElement NewValue { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class RejectTenantSettingsChangeDto
{
    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class RevertTenantSettingsChangeDto
{
    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class TenantSettingsHistoryDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string SettingType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? EffectiveAt { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CurrentTenantSettingsDto
{
    public Guid TenantId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public FiscalSettingsValueDto FiscalSettings { get; set; } = new();

    /// <summary>True when TSE-signed payments exist for this tenant (RKSV fiscal footprint).</summary>
    public bool HasFiscalData { get; set; }

    /// <summary>True when any invoice rows exist for this tenant.</summary>
    public bool HasInvoices { get; set; }
}

public sealed class FiscalSettingsValueDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public string CompanyTaxNumber { get; set; } = string.Empty;
    public string? CompanyVatNumber { get; set; }
    public string? CompanyRegistrationNumber { get; set; }
}

public sealed class SettingsChangeResultDto
{
    public bool Succeeded { get; set; }
    public Guid? ChangeId { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public string? Warning { get; set; }
}

public static class TenantSettingsHistoryMapping
{
    public static TenantSettingsHistoryDto ToDto(TenantSettingsHistory entity) =>
        new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SettingType = entity.SettingType,
            OldValue = entity.OldValue,
            NewValue = entity.NewValue,
            Status = entity.Status,
            RequestedBy = entity.RequestedBy,
            ApprovedBy = entity.ApprovedBy,
            RequestedAt = entity.RequestedAt,
            ApprovedAt = entity.ApprovedAt,
            EffectiveAt = entity.EffectiveAt,
            Reason = entity.Reason,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
        };
}
