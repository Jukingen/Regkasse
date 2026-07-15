using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public interface IReportPdfCaptureService
{
    Task TryCaptureClosingReportAsync(
        Guid closingId,
        string actorUserId,
        string language = "de",
        CancellationToken cancellationToken = default);

    Task TryCaptureReceiptReportAsync(
        Guid paymentId,
        string? specialReceiptKind,
        string actorUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>Generates and persists RKSV report PDFs at creation time (best-effort; never throws to callers).</summary>
public sealed class ReportPdfCaptureService : IReportPdfCaptureService
{
    private readonly AppDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReportPdfStorageService _storage;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly ILogger<ReportPdfCaptureService> _logger;

    public ReportPdfCaptureService(
        AppDbContext context,
        IServiceScopeFactory scopeFactory,
        IReportPdfStorageService storage,
        ISettingsTenantResolver tenantResolver,
        ILogger<ReportPdfCaptureService> logger)
    {
        _context = context;
        _scopeFactory = scopeFactory;
        _storage = storage;
        _tenantResolver = tenantResolver;
        _logger = logger;
    }

    public async Task TryCaptureClosingReportAsync(
        Guid closingId,
        string actorUserId,
        string language = "de",
        CancellationToken cancellationToken = default)
    {
        if (closingId == Guid.Empty)
            return;

        try
        {
            var closing = await _context.DailyClosings.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == closingId, cancellationToken)
                .ConfigureAwait(false);
            if (closing is null)
                return;

            var reportType = ReportPdfTypes.FromClosingType(closing.ClosingType);
            if (await _storage.HasStoredPdfAsync(reportType, closingId, language, cancellationToken).ConfigureAwait(false))
                return;

            using var scope = _scopeFactory.CreateScope();
            var closingReportService = scope.ServiceProvider.GetRequiredService<IDailyClosingReportService>();
            var pdf = await closingReportService.TryGenerateClosingReportPdfAsync(
                closingId,
                actorUserId: null,
                language,
                cancellationToken).ConfigureAwait(false);
            if (pdf is null or { Length: 0 })
                return;

            await _storage.SaveAsync(
                new ReportPdfStoreRequest
                {
                    TenantId = closing.TenantId,
                    ReportType = reportType,
                    ReportId = closingId,
                    PdfBytes = pdf,
                    GeneratedByUserId = ParseActorUserId(actorUserId),
                    Language = language,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture closing report PDF for closingId={ClosingId}", closingId);
        }
    }

    public async Task TryCaptureReceiptReportAsync(
        Guid paymentId,
        string? specialReceiptKind,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (paymentId == Guid.Empty)
            return;

        try
        {
            var reportType = ReportPdfTypes.FromSpecialReceiptKind(specialReceiptKind);
            if (await _storage.HasStoredPdfAsync(reportType, paymentId, "de", cancellationToken).ConfigureAwait(false))
                return;

            var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);

            using var scope = _scopeFactory.CreateScope();
            var receiptPdfService = scope.ServiceProvider.GetRequiredService<IReceiptPdfService>();
            var pdf = await receiptPdfService.GeneratePdfAsync(
                paymentId,
                includeReprintWatermark: false,
                cancellationToken).ConfigureAwait(false);
            if (pdf.Length == 0)
                return;

            await _storage.SaveAsync(
                new ReportPdfStoreRequest
                {
                    TenantId = tenantId,
                    ReportType = reportType,
                    ReportId = paymentId,
                    PdfBytes = pdf,
                    GeneratedByUserId = ParseActorUserId(actorUserId),
                    Language = "de",
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture receipt report PDF for paymentId={PaymentId}", paymentId);
        }
    }

    private static Guid ParseActorUserId(string actorUserId) =>
        Guid.TryParse(actorUserId, out var parsed) ? parsed : Guid.Empty;
}
