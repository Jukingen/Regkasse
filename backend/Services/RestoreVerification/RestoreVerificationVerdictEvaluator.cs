using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Composes PASS/FAIL from persisted drill columns. Does not invent SHA-256 or TSE-chain replay.
/// Hash maps to dump TOC inspection; TSE maps to fiscal/L4 continuity when present.
/// </summary>
public static class RestoreVerificationVerdictEvaluator
{
    public const string VerdictPending = "pending";
    public const string VerdictPassed = "passed";
    public const string VerdictFailed = "failed";

    public const string ResultPassed = "passed";
    public const string ResultFailed = "failed";
    public const string ResultSkipped = "skipped";
    public const string ResultUnavailable = "unavailable";

    public const string CheckHash = "hash";
    public const string CheckSchema = "schema";
    public const string CheckData = "data";
    public const string CheckTse = "tse";
    public const string CheckFiscal = "fiscal";

    public sealed record Snapshot(
        string Verdict,
        IReadOnlyList<RestoreVerificationCheckDto> Checks,
        IReadOnlyList<string> FailedCheckIds,
        IReadOnlyList<RestoreVerificationRowCountDto> RowCounts,
        string? FiscalSqlResult,
        DateTime? VerifiedAtUtc);

    public static Snapshot Evaluate(RestoreVerificationRun run)
    {
        var evidence = RestoreDrillEvidenceJson.TryDeserialize(run.EvidenceJson);
        var checks = new[]
        {
            EvaluateHash(run),
            EvaluateSchema(run),
            EvaluateData(run, evidence),
            EvaluateTse(run),
            EvaluateFiscal(run)
        };

        var failed = checks
            .Where(c => string.Equals(c.Result, ResultFailed, StringComparison.Ordinal))
            .Select(c => c.Id)
            .ToArray();

        var verdict = run.Status is RestoreVerificationStatus.Queued or RestoreVerificationStatus.Running
            ? VerdictPending
            : failed.Length > 0 || run.Status == RestoreVerificationStatus.Failed
                ? VerdictFailed
                : VerdictPassed;

        return new Snapshot(
            verdict,
            checks,
            failed,
            ExtractRowCounts(evidence),
            FormatFiscalSqlResult(run),
            run.CompletedAt ?? run.StartedAt ?? run.RequestedAt);
    }

    public static string FormatFiscalSqlResult(RestoreVerificationRun run)
    {
        if (run.FiscalSqlSkipped)
        {
            var reason = string.IsNullOrWhiteSpace(run.FiscalSqlSkipReason)
                ? "not configured"
                : run.FiscalSqlSkipReason.Trim();
            return $"SKIPPED: {reason}";
        }

        if (run.FiscalSqlPassed == true)
            return "RESULT OK";

        if (run.FiscalSqlPassed == false)
        {
            var fail = run.FiscalSqlFailCount?.ToString() ?? "?";
            var warn = run.FiscalSqlWarnCount?.ToString() ?? "?";
            return $"RESULT FAIL (fail={fail}, warn={warn})";
        }

        return "RESULT UNAVAILABLE";
    }

    private static RestoreVerificationCheckDto EvaluateHash(RestoreVerificationRun run)
    {
        if (run.PgRestoreListPassed == true)
        {
            return Check(
                CheckHash,
                ResultPassed,
                "Dump TOC readable (pg_restore --list). Artifact SHA-256 is verified on the backup run, not this drill.");
        }

        if (run.PgRestoreListPassed == false)
        {
            return Check(
                CheckHash,
                ResultFailed,
                run.FailureDetail ?? "pg_restore --list did not accept the dump.");
        }

        return Check(CheckHash, ResultUnavailable, "Dump inspection not recorded.");
    }

    private static RestoreVerificationCheckDto EvaluateSchema(RestoreVerificationRun run)
    {
        if (run.PostRestoreL4ContinuityProofState == PostRestoreContinuityProofState.Failed
            || run.PostRestoreContinuityChecksPassed == false)
        {
            return Check(CheckSchema, ResultFailed, "Post-restore continuity / schema checks failed.");
        }

        if (run.PostRestoreContinuityChecksExecuted && run.PostRestoreContinuityChecksPassed == true)
        {
            return Check(CheckSchema, ResultPassed, "Post-restore continuity SQL reported schema-compatible.");
        }

        if (!run.RestoreAttemptExecuted)
        {
            return Check(
                CheckSchema,
                ResultSkipped,
                run.RestoreAttemptSkipReason ?? "Isolated restore was not executed.");
        }

        if (run.RestoreAttemptPassed == false)
            return Check(CheckSchema, ResultFailed, "Isolated pg_restore attempt failed.");

        return Check(CheckSchema, ResultUnavailable, "Schema continuity was not executed.");
    }

