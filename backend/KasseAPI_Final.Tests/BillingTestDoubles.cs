using KasseAPI_Final.Data;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tests;

internal static class BillingTestDoubles
{
    internal static IOptions<BillingBackupConfig> DisabledBackupOptions { get; } =
        Options.Create(new BillingBackupConfig
        {
            Enabled = false,
            BackupOnSaleCreation = false,
        });

    internal static BillingAuditService CreateAuditService(IDbContextFactory<AppDbContext> factory) =>
        new(factory, NullCurrentUserService.Instance, NullLogger<BillingAuditService>.Instance);

    internal static NoOpReminderService NoOpReminder { get; } = new();

    internal static NoOpBillingBackupService NoOpBackup { get; } = new();

    internal static IServiceScopeFactory CreateReminderScopeFactory(IBillingReminderService? reminder = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBillingReminderService>(reminder ?? NoOpReminder);
        services.AddSingleton<IBillingBackupService>(NoOpBackup);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}

internal sealed class NullCurrentUserService : ICurrentUserService
{
    public static NullCurrentUserService Instance { get; } = new();

    public Guid GetCurrentUserId() => Guid.Empty;
}

internal sealed class NoOpReminderService : IReminderService, IBillingReminderService
{
    public Task CheckAndCreateRemindersAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendPendingRemindersAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<List<LicenseReminderResponse>> GetForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        Task.FromResult(new List<LicenseReminderResponse>());

    public Task MarkAsSentAsync(Guid reminderId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ScheduleRemindersForSaleAsync(Guid saleId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task CancelRemindersForSaleAsync(Guid saleId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task<int> ProcessDueRemindersAsync(CancellationToken ct = default) =>
        Task.FromResult(0);

    public Task<BillingReminderListResponse> ListAsync(BillingReminderQuery query, CancellationToken ct = default) =>
        Task.FromResult(new BillingReminderListResponse());
}

internal sealed class NoOpBillingBackupService : IBillingBackupService
{
    public Task<BackupResult> BackupSaleAsync(Guid saleId, Guid? triggeredByUserId = null, CancellationToken ct = default) =>
        Task.FromResult(new BackupResult { Success = true, BackupRunId = "BAK-TEST-0000" });

    public Task<BackupResult> BackupDailyAsync(DateTime date, Guid? triggeredByUserId = null, CancellationToken ct = default) =>
        Task.FromResult(new BackupResult { Success = true, BackupRunId = "BAK-TEST-0000" });

    public Task<BackupResult> BackupWeeklyAsync(DateTime weekStart, Guid? triggeredByUserId = null, CancellationToken ct = default) =>
        Task.FromResult(new BackupResult { Success = true, BackupRunId = "BAK-TEST-0000" });

    public Task<BackupResult> BackupFullAsync(Guid? triggeredByUserId = null, CancellationToken ct = default) =>
        Task.FromResult(new BackupResult { Success = true, BackupRunId = "BAK-TEST-0000" });

    public Task<BackupHistoryListResponse> ListBackupHistoryAsync(BackupHistoryQuery query, CancellationToken ct = default) =>
        Task.FromResult(new BackupHistoryListResponse());

    public Task<BackupHistoryResponse> GetBackupDetailsAsync(Guid backupId, CancellationToken ct = default) =>
        throw new KeyNotFoundException($"Backup {backupId} not found");

    public Task<byte[]> DownloadBackupFileAsync(Guid backupId, CancellationToken ct = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task<int> CleanupExpiredBackupsAsync(CancellationToken ct = default) =>
        Task.FromResult(0);
}
