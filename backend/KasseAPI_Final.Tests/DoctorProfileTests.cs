using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
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

namespace KasseAPI_Final.Tests;

public sealed class DoctorProfileTests
{
    [Fact]
    public void Seed_DoctorProfile_HasClinicalAppointmentFlags()
    {
        var doctor = Assert.Single(
            VerticalProfileSeedData.All,
            profile => profile.Id == VerticalProfileIds.Doctor);

        Assert.Equal(VerticalProfileLayouts.Appointment, doctor.PosLayout);
        using var features = JsonDocument.Parse(doctor.PosFeatures);
        Assert.True(features.RootElement.GetProperty("patientRecord").GetBoolean());
        Assert.True(features.RootElement.GetProperty("prescription").GetBoolean());
        Assert.True(features.RootElement.GetProperty("appointment").GetBoolean());
        Assert.True(features.RootElement.GetProperty("serviceDuration").GetBoolean());
        Assert.False(features.RootElement.GetProperty("kitchenDisplay").GetBoolean());
        Assert.False(features.RootElement.GetProperty("tables").GetBoolean());
        Assert.False(features.RootElement.GetProperty("roomTracking").GetBoolean());
        Assert.False(features.RootElement.GetProperty("imeiTracking").GetBoolean());
        Assert.False(features.RootElement.GetProperty("ticketScan").GetBoolean());
        Assert.False(features.RootElement.GetProperty("routeTracking").GetBoolean());

        var vet = Assert.Single(
            VerticalProfileSeedData.All,
            profile => profile.Id == VerticalProfileIds.Vet);
        using var vetFeatures = JsonDocument.Parse(vet.PosFeatures);
        Assert.True(vetFeatures.RootElement.GetProperty("patientRecord").GetBoolean());
        Assert.False(vetFeatures.RootElement.TryGetProperty("prescription", out _));
    }

    [Fact]
    public void ClinicalData_LegacyPetJson_StillLoadsPetAndSpecies()
    {
        const string stored =
            """{"PetName":"Bello","PetSpecies":"Hund","PetBreed":"Mix","PetBirthDate":"2021-05-03"}""";

        var data = CustomerClinicalDataJson.Deserialize(stored);

        Assert.NotNull(data);
        Assert.Equal("Bello", data!.PetName);
        Assert.Equal("Hund", data.PetSpecies);
        Assert.Equal("Hund", data.Species);
        Assert.Equal("Mix", data.Breed);
        Assert.Equal(new DateOnly(2021, 5, 3), data.PetBirthDate);
        Assert.Equal(new DateOnly(2021, 5, 3), data.BirthDate);

        var rewritten = CustomerClinicalDataJson.Deserialize(CustomerClinicalDataJson.Serialize(data));
        Assert.Equal("Bello", rewritten!.PetName);
        Assert.Equal("Hund", rewritten.PetSpecies);
    }

    [Fact]
    public void Migration_RenamesPetDataAndKeepsLegacyKeys()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Migrations",
            "20261012141037_AddDoctorClinicalData.cs"));
        var text = File.ReadAllText(path);
        var up = text.Split("protected override void Down", StringSplitOptions.None)[0];
        Assert.Contains("RenameColumn", up, StringComparison.Ordinal);
        Assert.Contains("name: \"pet_data\"", up, StringComparison.Ordinal);
        Assert.Contains("newName: \"clinical_data\"", up, StringComparison.Ordinal);
        Assert.Contains("clinical_data->>'PetSpecies'", up, StringComparison.Ordinal);
        Assert.DoesNotContain("DropColumn", up, StringComparison.Ordinal);
        Assert.Contains("requires_prescription", up, StringComparison.Ordinal);
        Assert.Contains("\"doctor\"", up, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Payment_WhenPrescriptionRequiredAndMissing_DoesNotSign()
    {
        await using var ctx = CreateContext(out var registry, out var extension);
        Enable(ctx, extension.FeatureName);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await RequirePrescriptionAsync(ctx, productId);
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                TseMock = tse,
                ExtensionRegistry = registry,
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal(ExtensionFailedException.ErrorCode, result.DiagnosticCode);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()),
            Times.Never);
    }

    [Fact]
    public async Task Payment_WhenPrescriptionPresent_SignsTheOriginalTotal()
    {
        await using var ctx = CreateContext(out var registry, out var extension);
        Enable(ctx, extension.FeatureName);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await RequirePrescriptionAsync(ctx, productId);
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        var profiles = new Mock<IVerticalProfileService>();
        profiles
            .Setup(service => service.GetForCurrentTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DoctorProfile());
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                TseMock = tse,
                ExtensionRegistry = registry,
                VerticalProfiles = profiles.Object,
            });
        var request = PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId);
        request.PrescriptionReference = "RX-100";

        var result = await sut.CreatePaymentAsync(request, PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(10m, result.Payment!.TotalAmount);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                10m,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task Payment_WhenExtensionDisabled_SignsEvenIfProductRequiresPrescription()
    {
        await using var ctx = CreateContext(out var registry, out _);
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        await RequirePrescriptionAsync(ctx, productId);
        var tse = PaymentServiceCoverageHarness.CreateTseMock();
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                TseMock = tse,
                ExtensionRegistry = registry,
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        Assert.Equal(10m, result.Payment!.TotalAmount);
        tse.Verify(
            x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                10m,
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()),
            Times.Once);
    }

    private static async Task RequirePrescriptionAsync(AppDbContext ctx, Guid productId)
    {
        var product = await ctx.Products.SingleAsync(row => row.Id == productId);
        product.RequiresPrescription = true;
        await ctx.SaveChangesAsync();
    }

    private static AppDbContext CreateContext(
        out ITenantExtensionRegistry registry,
        out PrescriptionRequiredExtension extension)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Doctor_{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var ctx = new AppDbContext(options, accessor);
        extension = new PrescriptionRequiredExtension(ctx, NullLogger<PrescriptionRequiredExtension>.Instance);
        registry = new TenantExtensionRegistry(
            [extension],
            TenantTestDoubles.DbContextFactoryForTests(options, TenantTestDoubles.TenantAccessorReturning(null)),
            new MemoryCache(new MemoryCacheOptions()),
            new PassThroughCache(),
            Options.Create(new CacheSettings()),
            Mock.Of<IAuditLogService>(),
            NullLogger<TenantExtensionRegistry>.Instance);
        return ctx;
    }

    private static void Enable(AppDbContext ctx, string featureName)
    {
        ctx.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            Key = TenantExtensionSettings.EnabledKey,
            Value = $"[\"{featureName}\"]",
            UpdatedAtUtc = DateTime.UtcNow,
        });
        ctx.SaveChanges();
    }

    private static EffectiveVerticalProfileDto DoctorProfile()
    {
        var features = JsonDocument.Parse("""{"patientRecord":true,"prescription":true}""").RootElement.Clone();
        var empty = JsonDocument.Parse("{}").RootElement.Clone();
        var fields = JsonDocument.Parse("""{"customer":["name"],"product":["name","price"]}""").RootElement.Clone();
        return new EffectiveVerticalProfileDto(
            VerticalProfileIds.Doctor,
            "verticalProfiles.doctor.name",
            features,
            fields,
            fields,
            VerticalProfileLayouts.Appointment,
            empty);
    }

    private sealed class PassThroughCache : ICacheService
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
