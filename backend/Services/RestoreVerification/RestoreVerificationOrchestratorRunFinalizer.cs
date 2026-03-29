using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Restore drill run için terminal olmayan durumda kalan satırları güvenli şekilde sonlandırır (ayrı <see cref="AppDbContext"/> ile çağrılmalıdır).
/// </summary>
public static class RestoreVerificationOrchestratorRunFinalizer
{
    public static bool IsTerminal(RestoreVerificationStatus status) =>
        status is RestoreVerificationStatus.Succeeded or RestoreVerificationStatus.Failed;

    public static async Task TryFinalizeCancelledAsync(
        AppDbContext db,
        Guid runId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await db.RestoreVerificationRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
            if (run == null || IsTerminal(run.Status))
                return;

            if (run.Status != RestoreVerificationStatus.Running)
                return;

            var now = DateTime.UtcNow;
            run.Status = RestoreVerificationStatus.Failed;
            run.CompletedAt = now;
            run.FailureCode = "CANCELLED";
            run.FailureDetail = "Restore verification cancelled before completion (host shutdown or token).";
            MergeUnhandledDetails(run, new { cancelled = true, reason = "host_shutdown_or_token" });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restore verification finalizer (cancelled) failed for runId={RunId}", runId);
        }
    }

    public static async Task TryFinalizeUnhandledExceptionAsync(
        AppDbContext db,
        Guid runId,
        Exception exception,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await db.RestoreVerificationRuns.FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
            if (run == null || IsTerminal(run.Status))
                return;

            if (run.Status != RestoreVerificationStatus.Running)
                return;

            var now = DateTime.UtcNow;
            run.Status = RestoreVerificationStatus.Failed;
            run.CompletedAt = now;
            run.FailureCode = "UNHANDLED_EXCEPTION";
            run.FailureDetail = exception.Message;
            MergeUnhandledDetails(run, new
            {
                unhandledOrchestratorException = true,
                exceptionType = exception.GetType().Name,
                message = exception.Message
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restore verification finalizer (unhandled) failed for runId={RunId}", runId);
        }
    }

    private static void MergeUnhandledDetails(RestoreVerificationRun run, object fragment)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(run.DetailsJson))
            root = new JsonObject();
        else
        {
            try
            {
                var n = JsonNode.Parse(run.DetailsJson);
                root = n as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                root = new JsonObject();
            }
        }

        root["orchestratorFinalizer"] = JsonSerializer.SerializeToNode(fragment);
        run.DetailsJson = root.ToJsonString();
    }
}
