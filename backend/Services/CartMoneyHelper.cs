using KasseAPI_Final.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace KasseAPI_Final.Services
{
    /// <summary>
    /// Cart fiyat/vergi hesaplamaları - TEK KAYNAK, tüm endpoint'ler bu motoru kullanır.
    /// İş kuralı: Product.Price = GROSS (Bruttopreis, inkl. MwSt.)
    /// Rounding: satır bazında; MidpointRounding.AwayFromZero; 2 ondalık (EUR).
    /// </summary>
    public static class CartMoneyHelper
    {
        private const int Decimals = 2;
        private static readonly MidpointRounding Rounding = MidpointRounding.AwayFromZero;

        /// <summary>Vergi oranı (taxType 1 → 0.20)</summary>
        public static decimal GetTaxRateFromType(int taxType) =>
            TaxTypes.GetTaxRate(taxType) / 100.0m;

        /// <summary>Yüzde oranı fraction'a çevirir (10 → 0.10). Tek rounding noktası: Round().</summary>
        public static decimal VatPercentToFraction(decimal vatRatePercent) =>
            Math.Round(vatRatePercent / 100m, 4);

        /// <summary>VAT yüzdesinden RKSV TaxType türetir (20→1, 10→2, 13→3, 0→4).</summary>
        public static int VatPercentToTaxType(decimal vatRatePercent)
        {
            var pct = Math.Round(vatRatePercent, 2);
            if (pct <= 0) return TaxTypes.ZeroRate;
            if (pct <= 10) return TaxTypes.Reduced;
            if (pct <= 13) return TaxTypes.Special;
            return TaxTypes.Standard;
        }

        public static decimal Round(decimal value) =>
            Math.Round(value, Decimals, Rounding);

        /// <summary>
        /// Satır seviyesi hesaplama - GROSS model, line-first rounding.
        /// lineGross = Round(unitGross * qty)
        /// lineNet = Round(lineGross / (1+rate))
        /// lineTax = Round(lineGross - lineNet)  → lineNet + lineTax = lineGross garanti
        /// </summary>
        public record LineAmounts(
            decimal UnitPriceGross,
            decimal LineGross,
            decimal LineNet,
            decimal LineTax,
            decimal TaxRate,
            int TaxType
        );

        public static LineAmounts ComputeLine(decimal unitGross, int quantity, int taxType)
        {
            var rate = GetTaxRateFromType(taxType);
            var lineGross = Round(unitGross * quantity);
            var lineNet = rate <= 0 ? lineGross : Round(lineGross / (1 + rate));
            var lineTax = Round(lineGross - lineNet);
            return new LineAmounts(unitGross, lineGross, lineNet, lineTax, rate, taxType);
        }

        /// <summary>
        /// Satır hesaplama – vergi oranı kategoriden (yüzde: 10, 20).
        /// Receipt/fatura tarafında VAT oranı category.VatRate ile kullanılır.
        /// </summary>
        public static LineAmounts ComputeLine(decimal unitGross, int quantity, decimal vatRatePercent)
        {
            var rate = VatPercentToFraction(vatRatePercent);
            var lineGross = Round(unitGross * quantity);
            var lineNet = rate <= 0 ? lineGross : Round(lineGross / (1 + rate));
            var lineTax = Round(lineGross - lineNet);
            var taxType = VatPercentToTaxType(vatRatePercent);
            return new LineAmounts(unitGross, lineGross, lineNet, lineTax, rate, taxType);
        }

        /// <summary>Net fiyattan gross hesaplama (TableOrder eski net verileri için)</summary>
        /// <param name="lineNet">Satır net tutarı</param>
        /// <param name="rate">0.20 gibi decimal oran</param>
        public static (decimal LineTax, decimal LineGross) ComputeGrossFromNet(decimal lineNet, decimal rate)
        {
            if (rate <= 0) return (0, lineNet);
            var lineTax = Round(lineNet * rate);
            var lineGross = Round(lineNet + lineTax);
            return (lineTax, lineGross);
        }

        /// <summary>Vergi grubu özeti - satır değerlerinin toplamı (header'da yeniden bölme YOK)</summary>
        public record TaxSummaryLine(
            int TaxType,
            decimal TaxRatePct,
            decimal NetAmount,
            decimal TaxAmount,
            decimal GrossAmount
        );

        public static List<TaxSummaryLine> BuildTaxSummaryFromLines(IEnumerable<LineAmounts> lines)
        {
            return lines
                .GroupBy(l => (l.TaxType, l.TaxRate))
                .Select(g =>
                {
                    var netAmount = g.Sum(l => l.LineNet);
                    var taxAmount = g.Sum(l => l.LineTax);
                    var grossAmount = g.Sum(l => l.LineGross);
                    return new TaxSummaryLine(
                        g.Key.TaxType,
                        TaxTypes.GetTaxRate(g.Key.TaxType),
                        netAmount,
                        taxAmount,
                        grossAmount
                    );
                })
                .OrderBy(t => t.TaxType)
                .ToList();
        }

        /// <summary>Cart totals - satır toplamlarından (header yeniden hesaplama yok)</summary>
        public record CartTotals(
            decimal SubtotalGross,
            decimal SubtotalNet,
            decimal IncludedTaxTotal,
            decimal GrandTotalGross,
            List<TaxSummaryLine> TaxSummary
        );

        /// <summary>Fiş toplamları: net, vergi, brüt. Deterministik (satır toplamlarından).</summary>
        public record ReceiptTotals(decimal TotalNet, decimal TotalVat, decimal TotalGross);

        /// <summary>VAT dökümü satırı: oran %, net, vergi, brüt.</summary>
        public record VatBreakdownLine(decimal VatRatePercent, decimal NetAmount, decimal VatAmount, decimal GrossAmount);

        /// <summary>Satır listesinden fiş toplamları ve VAT dökümü. Rounding tek noktada (satırda) yapıldığı için toplamlar tutarlı.</summary>
        public static (ReceiptTotals Totals, List<VatBreakdownLine> Breakdown) BuildReceiptTotalsAndBreakdown(IEnumerable<LineAmounts> lineAmounts)
        {
            var lines = lineAmounts.ToList();
            var totalNet = lines.Sum(l => l.LineNet);
            var totalVat = lines.Sum(l => l.LineTax);
            var totalGross = lines.Sum(l => l.LineGross);
            var summary = BuildTaxSummaryFromLines(lines);
            var breakdown = summary
                .Select(t => new VatBreakdownLine(t.TaxRatePct, t.NetAmount, t.TaxAmount, t.GrossAmount))
                .ToList();
            return (new ReceiptTotals(totalNet, totalVat, totalGross), breakdown);
        }

        public static CartTotals ComputeCartTotals(IEnumerable<LineAmounts> lineAmounts)
        {
            var lines = lineAmounts.ToList();
            var subtotalGross = lines.Sum(l => l.LineGross);
            var subtotalNet = lines.Sum(l => l.LineNet);
            var includedTaxTotal = lines.Sum(l => l.LineTax);
            var taxSummary = BuildTaxSummaryFromLines(lines);

            // Guard: satır toplamları tutarlı mı? (lineTax = lineGross - lineNet per line → sum exact)
            var check = subtotalNet + includedTaxTotal - subtotalGross;
            if (Math.Abs(check) > 0.01m)
            {
                // Log - production'da exception atmıyoruz
                System.Diagnostics.Debug.WriteLine(
                    $"[CartMoneyHelper] Totals inconsistency: subtotalNet+includedTaxTotal-subtotalGross={check}");
            }

            return new CartTotals(
                subtotalGross,
                subtotalNet,
                includedTaxTotal,
                subtotalGross, // grandTotalGross = subtotalGross (indirim/fee yoksa)
                taxSummary
            );
        }

        /// <summary>ILogger ile guard - opsiyonel</summary>
        public static void ValidateTotals(CartTotals totals, ILogger? logger = null)
        {
            var diff = Math.Abs((totals.SubtotalNet + totals.IncludedTaxTotal) - totals.SubtotalGross);
            if (diff > 0.01m)
                logger?.LogWarning("Cart totals inconsistency: diff={Diff}", diff);
            var taxSum = totals.TaxSummary.Sum(t => t.TaxAmount);
            var grossSum = totals.TaxSummary.Sum(t => t.GrossAmount);
            if (Math.Abs(taxSum - totals.IncludedTaxTotal) > 0.01m || Math.Abs(grossSum - totals.GrandTotalGross) > 0.01m)
                logger?.LogWarning("taxSummary vs cart totals mismatch");
        }
    }
}
