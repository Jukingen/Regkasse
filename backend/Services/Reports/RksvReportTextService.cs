using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services.Reports;

public interface IRksvReportTextService
{
    string Render(RksvReportTemplate template);

    string RenderTagesabschluss(
        TagesabschlussReportModel model,
        string environmentDisplay,
        string rksvFooter,
        string qrPayload,
        string? registerNumber = null);

    string RenderClosingReport(
        PosDailyClosingReportDto report,
        TagesabschlussCloudContext? cloudContext = null);

    Task<string> RenderReceiptAsync(
        ReceiptDTO receipt,
        CancellationToken cancellationToken = default);
}

public sealed class RksvReportTextService : IRksvReportTextService
{
    private readonly ITagesabschlussReportEnricher _contextEnricher;

    public RksvReportTextService(ITagesabschlussReportEnricher contextEnricher)
    {
        _contextEnricher = contextEnricher;
    }

    public string Render(RksvReportTemplate template) =>
        RksvReportTemplateRenderer.Render(template);

    public string RenderTagesabschluss(
        TagesabschlussReportModel model,
        string environmentDisplay,
        string rksvFooter,
        string qrPayload,
        string? registerNumber = null) =>
        Render(RksvReportTemplateMapper.FromTagesabschluss(
            model,
            environmentDisplay,
            rksvFooter,
            qrPayload,
            registerNumber));

    public string RenderClosingReport(
        PosDailyClosingReportDto report,
        TagesabschlussCloudContext? cloudContext = null) =>
        Render(RksvReportTemplateMapper.FromClosingReport(report, cloudContext));

    public async Task<string> RenderReceiptAsync(
        ReceiptDTO receipt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var (periodStart, periodEnd) = ResolveReceiptPeriod(receipt);
        var context = await _contextEnricher.BuildContextForRegisterAsync(
                receipt.CashRegisterId,
                periodStart,
                periodEnd,
                isSimulated: receipt.RksvFooterLabel.Contains("DEMO", StringComparison.OrdinalIgnoreCase),
                tseSignature: receipt.Signature?.SignatureValue,
                cancellationToken)
            .ConfigureAwait(false);

        return Render(RksvReportTemplateMapper.FromReceipt(receipt, context));
    }

    private static (DateTime? StartUtc, DateTime? EndUtc) ResolveReceiptPeriod(ReceiptDTO receipt)
    {
        if (receipt.Date == default)
            return (null, null);

        var issuedUtc = receipt.Date.Kind == DateTimeKind.Utc
            ? receipt.Date
            : DateTime.SpecifyKind(receipt.Date, DateTimeKind.Utc);

        return (issuedUtc, issuedUtc);
    }
}
