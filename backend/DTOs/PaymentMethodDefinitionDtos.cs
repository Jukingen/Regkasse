using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

/// <summary>POS GET /payment/methods row — type is the stable code sent back in POST payment.method.</summary>
public sealed record PosPaymentMethodDto(
    string Id,
    string Name,
    string Type,
    string Icon,
    bool IsDefault,
    bool RequiresReceivedAmount,
    bool RequiresTerminal,
    string? TerminalType,
    bool AllowRefund);

public sealed class PaymentMethodDefinitionAdminDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }
    public int LegacyPaymentMethodValue { get; set; }
    public string? FiscalCategory { get; set; }
    public bool RequiresTerminal { get; set; }
    public string? TerminalType { get; set; }
    public bool AllowRefund { get; set; }
    public string? Icon { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class CreatePaymentMethodDefinitionRequest
{
    [Required]
    [MaxLength(64)]
    [RegularExpression(@"^[a-z0-9_-]+$", ErrorMessage = "Code must be lowercase letters, digits, underscore or hyphen.")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 5)]
    public int LegacyPaymentMethodValue { get; set; }

    [MaxLength(64)]
    public string? FiscalCategory { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDefault { get; set; }

    public int DisplayOrder { get; set; }

    public bool RequiresTerminal { get; set; }

    [MaxLength(64)]
    public string? TerminalType { get; set; }

    public bool AllowRefund { get; set; } = true;

    [MaxLength(64)]
    public string? Icon { get; set; }

    public string? MetadataJson { get; set; }
}

public sealed class UpdatePaymentMethodDefinitionRequest : CreatePaymentMethodDefinitionRequest
{
}
