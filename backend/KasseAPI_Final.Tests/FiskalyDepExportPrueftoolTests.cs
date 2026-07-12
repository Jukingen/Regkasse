using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tests.Fixtures;
using KasseAPI_Final.Tse;
using KasseAPI_Final.Tse.Fiskaly;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// DEP export via <see cref="FiskalyTseKeyProvider"/> with mocked <see cref="IFiskalyClient"/>.
/// Cryptographic truth uses <see cref="FixedPrueftoolTseKeyProvider"/> material (Prüftool-compatible).
/// </summary>
public sealed class FiskalyDepExportPrueftoolTests
{
    private const string TestScuId = "test-fiskaly-scu";

    [Fact]
    public async Task DepExport_WithFiskalyProvider_PassesPrueftool()
    {
        var signingKey = new FixedPrueftoolTseKeyProvider();
        var cert = X509CertificateLoader.LoadCertificate(signingKey.GetCertificateBytes()!);
        var thumbprint = signingKey.GetCurrentCertificateThumbprint()!;
        var signedReceipts = SignChainedReceipts(signingKey);

        var fiskalyProvider = CreateFiskalyProviderWithMock(cert, thumbprint, signingKey);
        await using var db = CreateDb();
        var registerId = await SeedRegisterWithSignedReceiptsAsync(db, signedReceipts, thumbprint);

        var service = CreateDepExportService(db, fiskalyProvider);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var export = await service.GenerateDepExportAsync(registerId, from, to);
        var crypto = await service.GenerateCryptoMaterialAsync(registerId);

        AssertBmfDepStructure(export);
        Assert.All(export.BelegeGruppe[0].BelegeKompakt, jws => AssertValidPrueftoolJws(jws, signingKey));

        var tempDir = Path.Combine(Path.GetTempPath(), $"regkasse-fiskaly-dep-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var depPath = Path.Combine(tempDir, "dep-export.json");
            var cryptoPath = Path.Combine(tempDir, "crypto-material.json");
            WriteDepExport(export, depPath);
            PrueftoolDepVerificationHelper.WritePrueftoolCryptoMaterial(crypto, cryptoPath);

            Assert.True(File.Exists(depPath));
            Assert.Contains("\"Belege-Gruppe\"", await File.ReadAllTextAsync(depPath), StringComparison.Ordinal);
            Assert.Contains("\"base64AESKey\"", await File.ReadAllTextAsync(cryptoPath), StringComparison.Ordinal);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
        }
    }

