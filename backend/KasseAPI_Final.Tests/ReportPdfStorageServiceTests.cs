using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReportPdfStorageServiceTests
{
    private static (AppDbContext Ctx, string ContentRoot) CreateContext(Guid tenantId)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"regkasse-report-pdf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReportPdf_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        return (ctx, contentRoot);
    }

    private static ReportPdfStorageService CreateService(AppDbContext ctx, string contentRoot) =>
        new(
            ctx,
            new TestHostEnvironment(contentRoot),
            NullLogger<ReportPdfStorageService>.Instance);

    [Fact]
    public async Task SaveAsync_WritesFile_AndUpsertsMetadata()
    {
        var tenantId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var (ctx, contentRoot) = CreateContext(tenantId);
        await using (ctx)
        {
            var svc = CreateService(ctx, contentRoot);
            var pdf = "%PDF-1.4 test"u8.ToArray();

            var row = await svc.SaveAsync(new ReportPdfStoreRequest
            {
                TenantId = tenantId,
                ReportType = ReportPdfTypes.Tagesabschluss,
                ReportId = reportId,
                PdfBytes = pdf,
                GeneratedByUserId = Guid.NewGuid(),
                Language = "de",
            });

            Assert.NotEqual(Guid.Empty, row.Id);
            Assert.True(await svc.HasStoredPdfAsync(ReportPdfTypes.Tagesabschluss, reportId));

            var loaded = await svc.TryLoadBytesAsync(ReportPdfTypes.Tagesabschluss, reportId);
            Assert.NotNull(loaded);
            Assert.Equal(pdf, loaded);

            var updated = await svc.SaveAsync(new ReportPdfStoreRequest
            {
                TenantId = tenantId,
                ReportType = ReportPdfTypes.Tagesabschluss,
                ReportId = reportId,
                PdfBytes = [0x25, 0x50, 0x44, 0x46],
                GeneratedByUserId = Guid.NewGuid(),
                Language = "de",
            });
            Assert.Equal(row.Id, updated.Id);
            Assert.Equal(1, await ctx.ReportPdfs.CountAsync());
        }

        Directory.Delete(contentRoot, recursive: true);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "KasseAPI_Final.Tests";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
