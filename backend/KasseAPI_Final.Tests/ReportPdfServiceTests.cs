using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReportPdfServiceTests
{
    private static (AppDbContext Ctx, string ContentRoot) CreateContext(Guid tenantId)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"regkasse-report-pdf-svc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReportPdfService_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        return (ctx, contentRoot);
    }

    private static ReportPdfService CreateService(
        AppDbContext ctx,
        string contentRoot,
        Guid tenantId)
    {
        var storage = new ReportPdfStorageService(
            ctx,
            new TestHostEnvironment(contentRoot),
            new FileNamingService(TenantTestDoubles.TenantAccessorReturning(tenantId)),
            NullLogger<ReportPdfStorageService>.Instance);
        var tenantResolver = new FixedTenantResolver(tenantId);
        return new ReportPdfService(
            ctx,
            storage,
            tenantResolver,
            new TestHostEnvironment(contentRoot),
            NullLogger<ReportPdfService>.Instance);
    }

    [Fact]
    public async Task SavePdfAsync_GetPdfAsync_HasPdfAsync_RoundTrip()
    {
        var tenantId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pdf = "%PDF-1.4 service-test"u8.ToArray();
        var (ctx, contentRoot) = CreateContext(tenantId);
        await using (ctx)
        {
            var svc = CreateService(ctx, contentRoot, tenantId);
            var recordId = await svc.SavePdfAsync(
                ReportPdfTypes.Receipt,
                reportId,
                pdf,
                "Beleg-123.pdf",
                userId);

            Assert.NotEqual(Guid.Empty, recordId);
            Assert.True(await svc.HasPdfAsync(ReportPdfTypes.Receipt, reportId));
            Assert.Equal(pdf, await svc.GetPdfAsync(ReportPdfTypes.Receipt, reportId));
        }

        Directory.Delete(contentRoot, recursive: true);
    }

    [Fact]
    public async Task GetPdfAsync_WhenMissing_ThrowsKeyNotFoundException()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, contentRoot) = CreateContext(tenantId);
        await using (ctx)
        {
            var svc = CreateService(ctx, contentRoot, tenantId);
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => svc.GetPdfAsync(ReportPdfTypes.Receipt, Guid.NewGuid()));
        }

        Directory.Delete(contentRoot, recursive: true);
    }

    [Fact]
    public async Task DeletePdfAsync_RemovesMetadataAndFile()
    {
        var tenantId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pdf = "%PDF-1.4 delete-test"u8.ToArray();
        var (ctx, contentRoot) = CreateContext(tenantId);
        await using (ctx)
        {
            var svc = CreateService(ctx, contentRoot, tenantId);
            await svc.SavePdfAsync(ReportPdfTypes.Tagesabschluss, reportId, pdf, "Tagesabschluss.pdf", userId);

            await svc.DeletePdfAsync(ReportPdfTypes.Tagesabschluss, reportId);

            Assert.False(await svc.HasPdfAsync(ReportPdfTypes.Tagesabschluss, reportId));
            Assert.Equal(0, await ctx.ReportPdfs.CountAsync());
        }

        Directory.Delete(contentRoot, recursive: true);
    }

    [Fact]
    public async Task SavePdfAsync_UnknownReportType_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var (ctx, contentRoot) = CreateContext(tenantId);
        await using (ctx)
        {
            var svc = CreateService(ctx, contentRoot, tenantId);
            await Assert.ThrowsAsync<ArgumentException>(
                () => svc.SavePdfAsync("unknown-type", Guid.NewGuid(), [0x25], "x.pdf", Guid.NewGuid()));
        }

        Directory.Delete(contentRoot, recursive: true);
    }

    private sealed class FixedTenantResolver : ISettingsTenantResolver
    {
        private readonly Guid _tenantId;

        public FixedTenantResolver(Guid tenantId) => _tenantId = tenantId;

        public Task<Guid> ResolveEffectiveTenantIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_tenantId);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath) => ContentRootPath = contentRootPath;

        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "KasseAPI_Final.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
