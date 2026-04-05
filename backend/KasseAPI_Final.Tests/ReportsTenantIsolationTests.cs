using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// ReportsController: financial aggregates must not include invoices/payments from other tenants' cash registers.
/// Product report lines are scoped by <see cref="Product.TenantId"/> (orders have no tenant FK).
/// </summary>
public sealed class ReportsTenantIsolationTests
{
    private static readonly Guid TenantB = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RepTenant_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static void EnsureTenants(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        if (!ctx.Tenants.AsNoTracking().Any(t => t.Id == TenantB))
            ctx.Tenants.Add(new Tenant { Id = TenantB, Name = "Tenant B", Slug = "rep-isolation-b" });
    }

    private static Invoice MinimalInvoice(Guid id, Guid cashRegisterId, decimal total, DateTime invoiceDateUtc)
    {
        return new Invoice
        {
            Id = id,
            InvoiceNumber = $"INV-{id:N}"[..12],
            InvoiceDate = invoiceDateUtc,
            DueDate = invoiceDateUtc,
            Status = InvoiceStatus.Paid,
            Subtotal = total * 0.9m,
            TaxAmount = total * 0.1m,
            TotalAmount = total,
            PaidAmount = total,
            RemainingAmount = 0,
            CustomerName = "Cust",
            CompanyName = "Co",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Addr",
            TseSignature = "sig",
            KassenId = "K1",
            TseTimestamp = invoiceDateUtc,
            CashRegisterId = cashRegisterId,
            TaxDetails = JsonDocument.Parse("{}"),
            IsActive = true
        };
    }

    private static PaymentDetails MinimalPayment(Guid id, Guid customerId, Guid cashRegisterId, DateTime createdAt)
    {
        return new PaymentDetails
        {
            Id = id,
            CustomerId = customerId,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "cashier",
            TotalAmount = 50m,
            TaxAmount = 5m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "sig",
            TseTimestamp = createdAt,
            CashRegisterId = cashRegisterId,
            ReceiptNumber = "R1",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            IsActive = true
        };
    }

    private static async Task<(Guid regA, Guid regB, decimal totalA, decimal totalB)> SeedInvoicesAndPaymentsAsync(AppDbContext ctx)
    {
        EnsureTenants(ctx);
        var custA = Guid.NewGuid();
        var custB = Guid.NewGuid();
        ctx.Customers.Add(new Customer { Id = custA, Name = "A", Email = "a@a.com", Phone = "1", IsActive = true });
        ctx.Customers.Add(new Customer { Id = custB, Name = "B", Email = "b@b.com", Phone = "2", IsActive = true });
        var regA = Guid.NewGuid();
        var regB = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regA,
            RegisterNumber = "KA",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = TenantB,
            Id = regB,
            RegisterNumber = "KB",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        var day = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        const decimal totalA = 100m;
        const decimal totalB = 250m;
        ctx.Invoices.Add(MinimalInvoice(Guid.NewGuid(), regA, totalA, day));
        ctx.Invoices.Add(MinimalInvoice(Guid.NewGuid(), regB, totalB, day));
        ctx.PaymentDetails.Add(MinimalPayment(Guid.NewGuid(), custA, regA, day));
        ctx.PaymentDetails.Add(MinimalPayment(Guid.NewGuid(), custB, regB, day));
        await ctx.SaveChangesAsync();
        return (regA, regB, totalA, totalB);
    }

