using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Website;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace KasseAPI_Final.Tests;

public sealed class PublicTenantCatalogServiceTests
{
    [Fact]
    public async Task GetProfile_and_menu_by_slug()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var factory = new Factory(options);
        var tenantId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo Cafe",
            Slug = "demo-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Drinks",
            Key = "drinks",
            IsActive = true,
            SortOrder = 1,
            Color = "#112233"
        });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Espresso",
            Price = 2.5m,
            Category = "Drinks",
            CategoryId = categoryId,
            TaxType = 1,
            IsActive = true
        });
        await db.SaveChangesAsync();

        // Monday 2026-07-20 12:00 Vienna = 10:00 UTC — within default 09:00–22:00
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));
        var sut = new PublicTenantCatalogService(factory, time);
        var profile = await sut.GetProfileAsync("demo-cafe");
        Assert.NotNull(profile);
        Assert.Equal("Demo Cafe", profile!.DisplayName);
        Assert.Equal("#112233", profile.PrimaryColor);
        Assert.True(profile.AcceptingOnlineOrders);
        Assert.True(profile.RestaurantIsOpen);

        var menu = await sut.GetMenuAsync("demo-cafe");
        Assert.NotNull(menu);
        Assert.Single(menu!.Items);
        Assert.Equal("Espresso", menu.Items[0].Name);
        Assert.Single(menu.Categories);
    }

    [Fact]
    public async Task GetProfile_rejects_online_orders_on_closed_special_day()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var factory = new Factory(options);
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo Cafe",
            Slug = "demo-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        var hours = WorkingHoursSettings.CreateDefault();
        hours.ClosedDayMessage = "Heiligabend geschlossen";
        hours.SpecialDays =
        [
            new WorkingHoursSpecialDay { Date = "2026-12-24", IsClosed = true }
        ];
        hours.Normalize();

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
            IsActive = true
        });
        await db.SaveChangesAsync();

        // 2026-12-24 12:00 Vienna (CET) = 11:00 UTC
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 12, 24, 11, 0, 0, TimeSpan.Zero));
        var sut = new PublicTenantCatalogService(factory, time);
        var profile = await sut.GetProfileAsync("demo-cafe");

        Assert.NotNull(profile);
        Assert.False(profile!.AcceptingOnlineOrders);
        Assert.False(profile.RestaurantIsOpen);
        Assert.Equal("Heiligabend geschlossen", profile.OrderStatusMessage);
    }

    [Fact]
    public async Task GetWebsiteStatus_returns_open_and_can_order_during_hours()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var factory = new Factory(options);
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo Cafe",
            Slug = "demo-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Demo Cafe",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            BusinessHours = new Dictionary<string, string>(),
            WorkingHours = WorkingHoursSettings.CreateDefault(),
            Currency = "EUR",
            Language = "de-DE",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm:ss",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
            IsActive = true
        });
        await db.SaveChangesAsync();

        // Monday 2026-07-20 12:00 Vienna = 10:00 UTC
        var time = new FixedTimeProvider(new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));
        var sut = new PublicTenantCatalogService(factory, time);
        var status = await sut.GetWebsiteStatusAsync("demo-cafe");

        Assert.NotNull(status);
        Assert.True(status!.IsOpen);
        Assert.True(status.CanOrder);
        Assert.False(status.IsSpecial);
        Assert.Equal("09:00", status.OpenTime);
        Assert.Equal("22:00", status.CloseTime);
        Assert.Equal("Online-Bestellung möglich", status.Message);
    }

    [Fact]
    public async Task Unknown_slug_returns_null()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var sut = new PublicTenantCatalogService(new Factory(options));
        Assert.Null(await sut.GetProfileAsync("missing"));
        Assert.Null(await sut.GetMenuAsync("missing"));
        Assert.Null(await sut.GetWebsiteStatusAsync("missing"));
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
            new(new AppDbContext(_options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)));
    }
}
