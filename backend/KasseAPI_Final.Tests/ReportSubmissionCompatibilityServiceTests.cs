using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Xunit;

namespace KasseAPI_Final.Tests;

public class ReportSubmissionCompatibilityServiceTests
{
    [Fact]
    public async Task BuildEnvelopeAsync_Superseded_AddsChainHint()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var svc = new ReportSubmissionCompatibilityService(db, CreateOptionsMonitor());

        var env = await svc.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
        {
            ReportType = "Tagesbericht",
            ReportId = Guid.NewGuid(),
            ReportState = "Superseded",
            SupersededByReportId = Guid.NewGuid()
        });

        Assert.Contains(env.RemediationHintsDe, h => h.Contains("Superseded", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BuildEnvelopeAsync_OutboxAwaitingProtocol_HasReconciliationHints()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var oid = Guid.NewGuid();
        db.FinanzOnlineOutboxMessages.Add(new FinanzOnlineOutboxMessage
        {
            Id = oid,
            AggregateType = "TagesberichtReport",
            AggregateId = Guid.NewGuid(),
            MessageType = "TagesberichtDailySummary",
            BusinessKey = "bk",
            IdempotencyKey = new string('a', 128),
            PayloadJson = "{}",
            PayloadHash = new string('b', 128),
            Status = FinanzOnlineOutboxStatuses.AwaitingProtocol,
            CorrelationId = "c1"
        });
        await db.SaveChangesAsync();

        var svc = new ReportSubmissionCompatibilityService(db, CreateOptionsMonitor());

        var env = await svc.BuildEnvelopeAsync(new BuildReportSubmissionEnvelopeRequest
        {
            ReportType = "Tagesbericht",
            ReportId = Guid.NewGuid(),
            ReportState = "Finalized",
            OutboxMessageId = oid
        });

        Assert.Equal("awaiting_protocol", env.State.Lifecycle);
        Assert.Contains(env.RemediationHintsDe, h => h.Contains("Protokoll", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ReportFinanzOnlineBusinessKeys_Tages_IncludesAttempt()
    {
        var k0 = ReportFinanzOnlineBusinessKeys.Tagesbericht(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTime(2026, 3, 15),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            0);
        var k1 = ReportFinanzOnlineBusinessKeys.Tagesbericht(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTime(2026, 3, 15),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            1);
        Assert.StartsWith("v1:Tagesbericht:", k0, StringComparison.Ordinal);
        Assert.NotEqual(k0, k1);
    }

    private static IOptionsMonitor<FinanzOnlineOutboxOptions> CreateOptionsMonitor()
    {
        var mock = new Mock<IOptionsMonitor<FinanzOnlineOutboxOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(new FinanzOnlineOutboxOptions());
        return mock.Object;
    }
}
