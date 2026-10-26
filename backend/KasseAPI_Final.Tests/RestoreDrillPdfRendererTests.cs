using System.Text;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreDrillPdfRendererTests
{
    private readonly RestoreDrillPdfRenderer _renderer = new();

    [Fact]
    public void Passed_drill_renders_a_pdf()
    {
        var bytes = _renderer.Render(Sample(RestoreVerificationStatus.Succeeded, failureCode: null), "actor-1", DateTime.UtcNow);

        Assert.NotEmpty(bytes);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void Failed_drill_includes_the_failure_reason()
    {
        var run = Sample(RestoreVerificationStatus.Failed, failureCode: "DUMP_UNREADABLE", detail: "toc missing");
        var lines = RestoreDrillPdfRenderer.NarrativeLines(run, Array.Empty<string>());
        var bytes = _renderer.Render(run, "actor-1", DateTime.UtcNow);

        Assert.Contains(lines, line => line.Contains("DUMP_UNREADABLE", StringComparison.Ordinal) && line.Contains("toc missing", StringComparison.Ordinal));
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void Cancelled_drill_is_not_reportable()
    {
        var ex = Assert.Throws<RestoreDrillNotReportableException>(() =>
            _renderer.Render(
                Sample(RestoreVerificationStatus.Failed, failureCode: "CANCELLED", detail: "host shutdown"),
                "actor-1",
                DateTime.UtcNow));

        Assert.Equal(RestoreDrillPdfRenderer.NotReportableCode, ex.Message);
    }

    [Fact]
    public void Sensitive_detail_is_masked()
    {
        var run = Sample(RestoreVerificationStatus.Failed, failureCode: "FAILED", detail: "password=hunter2");
        var lines = string.Join('\n', RestoreDrillPdfRenderer.NarrativeLines(run, Array.Empty<string>()));
        var bytes = _renderer.Render(run, "actor-1", DateTime.UtcNow);

        Assert.DoesNotContain("hunter2", lines, StringComparison.Ordinal);
        Assert.Contains("***", lines, StringComparison.Ordinal);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
    }

    private static RestoreVerificationRun Sample(
        RestoreVerificationStatus status,
        string? failureCode,
        string? detail = null)
    {
        var backupId = Guid.NewGuid();
        return new RestoreVerificationRun
        {
            Id = Guid.NewGuid(),
            Status = status,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            SourceBackupRunId = backupId,
            SourceBackupRun = new BackupRun
            {
                Id = backupId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = new DateTime(2026, 10, 20, 6, 0, 0, DateTimeKind.Utc)
            },
            RequestedAt = new DateTime(2026, 10, 21, 6, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 10, 21, 6, 2, 0, DateTimeKind.Utc),
            DurationMs = 120_000,
            FailureCode = failureCode,
            FailureDetail = detail,
            PgRestoreListPassed = status == RestoreVerificationStatus.Succeeded
        };
    }
}