    [SkippableFact]
    public async Task DepExport_WithFiskalyProvider_PassesBmfCheckDepExport_WhenPrueftoolInstalled()
    {
        Skip.IfNot(
            PrueftoolDepVerificationHelper.IsDepVerificationAvailable(out var skipReason),
            skipReason ?? "Prüftool not available.");

        var signingKey = new FixedPrueftoolTseKeyProvider();
        var cert = X509CertificateLoader.LoadCertificate(signingKey.GetCertificateBytes()!);
        var thumbprint = signingKey.GetCurrentCertificateThumbprint()!;
        var signedReceipts = SignChainedReceipts(signingKey);

        var fiskalyProvider = CreateFiskalyProviderWithMock(cert, thumbprint, signingKey);
        await using var db = CreateDb();
        var registerId = await SeedRegisterWithSignedReceiptsAsync(db, signedReceipts, thumbprint);

        var service = CreateDepExportService(db, fiskalyProvider);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var export = await service.GenerateDepExportAsync(registerId, from, to);
        var crypto = await service.GenerateCryptoMaterialAsync(registerId);

        var tempDir = Path.Combine(Path.GetTempPath(), $"regkasse-fiskaly-prueftool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var depPath = Path.Combine(tempDir, "dep-export.json");
        var cryptoPath = Path.Combine(tempDir, "crypto-material.json");
        var outputDir = Path.Combine(tempDir, "verification_output");

        WriteDepExport(export, depPath);
        PrueftoolDepVerificationHelper.WritePrueftoolCryptoMaterial(crypto, cryptoPath);

        var result = PrueftoolDepVerificationHelper.RunCheckDepExport(depPath, cryptoPath, outputDir);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("PASS", result.VerificationState);
    }

    private static FiskalyTseKeyProvider CreateFiskalyProviderWithMock(
        X509Certificate2 certificate,
        string thumbprint,
        FixedPrueftoolTseKeyProvider signingKey)
    {
        var mockClient = new Mock<IFiskalyClient>();

        mockClient
            .Setup(x => x.GetCertificateAsync(TestScuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(certificate);

        mockClient
            .Setup(x => x.GetCertificateByThumbprintAsync(thumbprint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(certificate);

        mockClient
            .Setup(x => x.GetCertificateByThumbprintAsync(It.Is<string>(t => !string.Equals(t, thumbprint, StringComparison.OrdinalIgnoreCase)), It.IsAny<CancellationToken>()))
            .ReturnsAsync((X509Certificate2?)null);

        mockClient
            .Setup(x => x.GetCertificateChainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<X509Certificate2>());

        mockClient
            .Setup(x => x.GetSigningKeyAsync(TestScuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(signingKey.GetSigningKey());

        mockClient
            .Setup(x => x.GetSignatureCreationUnitAsync(TestScuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FiskalyScuInfo(TestScuId, "INITIALIZED", signingKey.GetCertificateSerialNumber()));

        var options = Options.Create(new FiskalyOptions
        {
            Enabled = true,
            ApiKey = "test-api-key",
            ApiSecret = "test-api-secret",
            TseSerialNumber = TestScuId,
            SigningCertificateDerBase64 = Convert.ToBase64String(certificate.RawData),
            TurnoverCounterAesKeyBase64 = Convert.ToBase64String(signingKey.GetTurnoverCounterAesKeyBytes()!),
        });

        return new FiskalyTseKeyProvider(mockClient.Object, options);
    }

    private static List<SignedReceiptSeed> SignChainedReceipts(FixedPrueftoolTseKeyProvider signingKey)
    {
        var pipeline = new SignaturePipeline(signingKey, NullLogger<SignaturePipeline>.Instance);
        var aesKey = signingKey.GetTurnoverCounterAesKeyBytes()!;
        var serial = signingKey.GetCertificateSerialNumber()!;

        var specs = new (DateTime IssuedAtUtc, string Belegnummer, decimal NormalGross, long TurnoverCents)[]
        {
            (new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc), "AT-FIXTURE-20260110-0001", 0m, 0),
            (new DateTime(2026, 1, 10, 10, 30, 0, DateTimeKind.Utc), "AT-FIXTURE-20260110-0002", 12.40m, 1240),
            (new DateTime(2026, 1, 10, 11, 15, 0, DateTimeKind.Utc), "AT-FIXTURE-20260110-0003", 25.00m, 3740),
        };

        string? previousJws = null;
        var results = new List<SignedReceiptSeed>(specs.Length);
        foreach (var (issuedAt, belegnummer, normalGross, turnoverCents) in specs)
        {
            var payload = BelegdatenPayloadBuilder.Build(
                "KASSE-FIXTURE-01",
                belegnummer,
                issuedAt,
                new RksvTaxSetAmounts { Normal = normalGross },
                turnoverCents,
                previousJws,
                serial,
                aesKey);

            var jws = pipeline.Sign(payload, "fiskaly-dep-export-test");
            results.Add(new SignedReceiptSeed(issuedAt, belegnummer, normalGross, jws));
            previousJws = jws;
        }

        return results;
    }

    private static async Task<Guid> SeedRegisterWithSignedReceiptsAsync(
        AppDbContext db,
        IReadOnlyList<SignedReceiptSeed> receipts,
        string thumbprint)
    {
        TenantTestDoubles.EnsureDefaultTenant(db);
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = registerId,
            RegisterNumber = "KASSE-FIXTURE-01",
            Location = "Fiskaly mock test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        foreach (var receipt in receipts)
        {
            db.PaymentDetails.Add(new PaymentDetails
            {
                Id = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                CustomerName = "Gast",
                TableNumber = 0,
                CashierId = "cashier-1",
                TotalAmount = receipt.NormalGross,
                TaxAmount = 0m,
                PaymentMethodRaw = "0",
                Steuernummer = "ATU12345678",
                CashRegisterId = registerId,
                TseSignature = receipt.CompactJws,
                TseTimestamp = receipt.IssuedAtUtc,
                ReceiptNumber = receipt.Belegnummer,
                CreatedAt = receipt.IssuedAtUtc,
                CertificateThumbprint = thumbprint,
                TaxDetails = JsonDocument.Parse("{}"),
                PaymentItems = JsonDocument.Parse("[]"),
            });
        }

        await db.SaveChangesAsync();
        return registerId;
    }

    private static void AssertBmfDepStructure(RksvDepExportRootDto export)
    {
        Assert.Single(export.BelegeGruppe);
        Assert.Equal(3, export.BelegeGruppe[0].BelegeKompakt.Count);
        Assert.False(string.IsNullOrWhiteSpace(export.BelegeGruppe[0].Signaturzertifikat));
        Assert.NotEmpty(export.BelegeGruppe[0].BelegeKompakt);
    }

    private static void AssertValidPrueftoolJws(string compactJws, FixedPrueftoolTseKeyProvider signingKey)
    {
        var pipeline = new SignaturePipeline(signingKey, NullLogger<SignaturePipeline>.Instance);
        var parts = compactJws.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.Equal("eyJhbGciOiJFUzI1NiJ9", parts[0]);
        Assert.True(pipeline.Verify(compactJws, signingKey.GetPublicKey()));
    }

    private static void WriteDepExport(RksvDepExportRootDto export, string path)
    {
        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = true,
        });
        File.WriteAllText(path, json);
    }

    private static RksvDepExportService CreateDepExportService(AppDbContext db, ITseKeyProvider keyProvider)
    {
        var env = new Mock<IRksvEnvironmentService>();
        env.Setup(x => x.IsDemoMode()).Returns(true);
        env.Setup(x => x.IsTseSimulated()).Returns(true);

        return new RksvDepExportService(
            db,
            keyProvider,
            env.Object,
            Mock.Of<IRksvDepPrueftoolRunner>(),
            Mock.Of<ILogger<RksvDepExportService>>());
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FiskalyDepExport_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(LegacyDefaultTenantIds.Primary));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
    }

    private sealed record SignedReceiptSeed(
        DateTime IssuedAtUtc,
        string Belegnummer,
        decimal NormalGross,
        string CompactJws);
}
