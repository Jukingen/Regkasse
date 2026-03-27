using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.DTOs
{
    /// <summary>
    /// Request for POS benefit eligibility preview. Distinct from assignment summary (GET benefit-summary).
    /// Cart-like input: product and quantity only; prices and categories are resolved server-side.
    /// </summary>
    public class BenefitEligibilityPreviewRequest
    {
        [Required]
        public Guid CustomerId { get; set; }

        /// <summary>Cart line items: product and quantity. Tax/category resolved from product.</summary>
        public List<BenefitEligibilityPreviewItemRequest> Items { get; set; } = new();

        /// <summary>Optional: kasa kapsamlı fiyat kuralları için POS kasa kimliği.</summary>
        public Guid? CashRegisterId { get; set; }
    }

    /// <summary>
    /// Single line for eligibility preview (minimal; no payment or TSE fields).
    /// </summary>
    public class BenefitEligibilityPreviewItemRequest
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }
    }

    /// <summary>
    /// Response for benefit eligibility preview. Semantics: payment applicability for this cart/customer, not assignment count.
    /// </summary>
    public class BenefitEligibilityPreviewResponse
    {
        /// <summary>Subtotal before benefits (cart total from resolved product prices).</summary>
        public decimal SubtotalBeforeBenefits { get; set; }

        /// <summary>Total discount amount that would be applied at payment (negative value as discount).</summary>
        public decimal TotalDiscountAmount { get; set; }

        /// <summary>Subtotal after benefits (SubtotalBeforeBenefits + TotalDiscountAmount; TotalDiscountAmount is negative).</summary>
        public decimal SubtotalAfterBenefits { get; set; }

        /// <summary>Benefits that would apply for this cart/customer.</summary>
        public List<ApplicableBenefitPreviewDto> ApplicableBenefits { get; set; } = new();

        /// <summary>Assigned benefits that are blocked and why (e.g. daily limit, quantity not reached).</summary>
        public List<BlockedBenefitPreviewDto> BlockedBenefits { get; set; } = new();
    }

    /// <summary>
    /// One benefit that would be applied (same semantics as AppliedBenefitSnapshotItem at payment time).
    /// </summary>
    public class ApplicableBenefitPreviewDto
    {
        public AppliedBenefitKind Kind { get; set; }
        public string Description { get; set; } = string.Empty;
        /// <summary>Discount amount (negative).</summary>
        public decimal Amount { get; set; }
        public int? Quantity { get; set; }
        public string? BenefitDefinitionCode { get; set; }
    }

    /// <summary>
    /// Assigned benefit that would not apply for this cart, with reason.
    /// </summary>
    public class BlockedBenefitPreviewDto
    {
        public AppliedBenefitKind Kind { get; set; }
        public string? BenefitDefinitionCode { get; set; }
        /// <summary>Machine-readable reason code for POS/UI.</summary>
        public string BlockedReasonCode { get; set; } = string.Empty;
        /// <summary>Optional human-readable message (English; UI may translate).</summary>
        public string? Message { get; set; }
        /// <summary>For QuantityNotReached: how many more units needed to unlock.</summary>
        public int? RequiredMoreQuantity { get; set; }
    }

    /// <summary>
    /// Well-known blocked reason codes for eligibility preview. English; stable for API contract.
    /// </summary>
    public static class BenefitBlockedReasonCodes
    {
        /// <summary>FreeAllowance: daily usage limit already reached.</summary>
        public const string DailyLimitReached = "DailyLimitReached";

        /// <summary>FreeAllowance: no cart items in the benefit's allowance category.</summary>
        public const string NoEligibleItems = "NoEligibleItems";

        /// <summary>BuyXGetY: cart quantity below required buy-X threshold.</summary>
        public const string QuantityNotReached = "QuantityNotReached";
    }
}
