using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.Services.Billing;

#region Request DTOs

public record CreateLicenseSaleRequest
{
    [Required]
    public Guid TenantId { get; init; }

    [Required]
    public string LicensePlan { get; init; } = "12_months";

    public DateTime? CustomValidUntilUtc { get; init; }

    [Required]
    [Range(0.01, 999999.99)]
    public decimal PriceNet { get; init; }

    [Range(0, 100)]
    public decimal VatRate { get; init; } = 20.00m;

    public string? Notes { get; init; }
}

public record LicenseSalePreviewRequest
{
    [Required]
    public Guid TenantId { get; init; }

    [Required]
    public string LicensePlan { get; init; } = "12_months";

    public DateTime? CustomValidUntilUtc { get; init; }

    [Required]
    [Range(0.01, 999999.99)]
    public decimal PriceNet { get; init; }

    [Range(0, 100)]
    public decimal VatRate { get; init; } = 20.00m;
}

public record CancelLicenseSaleRequest
{
    [Required]
    [MinLength(10)]
    public string CancellationReason { get; init; } = string.Empty;
}

public record LicenseSaleListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? TenantId { get; init; }
    public string? Status { get; init; } // active, cancelled, refunded, all
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? Search { get; init; } // search by tenant name, slug, license key, invoice number
}

#endregion

#region Response DTOs

public record LicenseSalePreviewResponse
{
    public string LicenseKey { get; init; } = string.Empty;
    public DateTime ValidFromUtc { get; init; }
    public DateTime ValidUntilUtc { get; init; }
    public int DurationDays { get; init; }
    public string DurationDisplay { get; init; } = string.Empty;
    public decimal PriceNet { get; init; }
    public decimal VatRate { get; init; }
    public decimal VatAmount { get; init; }
    public decimal PriceGross { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string TenantSlug { get; init; } = string.Empty;
    public string? TenantAddress { get; init; }
    public string? TenantVatId { get; init; }
    public string? TenantEmail { get; init; }
    public string Currency { get; init; } = "EUR";
}

public record LicenseSaleResponse
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string TenantSlug { get; init; } = string.Empty;
    public string LicenseKey { get; init; } = string.Empty;
    public string LicensePlan { get; init; } = string.Empty;
    public DateTime ValidFromUtc { get; init; }
    public DateTime ValidUntilUtc { get; init; }
    public decimal PriceNet { get; init; }
    public decimal VatRate { get; init; }
    public decimal VatAmount { get; init; }
    public decimal PriceGross { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string InvoiceNumber { get; init; } = string.Empty;
    public string? InvoicePdfPath { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime SoldAtUtc { get; init; }
    public string SoldBy { get; init; } = string.Empty; // user display name
    public string? Notes { get; init; }
    public DateTime? CancelledAtUtc { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime? ActivationDateUtc { get; init; }
    public DateTime? LastExtendedAtUtc { get; init; }
    public string? ExtendedBy { get; init; }
}

public record LicenseSaleListResponse
{
    public List<LicenseSaleResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record LicenseSaleStatsResponse
{
    public decimal TotalRevenueNet { get; init; }
    public decimal TotalRevenueGross { get; init; }
    public decimal TotalVat { get; init; }
    public int TotalSales { get; init; }
    public int ActiveLicenses { get; init; }
    public int ExpiringSoonLicenses { get; init; } // ≤30 days
    public int ExpiredLicenses { get; init; }
    public int CancelledSales { get; init; }
    public decimal AveragePriceNet { get; init; }
    public int TotalTenantsWithLicense { get; init; }
}

#endregion

#region Tenant License DTOs

public record TenantLicenseStatus
{
    public bool IsValid { get; init; }
    public string Status { get; init; } = "none"; // valid, expired, trial, none
    public DateTime? ValidUntilUtc { get; init; }
    public int? DaysRemaining { get; init; }
    public string? LicenseKey { get; init; }
    public string? LicensePlan { get; init; }
    public bool IsExpiringSoon { get; init; } // ≤30 days
    public bool IsTrial { get; init; }
}

public record TenantLicenseInfo
{
    public TenantLicenseStatus Status { get; init; } = new();
    public LicenseSaleResponse? CurrentSale { get; init; }
    public List<LicenseSaleResponse> History { get; init; } = [];
    public DateTime? LastActivationUtc { get; init; }
    public int ActivationCount { get; init; }
}

public record ActivationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? LicenseKey { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public string? LicensePlan { get; init; }
    public LicenseSaleResponse? Sale { get; init; }
}

public record ExtendResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? LicenseKey { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public string? LicensePlan { get; init; }
    public LicenseSaleResponse? Sale { get; init; }
}

public record ExpiringLicenseInfo
{
    public Guid TenantId { get; init; }
    public string TenantName { get; init; } = string.Empty;
    public string TenantSlug { get; init; } = string.Empty;
    public string LicenseKey { get; init; } = string.Empty;
    public DateTime ValidUntilUtc { get; init; }
    public int DaysRemaining { get; init; }
    public Guid LicenseSaleId { get; init; }
    public string? TenantEmail { get; init; }
}

public record ExtendLicenseRequest
{
    [Required]
    public string LicenseKey { get; init; } = string.Empty;
}

#endregion

#region Internal DTOs

public record LicensePlanDefinition
{
    public string Plan { get; init; } = string.Empty;
    public int DurationDays { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}

#endregion