    private static ReportsController ControllerForTenantA(AppDbContext ctx) =>
        new(ctx, NullLogger<ReportsController>.Instance, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary));

    [Fact]
    public async Task GetSalesReport_AsTenantA_ExcludesTenantBInvoices()
    {
        await using var ctx = CreateContext();
        var (_, _, totalA, totalB) = await SeedInvoicesAndPaymentsAsync(ctx);
        var c = ControllerForTenantA(ctx);
        var result = await c.GetSalesReport(new DateTime(2026, 4, 1), new DateTime(2026, 4, 1));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var report = Assert.IsType<SalesReport>(ok.Value);
        Assert.Equal(1, report.TotalInvoices);
        Assert.Equal(totalA, report.TotalSales);
        Assert.NotEqual(totalA + totalB, report.TotalSales);
    }

    [Fact]
    public async Task GetCustomerReport_AsTenantA_UsesOnlyTenantAInvoices()
    {
        await using var ctx = CreateContext();
        var (_, _, totalA, _) = await SeedInvoicesAndPaymentsAsync(ctx);
        var c = ControllerForTenantA(ctx);
        var result = await c.GetCustomerReport(new DateTime(2026, 4, 1), new DateTime(2026, 4, 1));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var report = Assert.IsType<CustomerReport>(ok.Value);
        Assert.Equal(1, report.TotalOrders);
        Assert.Equal(totalA, report.TopCustomers.Sum(t => t.TotalSpent));
    }

    [Fact]
    public async Task GetPaymentReport_AsTenantA_ExcludesTenantBPayments()
    {
        await using var ctx = CreateContext();
        await SeedInvoicesAndPaymentsAsync(ctx);
        var c = ControllerForTenantA(ctx);
        var result = await c.GetPaymentReport(new DateTime(2026, 4, 1), new DateTime(2026, 4, 1));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var report = Assert.IsType<PaymentReport>(ok.Value);
        Assert.Equal(1, report.TotalPayments);
        Assert.Equal(50m, report.TotalAmount);
    }

    [Fact]
    public async Task ExportSalesReport_Json_AsTenantA_ReturnsOnlyTenantAInvoices()
    {
        await using var ctx = CreateContext();
        await SeedInvoicesAndPaymentsAsync(ctx);
        var c = ControllerForTenantA(ctx);
        var result = await c.ExportSalesReport(new DateTime(2026, 4, 1), new DateTime(2026, 4, 1), "json");
        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<Invoice>>(ok.Value);
        Assert.Single(list);
    }

    [Fact]
    public async Task GetProductReport_AsTenantA_CountsOnlyLinesForTenantProducts()
    {
        await using var ctx = CreateContext();
        EnsureTenants(ctx);
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        ctx.Categories.Add(new Category { Id = catA, TenantId = LegacyDefaultTenantIds.Primary, Name = "CA", VatRate = 20m });
        ctx.Categories.Add(new Category { Id = catB, TenantId = TenantB, Name = "CB", VatRate = 20m });
        var prodA = Guid.NewGuid();
        var prodB = Guid.NewGuid();
        ctx.Products.Add(new Product
        {
            Id = prodA,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "PA",
            Price = 10m,
            TaxType = TaxTypes.Standard,
            Category = "CA",
            StockQuantity = 100,
            MinStockLevel = 0,
            Unit = "pcs",
            Barcode = "b1",
            CategoryId = catA,
            IsActive = true
        });
        ctx.Products.Add(new Product
        {
            Id = prodB,
            TenantId = TenantB,
            Name = "PB",
            Price = 20m,
            TaxType = TaxTypes.Standard,
            Category = "CB",
            StockQuantity = 100,
            MinStockLevel = 0,
            Unit = "pcs",
            Barcode = "b2",
            CategoryId = catB,
            IsActive = true
        });
        var day = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        const string orderKey = "ORD-MIX-1";
        ctx.Orders.Add(new Order
        {
            OrderId = orderKey,
            OrderDate = day,
            Status = OrderStatus.Completed,
            Subtotal = 30m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 30m,
            IsActive = true
        });
        ctx.OrderItems.Add(new OrderItem
        {
            OrderId = orderKey,
            ProductId = prodA,
            ProductName = "PA",
            Quantity = 1,
            UnitPrice = 10m,
            TaxRate = 20m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 10m,
            ProductCategory = "CA"
        });
        ctx.OrderItems.Add(new OrderItem
        {
            OrderId = orderKey,
            ProductId = prodB,
            ProductName = "PB",
            Quantity = 1,
            UnitPrice = 20m,
            TaxRate = 20m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 20m,
            ProductCategory = "CB"
        });
        await ctx.SaveChangesAsync();

        var c = ControllerForTenantA(ctx);
        var result = await c.GetProductReport(new DateTime(2026, 5, 1), new DateTime(2026, 5, 1));
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var report = Assert.IsType<ProductReport>(ok.Value);
        Assert.Equal(10m, report.TotalRevenue);
        Assert.Equal(1, report.TotalProductsSold);
    }
}
