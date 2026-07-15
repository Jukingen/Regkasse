using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Reports;

internal static class RksvSpecialReceiptReportSupport
{
    public static Task<string> RenderAsync(
        IRksvReportTextService reportText,
        ReceiptDTO receipt,
        string expectedKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedKind);

        var actual = receipt.RksvSpecialReceiptKind?.Trim();
        if (!string.Equals(actual, expectedKind, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Receipt kind '{actual ?? "(none)"}' does not match expected '{expectedKind}'.",
                nameof(receipt));
        }

        return reportText.RenderReceiptAsync(receipt, cancellationToken);
    }
}
