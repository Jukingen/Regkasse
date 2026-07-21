using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Reports;

public interface ITagesabschlussReportService
{
    /// <summary>RKSV footer using host environment only (Development/Staging → demo).</summary>
    string GetRksvFooter(IHostEnvironment env);

    /// <summary>RKSV footer for Tagesabschluss using full fiscal policy (TSE options + RKSV:Mode).</summary>
    string GetRksvFooterForClosing();

    /// <summary>Short single-line label for API/thermal headers.</summary>
    string GetRksvFooterLabel();

    /// <summary>TSE status badge for report header/footer blocks.</summary>
    string GetTseStatusBadge(bool isSimulated);

    /// <summary>Plain-text RKSV Tagesabschluss report for thermal/POS print (Cloud POS layout).</summary>
    string GenerateReport(
        DailyClosing closing,
        DailyClosingSummaryDto? daySummary = null,
        string? cashierName = null,
        TagesabschlussCloudContext? cloudContext = null,
        string? shiftNumber = null);

    /// <summary>Builds cloud context from tenant settings and renders the report.</summary>
    Task<string> GenerateReportAsync(
        DailyClosing closing,
        DailyClosingSummaryDto? daySummary = null,
        string? cashierName = null,
        string? shiftNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>Generates closing PDF, persists to filesystem + <c>report_pdfs</c>, returns stored row id.</summary>
    Task<Guid> GenerateAndSavePdfAsync(
        Guid closingId,
        Guid userId,
        string language = "de",
        CancellationToken cancellationToken = default);
}

public sealed class TagesabschlussReportService : ITagesabschlussReportService
{
    private readonly AppDbContext _context;
    private readonly IDailyClosingReportService _dailyClosingReportService;
    private readonly IReportPdfService _reportPdfService;
    private readonly IReportPdfStorageService _reportPdfStorage;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly IConfiguration _configuration;
    private readonly IRksvEnvironmentService _rksvEnvironment;
    private readonly ITagesabschlussReportEnricher _reportEnricher;
    private readonly IRksvReportTextService _reportText;

