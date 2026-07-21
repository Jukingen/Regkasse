using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class ReportMutationAuditTests
{
    [Fact]
    public async Task Tagesbericht_FinalizeDuplicate_LogsFailedAudit()
    {
        await using var db = CreateDb();
        var reportId = Guid.NewGuid();
        db.Set<TagesberichtReport>().Add(new TagesberichtReport
        {
            Id = reportId,
            OriginalReportId = reportId,
            CashRegisterId = Guid.NewGuid(),
            ViennaBusinessDate = new DateTime(2026, 3, 27),
            SnapshotJson = "{}",
            SnapshotHash = "h",
            SnapshotSchemaVersion = "1.0",
            ReportStatus = TagesberichtReportStatuses.Finalized,
            CorrectionKind = TagesberichtCorrectionKinds.None,
            CreatedByUserId = "u"
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var svc = new TagesberichtService(
            db,
            new Mock<ITagesabschlussService>().Object,
            new Mock<IFinanzOnlineOutboxService>().Object,
            new Mock<IReportSubmissionCompatibilityService>().Object,
            audit.Object,
            new Mock<ILogger<TagesberichtService>>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.FinalizeAsync(new TagesberichtFinalizeRequest { ReportId = reportId }, "actor"));

        audit.Verify(x => x.LogSystemOperationAsync(
            "TagesberichtFinalizeFailed",
            nameof(TagesberichtReport),
            "actor",
            "ReportActor",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Failed,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Tagesbericht_CorrectionOnNonFinalized_LogsFailedAudit()
    {
        await using var db = CreateDb();
        var reportId = Guid.NewGuid();
        db.Set<TagesberichtReport>().Add(new TagesberichtReport
        {
            Id = reportId,
            OriginalReportId = reportId,
            CashRegisterId = Guid.NewGuid(),
            ViennaBusinessDate = new DateTime(2026, 3, 27),
            SnapshotJson = "{}",
            SnapshotHash = "h",
            SnapshotSchemaVersion = "1.0",
            ReportStatus = TagesberichtReportStatuses.Provisional,
            CorrectionKind = TagesberichtCorrectionKinds.None,
            CreatedByUserId = "u"
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var svc = new TagesberichtService(
            db,
            new Mock<ITagesabschlussService>().Object,
            new Mock<IFinanzOnlineOutboxService>().Object,
            new Mock<IReportSubmissionCompatibilityService>().Object,
            audit.Object,
            new Mock<ILogger<TagesberichtService>>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateCorrectionAsync(new TagesberichtCorrectionRequest { SupersedesReportId = reportId }, "actor"));

        audit.Verify(x => x.LogSystemOperationAsync(
            "TagesberichtCorrectionFailed",
            nameof(TagesberichtReport),
            "actor",
            "ReportActor",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Failed,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Once);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }
}
