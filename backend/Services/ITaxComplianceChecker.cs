using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface ITaxComplianceChecker
{
    Task<ComplianceReport> CheckComplianceAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
