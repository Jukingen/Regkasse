using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseReportingServiceTests
{
    [Fact]
    public async Task GetDashboardDataAsync_AggregatesTransactionsDevicesAndTrends()
    {
        await using var db = CreateDb();
        var tenantId = await SeedAsync(db);
        var svc = new TseReportingService(db, NullLogger<TseReportingService>.Instance);

        var dash = await svc.GetDashboardDataAsync(tenantId, lookbackDays: 14);

        Assert.Equal(tenantId, dash.TenantId);
        Assert.True(dash.DiagnosticOnly);
        Assert.Equal(5, dash.TotalTransactions);
        Assert.Equal(1, dash.ActiveDevices);
        Assert.Equal(1, dash.TotalDevices);
        Assert.Equal(90, dash.OverallHealthScore);
        Assert.NotEmpty(dash.TransactionTrends);
        Assert.Contains(dash.ProviderBreakdown, p => p.Name == "fiskaly" && p.Count == 1);
        Assert.Contains(dash.StatusBreakdown, s => s.Name == "Healthy");
    }

    [Fact]
    public async Task GenerateReportAsync_IncludesSummary()
    {
        await using var db = CreateDb();
        var tenantId = await SeedAsync(db);
        var svc = new TseReportingService(db, NullLogger<TseReportingService>.Instance);

        var report = await svc.GenerateReportAsync(new TseBiReportRequestDto
        {
            TenantId = tenantId,
            LookbackDays = 14,
        });

        Assert.Contains("transactions", report.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(tenantId, report.Dashboard.TenantId);
    }

    [Fact]
    public async Task ExportReportAsync_Csv_ReturnsBase64Payload()
    {
        await using var db = CreateDb();
        var tenantId = await SeedAsync(db);
        var svc = new TseReportingService(db, NullLogger<TseReportingService>.Instance);

        var export = await svc.ExportReportAsync(new TseBiExportRequestDto
        {
            TenantId = tenantId,
            Format = "csv",
            LookbackDays = 14,
        });

        Assert.Equal("text/csv; charset=utf-8", export.ContentType);
        Assert.EndsWith(".csv", export.FileName);
        Assert.True(export.ByteLength > 0);
        var text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(export.ContentBase64));
        Assert.Contains("totalTransactions", text);
    }

    [Fact]
    public async Task ExportReportAsync_Pdf_ReturnsPdfOrCsvFallback()
    {
        await using var db = CreateDb();
        var tenantId = await SeedAsync(db);
        var svc = new TseReportingService(db, NullLogger<TseReportingService>.Instance);

        var export = await svc.ExportReportAsync(new TseBiExportRequestDto
        {
            TenantId = tenantId,
            Format = "pdf",
            LookbackDays = 14,
        });

        Assert.True(export.ByteLength > 0);
        Assert.True(
            export.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            || export.ContentType.Contains("csv", StringComparison.OrdinalIgnoreCase));
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_bi_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task<Guid> SeedAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "BI Cafe",
            Slug = "bi-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = now,
        });

        var deviceId = Guid.NewGuid();
        db.TseDevices.Add(new TseDevice
        {
            Id = deviceId,
            TenantId = tenantId,
            SerialNumber = "BI-1",
            DeviceType = "fiskaly",
            Provider = "fiskaly",
            VendorId = "v",
            ProductId = "p",
            IsConnected = true,
            CertificateStatus = "VALID",
            MemoryStatus = "OK",
            CanCreateInvoices = true,
            FinanzOnlineUsername = "fo",
            FinanzOnlineEnabled = false,
            LastFinanzOnlineSync = now,
            KassenId = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = now,
            HealthStatus = TseHealthStatus.Healthy,
            HealthScore = 90,
            IsPrimary = true,
            LastHealthCheck = now,
        });

        for (var i = 0; i < 5; i++)
        {
            db.Receipts.Add(new Receipt
            {
                ReceiptId = Guid.NewGuid(),
                PaymentId = Guid.NewGuid(),
                TenantId = tenantId,
                CashRegisterId = Guid.NewGuid(),
                ReceiptNumber = $"R-{i}",
                IssuedAt = now.Date.AddDays(-i).AddHours(12),
                SignatureValue = "sig",
                CreatedAt = now,
                SubTotal = 1,
                TaxTotal = 0,
                GrandTotal = 1,
            });
        }

        db.TseDeviceHealthSamples.Add(new TseDeviceHealthSample
        {
            DeviceId = deviceId,
            TenantId = tenantId,
            CheckedAtUtc = now.AddDays(-1),
            HealthScore = 88,
            HealthStatus = TseHealthStatus.Healthy,
        });

        await db.SaveChangesAsync();
        return tenantId;
    }
}
