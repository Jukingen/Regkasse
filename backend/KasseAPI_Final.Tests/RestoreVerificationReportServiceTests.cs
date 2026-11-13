using KasseAPI_Final.Data;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationReportServiceTests
{
    [Fact]
    public async Task GetReportAsync_Csv_Contains_Verdict_And_Fiscal_Result()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rv_report_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

        var run = new RestoreVerificationRun
        {
            Id = Guid.NewGuid(),
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Manual,
            PgRestoreListPassed = true,
            RestoreAttemptExecuted = true,
            RestoreAttemptPassed = true,
            PostRestoreContinuityChecksExecuted = true,
            PostRestoreContinuityChecksPassed = true,
            FiscalContinuityLayerPassed = true,
            FiscalSqlSkipped = false,
            FiscalSqlPassed = true,
            RequestedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow
        };
        db.RestoreVerificationRuns.Add(run);
        await db.SaveChangesAsync();

        var reports = new RestoreVerificationReportService(new RestoreVerificationRunQueryService(db));
        var dto = await reports.GetReportAsync(
            run.Id,
            new RestoreVerificationAccessScope(IsSuperAdmin: true, CallerTenantId: null));

        Assert.NotNull(dto);
        Assert.Equal(RestoreVerificationVerdictEvaluator.VerdictPassed, dto.Verdict);
        Assert.Equal("RESULT OK", dto.FiscalSqlResult);

        var csv = System.Text.Encoding.UTF8.GetString(reports.ToCsv(dto));
        Assert.Contains("passed", csv, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RESULT OK", csv, StringComparison.Ordinal);
        Assert.Contains(run.Id.ToString("D"), csv, StringComparison.OrdinalIgnoreCase);

        var pdf = reports.ToPdf(dto);
        Assert.True(pdf.Length > 8);
        Assert.Equal(0x25, pdf[0]); // %PDF
    }
}
