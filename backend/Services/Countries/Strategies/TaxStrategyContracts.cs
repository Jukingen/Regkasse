using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;

namespace KasseAPI_Final.Services.Countries.Strategies;

/// <summary>
/// One sale line as handed to <see cref="ITaxStrategy.CalculateTax"/>. Mirrors the two
/// <see cref="CartMoneyHelper.ComputeLine(decimal, int, int)"/> overloads: RKSV tax type
/// (POS catalog path) or VAT percent (receipt / category path).
/// </summary>
public sealed record TaxLineItemInput
{
    private TaxLineItemInput(decimal unitPriceGross, int quantity, int? taxType, decimal? vatRatePercent)
    {
        UnitPriceGross = unitPriceGross;
        Quantity = quantity;
        TaxType = taxType;
        VatRatePercent = vatRatePercent;
    }

    /// <summary>Gross unit price (Bruttopreis) — the repository-wide price model.</summary>
    public decimal UnitPriceGross { get; }

    public int Quantity { get; }

    /// <summary>RKSV tax type (<see cref="TaxTypes"/>). Null when <see cref="VatRatePercent"/> is used.</summary>
    public int? TaxType { get; }

    /// <summary>VAT percent (20, 10, …). Null when <see cref="TaxType"/> is used.</summary>
    public decimal? VatRatePercent { get; }

    public static TaxLineItemInput FromTaxType(decimal unitPriceGross, int quantity, int taxType) =>
        new(unitPriceGross, quantity, taxType, null);

    public static TaxLineItemInput FromVatPercent(decimal unitPriceGross, int quantity, decimal vatRatePercent) =>
        new(unitPriceGross, quantity, null, vatRatePercent);
}

/// <summary>Country and regime the calculation runs under. Resolved by <see cref="ITaxStrategyResolver"/>.</summary>
public sealed record TaxCalculationContext
{
    public required CountryProfile CountryProfile { get; init; }

    public required VatRegime VatRegime { get; init; }

    /// <summary>
    /// Mandant VAT exemption (<c>company_settings.tax_exempt</c>). The Austrian adapter ignores it:
    /// the live payment path has no exemption branch and this package must not introduce one.
    /// </summary>
    public bool TaxExempt { get; init; }
}

/// <summary>
/// Tax output built entirely from <see cref="CartMoneyHelper"/> records, so no rounding or
/// bucket rule is restated here.
/// </summary>
public sealed record TaxCalculationResult
{
    public required IReadOnlyList<CartMoneyHelper.LineAmounts> Lines { get; init; }

    public required IReadOnlyList<CartMoneyHelper.TaxSummaryLine> TaxSummary { get; init; }

    public required CartMoneyHelper.ReceiptTotals Totals { get; init; }

    /// <summary>
    /// <c>payment_details.tax_details</c> shape: RKSV tax type as string key → summed line VAT.
    /// Accumulated exactly like the live payment path (<c>PaymentService.CreatePaymentAsync</c>).
    /// </summary>
    public required IReadOnlyDictionary<string, decimal> TaxDetails { get; init; }
}

/// <summary>
/// Shape-only VAT-ID check result. Never a VIES registration answer, and never a normalization step:
/// the check is strict, so callers that accept user-typed input normalize before validating.
/// </summary>
public sealed record VatIdValidationResult
{
    /// <summary>Stable error code for API responses.</summary>
    public const string InvalidShapeErrorCode = "VAT_ID_SHAPE_INVALID";

    private VatIdValidationResult(bool isValid, string? vatId, string? errorCode)
    {
        IsValid = isValid;
        VatId = vatId;
        ErrorCode = errorCode;
    }

    public bool IsValid { get; }

    /// <summary>The accepted value, unchanged, when valid; otherwise null.</summary>
    public string? VatId { get; }

    public string? ErrorCode { get; }

    public static VatIdValidationResult Valid(string vatId) => new(true, vatId, null);

    public static VatIdValidationResult Invalid() => new(false, null, InvalidShapeErrorCode);
}

/// <summary>
/// Read-only projection of the tax fields an invoice or receipt must carry. Pure mapping of existing
/// <see cref="CompanySettings"/> / <see cref="Customer"/> values — no new fiscal rule.
/// </summary>
public sealed record InvoiceFieldRequirements
{
    /// <summary>Seller UID (<c>company_settings.company_tax_number</c>).</summary>
    public required string SellerVatId { get; init; }

    /// <summary>Operating country (<c>company_settings.country</c>).</summary>
    public required string SellerCountry { get; init; }

    /// <summary>Billing country when it differs from the operating country; otherwise null.</summary>
    public string? BillingCountry { get; init; }

    public required VatRegime VatRegime { get; init; }

    public required bool TaxExempt { get; init; }

    /// <summary>Customer tax number when present.</summary>
    public string? CustomerVatId { get; init; }

    /// <summary>
    /// Whether the country requires the customer VAT-ID on the document. False for Austria: POS
    /// receipts never require it. Reverse-charge and cross-border field rules are planned
    /// (see <c>docs/EINVOICING_EU.md</c>).
    /// </summary>
    public required bool RequiresCustomerVatId { get; init; }
}
