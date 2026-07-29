using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Database;

public interface IMigrationStatusService
{
    /// <summary>Lightweight status for health probes (pending ids only, no full history).</summary>
    Task<MigrationStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Admin dashboard: pending + recent applied rows.</summary>
    Task<AdminMigrationStatusDto> GetAdminStatusAsync(
        int recentTake = 50,
        CancellationToken cancellationToken = default);
}
