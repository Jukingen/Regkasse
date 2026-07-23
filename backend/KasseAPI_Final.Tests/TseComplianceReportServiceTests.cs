using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseComplianceReportServiceTests
{
    [Fact]
    public async Task GenerateComplianceReportAsync_FlagsUnsignedAndChainBreaks()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Receipts.AddRange(
            Receipt(tenantId, registerId, now.AddHours(-2), "sig-a"),
            Receipt(tenantId, registerId, now.AddHours(-1), ""),
            Receipt(tenantId, registerId, now.AddMinutes(-30), "sig-b"));
        await db.SaveChangesAsync();

        var rksv = new Mock<IRksvComplianceReportService>();
        rksv.Setup(s => s.BuildReportAsync(null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RksvComplianceReportDto
            {
                Summary = new RksvComplianceReportSummaryDto
                {
                    TseSignatureMissingCount = 1,
                    SignatureChainBreaks = 1,
                    SequenceGapCount = 0,
                    OverallPass = false,
                },
                LegalNoticeDe = "Diagnose only.",
            });

        var operational = new Mock<IComplianceOperationalReportingService>();
        operational.Setup(s => s.GetTseChainContinuityAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseChainContinuityReportDto
            {
                Registers =
                [
                    new TseContinuityRegisterReportDto
                    {
                        CashRegisterId = registerId,
                        ChainBreakCount = 1,
                        SequenceGapCount = 0,
                        MissingSignatureCount = 1,
                        ReceiptsInRange = 3,
                        SignatureCount = 2,
                    },
                ],
                TotalReceiptsChecked = 3,
                TotalSignatureCount = 2,
                BreakCount = 1,
                TotalGapsCount = 0,
                TotalDuplicateCount = 0,
            });

        var health = new Mock<ITseHealthTrendService>();
        health.Setup(s => s.GenerateHealthReportAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseHealthReportDto
            {
                TenantId = tenantId,
                TotalDevices = 1,
                HealthyDevices = 1,
                DegradedDevices = 0,
                UnhealthyDevices = 0,
                AverageHealthScore = 95,
                HealthyMinScore = 80,
                DegradedMinScore = 50,
                Recommendations = [],
            });

        var accessor = new CurrentTenantAccessor();
        var svc = new TseComplianceReportService(
            db,
            accessor,
            rksv.Object,
            operational.Object,
            health.Object,
            NullLogger<TseComplianceReportService>.Instance);

        var report = await svc.GenerateComplianceReportAsync(tenantId, now.AddDays(-1), now.AddMinutes(1));

        Assert.Equal(3, report.TotalTransactions);
        Assert.Equal(2, report.SignedTransactions);
        Assert.Equal(1, report.UnsignedTransactions);
        Assert.False(report.IsFullyCompliant);
        Assert.Contains(report.Issues, i => i.Code == "missing_tse_signature");
        Assert.Contains(report.Issues, i => i.Code == "signature_chain_break");
        Assert.Equal(1, report.SignatureChainSummary.ChainBreakCount);
        Assert.False(report.SignatureChainSummary.ChainHealthy);
        Assert.Null(accessor.TenantId);
    }

    [Fact]
    public async Task GetComplianceStatusAsync_ReturnsCompliantWhenClean()
    {
        await using var db = CreateDb();
        var tenantId = await SeedTenantAsync(db);
        var registerId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Receipts.Add(Receipt(tenantId, registerId, now.AddHours(-1), "compact.jws.sig"));
        await db.SaveChangesAsync();

        var rksv = new Mock<IRksvComplianceReportService>();
        rksv.Setup(s => s.BuildReportAsync(null, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RksvComplianceReportDto
            {
                Summary = new RksvComplianceReportSummaryDto { OverallPass = true },
            });

        var operational = new Mock<IComplianceOperationalReportingService>();
        operational.Setup(s => s.GetTseChainContinuityAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseChainContinuityReportDto
            {
                Registers = [],
                TotalReceiptsChecked = 1,
                TotalSignatureCount = 1,
            });

        var health = new Mock<ITseHealthTrendService>();
        health.Setup(s => s.GenerateHealthReportAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TseHealthReportDto
            {
                TenantId = tenantId,
                TotalDevices = 1,
                HealthyDevices = 1,
                AverageHealthScore = 100,
                HealthyMinScore = 80,
                DegradedMinScore = 50,
                Recommendations = [],
            });

        var svc = new TseComplianceReportService(
            db,
            new CurrentTenantAccessor(),
            rksv.Object,
            operational.Object,
            health.Object,
            NullLogger<TseComplianceReportService>.Instance);

        var status = await svc.GetComplianceStatusAsync(tenantId);

        Assert.Equal(TseComplianceStatusNames.Compliant, status.Status);
        Assert.True(status.IsFullyCompliant);
        Assert.Equal(0, status.UnsignedTransactions);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_compliance_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedTenantAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Compliance Cafe",
            Slug = "compliance-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static Receipt Receipt(Guid tenantId, Guid registerId, DateTime issuedAt, string signature) =>
        new()
        {
            ReceiptId = Guid.NewGuid(),
            PaymentId = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            ReceiptNumber = $"AT-R-{issuedAt:yyyyMMdd}-{Random.Shared.Next(1, 9999)}",
            IssuedAt = issuedAt,
            SignatureValue = signature,
            CreatedAt = issuedAt,
            SubTotal = 10m,
            TaxTotal = 2m,
            GrandTotal = 12m,
        };
}
