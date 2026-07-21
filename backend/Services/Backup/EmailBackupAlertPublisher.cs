using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Bridges <see cref="BackupAlertKind.BackupFailed"/> / <see cref="BackupAlertKind.VerificationFailed"/>
/// into German ops email via <see cref="IBackupFailureEmailAlertService"/>.
/// </summary>
public sealed class EmailBackupAlertPublisher : IBackupAlertPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackupFailureEmailAlertService _email;
    private readonly ILogger<EmailBackupAlertPublisher> _logger;

    public EmailBackupAlertPublisher(
        IServiceScopeFactory scopeFactory,
        IBackupFailureEmailAlertService email,
        ILogger<EmailBackupAlertPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _email = email;
        _logger = logger;
    }

    public void Publish(BackupAlertEvent evt) => _ = PublishInBackgroundAsync(evt);

    private async Task PublishInBackgroundAsync(BackupAlertEvent evt)
    {
        if (evt.Kind is not (BackupAlertKind.BackupFailed or BackupAlertKind.VerificationFailed))
            return;

        try
        {
            var slug = await ResolveTenantSlugAsync(evt).ConfigureAwait(false);
            var error = BuildErrorText(evt);
            await _email.SendFailureAlertAsync(
                    slug,
                    error,
                    evt.BackupRunId,
                    evt.CorrelationId)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Backup failure email publisher failed for kind={Kind} run={BackupRunId}",
                evt.Kind,
                evt.BackupRunId);
        }
    }

    private async Task<string> ResolveTenantSlugAsync(BackupAlertEvent evt)
    {
        if (evt.Data != null
            && evt.Data.TryGetValue("tenantSlug", out var fromData)
            && !string.IsNullOrWhiteSpace(fromData))
        {
            return fromData.Trim();
        }

        if (!evt.BackupRunId.HasValue)
            return BackupRunTenantSlugResolver.DeploymentSlug;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = await db.BackupRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == evt.BackupRunId.Value)
            .ConfigureAwait(false);
        if (run == null)
            return BackupRunTenantSlugResolver.DeploymentSlug;

        return await BackupRunTenantSlugResolver.ResolveSlugAsync(run, db).ConfigureAwait(false);
    }

    private static string BuildErrorText(BackupAlertEvent evt)
    {
        if (evt.Data != null
            && evt.Data.TryGetValue("errorCode", out var code)
            && !string.IsNullOrWhiteSpace(code))
        {
            return string.IsNullOrWhiteSpace(evt.Message)
                ? code.Trim()
                : $"{code.Trim()}: {evt.Message}";
        }

        return string.IsNullOrWhiteSpace(evt.Message) ? evt.Kind.ToString() : evt.Message;
    }
}
