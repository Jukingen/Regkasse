using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class UnifiedLicenseServiceTests
{
    [Fact]
    public async Task ValidateLicenseAsync_RejectsInvalidFormat()
    {
        var (sut, _) = CreateSut();

        var result = await sut.ValidateLicenseAsync("not-a-key");

        Assert.False(result.IsValid);
        Assert.False(result.IsFormatValid);
        Assert.Equal(LicenseKeyErrorCodes.InvalidFormat, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateLicenseAsync_SystemKey_ChecksIssuedRowAndExpiry()
    {
        const string key = "REGK-20990101-system-1R61EMER";
        var expiry = new DateTime(2099, 1, 1, 23, 59, 59, DateTimeKind.Utc);
        var (sut, db) = CreateSut();
        db.IssuedLicenses.Add(new IssuedLicense
        {
            LicenseKey = key,
            CustomerName = "Acme",
            ExpiryAtUtc = expiry,
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await sut.ValidateLicenseAsync(key);

        Assert.True(result.IsFormatValid);
        Assert.True(result.ExistsInDatabase);
        Assert.True(result.IsValid);
        Assert.Equal(LicenseKeyKinds.System, result.LicenseKind);
        Assert.False(result.IsExpired);
    }

    [Fact]
    public async Task ValidateLicenseAsync_ExpiredEncodedDate_IsExpired()
    {
        var (sut, _) = CreateSut();

        var result = await sut.ValidateLicenseAsync("REGK-20200101-cafe-A7F3K2D9");

        Assert.True(result.IsFormatValid);
        Assert.True(result.IsExpired);
        Assert.False(result.IsValid);
        Assert.Equal(LicenseKeyErrorCodes.Expired, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateLicenseAsync_TenantSlugMismatch_WhenSaleBelongsToOtherTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        const string key = "REGK-20990101-cafe-A7F3K2D9";
        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.Setup(x => x.TenantId).Returns(otherTenantId);

        var (sut, db) = CreateSut(accessor.Object);
        var tenant = NewTenant(tenantId, "cafe");
        db.Tenants.Add(tenant);
        db.LicenseSales.Add(NewSale(tenantId, key));
        await db.SaveChangesAsync();

        var result = await sut.ValidateLicenseAsync(key);

        Assert.True(result.IsFormatValid);
        Assert.True(result.ExistsInDatabase);
        Assert.False(result.SlugMatches);
        Assert.Equal(LicenseKeyErrorCodes.SlugMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task IsLicenseValidAsync_TrueForActiveIssuedSystemKey()
    {
        const string key = "REGK-20990101-system-ABCDEF12";
        var (sut, db) = CreateSut();
        db.IssuedLicenses.Add(new IssuedLicense
        {
            LicenseKey = key,
            CustomerName = "Acme",
            ExpiryAtUtc = DateTime.UtcNow.AddYears(1),
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.True(await sut.IsLicenseValidAsync(key));
    }

    [Fact]
    public async Task GetLicenseInfoAsync_ReturnsIssuedMetadata()
    {
        const string key = "REGK-20990101-system-INFOKEY1";
        var (sut, db) = CreateSut();
        var issued = new IssuedLicense
        {
            LicenseKey = key,
            CustomerName = "Acme GmbH",
            ExpiryAtUtc = new DateTime(2099, 1, 1, 23, 59, 59, DateTimeKind.Utc),
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        };
        db.IssuedLicenses.Add(issued);
        await db.SaveChangesAsync();

        var info = await sut.GetLicenseInfoAsync(key);

        Assert.True(info.Exists);
        Assert.True(info.IsValid);
        Assert.Equal(LicenseKeyKinds.System, info.LicenseKind);
        Assert.Equal("Acme GmbH", info.CustomerName);
        Assert.Equal("issued_licenses", info.SourceTable);
        Assert.Equal(issued.Id, info.SourceId);
    }

    [Fact]
    public async Task PreviewLicenseAsync_SystemKey_FindsIssuedRow()
    {
        const string key = "REGK-20990101-system-PREVSYS1";
        var (sut, db) = CreateSut();
        db.IssuedLicenses.Add(new IssuedLicense
        {
            LicenseKey = key,
            CustomerName = "Preview Host",
            ExpiryAtUtc = new DateTime(2099, 1, 1, 23, 59, 59, DateTimeKind.Utc),
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await sut.PreviewLicenseAsync(key);

        Assert.True(preview.Valid);
        Assert.Equal("valid", preview.Status);
        Assert.Equal(LicenseKeyKinds.System, preview.LicenseKind);
        Assert.Equal(key, preview.LicenseKey);
    }

    [Fact]
    public async Task PreviewLicenseAsync_SystemKey_IsCaseInsensitive()
    {
        const string stored = "REGK-20990101-system-CASEKEY1";
        var (sut, db) = CreateSut();
        db.IssuedLicenses.Add(new IssuedLicense
        {
            LicenseKey = stored,
            CustomerName = "Case Host",
            ExpiryAtUtc = new DateTime(2099, 1, 1, 23, 59, 59, DateTimeKind.Utc),
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await sut.PreviewLicenseAsync(stored.ToUpperInvariant());

        Assert.True(preview.Valid);
        Assert.Equal(LicenseKeyKinds.System, preview.LicenseKind);
    }

    [Fact]
    public async Task PreviewLicenseAsync_TenantKey_FindsLicenseSale()
    {
        const string key = "REGK-20990101-cafe-PREVTEN1";
        var tenantId = Guid.NewGuid();
        var (sut, db) = CreateSut();
        db.Tenants.Add(NewTenant(tenantId, "cafe"));
        db.LicenseSales.Add(NewSale(tenantId, key, new DateTime(2099, 1, 1, 23, 59, 59, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var preview = await sut.PreviewLicenseAsync(key);

        Assert.True(preview.Valid);
        Assert.Equal("valid", preview.Status);
        Assert.Equal(LicenseKeyKinds.Tenant, preview.LicenseKind);
        Assert.Equal(tenantId, preview.TenantId);
        Assert.Equal("cafe", preview.TenantName);
    }

    [Fact]
    public async Task PreviewLicenseAsync_UnknownKey_ReturnsNotFound()
    {
        var (sut, _) = CreateSut();

        var preview = await sut.PreviewLicenseAsync("REGK-20990101-system-MISSING1");

        Assert.False(preview.Valid);
        Assert.Equal(LicenseKeyErrorCodes.NotFound, preview.ErrorCode);
    }

    [Fact]
    public async Task DeactivateLicenseAsync_RevokesIssuedLicense()
    {
        const string key = "REGK-20990101-system-REVOKE01";
        var (sut, db) = CreateSut();
        db.IssuedLicenses.Add(new IssuedLicense
        {
            LicenseKey = key,
            CustomerName = "Acme",
            ExpiryAtUtc = DateTime.UtcNow.AddYears(1),
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await sut.DeactivateLicenseAsync(
            key,
            new UnifiedLicenseDeactivationContext(Guid.NewGuid(), "test revoke"));

        Assert.True(result.Success);
        var row = Assert.Single(db.IssuedLicenses);
        Assert.True(row.IsRevoked);
        Assert.Equal("test revoke", row.RevocationReason);
    }

    [Fact]
    public async Task GetUnifiedStatusAsync_CombinesSystemAndTenantLayers()
    {
        var tenantId = Guid.NewGuid();
        var until = DateTime.UtcNow.AddDays(20);
        var deployment = new Mock<ILicenseService>();
        deployment
            .Setup(x => x.GetCurrentStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusResponse(true, false, false, 100, DateTime.UtcNow.AddDays(100), "hash"));
        deployment
            .Setup(x => x.GetLicenseStatusAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseStatusInfo
            {
                CanAccess = true,
                CanTransact = true,
                DaysRemaining = 20,
                ValidUntil = until,
            });

        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.Setup(x => x.TenantId).Returns(tenantId);
        var (sut, db) = CreateSut(accessor.Object, licenseService: deployment.Object);
        db.Tenants.Add(NewTenant(tenantId, "cafe"));
        await db.SaveChangesAsync();

        var status = await sut.GetUnifiedStatusAsync();

        Assert.True(status.IsSystemLicense);
        Assert.True(status.IsTenantLicense);
        Assert.True(status.AnyLicenseActive);
        Assert.True(status.AllLicensesActive);
        Assert.Equal(LicenseKeyKinds.Tenant, status.LicenseType);
        Assert.Equal("cafe", status.Slug);
        Assert.Equal("active", status.Status);
    }

    [Fact]
    public async Task ActivateLicenseAsync_TenantKey_ResolvesTenantFromSlugWithoutAmbient()
    {
        const string key = "REGK-20990101-cafe-A7F3K2D9";
        var cafeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expiry = new DateTime(2099, 1, 1, 23, 59, 59, DateTimeKind.Utc);
        var billing = new Mock<ITenantLicenseService>();
        billing
            .Setup(x => x.ActivateLicenseAsync(cafeId, key, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivationResult
            {
                Success = true,
                Message = "Lizenz wurde erfolgreich aktiviert.",
                LicenseKey = key,
                ValidUntilUtc = expiry,
                LicensePlan = "12_months",
            });

        var (sut, db) = CreateSut(billing: billing.Object);
        db.Tenants.Add(NewTenant(cafeId, "cafe"));
        db.LicenseSales.Add(NewSale(cafeId, key, expiry));
        await db.SaveChangesAsync();

        var result = await sut.ActivateLicenseAsync(
            key,
            new UnifiedLicenseActivationContext(TenantId: null, ActorUserId: userId));

        Assert.True(result.Success);
        Assert.Equal(cafeId, result.TenantId);
        billing.Verify(
            x => x.ActivateLicenseAsync(cafeId, key, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateLicenseAsync_TenantKeyWithoutTenant_ReturnsTenantRequired()
    {
        var (sut, _) = CreateSut();

        var result = await sut.ActivateLicenseAsync("REGK-20990101-cafe-A7F3K2D9");

        Assert.False(result.Success);
        Assert.Equal("Tenant context required.", result.Message);
    }

    [Fact]
    public async Task ActivateLicenseAsync_SystemKey_Succeeds()
    {
        const string key = "REGK-20261231-system-C8YEM41L";
        var expiry = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var deployment = new Mock<ILicenseService>();
        deployment
            .Setup(x => x.ActivateAsync(
                It.Is<ActivateLicenseRequest>(r => r.LicenseKey == key),
                It.IsAny<LicenseActivationClientInfo?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LicenseActivationResult(true, "Lizenz erfolgreich aktiviert", expiry, "Licensed"));

        var (sut, db) = CreateSut(licenseService: deployment.Object);
        db.IssuedLicenses.Add(new IssuedLicense
        {
            LicenseKey = key,
            CustomerName = "Server",
            ExpiryAtUtc = expiry,
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await sut.ActivateLicenseAsync(
            key,
            new UnifiedLicenseActivationContext(TenantId: null, ActorUserId: null));

        Assert.True(result.Success);
        Assert.Equal(expiry, result.ValidUntil);
        deployment.Verify(
            x => x.ActivateAsync(
                It.Is<ActivateLicenseRequest>(r => r.LicenseKey == key),
                It.IsAny<LicenseActivationClientInfo?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateLicenseAsync_DevTenantKey_SucceedsForDevTenant()
    {
        const string key = "REGK-20261231-dev-A4WCG52H";
        var devId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expiry = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var accessor = new Mock<ICurrentTenantAccessor>();
        accessor.Setup(x => x.TenantId).Returns(devId);

        var billing = new Mock<ITenantLicenseService>();
        billing
            .Setup(x => x.ActivateLicenseAsync(devId, key, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivationResult
            {
                Success = true,
                Message = "Lizenz wurde erfolgreich aktiviert.",
                LicenseKey = key,
                ValidUntilUtc = expiry,
                LicensePlan = "12_months",
            });

        var (sut, db) = CreateSut(accessor.Object, billing.Object);
        db.Tenants.Add(NewTenant(devId, "dev"));
        db.LicenseSales.Add(NewSale(devId, key, expiry));
        await db.SaveChangesAsync();

        var result = await sut.ActivateLicenseAsync(
            key,
            new UnifiedLicenseActivationContext(devId, userId));

        Assert.True(result.Success);
        Assert.Equal("active", result.Status);
        billing.Verify(
            x => x.ActivateLicenseAsync(devId, key, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ActivateLicenseAsync_DevKeyOnProdTenant_FailsSlugMismatch()
    {
        const string key = "REGK-20261231-dev-A4WCG52H";
        var devId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expiry = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var billing = new Mock<ITenantLicenseService>(MockBehavior.Strict);

        var (sut, db) = CreateSut(billing: billing.Object);
        db.Tenants.AddRange(NewTenant(devId, "dev"), NewTenant(prodId, "prod"));
        db.LicenseSales.Add(NewSale(devId, key, expiry));
        await db.SaveChangesAsync();

        var result = await sut.ActivateLicenseAsync(
            key,
            new UnifiedLicenseActivationContext(prodId, userId));

        Assert.False(result.Success);
        Assert.Contains("anderen Mandanten", result.Message, StringComparison.OrdinalIgnoreCase);
        billing.Verify(
            x => x.ActivateLicenseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ActivateLicenseAsync_ExpiredTenantKey_Fails()
    {
        const string key = "REGK-20250101-dev-XXXXXXXX";
        var devId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billing = new Mock<ITenantLicenseService>(MockBehavior.Strict);

        var (sut, db) = CreateSut(billing: billing.Object);
        db.Tenants.Add(NewTenant(devId, "dev"));
        await db.SaveChangesAsync();

        var result = await sut.ActivateLicenseAsync(
            key,
            new UnifiedLicenseActivationContext(devId, userId));

        Assert.False(result.Success);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
        billing.Verify(
            x => x.ActivateLicenseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PreviewInfoAndValidate_ReturnConsistentResultsForSameKey()
    {
        const string key = "REGK-20990101-system-SAMEKEY1";
        var expiry = new DateTime(2099, 1, 1, 23, 59, 59, DateTimeKind.Utc);
        var (sut, db) = CreateSut();
        db.IssuedLicenses.Add(new IssuedLicense
        {
            LicenseKey = key,
            CustomerName = "Same Key Host",
            ExpiryAtUtc = expiry,
            SignedJwt = "jwt",
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = await sut.PreviewLicenseAsync(key);
        var info = await sut.GetLicenseInfoAsync(key);
        var validation = await sut.ValidateLicenseAsync(key);

        Assert.True(preview.Valid);
        Assert.True(info.IsValid);
        Assert.True(validation.IsValid);
        Assert.Equal(LicenseKeyKinds.System, preview.LicenseKind);
        Assert.Equal(LicenseKeyKinds.System, info.LicenseKind);
        Assert.Equal(LicenseKeyKinds.System, validation.LicenseKind);
        Assert.Equal(info.CanonicalLicenseKey, validation.CanonicalLicenseKey);
        Assert.Equal(info.Exists, validation.ExistsInDatabase);
        Assert.Equal(info.IsExpired, validation.IsExpired);
    }

    [Fact]
    public async Task ActivateLicenseAsync_AlreadyActivatedSameTenant_IsIdempotent()
    {
        const string key = "REGK-20990101-dev-ALREADY1";
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billing = new Mock<ITenantLicenseService>(MockBehavior.Strict);
        var (sut, db) = CreateSut(billing: billing.Object);
        db.Tenants.Add(NewTenant(tenantId, "dev"));
        var sale = NewSale(tenantId, key);
        sale.ActivationDateUtc = DateTime.UtcNow.AddDays(-1);
        db.LicenseSales.Add(sale);
        await db.SaveChangesAsync();

        var result = await sut.ActivateLicenseAsync(
            key,
            new UnifiedLicenseActivationContext(tenantId, userId));

        Assert.True(result.Success);
        Assert.Equal(LicenseKeyErrorCodes.AlreadyActivated, result.ErrorCode);
        billing.Verify(
            x => x.ActivateLicenseAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ActivateLicenseAsync_SerializesConcurrentActivations()
    {
        const string key = "REGK-20990101-dev-RACEKEY1";
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var inside = 0;
        var overlap = 0;
        var billing = new Mock<ITenantLicenseService>();
        billing
            .Setup(x => x.ActivateLicenseAsync(
                tenantId,
                It.IsAny<string>(),
                userId,
                It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var now = Interlocked.Increment(ref inside);
                if (now > 1)
                    Interlocked.Increment(ref overlap);
                await Task.Delay(80);
                Interlocked.Decrement(ref inside);
                return new ActivationResult
                {
                    Success = true,
                    Message = "ok",
                    ValidUntilUtc = DateTime.UtcNow.AddYears(1),
                    LicensePlan = "12_months",
                };
            });

        var (sut, db) = CreateSut(billing: billing.Object);
        db.Tenants.Add(NewTenant(tenantId, "dev"));
        db.LicenseSales.Add(NewSale(tenantId, key));
        await db.SaveChangesAsync();

        var context = new UnifiedLicenseActivationContext(tenantId, userId);
        await Task.WhenAll(
            sut.ActivateLicenseAsync(key, context),
            sut.ActivateLicenseAsync(key, context));

        Assert.Equal(0, overlap);
        billing.Verify(
            x => x.ActivateLicenseAsync(tenantId, It.IsAny<string>(), userId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ActivateLicenseAsync_InvalidatesTenantCache()
    {
        const string key = "REGK-20990101-dev-CACHE001";
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billing = new Mock<ITenantLicenseService>();
        billing
            .Setup(x => x.ActivateLicenseAsync(
                tenantId,
                It.IsAny<string>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivationResult
            {
                Success = true,
                Message = "ok",
                ValidUntilUtc = DateTime.UtcNow.AddYears(1),
                LicensePlan = "12_months",
            });
        var cache = new Mock<ILicenseCacheService>();
        cache.Setup(x => x.InvalidateAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cache.Setup(x => x.InvalidateForTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var (sut, db) = CreateSut(billing: billing.Object, licenseCache: cache.Object);
        db.Tenants.Add(NewTenant(tenantId, "dev"));
        db.LicenseSales.Add(NewSale(tenantId, key));
        await db.SaveChangesAsync();

        var result = await sut.ActivateLicenseAsync(
            key,
            new UnifiedLicenseActivationContext(tenantId, userId));

        Assert.True(result.Success);
        cache.Verify(x => x.InvalidateAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        cache.Verify(x => x.InvalidateForTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    private static (UnifiedLicenseService Sut, AppDbContext Db) CreateSut(
        ICurrentTenantAccessor? tenantAccessor = null,
        ITenantLicenseService? billing = null,
        ILicenseService? licenseService = null,
        ILicenseCacheService? licenseCache = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"UnifiedLicense_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var db = new AppDbContext(options, NullCurrentTenantAccessor.Instance);
        var accessor = tenantAccessor ?? Mock.Of<ICurrentTenantAccessor>();
        var sut = new UnifiedLicenseService(
            db,
            licenseService ?? Mock.Of<ILicenseService>(),
            billing ?? Mock.Of<ITenantLicenseService>(),
            Mock.Of<ILicenseStatusCache>(),
            accessor,
            NullLogger<UnifiedLicenseService>.Instance,
            LicenseKeyValidator.Instance,
            licenseCache);
        return (sut, db);
    }

    private static Tenant NewTenant(Guid id, string slug) =>
        new()
        {
            Id = id,
            Name = slug,
            Slug = slug,
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

    private static LicenseSale NewSale(Guid tenantId, string key, DateTime? validUntil = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LicenseKey = key,
            LicensePlan = "12_months",
            ValidFromUtc = DateTime.UtcNow.AddDays(-1),
            ValidUntilUtc = validUntil ?? DateTime.UtcNow.AddYears(1),
            Status = LicenseSaleStatuses.Active,
            SoldAtUtc = DateTime.UtcNow,
            SoldByUserId = Guid.NewGuid(),
            InvoiceNumber = "INV-1",
            CreatedAt = DateTime.UtcNow,
        };
}
