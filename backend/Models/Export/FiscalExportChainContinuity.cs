namespace KasseAPI_Final.Models.Export;

/// <summary>
/// Diagnostic only: compares consecutive exported receipts in order (PrevSignatureValue[i] vs SignatureValue[i-1]).
/// Does not validate against receipts outside the export list; observed-within-scope, not a chain guarantee.
/// </summary>
public static class FiscalExportChainContinuity
{
    /// <summary>Returns diagnostic warnings for adjacency in the given list only; not proof of full chain integrity.</summary>
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
