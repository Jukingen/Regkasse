using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using CacheSettings = KasseAPI_Final.Configuration.CacheSettings;

namespace KasseAPI_Final.Tests.Extensions;

public sealed class VetPrescriptionExtensionTests
{
    [Fact]
    public void AppliesTo_WithoutPatientRecord_IsFalse_AndHookDoesNotInvokeIt()
    {
        var tenantId = Guid.NewGuid();
        var options = InMemoryOptions();
        using var ctx = CreateContext(options, tenantId);
        SeedEnabled(ctx, tenantId);
        ctx.SaveChanges();

        var sut = CreateExtension(patientRecord: false, prescription: true);
        Assert.False(sut.AppliesTo(tenantId));

        var registry = CreateRegistry(options, sut);
        Assert.Empty(registry.GetForHook(tenantId, TenantExtensionHooks.PaymentBeforeSign));
    }

    [Fact]
    public async Task Execute_PatientRecordWithoutPrescriptionReference_ReturnsFailure()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var result = await CreateExtension(patientRecord: true, prescription: true)
            .ExecuteAsync(Sale(tenantId, productId, prescriptionReference: null, requiresPrescription: true), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(VetPrescriptionExtension.RejectionCode, result.Message);
        Assert.Null(result.ModifiedPayload);
    }

    [Fact]
    public async Task Execute_PatientRecordWithPrescriptionReference_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var result = await CreateExtension(patientRecord: true, prescription: true)
            .ExecuteAsync(Sale(tenantId, productId, prescriptionReference: "RX-100", requiresPrescription: true), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.Message);
        Assert.Null(result.ModifiedPayload);
    }

    [Fact]
    public async Task Execute_ProductDoesNotRequirePrescription_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var result = await CreateExtension(patientRecord: true, prescription: true)
            .ExecuteAsync(Sale(tenantId, productId, prescriptionReference: null, requiresPrescription: false), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ModifiedPayload);
    }

    [Fact]
    public async Task Execute_EmptyCart_ReturnsSuccess()
    {
        var tenantId = Guid.NewGuid();

        var result = await CreateExtension(patientRecord: true, prescription: true)
            .ExecuteAsync(
                new ExtensionContext(tenantId, TenantExtensionHooks.PaymentBeforeSign, new Dictionary<string, object?>()),
                CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ModifiedPayload);
    }

    private static VetPrescriptionExtension CreateExtension(bool patientRecord, bool prescription)
    {
        var profiles = new Mock<IVerticalProfileService>();
        profiles
            .Setup(service => service.GetForAdminTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Profile(patientRecord, prescription));
        return new VetPrescriptionExtension(profiles.Object);
    }

    private static EffectiveVerticalProfileDto Profile(bool patientRecord, bool prescription)
    {
        var features = JsonSerializer.SerializeToElement(new Dictionary<string, bool>
        {
            [VerticalProfileFeatureKeys.PatientRecord] = patientRecord,
            ["prescription"] = prescription,
        });
        return new EffectiveVerticalProfileDto(
            "vet",
            "Vet",
            features,
            default,
            default,
            "standard",
            default);
    }

    private static ExtensionContext Sale(
        Guid tenantId,
        Guid productId,
        string? prescriptionReference,
        bool requiresPrescription)
    {
        var payload = ExtensionCart.Write(
            [new ExtensionCartLine(productId, 1, 1, 10m, requiresPrescription)],
            []);
        payload[ExtensionCart.PrescriptionReferenceKey] = prescriptionReference;
        return new ExtensionContext(tenantId, TenantExtensionHooks.PaymentBeforeSign, payload);
    }

    private static void SeedEnabled(AppDbContext ctx, Guid tenantId)
    {
        ctx.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = TenantExtensionSettings.EnabledKey,
            Value = "[\"vet_prescription\"]",
            UpdatedAtUtc = DateTime.UtcNow,
        });
    }

    private static AppDbContext CreateContext(DbContextOptions<AppDbContext> options, Guid tenantId)
    {
        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureTenant(ctx, tenantId);
        return ctx;
    }

    private static ITenantExtensionRegistry CreateRegistry(
        DbContextOptions<AppDbContext> options,
        VetPrescriptionExtension extension)
    {
        return new TenantExtensionRegistry(
            [extension],
            TenantTestDoubles.DbContextFactoryForTests(options, TenantTestDoubles.TenantAccessorReturning(null)),
            new MemoryCache(new MemoryCacheOptions()),
            new EmptyCache(),
            Options.Create(new CacheSettings()),
            Mock.Of<KasseAPI_Final.Services.IAuditLogService>(),
            NullLogger<TenantExtensionRegistry>.Instance);
    }

    private static DbContextOptions<AppDbContext> InMemoryOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VetRx_{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private sealed class EmptyCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAllAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
