using System.Globalization;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Reports;

public enum AdminOperationalReportType
{
    DailyReconciliation,
    TseContinuity,
    OfflineRecovery,
    UserPerformance,
    PeakHours,
    ProductMovement,
}

public interface IAdminOperationalReportExportService
{
    Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        AdminOperationalReportType reportType,
        string format,
        DateTime? startDate,
        DateTime? endDate,
        DateTime? businessDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);
}

public sealed class AdminOperationalReportExportService : IAdminOperationalReportExportService
{
    private readonly IComplianceOperationalReportingService _compliance;
    private readonly IOperationalReportingService _operational;
    private readonly IPeakHoursAnalysisService _peakHours;
    private readonly IProductMovementAnalysisService _productMovement;
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IFileNamingService _fileNaming;

    public AdminOperationalReportExportService(
        IComplianceOperationalReportingService compliance,
        IOperationalReportingService operational,
        IPeakHoursAnalysisService peakHours,
        IProductMovementAnalysisService productMovement,
        AppDbContext db,
        ISettingsTenantResolver tenantResolver,
        IFileNamingService fileNaming)
    {
        _compliance = compliance;
        _operational = operational;
        _peakHours = peakHours;
        _productMovement = productMovement;
        _db = db;
        _tenantResolver = tenantResolver;
        _fileNaming = fileNaming;
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        AdminOperationalReportType reportType,
        string format,
        DateTime? startDate,
        DateTime? endDate,
        DateTime? businessDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var normalized = format.Trim().ToLowerInvariant();
        var rows = await BuildRowsAsync(reportType, startDate, endDate, businessDate, cashRegisterId, cancellationToken);
        var company = await ResolveCompanyNameAsync(cancellationToken);
        var tenantSlug = await ResolveTenantSlugAsync(cancellationToken);
        var periodDate = businessDate ?? endDate ?? startDate ?? DateTime.UtcNow;
        var period = ReportExportFileNames.PeriodFromDay(periodDate);
        var typeLabel = reportType switch
        {
            AdminOperationalReportType.DailyReconciliation => "tagesbericht",
            _ => reportType.ToString().ToLowerInvariant(),
        };
        var ext = normalized switch
        {
            "pdf" => "pdf",
            "json" => "json",
            _ => "csv",
        };
        var normalizedType = ReportExportFileNames.NormalizeReportTypeLabel(typeLabel);
        var fileName = _fileNaming.GenerateFileName(
            $"{ReportExportFileNames.Prefix}_{normalizedType}",
            ext,
            additional: period,
            tenantSlug: tenantSlug);

        return normalized switch
        {
            "pdf" => (
                AdminOperationalReportPdfGenerator.Generate(company, reportType.ToString(), rows),
                "application/pdf",
                fileName),
            "json" => (
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows)),
                "application/json",
                fileName),
            "xlsx" or "excel" => (
                BuildCsv(rows),
                "text/csv",
                fileName),
            _ => (
                BuildCsv(rows),
                "text/csv",
                fileName),
        };
    }

    private async Task<string?> ResolveTenantSlugAsync(CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        return await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Slug)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string> ResolveCompanyNameAsync(CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var name = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(name) ? "Regkasse" : name;
    }

    private async Task<List<(string Label, string Value)>> BuildRowsAsync(
        AdminOperationalReportType reportType,
        DateTime? startDate,
        DateTime? endDate,
        DateTime? businessDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var inv = CultureInfo.InvariantCulture;
        var rows = new List<(string, string)>();

        switch (reportType)
        {
            case AdminOperationalReportType.DailyReconciliation:
                {
                    var r = await _compliance.GetDailyReconciliationAsync(businessDate ?? startDate, cashRegisterId, cancellationToken);
                    rows.Add(("BusinessDate", r.BusinessDate.ToString("yyyy-MM-dd", inv)));
                    rows.Add(("CashTotal", r.CashTotal.ToString(inv)));
                    rows.Add(("CardTotal", r.CardTotal.ToString(inv)));
                    rows.Add(("ExpectedCash", r.ExpectedCash.ToString(inv)));
                    rows.Add(("ActualCash", r.ActualCash?.ToString(inv) ?? "—"));
                    rows.Add(("IsReconciled", r.IsReconciled.ToString(inv)));
                    break;
                }
            case AdminOperationalReportType.TseContinuity:
                {
                    var r = await _compliance.GetTseChainContinuityAsync(startDate, endDate, cashRegisterId, cancellationToken);
                    rows.Add(("ReceiptsChecked", r.TotalReceiptsChecked.ToString(inv)));
                    rows.Add(("BreakCount", r.BreakCount.ToString(inv)));
                    foreach (var reg in r.Registers.Take(20))
                    {
                        rows.Add(($"Register {reg.RegisterNumber}", $"gaps={reg.GapsCount}, chainBreaks={reg.ChainBreakCount}"));
                    }
                    break;
                }
            case AdminOperationalReportType.OfflineRecovery:
                {
                    var r = await _compliance.GetOfflineRecoveryAsync(startDate, endDate, cashRegisterId, 50, cancellationToken);
                    rows.Add(("PendingAtEnd", r.PendingAtEnd.ToString(inv)));
                    rows.Add(("RecoveredSuccessfully", r.RecoveredSuccessfully.ToString(inv)));
                    rows.Add(("PermanentlyFailed", r.PermanentlyFailed.ToString(inv)));
                    break;
                }
            case AdminOperationalReportType.UserPerformance:
                {
                    var r = await _operational.GetUserPerformanceAsync(startDate, endDate, cashRegisterId, null, null, true, cancellationToken: cancellationToken);
                    foreach (var u in r.PerUser.Take(50))
                    {
                        rows.Add((u.UserName, $"tx={u.TransactionCount}, stornoRate={u.StornoRate.ToString("P1", inv)}"));
                    }
                    break;
                }
            case AdminOperationalReportType.PeakHours:
                {
                    var r = await _peakHours.GetPeakHoursAsync(startDate, endDate, cashRegisterId, cancellationToken);
                    if (r.BusiestHour != null)
                        rows.Add(("Busiest", $"day={r.BusiestHour.Day} hour={r.BusiestHour.Hour} count={r.BusiestHour.TransactionCount}"));
                    rows.Add(("AvgPerHour", r.AverageTransactionsPerHour.ToString("F2", inv)));
                    break;
                }
            case AdminOperationalReportType.ProductMovement:
                {
                    var r = await _productMovement.GetProductMovementAsync(startDate, endDate, cancellationToken);
                    rows.Add(("StockTurnover", r.StockTurnoverRate.ToString("F4", inv)));
                    rows.Add(("DaysOnHand", r.DaysOfInventoryOnHand.ToString("F1", inv)));
                    foreach (var p in r.TopSellingByRevenue.Take(20))
                        rows.Add((p.ProductName, $"qty={p.QuantitySold}, revenue={p.Revenue.ToString(inv)}"));
                    break;
                }
        }

        return rows;
    }

    private static byte[] BuildCsv(IReadOnlyList<(string Label, string Value)> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Label,Value");
        foreach (var (label, value) in rows)
            sb.AppendLine($"\"{label.Replace("\"", "\"\"", StringComparison.Ordinal)}\",\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
