using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseCertificateServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tse_cert_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static (TseCertificateService Svc, Mock<ITseKeyProvider> Keys, Mock<IActivityEventPublisher> Activity)
        CreateService(AppDbContext db, TseOptions? opts = null, byte[]? certBytes = null)
    {
        var keys = new Mock<ITseKeyProvider>();
        keys.Setup(k => k.GetCertificateBytes()).Returns(certBytes);
        keys.Setup(k => k.GetCertificateSerialNumber()).Returns((string?)null);
        keys.Setup(k => k.GetCurrentCertificateThumbprint()).Returns((string?)null);

        var activity = new Mock<IActivityEventPublisher>();
        activity
            .Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var svc = new TseCertificateService(
            db,
            keys.Object,
            activity.Object,
            Mock.Of<IAuditLogService>(),
            Options.Create(opts ?? new TseOptions
            {
                TseMode = "Demo",
                Mode = "Fake",
                CertificateExpiringSoonDays = 30,
            }).ToMonitor(),
            NullLogger<TseCertificateService>.Instance);

        return (svc, keys, activity);
    }

    private static async Task<(Guid TenantId, Guid DeviceId)> SeedDeviceAsync(
        AppDbContext db,
        DateTime? expiresAt = null,
        string certificateStatus = "VALID")
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cert Cafe",
            Slug = "cert-cafe",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        var device = new TseDevice
        {
            SerialNumber = "CERT-001",
            DeviceType = "Soft",
            VendorId = "auto",
            ProductId = "soft",
            TenantId = tenantId,
            KassenId = Guid.NewGuid(),
            IsActive = true,
            IsPrimary = true,
            CertificateStatus = certificateStatus,
            IssuedAt = DateTime.UtcNow.AddYears(-1),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(10),
            MemoryStatus = "OK",
            FinanzOnlineUsername = string.Empty,
            CreatedAt = DateTime.UtcNow,
        };
        db.TseDevices.Add(device);
        await db.SaveChangesAsync();
        return (tenantId, device.Id);
    }

    private static byte[] CreateSelfSignedCertDer(DateTime notBefore, DateTime notAfter)
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=TSE-Test", ecdsa, HashAlgorithmName.SHA256);
        using var cert = req.CreateSelfSigned(notBefore, notAfter);
        return cert.Export(X509ContentType.Cert);
    }

    [Fact]
    public async Task GetCertificateInfoAsync_ExpiringSoon_FromDeviceMetadata()
    {
        await using var db = CreateDb();
        var (_, deviceId) = await SeedDeviceAsync(db, expiresAt: DateTime.UtcNow.AddDays(5));
        var (svc, _, _) = CreateService(db);

        var info = await svc.GetCertificateInfoAsync(deviceId);
        Assert.NotNull(info);
        Assert.Equal(nameof(TseCertLifecycleStatus.ExpiringSoon), info!.Status);
        Assert.False(info.IsExpired);
        Assert.Contains(info.Warnings, w => w.Code == "EXPIRING_SOON");
    }

    [Fact]
    public async Task GetCertificateInfoAsync_ParsesKeyProviderCert()
    {
        await using var db = CreateDb();
        var (_, deviceId) = await SeedDeviceAsync(db, expiresAt: null);
        var device = await db.TseDevices.FindAsync(deviceId);
        device!.ExpiresAt = null;
        device.IssuedAt = null;
        await db.SaveChangesAsync();

        var der = CreateSelfSignedCertDer(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(100));
        var (svc, _, _) = CreateService(db, certBytes: der);

        var info = await svc.GetCertificateInfoAsync(deviceId);
        Assert.NotNull(info);
        Assert.Equal(nameof(TseCertLifecycleStatus.Valid), info!.Status);
        Assert.NotNull(info.Issuer);
        Assert.NotNull(info.Subject);
        Assert.Equal("KeyProvider", info.Source);
    }

    [Fact]
    public async Task ScheduleCertificateRenewalAsync_PersistsDate()
    {
        await using var db = CreateDb();
        var (_, deviceId) = await SeedDeviceAsync(db);
        var (svc, _, activity) = CreateService(db);
        var when = DateTime.UtcNow.AddDays(3);

        var result = await svc.ScheduleCertificateRenewalAsync(deviceId, when, "sa-1");
        Assert.True(result.Success);
        Assert.Equal("Scheduled", result.Outcome);

        var device = await db.TseDevices.FindAsync(deviceId);
        Assert.NotNull(device!.ScheduledRenewalAt);
        activity.Verify(
            a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                ActivityEventType.TseCertificateRenewalScheduled,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RenewCertificateAsync_SoftMode_RotatesDates()
    {
        await using var db = CreateDb();
        var (_, deviceId) = await SeedDeviceAsync(db, expiresAt: DateTime.UtcNow.AddDays(-1));
        var (svc, _, _) = CreateService(db, new TseOptions { TseMode = "Demo", Mode = "Fake" });

        var result = await svc.RenewCertificateAsync(deviceId, "sa-1");
        Assert.True(result.Success);
        Assert.Equal("SoftMetadataRenewed", result.Outcome);

        var device = await db.TseDevices.FindAsync(deviceId);
        Assert.Equal("VALID", device!.CertificateStatus);
        Assert.True(device.ExpiresAt > DateTime.UtcNow.AddMonths(6));
    }

    [Fact]
    public async Task ProcessExpiryWarningsAsync_PublishesExpiringSoon()
    {
        await using var db = CreateDb();
        var (tenantId, _) = await SeedDeviceAsync(db, expiresAt: DateTime.UtcNow.AddDays(7));
        var (svc, _, activity) = CreateService(db);

        await svc.ProcessExpiryWarningsAsync();

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.TseCertificateExpiringSoon,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        var device = await db.TseDevices.SingleAsync();
        Assert.NotNull(device.ExpiryWarningSentAt);
    }

    [Fact]
    public async Task ValidateCertificateAsync_Expired_IsInvalid()
    {
        await using var db = CreateDb();
        var (_, deviceId) = await SeedDeviceAsync(
            db,
            expiresAt: DateTime.UtcNow.AddDays(-2),
            certificateStatus: "EXPIRED");
        var (svc, _, _) = CreateService(db);

        var result = await svc.ValidateCertificateAsync(deviceId);
        Assert.False(result.IsValid);
        Assert.Equal(nameof(TseCertLifecycleStatus.Expired), result.Status);
    }
}
