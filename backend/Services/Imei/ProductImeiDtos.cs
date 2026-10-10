using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Imei;

public static class ProductImeiErrorCodes
{
    public const string Duplicate = "IMEI_DUPLICATE";
    public const string TrackingDisabled = "IMEI_TRACKING_DISABLED";
    public const string Required = "IMEI_REQUIRED";
    public const string NotAllowed = "IMEI_NOT_ALLOWED";
    public const string NotAvailable = "IMEI_NOT_AVAILABLE";
}

public sealed class AdminProductImeiListItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Imei { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductImeiStatus Status { get; set; }

    public DateTime? SoldAtUtc { get; set; }
    public int WarrantyMonths { get; set; }
}

public sealed class ProductImeiDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Imei { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ProductImeiStatus Status { get; set; }

    public Guid? SoldPaymentId { get; set; }
    public int WarrantyMonths { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SoldAtUtc { get; set; }
}

public sealed class AddProductImeiRequest
{
    [Required]
    [MaxLength(20)]
    public string Imei { get; set; } = string.Empty;

    [Range(0, 120)]
    public int WarrantyMonths { get; set; }
}

public sealed class ProductImeiErrorDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
