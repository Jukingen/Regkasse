using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class DailyClosingReportService : IDailyClosingReportService
{
    private readonly AppDbContext _context;
    private readonly IDailyClosingService _dailyClosingService;
    private readonly IQrImageService _qrImageService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly IConfiguration _configuration;
    private readonly IRksvEnvironmentService _rksvEnvironment;
    private readonly ITagesabschlussReportEnricher _reportEnricher;
    private readonly IRksvReportTextService _reportText;
    private readonly IReportPdfStorageService _reportPdfStorage;
    private readonly IReportPdfService _reportPdfService;
    private readonly ICurrentUserService _currentUserService;

    public DailyClosingReportService(
        AppDbContext context,
        IDailyClosingService dailyClosingService,
        IQrImageService qrImageService,
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IConfiguration configuration,
        IRksvEnvironmentService rksvEnvironment,
        ITagesabschlussReportEnricher reportEnricher,
        IRksvReportTextService reportText,
        IReportPdfStorageService reportPdfStorage,
        IReportPdfService reportPdfService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _dailyClosingService = dailyClosingService;
        _qrImageService = qrImageService;
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _configuration = configuration;
        _rksvEnvironment = rksvEnvironment;
        _reportEnricher = reportEnricher;
        _reportText = reportText;
        _reportPdfStorage = reportPdfStorage;
        _reportPdfService = reportPdfService;
        _currentUserService = currentUserService;
    }

    public byte[] GenerateDailyReportPdf(PosDailyClosingReportDto report, string language = "de")
    {
        ArgumentNullException.ThrowIfNull(report);
        var normalized = DailyClosingReportTemplates.NormalizeLanguage(language);
        var labels = DailyClosingReportTemplates.Resolve(normalized, report.ClosingType);
        var culture = DailyClosingReportTemplates.GetCulture(normalized);
        var qrPng = TryGenerateClosingQr(report);
        return DailyClosingReportPdfGenerator.Generate(report, labels, culture, qrPng);
    }

    public string GenerateDailyReportText(
        PosDailyClosingReportDto report,
        TagesabschlussCloudContext? cloudContext = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        return _reportText.RenderClosingReport(report, cloudContext);
    }

    public Task<byte[]?> TryGenerateStoredDailyReportPdfAsync(
        Guid dailyClosingId,
        string cashierUserId,
        string language = "de",
        CancellationToken cancellationToken = default) =>
        TryGenerateClosingReportPdfAsync(dailyClosingId, cashierUserId, language, cancellationToken);

    public async Task<byte[]?> TryGenerateClosingReportPdfAsync(
        Guid closingId,
        string? actorUserId,
        string language = "de",
        CancellationToken cancellationToken = default)
    {
        if (closingId == Guid.Empty)
            return null;

        var closing = await _context.DailyClosings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == closingId, cancellationToken);

        if (closing == null ||
            !string.Equals(closing.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            return null;

        var reportType = ReportPdfTypes.FromClosingType(closing.ClosingType);
        var storedPdf = await _reportPdfStorage.TryLoadBytesAsync(
            reportType,
            closingId,
            language,
            cancellationToken);
        if (storedPdf is { Length: > 0 })
            return storedPdf;

        if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            var ownsClosing = string.Equals(closing.UserId, actorUserId, StringComparison.Ordinal);
            var ownsShift = await _context.CashierShifts.AsNoTracking()
                .AnyAsync(
                    s => s.DailyClosingId == closingId && s.CashierId == actorUserId && s.IsActive,
                    cancellationToken);
            if (!ownsClosing && !ownsShift)
                return null;
        }

        var shift = await _context.CashierShifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.DailyClosingId == closingId && s.IsActive, cancellationToken);

        var cashierName = shift?.CashierName;
        if (string.IsNullOrWhiteSpace(cashierName) && !string.IsNullOrWhiteSpace(closing.UserId))
        {
            cashierName = await _context.Users.AsNoTracking()
                .Where(u => u.Id == closing.UserId)
                .Select(u => u.UserName ?? u.Email)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var previousClosingSignature = await _context.DailyClosings.AsNoTracking()
            .Where(c =>
                c.CashRegisterId == closing.CashRegisterId
                && c.ClosingType == "Daily"
                && c.Status == "Completed"
                && c.ClosingDate < closing.ClosingDate)
            .OrderByDescending(c => c.ClosingDate)
            .Select(c => c.TseSignature)
            .FirstOrDefaultAsync(cancellationToken);

        var registerNumber = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == closing.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken);

        DailyClosingSummaryDto? daySummary = null;
        if (string.Equals(closing.ClosingType, "Daily", StringComparison.OrdinalIgnoreCase))
        {
            var businessDate = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closing.ClosingDate);
            daySummary = await _dailyClosingService.GenerateClosingSummaryAsync(
                closing.TenantId,
                closing.CashRegisterId,
                businessDate,
                cancellationToken);
        }

        var fiscalEnvironment = FiscalEnvironmentResolver.Resolve(
            _hostEnvironment,
            _tseOptions,
            _configuration,
            rksvEnvironment: _rksvEnvironment);

        var cloudContext = await _reportEnricher.BuildContextAsync(closing, cancellationToken);

        var report = DailyClosingReportComposer.Compose(
            closing,
            registerNumber,
            daySummary,
            cashCount: shift?.CashCount ?? 0m,
            shiftDifference: shift?.Difference ?? 0m,
            shiftCashSales: shift?.TotalCash,
            cashierName: cashierName ?? closing.CashierName,
            previousClosingSignature: previousClosingSignature,
            shiftNumber: RksvShiftNumberFormatter.Format(closing.ShiftNumber > 0 ? closing.ShiftNumber : null)
                           ?? RksvShiftNumberFormatter.Format(shift?.Id),
            fiscalEnvironment: fiscalEnvironment,
            cloudContext: cloudContext);

        var pdf = GenerateDailyReportPdf(report, language);
        if (pdf.Length > 0)
        {
            try
            {
                await AutoSaveClosingPdfAsync(
                    closing,
                    reportType,
                    closingId,
                    pdf,
                    actorUserId,
                    language,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort backfill; download must still succeed.
            }
        }

        return pdf;
    }

    private async Task AutoSaveClosingPdfAsync(
        DailyClosing closing,
        string reportType,
        Guid closingId,
        byte[] pdf,
        string? actorUserId,
        string language,
        CancellationToken cancellationToken)
    {
        var userId = ResolveActorUserId(closing, actorUserId);
        var fileName = ReportPdfArchiveNames.ForReport(reportType, closingId);
        var normalizedLanguage = DailyClosingReportTemplates.NormalizeLanguage(language);

        if (string.Equals(normalizedLanguage, "de", StringComparison.OrdinalIgnoreCase))
        {
            await _reportPdfService.SavePdfAsync(
                reportType,
                closingId,
                pdf,
                fileName,
                userId,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await _reportPdfStorage.SaveAsync(
            new ReportPdfStoreRequest
            {
                TenantId = closing.TenantId,
                ReportType = reportType,
                ReportId = closingId,
                PdfBytes = pdf,
                GeneratedByUserId = userId,
                Language = normalizedLanguage,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private Guid ResolveActorUserId(DailyClosing closing, string? actorUserId)
    {
        if (!string.IsNullOrWhiteSpace(actorUserId)
            && Guid.TryParse(actorUserId, out var parsedActor))
        {
            return parsedActor;
        }

        if (Guid.TryParse(closing.UserId, out var closingUser))
            return closingUser;

        return _currentUserService.GetCurrentUserId();
    }

    private byte[]? TryGenerateClosingQr(PosDailyClosingReportDto report)
    {
        var payload = report.QrPayload ?? report.TseSignature;
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            return _qrImageService.GenerateQrCodeImage(payload.Trim());
        }
        catch
        {
            return null;
        }
    }
}
