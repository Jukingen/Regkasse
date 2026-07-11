using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Reports;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class DailyClosingReportService : IDailyClosingReportService
{
    private readonly AppDbContext _context;

    public DailyClosingReportService(AppDbContext context)
    {
        _context = context;
    }

    public byte[] GenerateDailyReportPdf(PosDailyClosingReportDto report, string language = "de")
    {
        ArgumentNullException.ThrowIfNull(report);
        var normalized = DailyClosingReportTemplates.NormalizeLanguage(language);
        var labels = DailyClosingReportTemplates.Resolve(normalized, report.ClosingType);
        var culture = DailyClosingReportTemplates.GetCulture(normalized);
        return DailyClosingReportPdfGenerator.Generate(report, labels, culture);
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

        var registerNumber = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == closing.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var report = new PosDailyClosingReportDto
        {
            ClosingType = closing.ClosingType,
            BusinessDate = closing.ClosingDate,
            RegisterNumber = registerNumber,
            TotalSales = shift?.TotalSales ?? closing.TotalAmount,
            TotalCash = shift?.TotalCash ?? 0m,
            TotalCard = shift?.TotalCard ?? 0m,
            CashCount = shift?.CashCount ?? 0m,
            Difference = shift?.Difference ?? 0m,
            FiscalTotalAmount = closing.TotalAmount,
            FiscalTotalTaxAmount = closing.TotalTaxAmount,
            FiscalTransactionCount = closing.TransactionCount,
            TseSignature = closing.TseSignature,
        };

        return GenerateDailyReportPdf(report, language);
    }
}
