using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TagesabschlussServiceOperationalTests
{
    private static AppDbContext CreateContext(Guid tenantId)
    {
        return new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"TagesabschlussOp_{Guid.NewGuid():N}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static TagesabschlussService CreateService(
        AppDbContext ctx,
        IDevelopmentModeService? developmentMode = null,
        TseOptions? tseOptions = null) =>
        new(
            ctx,
            Mock.Of<ITseService>(s =>
                s.GetTseStatusAsync() == Task.FromResult(new TseStatus
                {
                    IsConnected = false,
                    Status = "Disconnected",
                    ErrorMessage = "TSE device is not connected",
                })),
            new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
            new SoftwareTseKeyProvider(),
            Mock.Of<IFinanzOnlineService>(),
            Options.Create(tseOptions ?? new TseOptions { Mode = "Real", TseMode = "Device" }),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Development),
            NullLogger<TagesabschlussService>.Instance,
            Mock.Of<IReportPdfCaptureService>(),
            Mock.Of<IReportPdfStorageService>(),
            developmentMode);

    [Fact]
    public async Task ResolveOperationalCashRegisterIdAsync_WhenNull_PicksDefaultThenFirst()
    {
        var tenantId = Guid.NewGuid();
        await using var ctx = CreateContext(tenantId);
        var defaultId = Guid.NewGuid();
        var otherId = Guid.NewGuid();

        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-op", IsActive = true });
        ctx.CashRegisters.AddRange(
            new CashRegister
            {
                Id = otherId,
                TenantId = tenantId,
                RegisterNumber = "K2",
                Location = "Bar",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsDefaultForTenant = false,
            },
            new CashRegister
            {
                Id = defaultId,
                TenantId = tenantId,
                RegisterNumber = "K1",
                Location = "Haupt",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CreatedAt = DateTime.UtcNow,
                IsDefaultForTenant = true,
            });
        await ctx.SaveChangesAsync();

        var sut = CreateService(ctx);
        var resolved = await sut.ResolveOperationalCashRegisterIdAsync(tenantId, null);

        Assert.Equal(defaultId, resolved);
    }

    [Fact]
    public async Task GetClosingHistoryAsync_ReturnsRegisterScopedRows_NotFilteredByActorUser()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var closingDay = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(
            PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(2026, 5, 1));

        await using var ctx = CreateContext(tenantId);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-hist", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
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
            CashRegisterId = registerId,
            UserId = "manager-user",
            ClosingDate = closingDay,
            ClosingType = "Daily",
            TotalAmount = 120m,
            TotalTaxAmount = 20m,
            TransactionCount = 3,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var sut = CreateService(ctx);
        var history = await sut.GetClosingHistoryAsync(
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 1),
            registerId);

        Assert.Single(history);
        Assert.Equal(120m, history[0].TotalAmount);
        Assert.Equal("manager-user", await ctx.DailyClosings.Select(d => d.UserId).FirstAsync());
    }

    [Fact]
    public async Task PerformDailyClosingAsync_WhenDevBypassTse_AllowsFakeClosingWithoutHardware()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var userId = "dev-user";
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var (dayStartUtc, dayEndExclusiveUtc) =
            PostgreSqlUtcDateTime.AustriaLocalCalendarDayToUtcRange(viennaToday);
        var noonUtc = dayStartUtc.AddHours(12);

        await using var ctx = CreateContext(tenantId);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-dev", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
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

        var custId = await ctx.Customers.Select(c => c.Id).FirstAsync();
        ctx.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            InvoiceNumber = "INV-1",
            InvoiceDate = noonUtc,
            DueDate = noonUtc,
            Subtotal = 8m,
            TaxAmount = 2m,
            TotalAmount = 10m,
            PaidAmount = 10m,
            RemainingAmount = 0m,
            CompanyName = "Test Co",
            CompanyTaxNumber = "ATU12345678",
            CompanyAddress = "Addr",
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            InvoiceItems = System.Text.Json.JsonDocument.Parse("[]"),
            Status = InvoiceStatus.Paid,
            CreatedAt = noonUtc,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(s => s.GetTseStatusAsync()).ReturnsAsync(new TseStatus
        {
            IsConnected = false,
            Status = "Disconnected",
            ErrorMessage = "TSE device is not connected",
        });
        tseMock
            .Setup(s => s.CreateDailyClosingSignatureAsync(
                registerId,
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<int>()))
            .ReturnsAsync("dev-daily-closing-jws");

        var devMode = Mock.Of<IDevelopmentModeService>(d => d.ShouldBypassTseCheck() == true);

        var sut = new TagesabschlussService(
            ctx,
            tseMock.Object,
            new FakeTseProvider(NullLogger<FakeTseProvider>.Instance),
            new SoftwareTseKeyProvider(),
            Mock.Of<IFinanzOnlineService>(f => f.IsEnabledAsync() == Task.FromResult(false)),
            Options.Create(new TseOptions { Mode = "Real", TseMode = "Device" }),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Development),
            NullLogger<TagesabschlussService>.Instance,
            Mock.Of<IReportPdfCaptureService>(),
            Mock.Of<IReportPdfStorageService>(),
            devMode);

        var result = await sut.PerformDailyClosingAsync(userId, registerId);

        Assert.True(result.Success);
        Assert.Equal(1, result.TransactionCount);
    }

    [Fact]
    public async Task PerformDailyClosingAsync_WhenAlreadyClosedToday_ReturnsBlocked()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var userId = "dev-user";
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var closingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaToday);

        await using var ctx = CreateContext(tenantId);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-dup", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            TenantId = tenantId,
            CashRegisterId = registerId,
            UserId = userId,
            ClosingDate = closingDate,
            ClosingType = "Daily",
            TotalAmount = 10m,
            TotalTaxAmount = 2m,
            TransactionCount = 1,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var sut = CreateService(ctx);
        var result = await sut.PerformDailyClosingAsync(userId, registerId);

        Assert.False(result.Success);
        Assert.Contains("already performed for today", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await ctx.DailyClosings.CountAsync());
    }

    [Fact]
    public async Task PerformMonthlyClosingAsync_WhenAlreadyClosedThisMonth_ReturnsBlocked()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var userId = "dev-user";
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var monthAnchor = new DateTime(viennaToday.Year, viennaToday.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        await using var ctx = CreateContext(tenantId);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-mon", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            TenantId = tenantId,
            CashRegisterId = registerId,
            UserId = userId,
            ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(monthAnchor),
            ClosingType = "Monthly",
            TotalAmount = 10m,
            TotalTaxAmount = 2m,
            TransactionCount = 1,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var sut = CreateService(ctx);
        var result = await sut.PerformMonthlyClosingAsync(userId, registerId);

        Assert.False(result.Success);
        Assert.Contains("current month", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PerformYearlyClosingAsync_WhenAlreadyClosedThisYear_ReturnsBlocked()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var userId = "dev-user";
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var yearAnchor = new DateTime(viennaToday.Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        await using var ctx = CreateContext(tenantId);
        ctx.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t-year", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            TenantId = tenantId,
            CashRegisterId = registerId,
            UserId = userId,
            ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(yearAnchor),
            ClosingType = "Yearly",
            TotalAmount = 10m,
            TotalTaxAmount = 2m,
            TransactionCount = 1,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var sut = CreateService(ctx);
        var result = await sut.PerformYearlyClosingAsync(userId, registerId);

        Assert.False(result.Success);
        Assert.Contains("current year", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Model_DefinesUniqueCompletedClosingPeriodIndex()
    {
        using var ctx = CreateContext(Guid.NewGuid());
        var entity = ctx.Model.FindEntityType(typeof(DailyClosing))!;
        var periodIndex = entity.GetIndexes()
            .Single(ix =>
                ix.IsUnique &&
                ix.Properties.Select(p => p.Name).SequenceEqual(new[]
                {
                    nameof(DailyClosing.CashRegisterId),
                    nameof(DailyClosing.ClosingDate),
                    nameof(DailyClosing.ClosingType),
                }));

        Assert.Equal("\"Status\" = 'Completed'", periodIndex.GetFilter());
    }
}
