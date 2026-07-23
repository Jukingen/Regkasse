using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvDepExportServiceTests
{
    private const string ValidJws = "eyJhbGci.eyJkYXRh.c2ln";
    private static string ValidJwsFor(string label) => $"eyJhbGci.{label}.c2ln";

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RksvDepExport_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(LegacyDefaultTenantIds.Primary));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
    public string? TenantSlug { get; set; }
    }

    private static async Task<Guid> SeedRegisterAsync(AppDbContext db)
    {
        TenantTestDoubles.EnsureDefaultTenant(db);
        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return regId;
    }

    private static PaymentDetails CreatePayment(
        Guid registerId,
        DateTime issuedAt,
        string receiptNumber,
        string? specialKind = null,
        string? thumbprint = null,
        string? tseSignature = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "cashier-1",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = tseSignature ?? ValidJws,
            TseTimestamp = issuedAt,
            ReceiptNumber = receiptNumber,
            CreatedAt = issuedAt,
            CertificateThumbprint = thumbprint,
            RksvSpecialReceiptKind = specialKind,
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
        };

    private static DailyClosing CreateDailyClosing(
        Guid registerId,
        DateTime issuedAt,
        DateTime closingDate,
        string? tseSignature = null,
        string? certificateThumbprint = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = registerId,
            UserId = "user-1",
            ClosingDate = closingDate,
            ClosingType = "Daily",
            TotalAmount = 100m,
            TotalTaxAmount = 10m,
            TransactionCount = 5,
            TseSignature = tseSignature ?? ValidJws,
            CertificateThumbprint = certificateThumbprint,
            Status = "Completed",
            CreatedAt = issuedAt,
        };

    private static RksvDepExportService CreateService(
        AppDbContext db,
        ITseKeyProvider? keyProvider = null,
        IRksvEnvironmentService? rksvEnv = null,
        IRksvDepPrueftoolRunner? prueftoolRunner = null) =>
        new(
            db,
            keyProvider ?? new SoftwareTseKeyProvider(),
            rksvEnv ?? CreateDemoEnvironment(),
            prueftoolRunner ?? Mock.Of<IRksvDepPrueftoolRunner>(),
            Mock.Of<ILogger<RksvDepExportService>>());

    private static IRksvEnvironmentService CreateDemoEnvironment()
    {
        var mock = new Mock<IRksvEnvironmentService>();
        mock.Setup(x => x.IsDemoMode()).Returns(true);
        mock.Setup(x => x.IsProductionMode()).Returns(false);
        mock.Setup(x => x.IsTseSimulated()).Returns(true);
        return mock.Object;
    }

    private static IRksvEnvironmentService CreateProductionEnvironment()
    {
        var mock = new Mock<IRksvEnvironmentService>();
        mock.Setup(x => x.IsDemoMode()).Returns(false);
        mock.Setup(x => x.IsProductionMode()).Returns(true);
        mock.Setup(x => x.IsTseSimulated()).Returns(false);
        return mock.Object;
    }
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("a.b", false)]
    [InlineData("a.b.c.d", false)]
    [InlineData("eyJhbGci.eyJkYXRh.c2ln", true)]
    public void IsValidCompactJws_ValidatesThreePartJws(string? value, bool expected)
    {
        Assert.Equal(expected, RksvDepExportService.IsValidCompactJws(value));
    }

    [Fact]
    public void BmfRootDto_SerializesWithExactPropertyNames()
    {
        var root = new RksvDepExportRootDto
        {
            BelegeGruppe =
            [
                new RksvDepBelegeGruppeDto
                {
                    Signaturzertifikat = "CERT",
                    Zertifizierungsstellen = ["CA1"],
                    BelegeKompakt = ["hdr.payload.sig"],
                },
            ],
        };

        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false,
        });

        Assert.Contains("\"Belege-Gruppe\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Signaturzertifikat\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Zertifizierungsstellen\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Belege-kompakt\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderReceiptsForDepExport_SortsByIssuedAtThenSequenceNumber()
    {
        var t0 = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        var receipts = new[]
        {
            new RksvDepReceiptSignatureInfo { IssuedAt = t0.AddHours(1), SequenceNumber = 2, ReceiptType = RksvDepReceiptTypes.Normal },
            new RksvDepReceiptSignatureInfo { IssuedAt = t0, SequenceNumber = 9, ReceiptType = RksvSpecialReceiptKinds.Startbeleg },
            new RksvDepReceiptSignatureInfo { IssuedAt = t0.AddHours(2), SequenceNumber = 1, ReceiptType = RksvDepReceiptTypes.DailyClosing },
        };

        var ordered = RksvDepExportService.OrderReceiptsForDepExport(receipts);

        Assert.Equal(RksvSpecialReceiptKinds.Startbeleg, ordered[0].ReceiptType);
        Assert.Equal(RksvDepReceiptTypes.Normal, ordered[1].ReceiptType);
        Assert.Equal(RksvDepReceiptTypes.DailyClosing, ordered[2].ReceiptType);
    }

    [Fact]
    public async Task GenerateDepExport_IncludesSpecialReceiptsAndDailyClosings_InChronologicalOrder()
    {
        await using var db = CreateDb();
        var regId = await SeedRegisterAsync(db);
        var keyProvider = new SoftwareTseKeyProvider();
        var thumb = keyProvider.GetCurrentCertificateThumbprint()!;
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        db.PaymentDetails.AddRange(
            CreatePayment(
                regId,
                new DateTime(2026, 1, 10, 10, 0, 0, DateTimeKind.Utc),
                "AT-TSE-20260110-0001",
                thumbprint: thumb,
                tseSignature: ValidJwsFor("normal")),
            CreatePayment(
                regId,
                new DateTime(2026, 1, 10, 11, 0, 0, DateTimeKind.Utc),
                "AT-TSE-20260110-0002",
                RksvSpecialReceiptKinds.Startbeleg,
                thumb,
                ValidJwsFor("start")),
            CreatePayment(
                regId,
                new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
                "AT-TSE-20260110-0003",
                RksvSpecialReceiptKinds.Monatsbeleg,
                thumb,
                ValidJwsFor("monat")));
        db.DailyClosings.Add(CreateDailyClosing(
            regId,
            new DateTime(2026, 1, 10, 13, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 10),
            ValidJwsFor("closing"),
            thumb));
        await db.SaveChangesAsync();

        var export = await CreateService(db, keyProvider).GenerateDepExportAsync(regId, from, to);

        Assert.Single(export.BelegeGruppe);
        Assert.Equal(4, export.BelegeGruppe[0].BelegeKompakt.Count);
        Assert.Equal(
            new[]
            {
                ValidJwsFor("closing"),
                ValidJwsFor("normal"),
                ValidJwsFor("start"),
                ValidJwsFor("monat"),
            },
            export.BelegeGruppe[0].BelegeKompakt);
        Assert.False(string.IsNullOrEmpty(export.BelegeGruppe[0].Signaturzertifikat));
    }

    [Fact]
    public async Task GenerateDepExport_ExcludesSpecialReceipts_WhenFlagDisabled()
    {
        await using var db = CreateDb();
        var regId = await SeedRegisterAsync(db);
        var from = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 2, 28, 23, 59, 59, DateTimeKind.Utc);

        db.PaymentDetails.AddRange(
            CreatePayment(regId, new DateTime(2026, 2, 5, 10, 0, 0, DateTimeKind.Utc), "AT-TSE-20260205-0001"),
            CreatePayment(
                regId,
                new DateTime(2026, 2, 5, 11, 0, 0, DateTimeKind.Utc),
                "AT-TSE-20260205-0002",
                RksvSpecialReceiptKinds.Nullbeleg));
        await db.SaveChangesAsync();

        var export = await CreateService(db).GenerateDepExportAsync(
            regId,
            from,
            to,
            includeSpecialReceipts: false,
            includeDailyClosings: false);

        Assert.Single(export.BelegeGruppe);
        Assert.Single(export.BelegeGruppe[0].BelegeKompakt);
    }

    [Fact]
    public async Task GenerateDepExport_ExcludesDailyClosings_WhenFlagDisabled()
    {
        await using var db = CreateDb();
        var regId = await SeedRegisterAsync(db);
        var from = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc);

        db.PaymentDetails.Add(CreatePayment(
            regId,
            new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc),
            "AT-TSE-20260305-0001"));
        db.DailyClosings.Add(CreateDailyClosing(
            regId,
            new DateTime(2026, 3, 5, 22, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 3, 5)));
        await db.SaveChangesAsync();

        var export = await CreateService(db).GenerateDepExportAsync(
            regId,
            from,
            to,
            includeSpecialReceipts: true,
            includeDailyClosings: false);

        Assert.Single(export.BelegeGruppe);
        Assert.Single(export.BelegeGruppe[0].BelegeKompakt);
    }

    [Fact]
    public async Task GenerateCryptoMaterial_IncludesAesKeyAndActiveCertificate()
    {
        await using var db = CreateDb();
        var regId = await SeedRegisterAsync(db);
        var keyProvider = new SoftwareTseKeyProvider();
        var thumb = keyProvider.GetCurrentCertificateThumbprint()!;

        db.PaymentDetails.Add(CreatePayment(
            regId,
            new DateTime(2026, 4, 5, 10, 0, 0, DateTimeKind.Utc),
            "AT-TSE-20260405-0001",
            thumbprint: thumb));
        await db.SaveChangesAsync();

        var material = await CreateService(db, keyProvider).GenerateCryptoMaterialAsync(regId);

        Assert.False(string.IsNullOrEmpty(material.AesKeyBase64));
        Assert.Single(material.Certificates);
        var entry = material.Certificates.Single();
        Assert.Equal(keyProvider.GetCertificateSerialNumber(), entry.SerialNumber);
        Assert.Equal(thumb, entry.Thumbprint);
        Assert.False(string.IsNullOrEmpty(entry.CertificateDerBase64));
        Assert.Single(material.TurnoverCounters);
        Assert.Equal("0", material.TurnoverCounters["AT-TSE-20260405-0001"]);
    }

    [Fact]
    public void CryptoMaterialDto_SerializesWithExactPropertyNames()
    {
        var dto = new CryptoMaterialDto
        {
            AesKeyBase64 = "abc=",
            Certificates =
            [
                new CertificateInfoDto
                {
                    SerialNumber = "SERIAL1",
                    CertificateDerBase64 = "CERTDER",
                    Thumbprint = "THUMB1",
                },
            ],
            TurnoverCounters = new Dictionary<string, string> { ["AT-TSE-1"] = "1240" },
        };

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = false,
        });

        Assert.Contains("\"aesKeyBase64\"", json, StringComparison.Ordinal);
        Assert.Contains("\"certificates\"", json, StringComparison.Ordinal);
        Assert.Contains("\"certificateDerBase64\"", json, StringComparison.Ordinal);
        Assert.Contains("\"turnoverCounters\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateCryptoMaterial_ThrowsWhenRegisterMissing()
    {
        await using var db = CreateDb();
        await SeedRegisterAsync(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(db).GenerateCryptoMaterialAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ValidateExportFormat_AcceptsValidBmfStructure()
    {
        var root = new RksvDepExportRootDto
        {
            BelegeGruppe =
            [
                new RksvDepBelegeGruppeDto
                {
                    Signaturzertifikat = "CERT",
                    Zertifizierungsstellen = [],
                    BelegeKompakt = ["eyJhbGci.eyJkYXRh.c2ln"],
                },
            ],
        };

        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { PropertyNamingPolicy = null });
        var result = await CreateService(CreateDb()).ValidateExportFormatAsync(json);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.BelegeGruppeCount);
        Assert.Equal(1, result.BelegCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateExportFormat_RejectsMissingBelegeGruppe()
    {
        var result = await CreateService(CreateDb()).ValidateExportFormatAsync("{}");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Belege-Gruppe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateExportFormat_WarnsOnEmptyCaChain_InProductionMode()
    {
        var root = new RksvDepExportRootDto
        {
            BelegeGruppe =
            [
                new RksvDepBelegeGruppeDto
                {
                    Signaturzertifikat = "CERT",
                    Zertifizierungsstellen = [],
                    BelegeKompakt = ["eyJhbGci.eyJkYXRh.c2ln"],
                },
            ],
        };

        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { PropertyNamingPolicy = null });
        await using var db = CreateDb();
        var result = await CreateService(db, rksvEnv: CreateProductionEnvironment())
            .ValidateExportFormatAsync(json);

        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public async Task GenerateDepExportWithValidation_IncludesDemoMetadata()
    {
        await using var db = CreateDb();
        var regId = await SeedRegisterAsync(db);
        var from = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 5, 31, 23, 59, 59, DateTimeKind.Utc);

        db.PaymentDetails.Add(CreatePayment(
            regId,
            new DateTime(2026, 5, 5, 10, 0, 0, DateTimeKind.Utc),
            "AT-TSE-20260505-0001"));
        await db.SaveChangesAsync();

        var build = await CreateService(db).GenerateDepExportWithValidationAsync(regId, from, to);

        Assert.True(build.IsDemo);
        Assert.Equal("Demo", build.Environment);
        Assert.True(build.FormatValidated);
        Assert.Contains("DEMO / NICHT FISKAL", build.LegalNotice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunPrueftool_SkipsInDemoMode_WhenNotForced()
    {
        await using var db = CreateDb();
        var regId = await SeedRegisterAsync(db);
        var export = new RksvDepExportRootDto
        {
            BelegeGruppe =
            [
                new RksvDepBelegeGruppeDto
                {
                    Signaturzertifikat = "CERT",
                    BelegeKompakt = ["eyJhbGci.eyJkYXRh.c2ln"],
                },
            ],
        };

        var result = await CreateService(db).RunPrueftoolAsync(regId, export);

        Assert.True(result.Success);
        Assert.True(result.Skipped);
        Assert.Equal("Demo", result.Environment);
        Assert.Contains("skipped", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunPrueftool_RequiresPass_InProductionMode_WhenAvailable()
    {
        await using var db = CreateDb();
        var regId = await SeedRegisterAsync(db);
        var export = new RksvDepExportRootDto
        {
            BelegeGruppe =
            [
                new RksvDepBelegeGruppeDto
                {
                    Signaturzertifikat = "CERT",
                    BelegeKompakt = ["eyJhbGci.eyJkYXRh.c2ln"],
                },
            ],
        };

        var prueftool = new Mock<IRksvDepPrueftoolRunner>();
        prueftool.Setup(x => x.IsAvailable(out It.Ref<string?>.IsAny)).Returns(true);
        prueftool
            .Setup(x => x.RunCheckDepExport(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new RksvDepPrueftoolRunResult(1, "FAIL", "stderr", "stdout"));

        var result = await CreateService(db, rksvEnv: CreateProductionEnvironment(), prueftoolRunner: prueftool.Object)
            .RunPrueftoolAsync(regId, export);

        Assert.False(result.Success);
        Assert.False(result.Skipped);
        Assert.Equal("Production", result.Environment);
    }
}
