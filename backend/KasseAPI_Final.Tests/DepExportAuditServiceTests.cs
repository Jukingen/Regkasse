using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportAuditServiceTests
{
    private static readonly Guid TenantId = LegacyDefaultTenantIds.Primary;

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepExportAudit_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(TenantId));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    private static DepExportAuditService CreateSut(AppDbContext db, DateTime? utcNow = null)
    {
        var auditLog = new Mock<IAuditLogService>();
        auditLog
            .Setup(a => a.LogSystemOperationAsync(
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
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new DepExportAuditService(
            db,
            auditLog.Object,
            new HttpContextAccessor(),
            new FakeTimeProvider(utcNow ?? new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
            NullLogger<DepExportAuditService>.Instance);
    }

    [Theory]
    [InlineData("created", DepExportAuditActions.Created)]
    [InlineData("Purged", DepExportAuditActions.Deleted)]
    [InlineData("DOWNLOAD", DepExportAuditActions.Downloaded)]
    public void NormalizeAction_MapsAliases(string input, string expected)
    {
        Assert.Equal(expected, DepExportAuditService.NormalizeAction(input));
    }

    [Fact]
    public async Task LogAndQuery_RoundTrips()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsureDefaultTenant(db);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        await sut.LogExportActionAsync(new DepExportAuditEntry
        {
            TenantId = TenantId,
            Action = DepExportAuditActions.Created,
            ExportName = "dep-export_test.json",
            UserEmail = "manager@example.com",
            UserRole = "Manager",
            ActionAt = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc),
        });
        await sut.LogExportActionAsync(new DepExportAuditEntry
        {
            TenantId = TenantId,
            Action = DepExportAuditActions.Downloaded,
            ExportName = "dep-export_test.json",
            UserEmail = "manager@example.com",
            ActionAt = new DateTime(2026, 6, 16, 10, 0, 0, DateTimeKind.Utc),
        });

        var trail = await sut.GetAuditTrailAsync(
            TenantId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            action: DepExportAuditActions.Created,
            userSearch: "manager");

        Assert.Single(trail);
        Assert.Equal(DepExportAuditActions.Created, trail[0].Action);

        var report = await sut.GenerateAuditReportAsync(TenantId);
        Assert.Equal(2, report.TotalEntries);
        Assert.Equal(1, report.CountsByAction[DepExportAuditActions.Created]);
        Assert.Equal(1, report.CountsByAction[DepExportAuditActions.Downloaded]);
        Assert.Equal(DepExportAuditActions.Downloaded, report.LastAction);
    }
}
