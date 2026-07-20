using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Metrics;
using KasseAPI_Final.Services.Order;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OnlineOrderIntakeServiceTests
{
    [Fact]
    public async Task CreateAsync_rejects_when_closed_special_day()
    {
        var (sut, tenantId, productId) = await CreateSutAsync(
            hours: ClosedChristmasHours(),
            utcNow: new DateTimeOffset(2026, 12, 24, 11, 0, 0, TimeSpan.Zero));

        var result = await sut.CreateAsync(ValidRequest(productId));

        Assert.False(result.Succeeded);
        Assert.Equal(OnlineOrderIntakeService.ClosedCode, result.Code);
    }

    [Fact]
    public async Task CreateAsync_accepts_during_open_hours()
    {
        var (sut, tenantId, productId) = await CreateSutAsync(
            hours: WorkingHoursSettings.CreateDefault(),
            // Monday 2026-07-20 12:00 Vienna = 10:00 UTC
            utcNow: new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));

        var result = await sut.CreateAsync(ValidRequest(productId));

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.OrderNumber));
        Assert.Equal(5m, result.Total);
    }

    [Fact]
    public async Task CreateAsync_rejects_in_stop_window()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.StopOnlineOrdersMinutesBeforeClose = 30;
        hours.Monday = new WorkingHoursDay { OpenTime = "09:00", CloseTime = "22:00", IsClosed = false };
        hours.Normalize();

        var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var local = new DateTime(2026, 7, 20, 21, 45, 0, DateTimeKind.Unspecified);
        var utc = new DateTimeOffset(local, tz.GetUtcOffset(local));

        var (sut, _, productId) = await CreateSutAsync(hours, utc);
        var result = await sut.CreateAsync(ValidRequest(productId));

        Assert.False(result.Succeeded);
        Assert.Equal(OnlineOrderIntakeService.ClosedCode, result.Code);
    }

    private static WorkingHoursSettings ClosedChristmasHours()
    {
        var hours = WorkingHoursSettings.CreateDefault();
        hours.ClosedDayMessage = "Heiligabend geschlossen";
        hours.SpecialDays =
        [
            new WorkingHoursSpecialDay { Date = "2026-12-24", IsClosed = true },
        ];
        hours.Normalize();
        return hours;
    }

    private static CreatePublicOnlineOrderRequestDto ValidRequest(Guid productId) =>
        new()
        {
            Tenant = "demo-cafe",
            CustomerName = "Max Mustermann",
            CustomerPhone = "+431234567890",
            Items = [new CreatePublicOnlineOrderItemDto { ProductId = productId, Quantity = 2 }],
        };

    private static async Task<(OnlineOrderIntakeService Sut, Guid TenantId, Guid ProductId)> CreateSutAsync(
        WorkingHoursSettings hours,
        DateTimeOffset utcNow)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        var factory = new Factory(options);
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo Cafe",
            Slug = "demo-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Demo Cafe",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            BusinessHours = new Dictionary<string, string>(),
            WorkingHours = hours,
            Currency = "EUR",
            Language = "de-DE",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm:ss",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
            IsActive = true,
        });
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = "Espresso",
            Price = 2.5m,
            Category = "Drinks",
            TaxType = 1,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var sut = new OnlineOrderIntakeService(
            factory,
            new FixedTimeProvider(utcNow),
            new BusinessMetricsService(),
            NullLogger<OnlineOrderIntakeService>.Instance);
        return (sut, tenantId, productId);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options));
    }
}
