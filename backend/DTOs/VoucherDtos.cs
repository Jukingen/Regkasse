using System.ComponentModel.DataAnnotations;

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
    [Required]
    [MinLength(3)]
    [MaxLength(128)]
    public string VoucherCode { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal? Amount { get; set; }
}

public sealed class VoucherValidateResponse
{
    public bool Ok { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }

    public string? Status { get; init; }
    public decimal? RemainingAmount { get; init; }
    public decimal? MaxRedeemableAmount { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public string? MaskedCode { get; init; }

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