    public TagesabschlussReportService(
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IConfiguration configuration,
        IRksvEnvironmentService rksvEnvironment,
        ITagesabschlussReportEnricher reportEnricher,
        IRksvReportTextService reportText,
        AppDbContext context,
        IDailyClosingReportService dailyClosingReportService,
        IReportPdfService reportPdfService,
        IReportPdfStorageService reportPdfStorage,
        ICurrentUserService currentUserService)
    {
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _configuration = configuration;
        _rksvEnvironment = rksvEnvironment;
        _reportEnricher = reportEnricher;
        _reportText = reportText;
        _context = context;
        _dailyClosingReportService = dailyClosingReportService;
        _reportPdfService = reportPdfService;
        _reportPdfStorage = reportPdfStorage;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc />
    public string GetRksvFooter(IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(env);
        return RksvEnvironmentService.FormatFooter(env.IsDevelopment() || env.IsStaging());
    }

    /// <inheritdoc />
    public string GetRksvFooterForClosing() =>
        _rksvEnvironment.GetRksvFooter();

    /// <inheritdoc />
    public string GetRksvFooterLabel() =>
        ResolveClosingIsDemo()
            ? "DEMO / NICHT FISKAL"
            : "RKSV-konform (Registrierkassensicherheitsverordnung)";

    internal static string FormatFooter(bool isDemoFiscal) =>
        RksvEnvironmentService.FormatFooter(isDemoFiscal);

    /// <inheritdoc />
    public string GetTseStatusBadge(bool isSimulated) =>
        FormatTseStatusBadge(isSimulated);

    internal static string FormatTseStatusBadge(bool isSimulated) =>
        isSimulated
            ? "TSE SIMULIERT"
            : "TSE AKTIV";

    /// <inheritdoc />
    public async Task<string> GenerateReportAsync(
        DailyClosing closing,
        DailyClosingSummaryDto? daySummary = null,
        string? cashierName = null,
        string? shiftNumber = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(closing);
        var cloudContext = await _reportEnricher.BuildContextAsync(closing, cancellationToken)
            .ConfigureAwait(false);
        return GenerateReport(
            closing,
            daySummary,
            cashierName,
            cloudContext,
            shiftNumber ?? RksvShiftNumberFormatter.Format(closing.ShiftNumber));
    }

    /// <inheritdoc />
    public async Task<Guid> GenerateAndSavePdfAsync(
        Guid closingId,
        Guid userId,
        string language = "de",
        CancellationToken cancellationToken = default)
    {
        if (closingId == Guid.Empty)
            throw new ArgumentException("Closing id is required.", nameof(closingId));

        var closing = await _context.DailyClosings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == closingId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Daily closing {closingId} was not found.");

        var pdfBytes = await GeneratePdfAsync(closingId, language, cancellationToken).ConfigureAwait(false);
        var reportType = ReportPdfTypes.FromClosingType(closing.ClosingType);
        var actorId = ResolveActorUserId(userId);
        var fileName = ReportPdfArchiveNames.ForReport(reportType, closingId);

        if (string.Equals(
                DailyClosingReportTemplates.NormalizeLanguage(language),
                "de",
                StringComparison.OrdinalIgnoreCase))
        {
            return await _reportPdfService.SavePdfAsync(
                reportType,
                closingId,
                pdfBytes,
                fileName,
                actorId,
                cancellationToken).ConfigureAwait(false);
        }

        var pdfRecord = await _reportPdfStorage.SaveAsync(
            new ReportPdfStoreRequest
            {
                TenantId = closing.TenantId,
                ReportType = reportType,
                ReportId = closingId,
                PdfBytes = pdfBytes,
                GeneratedByUserId = actorId,
                Language = language,
            },
            cancellationToken).ConfigureAwait(false);

        return pdfRecord.Id;
    }

    private Guid ResolveActorUserId(Guid userId) =>
        userId != Guid.Empty ? userId : _currentUserService.GetCurrentUserId();

    private async Task<byte[]> GeneratePdfAsync(
        Guid closingId,
        string language,
        CancellationToken cancellationToken)
    {
        var pdfBytes = await _dailyClosingReportService.TryGenerateClosingReportPdfAsync(
            closingId,
            actorUserId: null,
            language,
            cancellationToken).ConfigureAwait(false);

        if (pdfBytes is null or { Length: 0 })
            throw new InvalidOperationException($"Could not generate PDF for closing {closingId}.");

        return pdfBytes;
    }

    /// <inheritdoc />
    public string GenerateReport(
        DailyClosing closing,
        DailyClosingSummaryDto? daySummary = null,
        string? cashierName = null,
        TagesabschlussCloudContext? cloudContext = null,
        string? shiftNumber = null)
    {
        ArgumentNullException.ThrowIfNull(closing);

        cloudContext ??= BuildFallbackCloudContext(closing);
        var model = TagesabschlussReportModel.From(
            closing,
            cloudContext,
            daySummary,
            cashierName ?? (string.IsNullOrWhiteSpace(closing.CashierName) ? null : closing.CashierName),
            shiftNumber ?? RksvShiftNumberFormatter.Format(closing.ShiftNumber));
        var closingLocal = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closing.ClosingDate);
        var qrPayload = FiscalEnvironmentResolver.BuildClosingQrPayload(
            closing.IsSimulated || _rksvEnvironment.IsDemoMode(),
            closing.TseSignature,
            closingLocal,
            model.TotalGross);

        var registerNumber = closing.CashRegister?.RegisterNumber ?? cloudContext.RegisterNumber;

        return _reportText.RenderTagesabschluss(
            model,
            _rksvEnvironment.GetEnvironmentDisplayName(),
            _rksvEnvironment.GetRksvFooter(),
            qrPayload,
            registerNumber);
    }

    private TagesabschlussCloudContext BuildFallbackCloudContext(DailyClosing closing)
    {
        var company = _configuration.GetSection("Company").Get<TagesabschlussCompanyConfig>()
                      ?? new TagesabschlussCompanyConfig();
        var businessDay = PostgreSqlUtcDateTime.ViennaCalendarMidnightContainingInstant(closing.ClosingDate);
        var (periodStartUtc, periodEndExclusiveUtc) =
            PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(businessDay);
        var isSimulated = closing.IsSimulated || _rksvEnvironment.IsTseSimulated();

        return new TagesabschlussCloudContext
        {
            CompanyName = company.Name,
            CompanyAddress = company.Address,
            CompanyVatId = company.VatId,
            RegisterNumber = closing.CashRegister?.RegisterNumber,
            TseProviderLabel = isSimulated ? "TSE simuliert (Demo)" : "fiskaly Cloud-HSM",
            DepExportStatusLabel = "Ausstehend",
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndExclusiveUtc,
            TseSignatureVerified = !isSimulated && !string.IsNullOrWhiteSpace(closing.TseSignature),
        };
    }

    private bool ResolveClosingIsDemo() =>
        FiscalEnvironmentResolver.Resolve(
                _hostEnvironment,
                _tseOptions,
                _configuration,
                rksvEnvironment: _rksvEnvironment)
            .IsDemoFiscal;

    private sealed class TagesabschlussCompanyConfig
    {
        public string Name { get; set; } = "Regkasse Software";

        public string Address { get; set; } = string.Empty;

        public string VatId { get; set; } = string.Empty;
    }
}
