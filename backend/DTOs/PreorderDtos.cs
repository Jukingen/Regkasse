using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public sealed class PreorderDto
{
    public Guid Id { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string? ReceiptNumber { get; init; }
    public string? PreorderNumber { get; init; }
    public Guid? SourcePaymentId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public string? CustomerNotes { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount { get; init; }
    public DateTime OrderDate { get; init; }
    public DateTime? PickupDeadlineUtc { get; init; }
    public int PickupWeeks { get; init; }
    public DateTime? ReadyAtUtc { get; init; }
    public DateTime? CollectedAtUtc { get; init; }
}

public sealed class PreorderListResponseDto
{
    public int Pending { get; init; }
    public int Ready { get; init; }
    public int Collected { get; init; }
    public int Cancelled { get; init; }
    public IReadOnlyList<PreorderDto> Orders { get; init; } = [];
}

public sealed class PreorderStatsDto
{
    public int Pending { get; init; }
    public int Ready { get; init; }
    public int Collected { get; init; }
    public int Cancelled { get; init; }
}

public sealed class UpdatePreorderStatusRequest
{
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    /// <summary>Required when <see cref="Status"/> is cancelled; forwarded to existing fiscal storno.</summary>
    [MaxLength(500)]
    public string? CancellationReason { get; set; }
}

public sealed class PreorderSettingsDto
{
    [Range(PreorderPolicyDefaults.MinPickupDeadlineWeeks, PreorderPolicyDefaults.MaxPickupDeadlineWeeks)]
    public int PickupDeadlineWeeks { get; set; } = PreorderPolicyDefaults.PickupDeadlineWeeks;

    [MaxLength(500)]
    public string CancellationPolicyText { get; set; } = PreorderPolicyDefaults.CancellationPolicyText;
}

public sealed class PreorderBalanceGuardResult
{
    public bool Ok { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }

    public static PreorderBalanceGuardResult Success() => new() { Ok = true };

    public static PreorderBalanceGuardResult Fail(string code, string message) =>
        new() { Ok = false, Code = code, Message = message };
}
