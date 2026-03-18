namespace KasseAPI_Final.Services;

/// <summary>Sprint 5: Internal consistency checks for fiscal data (sequence, orphan refunds, payment without invoice).</summary>
public interface IIntegrityCheckService
{
    Task<IntegrityReportDto> GetReportAsync(DateTime? fromDate = null, DateTime? toDate = null, bool includeDetails = false);
}
