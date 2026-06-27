using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class InvoicePdfGeneratorTests
{
    private AppDbContext _db = null!;
    private IDbContextFactory<AppDbContext> _factory = null!;
    private BillingService _billingService = null!;

    [Fact]
    public async Task GenerateInvoicePdf_ValidSale_ReturnsPdfBytes()
    {
        var service = CreatePdfGenerator();
        var sale = await CreateTestSale();

        var pdfBytes = await service.GenerateInvoicePdfAsync(sale.Id);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdfBytes[..4]));
    }

    [Fact]
    public async Task GeneratePreviewPdf_ValidPreview_ReturnsPdfBytes()
    {
        var service = CreatePdfGenerator();
        var preview = new LicenseSalePreviewResponse
        {
            TenantName = "Test Tenant",
            TenantSlug = "test",
            LicenseKey = "REGK-20261231-test-A7F3K2D9",
            PriceNet = 299.00m,
            VatRate = 20.00m,
            VatAmount = 59.80m,
            PriceGross = 358.80m,
            ValidFromUtc = DateTime.UtcNow,
            ValidUntilUtc = DateTime.UtcNow.AddDays(365),
            DurationDays = 365,
            InvoiceNumber = "RE20260841",
            LicensePlan = "12_months",
            Currency = "EUR",
        };

        var pdfBytes = await service.GeneratePreviewPdfAsync(preview);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdfBytes[..4]));
    }

    private InvoicePdfGenerator CreatePdfGenerator()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"InvoicePdfGenerator_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        _factory = TenantTestDoubles.DbContextFactoryForTests(options, NullCurrentTenantAccessor.Instance);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(e => e.ContentRootPath).Returns(ResolveBackendContentRoot());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Company:Name"] = "Regkasse Software",
                ["Company:Address"] = "Hans Grüneis-Gasse 3, 2700 Wiener Neustadt",
                ["Company:VatId"] = "ATU12345678",
                ["Company:Phone"] = "+43 123 456 789",
                ["Company:Email"] = "info@regkasse.at",
                ["Company:Website"] = "www.regkasse.at",
                ["Company:Bank:Iban"] = "AT00 0000 0000 0000 0000",
                ["Company:Bank:Bic"] = "XXXAT2B",
            })
            .Build();

        var templateService = new InvoicePdfTemplateService(configuration, environment.Object);

        BillingService? billingService = null;
        var services = new ServiceCollection();
        services.AddSingleton<Func<IBillingService>>(() => billingService!);
        services.AddScoped<IBillingService>(sp => sp.GetRequiredService<Func<IBillingService>>()());
        var provider = services.BuildServiceProvider();

        var generator = new InvoicePdfGenerator(
            _factory,
            provider.GetRequiredService<IServiceScopeFactory>(),
            templateService,
            NullLogger<InvoicePdfGenerator>.Instance,
            configuration);

        billingService = new BillingService(
            _factory,
            new LicenseKeyGenerator(),
            BillingTestDoubles.CreateAuditService(_factory),
            BillingTestDoubles.CreateReminderScopeFactory(),
            environment.Object,
            generator,
            BillingTestDoubles.DisabledBackupOptions,
            NullLogger<BillingService>.Instance);

        _billingService = billingService;
        return generator;
    }

    private async Task<LicenseSale> CreateTestSale()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Test Tenant",
            Slug = "test",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Tenants.Add(tenant);

        var userId = Guid.NewGuid();
        _db.Users.Add(new ApplicationUser
        {
            Id = userId.ToString("D"),
            UserName = "billing.tester",
            NormalizedUserName = "BILLING.TESTER",
            Email = "billing.tester@regkasse.test",
            NormalizedEmail = "BILLING.TESTER@REGKASSE.TEST",
            FirstName = "Billing",
            LastName = "Tester",
            EmailConfirmed = true,
        });
        await _db.SaveChangesAsync().ConfigureAwait(false);

        var saleResponse = await _billingService.CreateLicenseSaleAsync(
            new CreateLicenseSaleRequest
            {
                TenantId = tenant.Id,
                LicensePlan = LicenseSalePlans.TwelveMonths,
                PriceNet = 299.00m,
            },
            userId).ConfigureAwait(false);

        return await _db.LicenseSales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(s => s.Id == saleResponse.Id)
            .ConfigureAwait(false);
    }

    private static string ResolveBackendContentRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Templates", "InvoiceTemplate.html")))
                return directory.FullName;

            if (File.Exists(Path.Combine(directory.FullName, "KasseAPI_Final.csproj")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return AppContext.BaseDirectory;
    }
}
