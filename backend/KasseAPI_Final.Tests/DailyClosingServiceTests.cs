using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DailyClosingServiceTests
{
    [Fact]
    public async Task GenerateClosingSummaryAsync_ExcludesStornoFromSalesTotals_AndListsStornos()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 10);
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);
        var noonUtc = fromUtc.AddHours(12);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosing_{Guid.NewGuid():N}")
                .Options,
            // Customer is tenant-scoped; run under the seeded tenant so the customer is visible.
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-test", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "REG1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = noonUtc,
            Status = RegisterStatus.Open,
            CreatedAt = noonUtc,
        });
        ctx.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "C",
            CustomerNumber = "00000001",
            TaxNumber = "ATU12345678",
            CreatedAt = noonUtc,
        });
        await ctx.SaveChangesAsync();

        var cust = await ctx.Customers.AsNoTracking().FirstAsync();
        var sale = new PaymentDetails
        {
            CustomerId = cust.Id,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "x",
            TseTimestamp = noonUtc,
            ReceiptNumber = "R-SALE",
            CreatedAt = noonUtc,
        };
        var storno = new PaymentDetails
        {
            CustomerId = cust.Id,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = -10m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "y",
            TseTimestamp = noonUtc,
            ReceiptNumber = "R-STO",
            IsStorno = true,
            StornoReason = StornoReason.KundeStorniert,
            OriginalReceiptId = Guid.NewGuid(),
            CreatedAt = noonUtc.AddMinutes(1),
        };
        ctx.PaymentDetails.AddRange(sale, storno);
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var dto = await sut.GenerateClosingSummaryAsync(tenantId, regId, day);

        Assert.Equal(10m, dto.TotalSales);
        Assert.Equal(1, dto.ReceiptCount);
        Assert.Equal(10m, dto.TotalCash);
        Assert.Equal(10m, dto.PaymentBreakdown.Cash);
        Assert.Equal(10m, dto.PaymentBreakdown.Total);
        Assert.Single(dto.Stornos);
        Assert.Equal(-10m, dto.Stornos[0].TotalAmount);
        Assert.Equal(1, dto.StornoRowCount);
        Assert.Equal(-10m, dto.StornoTotalAmount);
        Assert.Equal(1, dto.TransactionBreakdown.Cash);
        Assert.Equal(0, dto.TransactionBreakdown.Card);
        Assert.Equal(1, dto.TransactionBreakdown.Cancellations);
        Assert.Equal(1, dto.TransactionBreakdown.Total);
    }

    [Fact]
    public async Task GenerateClosingSummaryAsync_AlignsPaymentAndFiscalTotals_ForPaidInvoices()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 11);
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);
        var noonUtc = fromUtc.AddHours(12);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosingFiscal_{Guid.NewGuid():N}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-fiscal", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "REG1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = noonUtc,
            Status = RegisterStatus.Open,
            CreatedAt = noonUtc,
        });
        ctx.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            Name = "C",
            CustomerNumber = "00000002",
            TaxNumber = "ATU12345678",
            CreatedAt = noonUtc,
        });
        await ctx.SaveChangesAsync();

        var cust = await ctx.Customers.AsNoTracking().FirstAsync();
        var paymentId = Guid.NewGuid();
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = cust.Id,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 146.50m,
            TaxAmount = 24.42m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "x",
            TseTimestamp = noonUtc,
            ReceiptNumber = "R-146",
            CreatedAt = noonUtc,
        });
        ctx.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            SourcePaymentId = paymentId,
            InvoiceNumber = "INV-146",
            InvoiceDate = noonUtc,
            DueDate = noonUtc,
            Subtotal = 122.08m,
            TaxAmount = 24.42m,
            TotalAmount = 146.50m,
            PaidAmount = 146.50m,
            RemainingAmount = 0m,
            CompanyName = "Test Co",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Addr",
            TseSignature = "sig",
            KassenId = "REG1",
            TseTimestamp = noonUtc,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            InvoiceItems = System.Text.Json.JsonDocument.Parse("[]"),
            Status = InvoiceStatus.Paid,
            CreatedAt = noonUtc,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var dto = await sut.GenerateClosingSummaryAsync(tenantId, regId, day);

        Assert.Equal(146.50m, dto.TotalSales);
        Assert.Equal(146.50m, dto.FiscalTotalAmount);
        Assert.Equal(0m, dto.SalesFiscalDelta);
        Assert.Equal(1, dto.FiscalTransactionCount);
    }

    [Fact]
    public async Task GenerateClosingSummaryAsync_BuildsTaxBreakdown_FromInvoiceTaxDetails()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 12);
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(day);
        var noonUtc = fromUtc.AddHours(12);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosingTax_{Guid.NewGuid():N}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-tax", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "REG1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = noonUtc,
            Status = RegisterStatus.Open,
            CreatedAt = noonUtc,
        });
        await ctx.SaveChangesAsync();

        var paymentId = Guid.NewGuid();
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "u1",
            TotalAmount = 12m,
            TaxAmount = 1.09m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "x",
            TseTimestamp = noonUtc,
            ReceiptNumber = "R-12",
            CreatedAt = noonUtc,
        });
        ctx.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            SourcePaymentId = paymentId,
            InvoiceNumber = "INV-12",
            InvoiceDate = noonUtc,
            DueDate = noonUtc,
            Subtotal = 10.91m,
            TaxAmount = 1.09m,
            TotalAmount = 12m,
            PaidAmount = 12m,
            RemainingAmount = 0m,
            CompanyName = "Test Co",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Addr",
            TseSignature = "sig",
            KassenId = "REG1",
            TseTimestamp = noonUtc,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{\"reduced\":1.09}"),
            InvoiceItems = System.Text.Json.JsonDocument.Parse("[]"),
            Status = InvoiceStatus.Paid,
            CreatedAt = noonUtc,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var dto = await sut.GenerateClosingSummaryAsync(tenantId, regId, day);

        Assert.Equal(1.09m, dto.TaxBreakdown.TaxAt10);
        Assert.Equal(11.99m, dto.TaxBreakdown.GrossAt10);
    }

    [Fact]
    public async Task CreateDailyClosingAsync_PersistsRksvPhase1Fields_AndReturnsBreakdowns()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
        var noonUtc = fromUtc.AddHours(12);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosingCreate_{Guid.NewGuid():N}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-create", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = noonUtc,
            Status = RegisterStatus.Open,
            CreatedAt = noonUtc,
        });
        var paymentId = Guid.NewGuid();
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CashRegisterId = regId,
            TotalAmount = 20m,
            TaxAmount = 3.33m,
            PaymentMethodRaw = "0",
            CashierId = "cashier-test",
            CustomerName = "Guest",
            Steuernummer = "ATU12345678",
            TseSignature = "sig",
            TseTimestamp = noonUtc,
            ReceiptNumber = "R-20",
            CreatedAt = noonUtc,
            IsActive = true,
        });
        ctx.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            SourcePaymentId = paymentId,
            InvoiceNumber = "INV-20",
            InvoiceDate = noonUtc,
            DueDate = noonUtc,
            Subtotal = 16.67m,
            TaxAmount = 3.33m,
            TotalAmount = 20m,
            PaidAmount = 20m,
            RemainingAmount = 0m,
            CompanyName = "Test Co",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Addr",
            TseSignature = "sig",
            KassenId = "K1",
            TseTimestamp = noonUtc,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{\"standard\":3.33}"),
            InvoiceItems = System.Text.Json.JsonDocument.Parse("[]"),
            Status = InvoiceStatus.Paid,
            CreatedAt = noonUtc,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var result = await sut.CreateDailyClosingAsync(regId);

        Assert.True(result.Success);
        Assert.NotNull(result.Closing);
        Assert.Equal(20m, result.Closing!.TotalAmount);
        Assert.Equal("eyJhbGciOiJFUzI1NiJ9.eyJ.test.daily.closing", result.Closing.TseSignature);
        Assert.Equal(1, result.Closing.SignatureChainLength);
        Assert.True(result.Closing.IsSimulated);
        Assert.Equal("Demo", result.Closing.Environment);
        Assert.Contains("DEMO", result.Closing.RksvFooter, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(20m, result.PaymentBreakdown.Cash);
        Assert.Equal(3.33m, result.TaxBreakdown.TaxAt20);

        var persisted = await ctx.DailyClosings.SingleAsync();
        Assert.Equal("Daily", persisted.ClosingType);
        Assert.Equal("thumb-test", persisted.CertificateThumbprint);
        Assert.True(persisted.IsSimulated);
    }
}
