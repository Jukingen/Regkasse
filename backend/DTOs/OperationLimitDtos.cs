using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.DTOs;

public sealed class OperationLimitCheckResult
{
    public bool IsAllowed { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }
    public int Limit { get; init; }
    public int Current { get; init; }
    public int Remaining { get; init; }
    public DateTime? ResetAt { get; init; }
    public bool RequiresApproval { get; init; }
    public TenantOperationLimitKind? Kind { get; init; }

    public static OperationLimitCheckResult Allow(
        TenantOperationLimitKind kind,
        int limit,
        int current,
        DateTime resetAt) =>
        new()
        {
            IsAllowed = true,
            Kind = kind,
            Limit = limit,
            Current = current,
            Remaining = Math.Max(0, limit - current),
            ResetAt = resetAt,
        };

    public static OperationLimitCheckResult Deny(
        TenantOperationLimitKind kind,
        string code,
        string message,
        int limit,
        int current,
        DateTime resetAt,
        bool requiresApproval = false) =>
        new()
        {
            IsAllowed = false,
            Code = code,
            Message = message,
            Kind = kind,
            Limit = limit,
            Current = current,
            Remaining = Math.Max(0, limit - current),
            ResetAt = resetAt,
            RequiresApproval = requiresApproval,
        };
}

public sealed class OperationLimitStatusDto
{
    public bool Enabled { get; set; }
    public int MaxBulkDeletePerDay { get; set; }
    public int MaxPriceUpdatePerHour { get; set; }
    public int MaxProductCreatePerDay { get; set; }
    public int MaxUserCreatePerDay { get; set; }
    public int MaxBackupPerDay { get; set; }
    public int MaxExportPerDay { get; set; }
    public int RequireApprovalForBulkDelete { get; set; }
    public int RequireApprovalForPriceUpdate { get; set; }
    public int BulkDeleteUsedToday { get; set; }
    public int BulkDeleteRemainingToday { get; set; }
    public DateTime BulkDeleteResetAtUtc { get; set; }
    public int PriceUpdateUsedThisHour { get; set; }
    public int PriceUpdateRemainingThisHour { get; set; }
    public DateTime PriceUpdateResetAtUtc { get; set; }
}
