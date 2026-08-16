using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
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
        Assert.False(persisted.IsBackdated);
        Assert.False(result.IsBackdated);
    }

    [Fact]
    public async Task CreateDailyClosingAsync_WhenPastBusinessDay_SetsIsBackdatedAndKeepsRealCreatedAt()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var pastDay = viennaToday.AddDays(-3);
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(pastDay);
        var noonUtc = fromUtc.AddHours(12);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosingBackdated_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-back-dc", IsActive = true });
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
            TotalAmount = 15m,
            TaxAmount = 2.5m,
            PaymentMethodRaw = "0",
            CashierId = "cashier-test",
            CustomerName = "Guest",
            Steuernummer = "ATU12345678",
            TseSignature = "sig",
            TseTimestamp = noonUtc,
            ReceiptNumber = "R-15",
            CreatedAt = noonUtc,
            IsActive = true,
        });
        ctx.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            SourcePaymentId = paymentId,
            InvoiceNumber = "INV-15",
            InvoiceDate = noonUtc,
            DueDate = noonUtc,
            Subtotal = 12.5m,
            TaxAmount = 2.5m,
            TotalAmount = 15m,
            PaidAmount = 15m,
            RemainingAmount = 0m,
            CompanyName = "Test Co",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Addr",
            TseSignature = "sig",
            KassenId = "K1",
            TseTimestamp = noonUtc,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{\"standard\":2.5}"),
            InvoiceItems = System.Text.Json.JsonDocument.Parse("[]"),
            Status = InvoiceStatus.Paid,
            CreatedAt = noonUtc,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var beforeUtc = DateTime.UtcNow.AddSeconds(-1);
        var sut = DailyClosingTestDoubles.Create(ctx);
        var result = await sut.CreateDailyClosingAsync(
            regId,
            pastDay,
            isBackdated: true,
            reason: "Technisches Problem / Systemausfall");
        var afterUtc = DateTime.UtcNow.AddSeconds(1);

        Assert.True(result.Success);
        Assert.True(result.IsBackdated);
        Assert.NotNull(result.Closing);
        Assert.True(result.Closing!.IsBackdated);
        Assert.Equal("Technisches Problem / Systemausfall", result.Closing.LateCreationReason);

        var persisted = await ctx.DailyClosings.SingleAsync();
        Assert.True(persisted.IsBackdated);
        Assert.Equal("Technisches Problem / Systemausfall", persisted.LateCreationReason);
        Assert.Equal(
            PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(pastDay),
            persisted.ClosingDate);
        Assert.InRange(persisted.CreatedAt, beforeUtc, afterUtc);
    }

    [Fact]
    public async Task CreateDailyClosingAsync_WhenFutureDate_Fails()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosingFuture_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-future-dc", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var result = await sut.CreateDailyClosingAsync(regId, viennaToday.AddDays(1));

        Assert.False(result.Success);
        Assert.Contains("future", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateDailyClosingAsync_WhenNoTransactions_CreatesEmptyClosing()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
        var noonUtc = fromUtc.AddHours(12);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosingEmpty_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-empty-dc", IsActive = true });
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
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var result = await sut.CreateDailyClosingAsync(regId);

        Assert.True(result.Success);
        Assert.True(result.IsEmpty);
        Assert.Equal(DailyClosingDayKinds.Empty, result.DayKind);
        Assert.NotNull(result.Closing);
        Assert.Equal(0, result.Closing!.TransactionCount);
        Assert.Equal(0m, result.Closing.TotalAmount);
        Assert.Equal(DailyClosingDayKinds.Empty, result.Closing.DayKind);
        Assert.True(result.Closing.IsEmpty);
        Assert.Equal("Daily", result.Closing.ClosingType);

        var persisted = await ctx.DailyClosings.SingleAsync();
        Assert.Equal(DailyClosingDayKinds.Empty, persisted.DayKind);
        Assert.Equal(0, persisted.TransactionCount);
        Assert.Equal("Daily", persisted.ClosingType);
        Assert.False(persisted.IsBackdated);
    }

    [Fact]
    public async Task CreateDailyClosingAsync_WhenEmptyClosingAlreadyExists_Fails()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var closingAnchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaToday);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosingEmptyDup_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-empty-dup", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            UserId = "cashier-test",
            ClosingDate = closingAnchorUtc,
            ClosingType = "Daily",
            DayKind = DailyClosingDayKinds.Empty,
            TotalAmount = 0m,
            TotalTaxAmount = 0m,
            TransactionCount = 0,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var result = await sut.CreateDailyClosingAsync(regId);

        Assert.False(result.Success);
        Assert.Contains("already", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetCalendarAsync_IncludesJanuaryFirstClosing_WhenUtcInstantIsPreviousYear()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var jan1 = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 1, 1);
        var closingAnchorUtc = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(jan1);
        Assert.True(closingAnchorUtc.Year == 2025, "January 1 Vienna midnight must land in previous UTC year (regression for .Year/.Month filters).");

        await using var ctx = CreateCalendarContext(tenantId);
        SeedRegister(ctx, tenantId, regId);
        ctx.DailyClosings.Add(CreateDailyClosing(tenantId, regId, closingAnchorUtc, DailyClosingDayKinds.Normal, transactionCount: 2));
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var calendar = await sut.GetCalendarAsync(tenantId, 2026, 1, regId);

        Assert.Equal(31, calendar.Days.Count);
        var day = calendar.Days.Single(d => d.Date == new DateOnly(2026, 1, 1));
        Assert.True(day.IsClosed);
        Assert.Equal(DailyClosingDayKinds.Normal, day.DayKind);
        Assert.Equal(DailyClosingDayKinds.Normal, day.ClosingType);
        Assert.False(day.CanClose);
        Assert.NotNull(day.ClosingId);
    }

    [Fact]
    public async Task GetCalendarAsync_MarksEmptyClosedOpenAndFutureDays()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var emptyDay = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 1);
        var openDay = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 2);
        var (openFromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(openDay);
        var noonUtc = openFromUtc.AddHours(12);

        await using var ctx = CreateCalendarContext(tenantId);
        SeedRegister(ctx, tenantId, regId);
        ctx.DailyClosings.Add(CreateDailyClosing(
            tenantId,
            regId,
            PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(emptyDay),
            DailyClosingDayKinds.Empty,
            transactionCount: 0));
        ctx.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            InvoiceNumber = "INV-CAL",
            InvoiceDate = noonUtc,
            DueDate = noonUtc,
            Subtotal = 10m,
            TaxAmount = 2m,
            TotalAmount = 12m,
            PaidAmount = 12m,
            RemainingAmount = 0m,
            CompanyName = "Test Co",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Addr",
            TseSignature = "sig",
            KassenId = "K1",
            TseTimestamp = noonUtc,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            InvoiceItems = System.Text.Json.JsonDocument.Parse("[]"),
            Status = InvoiceStatus.Paid,
            CreatedAt = noonUtc,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var calendar = await sut.GetCalendarAsync(tenantId, 2026, 5, regId);

        var empty = calendar.Days.Single(d => d.Date == new DateOnly(2026, 5, 1));
        Assert.True(empty.IsClosed);
        Assert.Equal(DailyClosingDayKinds.Empty, empty.DayKind);
        Assert.Equal(0, empty.TransactionCount);
        Assert.False(empty.CanClose);

        var open = calendar.Days.Single(d => d.Date == new DateOnly(2026, 5, 2));
        Assert.False(open.IsClosed);
        Assert.Null(open.DayKind);
        Assert.Equal(1, open.TransactionCount);
        Assert.True(open.CanClose);

        var noTx = calendar.Days.Single(d => d.Date == new DateOnly(2026, 5, 3));
        Assert.False(noTx.IsClosed);
        Assert.Equal(0, noTx.TransactionCount);
        Assert.True(noTx.CanClose);
    }

    [Fact]
    public async Task GetCalendarAsync_FutureDaysCannotClose_AndUnknownRegisterThrows()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        await using var ctx = CreateCalendarContext(tenantId);
        SeedRegister(ctx, tenantId, regId);
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var calendar = await sut.GetCalendarAsync(tenantId, 2099, 12, regId);
        Assert.All(calendar.Days, d =>
        {
            Assert.True(d.IsFuture);
            Assert.False(d.CanClose);
            Assert.False(d.IsClosed);
        });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.GetCalendarAsync(tenantId, 2026, 13, regId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.GetCalendarAsync(tenantId, 2026, 5, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetCalendarAsync_IgnoresMonthlyClosings()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 10);
        await using var ctx = CreateCalendarContext(tenantId);
        SeedRegister(ctx, tenantId, regId);
        var monthly = CreateDailyClosing(
            tenantId,
            regId,
            PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(day),
            DailyClosingDayKinds.Normal,
            transactionCount: 4);
        monthly.ClosingType = "Monthly";
        ctx.DailyClosings.Add(monthly);
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var calendar = await sut.GetCalendarAsync(tenantId, 2026, 5, regId);
        var cell = calendar.Days.Single(d => d.Date == new DateOnly(2026, 5, 10));
        Assert.False(cell.IsClosed);
        Assert.True(cell.CanClose);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_TodayOpenWithTransactions_RequiresAttention()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var today = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (fromUtc, _) = PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(today);
        var noonUtc = fromUtc.AddHours(12);

        await using var ctx = CreateCalendarContext(tenantId);
        SeedRegister(ctx, tenantId, regId);
        ctx.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            InvoiceNumber = "INV-DASH",
            InvoiceDate = noonUtc,
            DueDate = noonUtc,
            Subtotal = 10m,
            TaxAmount = 2m,
            TotalAmount = 12m,
            PaidAmount = 12m,
            RemainingAmount = 0m,
            CompanyName = "Test Co",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Addr",
            TseSignature = "sig",
            KassenId = "K1",
            TseTimestamp = noonUtc,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            InvoiceItems = System.Text.Json.JsonDocument.Parse("[]"),
            Status = InvoiceStatus.Paid,
            CreatedAt = noonUtc,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var summary = await sut.GetDashboardSummaryAsync(tenantId, regId);

        Assert.False(summary.Today.IsClosed);
        Assert.Equal(1, summary.Today.TransactionCount);
        Assert.True(summary.Today.CanClose);
        Assert.True(summary.RequiresAttention);
        Assert.Equal(7, summary.Week.TotalDays);
        Assert.True(summary.Week.OpenDays >= 1);
        Assert.Null(summary.LastClosing);
        Assert.True(summary.Week.Start <= DateOnly.FromDateTime(today));
        Assert.True(summary.Week.End >= DateOnly.FromDateTime(today));
        Assert.Equal(DayOfWeek.Monday, summary.Week.Start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, summary.Week.End.DayOfWeek);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_TodayClosedEmpty_DoesNotRequireAttention()
    {
        var tenantId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var today = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        await using var ctx = CreateCalendarContext(tenantId);
        SeedRegister(ctx, tenantId, regId);
        var closingId = Guid.NewGuid();
        var row = CreateDailyClosing(
            tenantId,
            regId,
            PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(today),
            DailyClosingDayKinds.Empty,
            transactionCount: 0);
        row.Id = closingId;
        ctx.DailyClosings.Add(row);
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        var summary = await sut.GetDashboardSummaryAsync(tenantId, regId);

        Assert.True(summary.Today.IsClosed);
        Assert.Equal(DailyClosingDayKinds.Empty, summary.Today.DayKind);
        Assert.False(summary.RequiresAttention);
        Assert.False(summary.Today.CanClose);
        Assert.Equal(closingId, summary.LastClosing?.ClosingId);
        Assert.True(summary.Week.ClosedDays >= 1);
        Assert.True(summary.Week.EmptyDays >= 1);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_UnknownRegister_Throws()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateCalendarContext(tenantId);
        SeedRegister(ctx, tenantId, Guid.NewGuid());
        await ctx.SaveChangesAsync();

        var sut = DailyClosingTestDoubles.Create(ctx);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            sut.GetDashboardSummaryAsync(tenantId, Guid.NewGuid()));
    }

    private static AppDbContext CreateCalendarContext(Guid tenantId) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"DailyClosingCal_{Guid.NewGuid():N}")
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

    private static void SeedRegister(AppDbContext ctx, Guid tenantId, Guid regId)
    {
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = $"t-{tenantId:N}"[..12], IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
    }

    private static DailyClosing CreateDailyClosing(
        Guid tenantId,
        Guid regId,
        DateTime closingAnchorUtc,
        string dayKind,
        int transactionCount) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            UserId = "cashier-test",
            ClosingDate = closingAnchorUtc,
            ClosingType = "Daily",
            DayKind = dayKind,
            TotalAmount = transactionCount,
            TotalTaxAmount = 0m,
            TransactionCount = transactionCount,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        };
}