    private static RestoreVerificationCheckDto EvaluateData(
        RestoreVerificationRun run,
        RestoreDrillEvidenceDocument? evidence)
    {
        var counts = ExtractRowCounts(evidence);
        var requiredFailed = counts.Any(c =>
            string.Equals(c.Status, "failed", StringComparison.OrdinalIgnoreCase));

        if (requiredFailed)
            return Check(CheckData, ResultFailed, "One or more measured row-count checks failed.");

        if (counts.Count > 0 && counts.All(c =>
                string.Equals(c.Status, "passed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Status, "informative", StringComparison.OrdinalIgnoreCase)))
        {
            return Check(CheckData, ResultPassed, $"Row counts recorded for {counts.Count} check(s).");
        }

        if (run.RestoreAttemptExecuted && run.RestoreAttemptPassed == false)
            return Check(CheckData, ResultFailed, "Isolated restore failed; row counts were not proven.");

        if (!run.RestoreAttemptExecuted)
        {
            return Check(
                CheckData,
                ResultSkipped,
                run.RestoreAttemptSkipReason ?? "Isolated restore was not executed; no clone row counts.");
        }

        if (run.PostRestoreContinuityChecksPassed == true)
            return Check(CheckData, ResultPassed, "Continuity checks passed without a persisted count table.");

        return Check(CheckData, ResultUnavailable, "Row-count comparison is not available for this drill.");
    }

    private static RestoreVerificationCheckDto EvaluateTse(RestoreVerificationRun run)
    {
        if (run.FiscalContinuityLayerPassed == false)
            return Check(CheckTse, ResultFailed, "Fiscal / TSE continuity layer failed.");

        if (run.FiscalContinuityLayerPassed == true)
        {
            return Check(
                CheckTse,
                ResultPassed,
                "Fiscal continuity layer passed. This is not a full vendor TSE-chain replay.");
        }

        if (LooksLikeDeferredTse(run.DetailsJson))
        {
            return Check(
                CheckTse,
                ResultSkipped,
                "TSE vendor restore verification is deferred for this drill.");
        }

        if (run.IntegrityChecksPassed == false)
            return Check(CheckTse, ResultFailed, "Live integrity checks failed (not a TSE replay).");

        if (run.IntegrityChecksPassed == true)
        {
            return Check(
                CheckTse,
                ResultPassed,
                "Integrity checks passed. TSE signature-chain replay is not guaranteed on every drill.");
        }

        return Check(CheckTse, ResultSkipped, "TSE chain replay was not part of this drill.");
    }

    private static RestoreVerificationCheckDto EvaluateFiscal(RestoreVerificationRun run)
    {
        if (run.FiscalSqlSkipped)
        {
            return Check(
                CheckFiscal,
                ResultSkipped,
                run.FiscalSqlSkipReason ?? "Fiscal SQL was not run.");
        }

        if (run.FiscalSqlPassed == true)
            return Check(CheckFiscal, ResultPassed, FormatFiscalSqlResult(run));

        if (run.FiscalSqlPassed == false)
            return Check(CheckFiscal, ResultFailed, FormatFiscalSqlResult(run));

        return Check(CheckFiscal, ResultUnavailable, "Fiscal SQL result is not recorded.");
    }

    private static IReadOnlyList<RestoreVerificationRowCountDto> ExtractRowCounts(
        RestoreDrillEvidenceDocument? evidence)
    {
        var checks = evidence?.PostRestoreContinuity?.Checks;
        if (checks is null || checks.Count == 0)
            return Array.Empty<RestoreVerificationRowCountDto>();

        return checks
            .Where(c => c.MeasuredValue.HasValue
                        || string.Equals(c.Category, "fiscal_spine", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(c.Category, "continuity_resilience", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(c.Category, "platform", StringComparison.OrdinalIgnoreCase))
            .Select(c => new RestoreVerificationRowCountDto
            {
                Id = c.Id,
                Name = c.Name,
                Category = c.Category,
                Measured = c.MeasuredValue,
                ExpectedAtLeast = c.ExpectedAtLeast,
                Status = c.Status.ToString()
            })
            .ToList();
    }

    private static bool LooksLikeDeferredTse(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
            return false;
        return detailsJson.Contains("tseRestoreVerification", StringComparison.OrdinalIgnoreCase)
               || detailsJson.Contains("tse_restore_verification", StringComparison.OrdinalIgnoreCase);
    }

    private static RestoreVerificationCheckDto Check(string id, string result, string? detail) =>
        new() { Id = id, Result = result, Detail = detail };
}
