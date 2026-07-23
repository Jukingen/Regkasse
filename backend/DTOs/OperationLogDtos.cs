using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

public class OperationLogListItemDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserEmail { get; set; }
    public string? UserDisplayName { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public bool IsUndone { get; set; }
    public DateTime? UndoneAt { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool CanUndo { get; set; }
}

public class OperationLogDetailDto : OperationLogListItemDto
{
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? UndoneBy { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public sealed class OperationLogListResponseDto
{
    public IReadOnlyList<OperationLogListItemDto> Items { get; set; } = Array.Empty<OperationLogListItemDto>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public sealed class UndoOperationRequest
{
    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed class UndoOperationResponse
{
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? OperationId { get; set; }
}

/// <summary>JSON snapshot for product undo restore.</summary>
public sealed class ProductOperationSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameDe { get; set; }
    public string? NameEn { get; set; }
    public string? NameTr { get; set; }
    public string? Description { get; set; }
    public string? DescriptionDe { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionTr { get; set; }
    public decimal Price { get; set; }
    public int TaxType { get; set; }
    public decimal TaxRate { get; set; }
    public Guid CategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsFiscalCompliant { get; set; }
    public bool IsTaxable { get; set; }
    public string? FiscalCategoryCode { get; set; }
    public string? TaxExemptionReason { get; set; }
    public string RksvProductType { get; set; } = "Standard";
}

/// <summary>JSON snapshot for customer undo restore.</summary>
public sealed class CustomerOperationSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string TaxNumber { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsVip { get; set; }
    public bool IsActive { get; set; }
    public decimal DiscountPercentage { get; set; }
}

/// <summary>JSON snapshot for category create undo (soft-deactivate).</summary>
public sealed class CategoryOperationSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>JSON snapshot for voucher create undo (cancel unused).</summary>
public sealed class VoucherOperationSnapshot
{
    public Guid Id { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal InitialAmount { get; set; }
    public int Status { get; set; }
}
