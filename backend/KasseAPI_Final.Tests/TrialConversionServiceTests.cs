using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Trial;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TrialConversionServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"trial_conv_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TrialConversionService CreateService(AppDbContext db)
    {
        return new TrialConversionService(
            db,
            Mock.Of<IEmailService>(),
            Mock.Of<IActivityEventService>(),
            Mock.Of<IBillingAuditService>(),
            Mock.Of<IAuditLogService>(),
            Mock.Of<IOptionsMonitor<LicenseOptions>>(m => m.CurrentValue == new LicenseOptions()),
            Mock.Of<ILogger<TrialConversionService>>());
    }

    private static async Task<(Guid TenantId, Guid SaleId, DateTime BaseUntil)> SeedOpenTrialAsync(
        AppDbContext db,
        string slug = "trial-cafe",
        int remainingTrialDays = 7)
    {
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var baseUntil = DateTime.UtcNow.AddDays(365);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Trial Cafe",
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            Email = $"owner@{slug}.at",
            TrialStatus = TrialStatuses.Active,
            TrialStartedAtUtc = DateTime.UtcNow.AddDays(-7),
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(remainingTrialDays),
            LicenseValidUntilUtc = DateTime.UtcNow.AddDays(remainingTrialDays),
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(new LicenseSale
        {
            Id = saleId,
            TenantId = tenantId,
            LicenseKey = $"REGK-CONV-{slug.ToUpperInvariant()}",
            LicensePlan = "12_months",
            LicenseType = LicenseType.Starter,
            ValidFromUtc = DateTime.UtcNow,
            ValidUntilUtc = baseUntil,
            Status = LicenseSaleStatuses.Active,
            InvoiceNumber = $"INV-{slug}",
            SoldByUserId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();
        return (tenantId, saleId, baseUntil);
    }

    [Fact]
    public async Task ConvertToPaidAsync_Succeeds_AndMarksConverted()
    {
        await using var db = CreateDb();
        var (tenantId, saleId, _) = await SeedOpenTrialAsync(db, "ok-trial");

        var (result, error) = await CreateService(db).ConvertToPaidAsync(
            tenantId,
            saleId,
            addRemainingTrialDays: true,
            notes: "paid upgrade",
            actorUserId: Guid.NewGuid().ToString("D"),
            actorRole: Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(saleId, result.LicenseSaleId);
        Assert.Contains("successfully converted", result.Message, StringComparison.OrdinalIgnoreCase);

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TrialStatuses.Converted, tenant.TrialStatus);
        Assert.NotNull(tenant.TrialConvertedAtUtc);
        Assert.Equal(saleId, tenant.CurrentLicenseSaleId);
    }

    [Fact]
    public async Task ConvertToPaidAsync_AddsRemainingTrialDays_WhenEnabled()
    {
        await using var db = CreateDb();
        var (tenantId, saleId, baseUntil) = await SeedOpenTrialAsync(db, "add-days", remainingTrialDays: 7);

        var (result, error) = await CreateService(db).ConvertToPaidAsync(
            tenantId,
            saleId,
            addRemainingTrialDays: true,
            notes: "upgrade",
            actorUserId: Guid.NewGuid().ToString("D"),
            actorRole: Roles.SuperAdmin);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.True(result.RemainingTrialDaysAdded is >= 6 and <= 8);

        var sale = await db.LicenseSales.IgnoreQueryFilters().SingleAsync(s => s.Id == saleId);
        Assert.True(sale.ConvertedFromTrial);
        Assert.Equal(result.RemainingTrialDaysAdded, sale.RemainingTrialDaysAdded);
        Assert.True(sale.ValidUntilUtc > baseUntil);

        var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Id == tenantId);
        Assert.Equal(TrialStatuses.Converted, tenant.TrialStatus);
        Assert.Equal(sale.ValidUntilUtc, tenant.LicenseValidUntilUtc);
    }

    [Fact]
    public async Task ConvertToPaidAsync_DoesNotAddRemainingDays_WhenDisabled()
    {
        await using var db = CreateDb();
        var (tenantId, saleId, baseUntil) = await SeedOpenTrialAsync(db, "no-add", remainingTrialDays: 10);

        var (result, error) = await CreateService(db).ConvertToPaidAsync(
            tenantId,
            saleId,
            addRemainingTrialDays: false);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(0, result!.RemainingTrialDaysAdded);

        var sale = await db.LicenseSales.IgnoreQueryFilters().SingleAsync(s => s.Id == saleId);
        Assert.Equal(baseUntil, sale.ValidUntilUtc, TimeSpan.FromSeconds(2));
        Assert.True(sale.ConvertedFromTrial);
        Assert.Null(sale.RemainingTrialDaysAdded);
    }

    [Fact]
    public async Task ConvertToPaidAsync_RejectsNonTrialTenant()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Paid Co",
            Slug = "paid-co",
            Status = TenantStatuses.Active,
            IsActive = true,
            TrialStatus = TrialStatuses.Converted,
            CreatedAt = DateTime.UtcNow,
        });
        db.LicenseSales.Add(new LicenseSale
        {
            Id = saleId,
            TenantId = tenantId,
            LicenseKey = "REGK-PAID-0001",
            LicensePlan = "12_months",
            ValidFromUtc = DateTime.UtcNow,
            ValidUntilUtc = DateTime.UtcNow.AddDays(100),
            Status = LicenseSaleStatuses.Active,
            InvoiceNumber = "INV-PAID-1",
            SoldByUserId = Guid.NewGuid(),
        });
        await db.SaveChangesAsync();

        var (result, error) = await CreateService(db).ConvertToPaidAsync(tenantId, saleId);
        Assert.Null(result);
        Assert.Contains("open trial", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToPaidAsync_RejectsMissingLicenseSale()
    {
        await using var db = CreateDb();
        var (tenantId, _, _) = await SeedOpenTrialAsync(db, "missing-sale");

        var (result, error) = await CreateService(db).ConvertToPaidAsync(tenantId, Guid.NewGuid());
        Assert.Null(result);
        Assert.Contains("License sale not found", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToPaidAsync_RejectsMissingTenant()
    {
        await using var db = CreateDb();
        var (result, error) = await CreateService(db).ConvertToPaidAsync(Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
        Assert.Equal("Tenant not found.", error);
    }
}
