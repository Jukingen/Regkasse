using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Services.Reports;

public interface IJahresbelegReportService
{
    Task<PosDailyClosingReportDto> ComposeReportDtoAsync(
        Jahresbeleg closing,
        JahresbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        DailyClosing? linkedDailyClosing = null,
        CancellationToken cancellationToken = default);

    Task<string> GeneratePlainTextReportAsync(
        Jahresbeleg closing,
        JahresbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        DailyClosing? linkedDailyClosing = null,
        CancellationToken cancellationToken = default);

    string GeneratePlainTextReport(
        PosDailyClosingReportDto report,
        TagesabschlussCloudContext? cloudContext = null);
}

public sealed class JahresbelegReportService : IJahresbelegReportService
{
    private readonly ITagesabschlussReportEnricher _enricher;
    private readonly IRksvReportTextService _reportText;

    public JahresbelegReportService(
        ITagesabschlussReportEnricher enricher,
        IRksvReportTextService reportText)
    {
        _enricher = enricher;
        _reportText = reportText;
    }

    public async Task<PosDailyClosingReportDto> ComposeReportDtoAsync(
        Jahresbeleg closing,
        JahresbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        DailyClosing? linkedDailyClosing = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(closing);
        ArgumentNullException.ThrowIfNull(summary);

        var (periodStartUtc, periodEndUtc) = RksvClosingPeriodHelper.YearUtcRange(closing.Year);
        var cloudContext = await _enricher.BuildContextForRegisterAsync(
                closing.CashRegisterId,
                periodStartUtc,
                periodEndUtc,
                fiscalEnvironment.IsDemoFiscal || closing.IsSimulated,
                closing.TseSignature,
                cancellationToken)
            .ConfigureAwait(false);

        return JahresbelegReportComposer.Compose(
            closing,
            summary,
            registerNumber,
            fiscalEnvironment,
            cloudContext,
            linkedDailyClosing);
    }

    public async Task<string> GeneratePlainTextReportAsync(
        Jahresbeleg closing,
        JahresbelegSummaryDto summary,
        string? registerNumber,
        FiscalEnvironmentResolver.FiscalEnvironment fiscalEnvironment,
        DailyClosing? linkedDailyClosing = null,
        CancellationToken cancellationToken = default)
    {
        var report = await ComposeReportDtoAsync(
                closing,
                summary,
                registerNumber,
                fiscalEnvironment,
                linkedDailyClosing,
                cancellationToken)
            .ConfigureAwait(false);

        var (periodStartUtc, periodEndUtc) = RksvClosingPeriodHelper.YearUtcRange(closing.Year);
        var cloudContext = await _enricher.BuildContextForRegisterAsync(
                closing.CashRegisterId,
                periodStartUtc,
                periodEndUtc,
                fiscalEnvironment.IsDemoFiscal || closing.IsSimulated,
                closing.TseSignature,
                cancellationToken)
            .ConfigureAwait(false);

        return GeneratePlainTextReport(report, cloudContext);
    }

    public string GeneratePlainTextReport(
        PosDailyClosingReportDto report,
        TagesabschlussCloudContext? cloudContext = null) =>
        _reportText.RenderClosingReport(report, cloudContext);
}
