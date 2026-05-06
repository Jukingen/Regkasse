using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class AdminVoucherListItemDto
{
    public Guid Id { get; init; }
    public string MaskedCode { get; init; } = string.Empty;
    public decimal InitialAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public string Currency { get; init; } = "EUR";
    public string Status { get; init; } = string.Empty;
    public DateTime ValidFromUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public string? CreatedByDisplayName { get; init; }
    public string? CreatedByEmail { get; init; }
    public IReadOnlyList<string>? CreatedByRoles { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class AdminVoucherDetailDto
{
    public Guid Id { get; init; }
    public string MaskedCode { get; init; } = string.Empty;
    public decimal InitialAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public string Currency { get; init; } = "EUR";
    public string Status { get; init; } = string.Empty;
    public DateTime ValidFromUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public string? CreatedByDisplayName { get; init; }
    public string? CreatedByEmail { get; init; }
    public IReadOnlyList<string>? CreatedByRoles { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public string? CancellationReason { get; init; }
    public string? InternalNote { get; init; }
}

public sealed class AdminVoucherLedgerLineDto
{
    public Guid Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
    public Guid? PaymentId { get; init; }
    public Guid? ReceiptId { get; init; }
    public string? ReceiptNumber { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public string? CreatedByDisplayName { get; init; }
    public string? CreatedByEmail { get; init; }
    public IReadOnlyList<string>? CreatedByRoles { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string? CorrelationId { get; init; }
}

public sealed class AdminVoucherListResponse
{
    public IReadOnlyList<AdminVoucherListItemDto> Items { get; init; } = Array.Empty<AdminVoucherListItemDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class CreateAdminVoucherRequest
{
    [Range(0.01, 999_999.99)]
    public decimal InitialAmount { get; set; }

    [Required]
    [MaxLength(3)]
    [MinLength(3)]
    public string Currency { get; set; } = "EUR";

    /// <summary>DefaultOneYear | Custom</summary>
    [Required]
    [MaxLength(32)]
    public string ExpiryMode { get; set; } = "DefaultOneYear";

    public DateTime? ExpiresAtUtc { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public sealed class CreateAdminVoucherResponse
{
    public Guid Id { get; init; }
    /// <summary>Plaintext code — returned once; never stored server-side.</summary>
    public string PlaintextCode { get; init; } = string.Empty;
    public string MaskedCode { get; init; } = string.Empty;
    public decimal InitialAmount { get; init; }
    public string Currency { get; init; } = "EUR";
    public DateTime ValidFromUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
}

public sealed class CancelAdminVoucherRequest
{
    [Required]
    [MinLength(5)]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Admin compares a customer-provided code to a voucher row (hash-only storage; plaintext is never persisted).</summary>
public sealed class VerifyAdminVoucherCodeRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(128)]
    public string Code { get; set; } = string.Empty;
}

public sealed class VerifyAdminVoucherCodeResponse
{
    /// <summary>True when normalized code hash equals this voucher's stored hash.</summary>
    public bool Matches { get; init; }
}
