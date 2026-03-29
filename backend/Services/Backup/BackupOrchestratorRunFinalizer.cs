using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Kuyruktan alınan bir <see cref="BackupRun"/> için beklenmeyen exception veya iptal sonrası terminal duruma geçiş (EF takip çakışmasından kaçınmak için ayrı <see cref="AppDbContext"/> ile çağrılmalıdır).
/// </summary>
public static class BackupOrchestratorRunFinalizer
{
    public static bool IsTerminal(BackupRunStatus status) =>
        status is BackupRunStatus.Succeeded
            or BackupRunStatus.Failed
            or BackupRunStatus.VerificationFailed
            or BackupRunStatus.Cancelled;

    /// <summary>
    /// İşleyici token iptali (host kapanışı): mevcut iptal semantiğiyle uyumlu terminal durum.
    /// </summary>
    public static async Task TryFinalizeCancelledAsync(
        AppDbContext db,
        Guid runId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await db.BackupRuns
                .Include(r => r.Verifications)
                .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
            if (run == null || IsTerminal(run.Status))
                return;

            var now = DateTime.UtcNow;
            if (run.Status == BackupRunStatus.AwaitingVerification)
            {
                run.Status = BackupRunStatus.Cancelled;
                run.CompletedAt = now;
                run.FailureCode = "CANCELLED";
                run.FailureDetail = "Backup run cancelled during artifact verification (host shutdown or token).";
                foreach (var v in run.Verifications)
                {
                    if (v.Status != BackupVerificationStatus.Pending || v.CompletedAt != null)
                        continue;
                    v.Status = BackupVerificationStatus.Failed;
                    v.CompletedAt = now;
                    v.FailureReason = run.FailureDetail;
                    v.DetailsJson = JsonSerializer.Serialize(new { error = "cancelled_during_verification" });
                }
            }
            else if (run.Status == BackupRunStatus.Running)
            {
                run.Status = BackupRunStatus.Cancelled;
                run.CompletedAt = now;
                run.FailureCode = "CANCELLED";
                run.FailureDetail = "Backup run cancelled before completion (host shutdown or token).";
            }
            else
            {
                return;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup run finalizer (cancelled) failed for runId={RunId}", runId);
        }
    }

    /// <summary>
    /// Yakalanmamış exception: <see cref="BackupRunStatus.Running"/> iken <see cref="BackupRunStatus.Failed"/>,
    /// <see cref="BackupRunStatus.AwaitingVerification"/> iken <see cref="BackupRunStatus.VerificationFailed"/>.
    /// </summary>
    public static async Task TryFinalizeUnhandledExceptionAsync(
        AppDbContext db,
        Guid runId,
        Exception exception,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await db.BackupRuns
                .Include(r => r.Verifications)
                .FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
            if (run == null || IsTerminal(run.Status))
                return;

            var now = DateTime.UtcNow;
            if (run.Status == BackupRunStatus.AwaitingVerification)
            {
                run.Status = BackupRunStatus.VerificationFailed;
                run.CompletedAt = now;
                run.FailureCode = "UNHANDLED_EXCEPTION";
                run.FailureDetail = exception.Message;
                foreach (var v in run.Verifications)
                {
                    if (v.Status == BackupVerificationStatus.Passed)
                        continue;
                    if (v.Status == BackupVerificationStatus.Failed && v.CompletedAt != null)
                        continue;
                    v.Status = BackupVerificationStatus.Failed;
                    v.CompletedAt = now;
                    v.FailureReason = exception.Message;
                    v.DetailsJson = JsonSerializer.Serialize(new
                    {
                        error = "unhandled_exception_during_backup_pipeline",
                        exceptionType = exception.GetType().Name
                    });
                }
            }
            else if (run.Status == BackupRunStatus.Running)
            {
                run.Status = BackupRunStatus.Failed;
                run.CompletedAt = now;
                run.FailureCode = "UNHANDLED_EXCEPTION";
                run.FailureDetail = exception.Message;
            }
            else
            {
                return;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup run finalizer (unhandled) failed for runId={RunId}", runId);
        }
    }
}
