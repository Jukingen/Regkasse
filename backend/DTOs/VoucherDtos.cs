using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.DTOs;

public static class VoucherValidateErrorCodes
{
    public const string InvalidCode = "INVALID_CODE";
    public const string NotFound = "NOT_FOUND";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";
    public const string Redeemed = "REDEEMED";
    public const string NotYetValid = "NOT_YET_VALID";
    public const string NoBalance = "NO_BALANCE";
    public const string WrongTenant = "WRONG_TENANT";
    public const string AmountExceedsBalance = "AMOUNT_EXCEEDS_BALANCE";
    public const string NotRedeemable = "NOT_REDEEMABLE";
}

/// <summary>POS: validate Gutschein code before payment.</summary>
public class ValidateVoucherRequest
{
    private string _voucherCode = string.Empty;

    [Required]
    [MinLength(3)]
    [MaxLength(128)]
    [JsonPropertyName("voucherCode")]
    public string VoucherCode
    {
        get => _voucherCode;
        set => _voucherCode = (value ?? string.Empty).Trim();
    }

    /// <summary>Alias for <see cref="VoucherCode"/> (API spec: <c>code</c>).</summary>
    [JsonPropertyName("code")]
    public string? Code
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(_voucherCode))
                _voucherCode = value.Trim();
        }
    }

    [Range(0.01, double.MaxValue)]
    public decimal? Amount { get; set; }
}

/// <summary>Compact POS validation result (subset of <see cref="VoucherValidateResponse"/>).</summary>
public sealed class VoucherValidationResult
{
    public bool IsValid { get; init; }
    public decimal RemainingAmount { get; init; }
    public string Code { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }

    public static VoucherValidationResult FromSuccess(VoucherValidateResponse response) => new()
    {
        IsValid = true,
        RemainingAmount = response.RemainingAmount ?? 0,
        Code = response.MaskedCode ?? string.Empty,
        ExpiresAt = response.ExpiresAtUtc ?? DateTime.UtcNow,
    };
}

public sealed class VoucherValidateResponse
{
    public bool Ok { get; init; }

    [JsonPropertyName("isValid")]
    public bool IsValid => Ok;

    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public string? Status { get; init; }
    public decimal? RemainingAmount { get; init; }
    public decimal? MaxRedeemableAmount { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt => ExpiresAtUtc;

    public string? MaskedCode { get; init; }

    [JsonPropertyName("code")]
    public string? Code => MaskedCode;

    public static VoucherValidateResponse Valid(
        string status,
        decimal remainingAmount,
        decimal maxRedeemableAmount,
        DateTime expiresAtUtc,
        string maskedCode) =>
        new()
        {
            Ok = true,
            Status = status,
            RemainingAmount = remainingAmount,
            MaxRedeemableAmount = maxRedeemableAmount,
            ExpiresAtUtc = expiresAtUtc,
            MaskedCode = maskedCode
        };

    public static VoucherValidateResponse NotFound() =>
        new()
        {
            Ok = false,
            ErrorCode = VoucherValidateErrorCodes.NotFound,
            Message = "Voucher not found or not valid for this location."
        };

    public static VoucherValidateResponse Fail(string code, string message) =>
        new()
        {
            Ok = false,
            ErrorCode = code,
            Message = message
        };
}

/// <summary>Single line in a multi-voucher payment (same transaction).</summary>
public class VoucherRedemptionRequestItem
{
    [Required]
    [MinLength(3)]
    [MaxLength(128)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
}
