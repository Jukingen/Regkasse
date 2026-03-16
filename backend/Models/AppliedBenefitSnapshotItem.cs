using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models
{
    /// <summary>
    /// Value shape for a single applied customer benefit stored in PaymentDetails.AppliedBenefitsSnapshot.
    /// Used for audit and receipt display; not an EF entity. Supports percentage discount,
    /// free daily allowance, and buy-X-get-Y loyalty benefits.
    /// </summary>
    public class AppliedBenefitSnapshotItem
    {
        [JsonPropertyName("kind")]
        public AppliedBenefitKind Kind { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>Discount or benefit amount (negative for reduction).</summary>
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        /// <summary>Optional quantity for free-allowance or buy-X-get-Y (e.g. free items granted).</summary>
        [JsonPropertyName("quantity")]
        public int? Quantity { get; set; }
    }

    /// <summary>
    /// Benefit types supported in applied-benefit snapshot. Extensible for future handlers.
    /// </summary>
    public enum AppliedBenefitKind
    {
        PercentageDiscount = 0,
        FreeAllowance = 1,
        BuyXGetY = 2
    }
}
