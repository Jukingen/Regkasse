using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReportPdfDownloadIntegrationTests : IClassFixture<ManagerOversightWebApplicationFactory>
{
    private readonly ManagerOversightWebApplicationFactory _factory;

    public ReportPdfDownloadIntegrationTests(ManagerOversightWebApplicationFactory factory) =>
        _factory = factory;

    [Fact]
    public async Task DownloadStoredReceiptPdf_ReturnsPdf_AndWritesAudit()
    {
        var paymentId = ManagerOversightWebApplicationFactory.PaymentAId;
        var pdfBytes = "%PDF-1.4 stored-receipt"u8.ToArray();

        using (var scope = _factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IReportPdfStorageService>();
            await storage.SaveAsync(new ReportPdfStoreRequest
            {
                TenantId = ManagerOversightWebApplicationFactory.TenantAId,
                ReportType = ReportPdfTypes.Receipt,
                ReportId = paymentId,
                PdfBytes = pdfBytes,
                GeneratedByUserId = Guid.NewGuid(),
                Language = "de",
            });
        }

        var client = await CreateAuthenticatedManagerClientAsync();
        var response = await client.GetAsync($"/api/admin/reports/receipt/{paymentId}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(pdfBytes, body);

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditLogs.IgnoreQueryFilters()
            .Where(a => a.ActionType == AuditEventType.ReportPdfDownloaded && a.EntityId == paymentId)
            .OrderByDescending(a => a.Timestamp)
            .FirstOrDefaultAsync();
        Assert.NotNull(audit);
        Assert.Equal("ReportPdf", audit!.EntityType);
    }

    [Fact]
    public async Task DownloadStoredReceiptPdf_NewPdfRoute_ReturnsPdf()
    {
        var paymentId = ManagerOversightWebApplicationFactory.PaymentAId;
        var pdfBytes = "%PDF-1.4 stored-receipt-new-route"u8.ToArray();

        using (var scope = _factory.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<IReportPdfStorageService>();
            await storage.SaveAsync(new ReportPdfStoreRequest
            {
                TenantId = ManagerOversightWebApplicationFactory.TenantAId,
                ReportType = ReportPdfTypes.Receipt,
                ReportId = paymentId,
                PdfBytes = pdfBytes,
                GeneratedByUserId = Guid.NewGuid(),
                Language = "de",
            });
        }

        var client = await CreateAuthenticatedManagerClientAsync();
        var response = await client.GetAsync($"/api/admin/reports/pdf/receipt/{paymentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(pdfBytes, body);
    }

    [Fact]
    public async Task DownloadStoredClosingPdf_AliasRoute_ReturnsPdf()
    {
        var closingId = Guid.Parse("d0a00001-0001-0001-0001-000000000001");
        var pdfBytes = "%PDF-1.4 stored-closing"u8.ToArray();
        var now = DateTime.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.DailyClosings.Add(new DailyClosing
            {
                Id = closingId,
                TenantId = ManagerOversightWebApplicationFactory.TenantAId,
                CashRegisterId = ManagerOversightWebApplicationFactory.CashRegisterAId,
                UserId = "manager-a",
                CashierName = "Manager Tenant A",
                ShiftNumber = 1,
                ClosingDate = now.Date,
                ClosingType = "Daily",
                TotalAmount = 100m,
                TotalTaxAmount = 20m,
                TransactionCount = 5,
                Status = "Completed",
                CreatedAt = now,
            });
            await db.SaveChangesAsync();

            var storage = scope.ServiceProvider.GetRequiredService<IReportPdfStorageService>();
            await storage.SaveAsync(new ReportPdfStoreRequest
            {
                TenantId = ManagerOversightWebApplicationFactory.TenantAId,
                ReportType = ReportPdfTypes.Tagesabschluss,
                ReportId = closingId,
                PdfBytes = pdfBytes,
                GeneratedByUserId = Guid.NewGuid(),
                Language = "de",
            });
        }

        var client = await CreateAuthenticatedManagerClientAsync();
        var response = await client.GetAsync($"/api/admin/report-pdfs/download/tagesabschluss/{closingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(pdfBytes, body);
    }

    private async Task<HttpClient> CreateAuthenticatedManagerClientAsync()
    {
        var client = _factory.CreateTenantClient(ManagerOversightWebApplicationFactory.TenantASlug);
        var token = await LoginAsManagerAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> LoginAsManagerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            loginIdentifier = ManagerOversightWebApplicationFactory.ManagerEmail,
            password = ManagerOversightWebApplicationFactory.ManagerPassword,
            clientApp = "admin",
        });

        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response missing token.");
    }
}
