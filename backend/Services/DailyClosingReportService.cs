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
        var labels = DailyClosingReportTemplates.Resolve(normalized);
        var culture = DailyClosingReportTemplates.GetCulture(normalized);
        return DailyClosingReportPdfGenerator.Generate(report, labels, culture);
    }

    public async Task<byte[]?> TryGenerateStoredDailyReportPdfAsync(
        Guid dailyClosingId,
        string cashierUserId,
        string language = "de",
        CancellationToken cancellationToken = default)
    {
        if (dailyClosingId == Guid.Empty || string.IsNullOrWhiteSpace(cashierUserId))
            return null;

        var shift = await _context.CashierShifts.AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.DailyClosingId == dailyClosingId && s.CashierId == cashierUserId && s.IsActive,
                cancellationToken);

        if (shift == null)
            return null;

        var closing = await _context.DailyClosings.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dailyClosingId, cancellationToken);

        if (closing == null)
            return null;

        var registerNumber = await _context.CashRegisters.AsNoTracking()
            .Where(r => r.Id == shift.CashRegisterId)
            .Select(r => r.RegisterNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var report = new PosDailyClosingReportDto
        {
            BusinessDate = closing.ClosingDate,
            RegisterNumber = registerNumber,
            TotalSales = shift.TotalSales,
            TotalCash = shift.TotalCash,
            TotalCard = shift.TotalCard,
            CashCount = shift.CashCount ?? 0m,
            Difference = shift.Difference,
            FiscalTotalAmount = closing.TotalAmount,
            FiscalTotalTaxAmount = closing.TotalTaxAmount,
            FiscalTransactionCount = closing.TransactionCount,
            TseSignature = closing.TseSignature,
        };

        return GenerateDailyReportPdf(report, language);
    }
}
