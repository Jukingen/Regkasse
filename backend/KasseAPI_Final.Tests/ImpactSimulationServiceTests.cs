using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ImpactSimulationServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
    private static readonly Guid ProductCoffeeId = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000001");
    private static readonly Guid ProductBreadId = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000002");
    private static readonly Guid RegisterId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ImpactSim_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(null));
    }

    private sealed class FixedTenantAccessor(Guid? tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        db.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Impact Tenant",
            Slug = "impact-tenant",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CompanyName = "Impact GmbH",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            Currency = "EUR",
            Country = "AT",
            Language = "de",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm",
            TaxCalculationMethod = "inclusive",
            InvoiceNumbering = "INV-{yyyy}-{seq}",
            ReceiptNumbering = "R-{seq}",
            DefaultPaymentMethod = "Cash",
            BusinessHours = new Dictionary<string, string>(),
            WorkingHours = WorkingHoursSettings.CreateDefault(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        db.CashRegisters.Add(new CashRegister
        {
            Id = RegisterId,
            TenantId = TenantId,
            RegisterNumber = "K-1",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        db.Products.AddRange(
            new Product
            {
                Id = ProductCoffeeId,
                TenantId = TenantId,
                Name = "Coffee",
                Price = 120m,
                TaxType = TaxTypes.Standard,
                TaxRate = 20m,
                StockQuantity = 10,
                MinStockLevel = 0,
                Unit = "Stk",
                Barcode = "bc-coffee",
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            },
            new Product
            {
                Id = ProductBreadId,
                TenantId = TenantId,
                Name = "Bread",
                Price = 50m,
                TaxType = TaxTypes.Reduced,
                TaxRate = 10m,
                StockQuantity = 10,
                MinStockLevel = 0,
                Unit = "Stk",
                Barcode = "bc-bread",
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Reduced,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CashRegisterId = RegisterId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 12m,
            TaxAmount = 2m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        var now = DateTime.UtcNow;
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            InvoiceNumber = "INV-1",
            InvoiceDate = now,
            DueDate = now.AddDays(14),
            Status = InvoiceStatus.Draft,
            Subtotal = 10m,
            TaxAmount = 2m,
            TotalAmount = 12m,
            PaidAmount = 0m,
            RemainingAmount = 12m,
            CompanyName = "Impact GmbH",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Wien",
            TseSignature = "test-sig",
            KassenId = "K-1",
            TseTimestamp = now,
            CashRegisterId = RegisterId,
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = now,
            IsActive = true,
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public void CalculateTaxRateCatalogImpact_GrossInclusive_ComputesDelta()
    {
        // 120 gross @ 20% => net 100; @ 10% => gross 110; delta -10
        var delta = ImpactSimulationService.CalculateTaxRateCatalogImpact(120m, 20m, 10m);
        Assert.Equal(-10m, delta);
    }

    [Fact]
    public async Task SimulateTaxRateChange_CountsMatchingProductsAndPayments()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = new ImpactSimulationService(db);

        var report = await service.SimulateTaxRateChangeAsync(TenantId, newRate: 19m, currentRateOverride: 20m);

        Assert.Equal(ChangeType.TaxRate, report.ChangeType);
        Assert.Equal(1, report.AffectedRecords.Products);
        Assert.Equal(1, report.AffectedRecords.Payments);
        Assert.Equal(1, report.AffectedRecords.Invoices);
        Assert.Equal(-1m, report.EstimatedFinancialImpact);
        Assert.Contains(report.Warnings, w => w.Contains("product", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.Warnings, w => w.Contains('⚠'));
    }

    [Fact]
    public async Task SimulateCurrencyChange_WarnsWhenPaymentsExist()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = new ImpactSimulationService(db);

        var report = await service.SimulateCurrencyChangeAsync(TenantId, "USD");

        Assert.Equal(ChangeType.Currency, report.ChangeType);
        Assert.Equal(2, report.AffectedRecords.Products);
        Assert.Equal(1, report.AffectedRecords.Payments);
        Assert.Equal(ImpactSeverity.Warning, report.Severity);
        Assert.Contains(report.Warnings, w => w.Contains("historical", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("EUR", report.Summary);
        Assert.Contains("USD", report.Summary);
    }

    [Fact]
    public async Task SimulatePriceChange_SumsDeltaForMatchedProducts()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = new ImpactSimulationService(db);

        var report = await service.SimulatePriceChangeAsync(
            TenantId,
            [
                new ProductPriceUpdate { ProductId = ProductCoffeeId, NewPrice = 130m },
                new ProductPriceUpdate
                {
                    ProductId = Guid.Parse("aaaaaaaa-0001-0001-0001-000000000099"),
                    NewPrice = 1m,
                },
            ]);

        Assert.Equal(ChangeType.ProductPrice, report.ChangeType);
        Assert.Equal(1, report.AffectedRecords.Products);
        Assert.Equal(10m, report.EstimatedFinancialImpact);
        Assert.Contains(report.Warnings, w => w.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SimulateChangeAsync_DispatchesByChangeType()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var service = new ImpactSimulationService(db);

        var report = await service.SimulateChangeAsync(TenantId, ChangeType.Currency, "CHF");

        Assert.Equal(ChangeType.Currency, report.ChangeType);
        Assert.Contains("CHF", report.Summary);
    }
}
