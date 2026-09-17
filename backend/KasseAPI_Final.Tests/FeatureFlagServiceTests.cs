using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FeatureFlagServiceTests
{
    private static (IDbContextFactory<AppDbContext> Factory, string DbName) CreateFactory()
    {
        var dbName = $"FeatureFlags_{Guid.NewGuid():N}";
        var factory = new Mock<IDbContextFactory<AppDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(() => CreateDb(dbName));
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((CancellationToken _) => CreateDb(dbName));
        return (factory.Object, dbName);
    }

    private static AppDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(null));
    }

    private static FeatureFlagService CreateService(
        IDbContextFactory<AppDbContext> factory,
        FeatureFlagsOptions? opts = null)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        return new FeatureFlagService(
            factory,
            Options.Create(opts ?? new FeatureFlagsOptions()).ToMonitor(),
            new MemoryCache(new MemoryCacheOptions()),
            audit.Object,
            NullLogger<FeatureFlagService>.Instance,
            new CountryProfileRegistry());
    }

    private static async Task SeedCountryAsync(IDbContextFactory<AppDbContext> factory, Guid tenantId, string country)
    {
        await using var db = await factory.CreateDbContextAsync();
        db.CompanySettings.Add(new CompanySettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyName = "Flag Test GmbH",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            Currency = country == "CH" ? "CHF" : "EUR",
            Country = country,
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
        await db.SaveChangesAsync();
    }

    [Fact]
    public void IsEnabled_UsesConfigDefault()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, new FeatureFlagsOptions { EnableDepExportV2 = true });
        Assert.True(svc.IsEnabled("DepExportV2"));
        Assert.False(svc.IsEnabled("EnableNewPaymentFlow"));
    }

    [Fact]
    public async Task SetEnabled_TenantOverride_BeatsConfig()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, new FeatureFlagsOptions { EnableOnlineOrdersV2 = false });
        var tenantId = Guid.NewGuid();

        await svc.SetEnabledAsync(
            FeatureFlagNames.EnableOnlineOrdersV2,
            enabled: true,
            tenantId: tenantId.ToString("D"),
            actorUserId: "admin");

        Assert.True(svc.IsEnabled("EnableOnlineOrdersV2", tenantId.ToString("D")));
        Assert.False(svc.IsEnabled("EnableOnlineOrdersV2"));
    }

    [Fact]
    public async Task ClearOverride_RestoresConfig()
    {
        var (factory, _) = CreateFactory();
        var svc = CreateService(factory, new FeatureFlagsOptions { EnableAutoAusfall = true });

        await svc.SetEnabledAsync(FeatureFlagNames.EnableAutoAusfall, false, tenantId: null, actorUserId: "admin");
        Assert.False(svc.IsEnabled(FeatureFlagNames.EnableAutoAusfall));

        await svc.ClearOverrideAsync(FeatureFlagNames.EnableAutoAusfall, tenantId: null, actorUserId: "admin");
        Assert.True(svc.IsEnabled(FeatureFlagNames.EnableAutoAusfall));
    }

    [Fact]
    public void Normalize_AcceptsShortName()
    {
        Assert.Equal(FeatureFlagNames.EnableNewPaymentFlow, FeatureFlagNames.Normalize("NewPaymentFlow"));
        Assert.Equal(FeatureFlagNames.EnableAutoAusfall, FeatureFlagNames.Normalize("enableAutoAusfall"));
    }

    [Fact]
    public void Normalize_PreservesDottedCountryFlagNames()
    {
        Assert.Equal(FeatureFlagNames.FiscalRksvAt, FeatureFlagNames.Normalize("Fiscal.RksvAt"));
        Assert.Equal(FeatureFlagNames.EInvoicingEn16931, FeatureFlagNames.Normalize("einvoicing.en16931"));
        Assert.Equal(12, FeatureFlagNames.All.Count);
        Assert.Equal(4, FeatureFlagNames.Experimental.Count);
    }

    [Fact]
    public async Task AtTenant_FiscalRksvAt_IsLockedTrue_OverrideFalseRejected()
    {
        var (factory, _) = CreateFactory();
        var tenantId = Guid.NewGuid();
        await SeedCountryAsync(factory, tenantId, CountryProfileCodes.Austria);
        var svc = CreateService(factory);
        var tenant = tenantId.ToString("D");

        Assert.True(svc.IsEnabled(FeatureFlagNames.FiscalRksvAt, tenant));

        var ex = await Assert.ThrowsAsync<FeatureFlagLockedException>(() =>
            svc.SetEnabledAsync(FeatureFlagNames.FiscalRksvAt, enabled: false, tenantId: tenant, actorUserId: "admin"));
        Assert.Equal(FeatureFlagLockedException.Code, ex.ErrorCode);
        Assert.True(svc.IsEnabled(FeatureFlagNames.FiscalRksvAt, tenant));

        var statuses = await svc.GetStatusesAsync(tenant);
        var rksv = Assert.Single(statuses, s => s.Name == FeatureFlagNames.FiscalRksvAt);
        Assert.True(rksv.Enabled);
        Assert.Equal(FeatureFlagSources.Locked, rksv.Source);
        Assert.False(rksv.ConfigDefault);
    }

    [Fact]
    public async Task DeTenant_KassenSicherheitOn_RksvAtOff_EInvoicingOffUntilOverride()
    {
        var (factory, _) = CreateFactory();
        var tenantId = Guid.NewGuid();
        await SeedCountryAsync(factory, tenantId, CountryProfileCodes.Germany);
        var svc = CreateService(factory);
        var tenant = tenantId.ToString("D");

        Assert.True(svc.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, tenant));
        Assert.False(svc.IsEnabled(FeatureFlagNames.FiscalRksvAt, tenant));
        Assert.False(svc.IsEnabled(FeatureFlagNames.EInvoicingZugferd, tenant));
        Assert.False(svc.IsEnabled(FeatureFlagNames.EInvoicingXRechnung, tenant));

        await svc.SetEnabledAsync(FeatureFlagNames.EInvoicingZugferd, true, tenant, "admin");
        Assert.True(svc.IsEnabled(FeatureFlagNames.EInvoicingZugferd, tenant));
        Assert.False(svc.IsEnabled(FeatureFlagNames.EInvoicingZugferd));
    }

    [Fact]
    public async Task ChTenant_MwstOn_RksvAtOff_QrRechnungFromProfile()
    {
        var (factory, _) = CreateFactory();
        var tenantId = Guid.NewGuid();
        await SeedCountryAsync(factory, tenantId, CountryProfileCodes.Switzerland);
        var svc = CreateService(factory);
        var tenant = tenantId.ToString("D");

        Assert.True(svc.IsEnabled(FeatureFlagNames.FiscalMwstCh, tenant));
        Assert.False(svc.IsEnabled(FeatureFlagNames.FiscalRksvAt, tenant));
        Assert.True(svc.IsEnabled(FeatureFlagNames.EInvoicingQrRechnung, tenant));
    }

    [Fact]
    public async Task EuDefaultTenant_En16931On_AllFiscalOff()
    {
        var (factory, _) = CreateFactory();
        var tenantId = Guid.NewGuid();
        await SeedCountryAsync(factory, tenantId, CountryProfileCodes.EuDefault);
        var svc = CreateService(factory);
        var tenant = tenantId.ToString("D");

        Assert.True(svc.IsEnabled(FeatureFlagNames.EInvoicingEn16931, tenant));
        Assert.False(svc.IsEnabled(FeatureFlagNames.FiscalRksvAt, tenant));
        Assert.False(svc.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, tenant));
        Assert.False(svc.IsEnabled(FeatureFlagNames.FiscalMwstCh, tenant));
    }

    [Fact]
    public void CountryDefaults_EuDefaultProfile_MatchesRegistry()
    {
        var profile = new CountryProfileRegistry().Get(CountryProfileCodes.EuDefault);

        Assert.True(CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.EInvoicingEn16931, profile, out var en) && en);
        Assert.True(CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.FiscalRksvAt, profile, out var rksv) && !rksv);
        Assert.True(CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.FiscalKassenSicherheitDe, profile, out var de) && !de);
        Assert.True(CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.FiscalMwstCh, profile, out var ch) && !ch);
        Assert.False(CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.EInvoicingZugferd, profile, out _));
        Assert.False(CountryFeatureFlagDefaults.TryGet(FeatureFlagNames.ViesCheckEnabled, profile, out _));
    }

    [Fact]
    public async Task ViesCheckEnabled_DefaultFalse_TenantOverrideWorks()
    {
        var (factory, _) = CreateFactory();
        var tenantId = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        await SeedCountryAsync(factory, tenantId, CountryProfileCodes.Austria);
        await SeedCountryAsync(factory, otherTenant, CountryProfileCodes.Austria);
        var svc = CreateService(factory);

        Assert.False(svc.IsEnabled(FeatureFlagNames.ViesCheckEnabled, tenantId.ToString("D")));

        await svc.SetEnabledAsync(FeatureFlagNames.ViesCheckEnabled, true, tenantId.ToString("D"), "admin");
        Assert.True(svc.IsEnabled(FeatureFlagNames.ViesCheckEnabled, tenantId.ToString("D")));
        Assert.False(svc.IsEnabled(FeatureFlagNames.ViesCheckEnabled, otherTenant.ToString("D")));
        Assert.False(svc.IsEnabled(FeatureFlagNames.ViesCheckEnabled));
    }

    [Fact]
    public async Task ExperimentalFlags_IgnoreCountryProfile()
    {
        var (factory, _) = CreateFactory();
        var tenantId = Guid.NewGuid();
        await SeedCountryAsync(factory, tenantId, CountryProfileCodes.Germany);
        var svc = CreateService(factory, new FeatureFlagsOptions { EnableDepExportV2 = true });

        Assert.True(svc.IsEnabled(FeatureFlagNames.EnableDepExportV2, tenantId.ToString("D")));
        Assert.False(svc.IsEnabled(FeatureFlagNames.EnableNewPaymentFlow, tenantId.ToString("D")));

        await svc.SetEnabledAsync(FeatureFlagNames.EnableOnlineOrdersV2, true, tenantId.ToString("D"), "admin");
        Assert.True(svc.IsEnabled(FeatureFlagNames.EnableOnlineOrdersV2, tenantId.ToString("D")));
        Assert.False(svc.IsEnabled(FeatureFlagNames.EnableOnlineOrdersV2));
    }
}
