using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Limits;

namespace KasseAPI_Final.DTOs;

/// <summary>Super Admin payload to patch a mandant's operational caps. Omitted fields keep current values.</summary>
public sealed class UpdateTenantLimitsRequest
{
    [Range(1, 1_000_000)]
    public int? MaxActiveRegistersPerUser { get; set; }

    [Range(1, 1_000_000)]
    public int? MaxProductsPerTenant { get; set; }

    [Range(1, 1_000_000)]
    public int? MaxUsersPerTenant { get; set; }

    [Range(1, 1_000_000)]
    public int? DailyMaxTransactions { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? MaxTransactionAmount { get; set; }

    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal? DailyMaxRevenue { get; set; }

    [Range(1, 1_000_000)]
    public int? MaxBackupsPerTenant { get; set; }

    [Range(1, 1_000_000)]
    [JsonPropertyName("maxBackupSizeMB")]
    public int? MaxBackupSizeMb { get; set; }

    [Range(1, 1_000_000)]
    public int? MaxOfflineTransactions { get; set; }
}

/// <summary>Admin FA projection of <see cref="TenantLimits"/>.</summary>
public sealed class TenantLimitsDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public int MaxActiveRegistersPerUser { get; init; }
    public int MaxProductsPerTenant { get; init; }
    public int MaxUsersPerTenant { get; init; }
    public int DailyMaxTransactions { get; init; }
    public decimal MaxTransactionAmount { get; init; }
    public decimal DailyMaxRevenue { get; init; }
    public int MaxBackupsPerTenant { get; init; }
    [JsonPropertyName("maxBackupSizeMB")]
    public int MaxBackupSizeMb { get; init; }
    public int MaxOfflineTransactions { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static TenantLimitsDto FromEntity(TenantLimits row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return new TenantLimitsDto
        {
            Id = row.Id,
            TenantId = row.TenantId,
            MaxActiveRegistersPerUser = row.MaxActiveRegistersPerUser,
            MaxProductsPerTenant = row.MaxProductsPerTenant,
            MaxUsersPerTenant = row.MaxUsersPerTenant,
            DailyMaxTransactions = row.DailyMaxTransactions,
            MaxTransactionAmount = row.MaxTransactionAmount,
            DailyMaxRevenue = row.DailyMaxRevenue,
            MaxBackupsPerTenant = row.MaxBackupsPerTenant,
            MaxBackupSizeMb = row.MaxBackupSizeMb,
            MaxOfflineTransactions = row.MaxOfflineTransactions,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };
    }
}

/// <summary>Current-tenant caps plus live usage for FA warnings (Manager + Super Admin).</summary>
public sealed class TenantLimitUsageDto
{
    public Guid TenantId { get; init; }
    public required TenantLimitsDto Limits { get; init; }
    public int CurrentProducts { get; init; }
    public int CurrentUsers { get; init; }
    public int CurrentDailyTransactions { get; init; }
    public decimal CurrentDailyRevenue { get; init; }
    public int CurrentBackups { get; init; }
    public decimal CurrentBackupSizeMb { get; init; }
    public int CurrentOfflineTransactions { get; init; }
    /// <summary>
    /// Peak count of non-decommissioned registers assigned to any single user (for maxActiveRegistersPerUser).
    /// </summary>
    public int CurrentMaxAssignedRegistersPerUser { get; init; }
}

/// <summary>Aggregated limit usage, critical users, and recent activity for FA.</summary>
public sealed class LimitDashboardDto
{
    public DateTime LastUpdated { get; init; }
    public DashboardSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<LimitStatusDto> Limits { get; init; } = [];
    public IReadOnlyList<CriticalUserDto> CriticalUsers { get; init; } = [];
    public IReadOnlyList<LimitActivityDto> RecentActivity { get; init; } = [];
    public int TotalViolations { get; init; }
    public int ApproachingLimits { get; init; }
    public int UnreadAlertCount { get; init; }
    public bool AllTenants { get; init; }
}

public sealed class DashboardSummaryDto
{
    public int Total { get; init; }
    public int Healthy { get; init; }
    public int Warning { get; init; }
    public int Critical { get; init; }
}

public sealed class LimitStatusDto
{
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? TenantSlug { get; init; }
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int Current { get; init; }
    public int Limit { get; init; }
    public double Percentage { get; init; }
    /// <summary><see cref="LimitUsageStatuses.Healthy"/>, <see cref="LimitUsageStatuses.Warning"/>, or <see cref="LimitUsageStatuses.Critical"/>.</summary>
    public string Status { get; init; } = LimitUsageStatuses.Healthy;
    /// <summary><see cref="LimitUsageStatuses.Increasing"/>, <see cref="LimitUsageStatuses.Stable"/>, or <see cref="LimitUsageStatuses.Decreasing"/>.</summary>
    public string Trend { get; init; } = LimitUsageStatuses.Stable;
    public int ChangeCount { get; init; }
    public string ChangeUnit { get; init; } = string.Empty;
}

public sealed class CriticalUserDto
{
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? TenantSlug { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string LimitKey { get; init; } = string.Empty;
    public int Current { get; init; }
    public int Limit { get; init; }
    public double Percentage { get; init; }
    /// <summary><see cref="LimitUsageStatuses.Approaching"/>, <see cref="LimitUsageStatuses.Full"/>, or <see cref="LimitUsageStatuses.Exceeded"/>.</summary>
    public string Status { get; init; } = LimitUsageStatuses.Approaching;
    public string RecommendedAction { get; init; } = string.Empty;
}

public sealed class LimitActivityDto
{
    public Guid Id { get; init; }
    public DateTime TimestampUtc { get; init; }
    public Guid TenantId { get; init; }
    public string? TenantName { get; init; }
    public string? TenantSlug { get; init; }
    public string LimitKey { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public bool IsRead { get; init; }
}

public static class LimitUsageStatuses
{
    public const string Healthy = "Healthy";
    public const string Warning = "Warning";
    public const string Critical = "Critical";
    public const string Approaching = "Approaching";
    public const string Full = "Full";
    public const string Exceeded = "Exceeded";
    public const string Increasing = "Increasing";
    public const string Stable = "Stable";
    public const string Decreasing = "Decreasing";
    public const decimal ApproachingRatio = 0.80m;
}

/// <summary>Canonical HTTP 409 body for <see cref="LimitExceededException"/>.</summary>
public sealed class LimitErrorDto
{
    public string Code { get; init; } = LimitExceededException.ErrorCodeValue;
    public string LimitKey { get; init; } = string.Empty;
    public int Limit { get; init; }
    public int Current { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool CanForce { get; init; }

    public const char ServiceErrorSeparator = '|';

    public static string FormatServiceError(LimitExceededException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        return string.Join(
            ServiceErrorSeparator,
            LimitExceededException.ErrorCodeValue,
            ex.LimitKey,
            ex.Limit.ToString(CultureInfo.InvariantCulture),
            ex.CurrentValue.ToString(CultureInfo.InvariantCulture),
            ex.Message.Replace(ServiceErrorSeparator, ' '));
    }

    public static bool TryParseServiceError(string? error, [NotNullWhen(true)] out LimitErrorDto? dto)
    {
        dto = null;
        if (string.IsNullOrWhiteSpace(error)
            || !error.StartsWith(LimitExceededException.ErrorCodeValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = error.Split(ServiceErrorSeparator, 5);
        if (parts.Length == 5
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
            && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var current))
        {
            dto = new LimitErrorDto
            {
                Code = LimitExceededException.ErrorCodeValue,
                LimitKey = parts[1],
                Limit = limit,
                Current = current,
                Message = parts[4],
                CanForce = string.Equals(
                    parts[1],
                    TenantLimitKeys.MaxActiveRegistersPerUser,
                    StringComparison.Ordinal),
            };
            return true;
        }

        var message = error.Contains(':')
            ? error[(error.IndexOf(':') + 1)..].Trim()
            : error;
        dto = new LimitErrorDto
        {
            Code = LimitExceededException.ErrorCodeValue,
            Message = message,
        };
        return true;
    }
}
