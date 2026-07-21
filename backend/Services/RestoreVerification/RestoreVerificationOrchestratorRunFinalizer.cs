using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using Microsoft.EntityFrameworkCore;

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
            var (failureCode, failureDetail) = ClassifyUnhandledOrchestratorException(exception);
            run.FailureCode = failureCode;
            run.FailureDetail = failureDetail;
            MergeUnhandledDetails(run, new
            {
                unhandledOrchestratorException = true,
                exceptionType = exception.GetType().Name,
                message = exception.Message,
                innerException = exception.InnerException?.Message,
                classifiedFailureCode = failureCode
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restore verification finalizer (unhandled) failed for runId={RunId}", runId);
        }
    }

    /// <summary>
    /// Maps known infrastructure exceptions to stable codes so operators are not stuck on generic UNHANDLED_EXCEPTION only.
    /// </summary>
    public static (string Code, string Detail) ClassifyUnhandledOrchestratorException(Exception exception)
    {
        if (exception is InvalidOperationException io
            && io.Message.Contains("already has a parent", StringComparison.OrdinalIgnoreCase))
        {
            return (
                "RESTORE_DRILL_JSON_NODE_PARENT_CONFLICT",
                "Restore drill details JSON: a System.Text.Json.Nodes.JsonNode was reused under two parent keys (invalid graph).");
        }

        return ("UNHANDLED_EXCEPTION", exception.Message);
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
