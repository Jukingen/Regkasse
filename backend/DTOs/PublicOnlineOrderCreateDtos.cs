using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>Customer website/app order placement (non-fiscal). Gated by working hours.</summary>
public sealed class CreatePublicOnlineOrderRequestDto
{
    [Required]
    [MaxLength(80)]
    public string Tenant { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(256)]
    [EmailAddress]
    public string? CustomerEmail { get; set; }

    /// <summary><c>takeaway</c> | <c>delivery</c> | <c>dine-in</c></summary>
    [MaxLength(20)]
    public string OrderType { get; set; } = "takeaway";

    [MaxLength(500)]
    public string? DeliveryAddress { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary><c>cash</c> | <c>card</c> | <c>online</c></summary>
    [MaxLength(20)]
    public string PaymentMethod { get; set; } = "cash";

    /// <summary><c>web</c> | <c>pwa</c> | <c>native</c></summary>
    [MaxLength(20)]
    public string Source { get; set; } = "web";

    [Required]
    [MinLength(1)]
    public List<CreatePublicOnlineOrderItemDto> Items { get; set; } = new();
}

public sealed class CreatePublicOnlineOrderItemDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 99)]
    public int Quantity { get; set; } = 1;
}

public sealed class CreatePublicOnlineOrderResponseDto
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }
    public Guid? OrderId { get; init; }
    public string? OrderNumber { get; init; }
    public decimal? Total { get; init; }
    public string? Message { get; init; }
}
