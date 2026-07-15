using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Reports;

public interface IMonatsbelegReportService
{
    Task<PosDailyClosingReportDto> ComposeReportDtoAsync(
        Monatsbeleg closing,
        MonatsbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        DailyClosing? linkedDailyClosing = null,
        CancellationToken cancellationToken = default);

    Task<string> GeneratePlainTextReportAsync(
        Monatsbeleg closing,
        MonatsbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        DailyClosing? linkedDailyClosing = null,
        CancellationToken cancellationToken = default);

    string GeneratePlainTextReport(
        PosDailyClosingReportDto report,
        TagesabschlussCloudContext? cloudContext = null);
}

public sealed class MonatsbelegReportService : IMonatsbelegReportService
{
    private readonly ITagesabschlussReportEnricher _enricher;
    private readonly IRksvReportTextService _reportText;

    public MonatsbelegReportService(
        ITagesabschlussReportEnricher enricher,
        IRksvReportTextService reportText)
    {
        _enricher = enricher;
        _reportText = reportText;
    }

    public async Task<PosDailyClosingReportDto> ComposeReportDtoAsync(
        Monatsbeleg closing,
        MonatsbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        DailyClosing? linkedDailyClosing = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(closing);
        ArgumentNullException.ThrowIfNull(summary);

        var (periodStartUtc, periodEndUtc) = RksvClosingPeriodHelper.MonthUtcRange(closing.Year, closing.Month);
        var cloudContext = await _enricher.BuildContextForRegisterAsync(
                closing.CashRegisterId,
                periodStartUtc,
                periodEndUtc,
                fiscalEnvironment.IsDemoFiscal || closing.IsSimulated,
                closing.TseSignature,
                cancellationToken)
            .ConfigureAwait(false);

        return MonatsbelegReportComposer.Compose(
            closing,
            summary,
            registerNumber,
            fiscalEnvironment,
            cloudContext,
            linkedDailyClosing);
    }

    public async Task<string> GeneratePlainTextReportAsync(
        Monatsbeleg closing,
        MonatsbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        DailyClosing? linkedDailyClosing = null,
        CancellationToken cancellationToken = default)
    {
        var (periodStartUtc, periodEndUtc) = RksvClosingPeriodHelper.MonthUtcRange(closing.Year, closing.Month);
        var cloudContext = await _enricher.BuildContextForRegisterAsync(
                closing.CashRegisterId,
                periodStartUtc,
                periodEndUtc,
                fiscalEnvironment.IsDemoFiscal || closing.IsSimulated,
                closing.TseSignature,
                cancellationToken)
            .ConfigureAwait(false);

        var report = MonatsbelegReportComposer.Compose(
            closing,
            summary,
            registerNumber,
            fiscalEnvironment,
            cloudContext,
            linkedDailyClosing);

        return GeneratePlainTextReport(report, cloudContext);
    }

    public string GeneratePlainTextReport(
        PosDailyClosingReportDto report,
        TagesabschlussCloudContext? cloudContext = null) =>
        _reportText.RenderClosingReport(report, cloudContext);
}
