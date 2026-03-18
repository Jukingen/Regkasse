namespace KasseAPI_Final.Models.Export;

/// <summary>Validates receipt-to-receipt signature linkage for export packages (best-effort).</summary>
public static class FiscalExportChainContinuity
{
    public static IReadOnlyList<string> BuildWarnings(IReadOnlyList<FiscalReceiptChainLinkDto> chainLinks)
    {
        if (chainLinks == null || chainLinks.Count < 2)
            return Array.Empty<string>();

        var warnings = new List<string>();
        for (var i = 1; i < chainLinks.Count; i++)
        {
            var prevSig = chainLinks[i - 1].SignatureValue ?? string.Empty;
            var currPrev = chainLinks[i].PrevSignatureValue ?? string.Empty;
            if (string.IsNullOrEmpty(prevSig) && string.IsNullOrEmpty(currPrev))
                continue;
            if (!string.Equals(prevSig, currPrev, StringComparison.Ordinal))
            {
                warnings.Add(
                    $"Receipt {chainLinks[i].ReceiptNumber}: PrevSignatureValue does not match previous receipt SignatureValue in export order.");
            }
        }

        return warnings;
    }
}
