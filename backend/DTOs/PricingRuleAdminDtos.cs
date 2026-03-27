using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs;

public class PricingRuleAdminDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateOnly ValidFromDate { get; set; }
    public DateOnly ValidToDate { get; set; }
    public int DaysOfWeekMask { get; set; }
    public bool TimeWindowEnabled { get; set; }
    public int TimeStartMinutes { get; set; }
    public int TimeEndMinutes { get; set; }
    public PricingRuleTargetScope TargetScope { get; set; }
    public Guid TargetId { get; set; }
    public PricingRuleActionType ActionType { get; set; }
    public decimal ActionValue { get; set; }
    public Guid? CashRegisterId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class CreatePricingRuleRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int Priority { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    public DateOnly ValidFromDate { get; set; }

    [Required]
    public DateOnly ValidToDate { get; set; }

    [Range(1, 127)]
    public int DaysOfWeekMask { get; set; } = 0b1111111;

    public bool TimeWindowEnabled { get; set; }

    [Range(0, 1439)]
    public int TimeStartMinutes { get; set; }

    [Range(0, 1439)]
    public int TimeEndMinutes { get; set; } = 1439;

    [Required]
    public PricingRuleTargetScope TargetScope { get; set; }

    [Required]
    public Guid TargetId { get; set; }

    [Required]
    public PricingRuleActionType ActionType { get; set; }

    [Range(0, 999999.9999)]
    public decimal ActionValue { get; set; }

    public Guid? CashRegisterId { get; set; }
}

public class UpdatePricingRuleRequest : CreatePricingRuleRequest
{
}
