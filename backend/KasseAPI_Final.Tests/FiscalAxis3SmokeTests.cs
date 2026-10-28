using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Export;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Axis 3 fiscal smoke. Reads existing payment, DEP, and tenant-backup paths.
/// Does not replace <see cref="RksvDepExportServiceTests"/>, <see cref="FiskalyDepExportPrueftoolTests"/>,
/// <see cref="TseServiceSignatureChainPostgreSqlTests"/>, or <see cref="TseBackupServiceTests"/>.
/// </summary>
public sealed class FiscalAxis3SmokeTests
{
    private const string StoredSignature = "axis3-stored-signature";
    private const string StoredPrevious = "axis3-stored-previous";
    private const string StoredThumbprint = "axis3-stored-thumb";

    [Fact]
    public async Task CreatePayment_PersistsExpectedPaymentDetailsRow()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Tse = new TseOptions { TseMode = "Demo" },
                TseMock = CreateTseMock(StoredSignature, StoredPrevious, StoredThumbprint),
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        var row = await ctx.PaymentDetails.AsNoTracking().SingleAsync();
        Assert.Equal(registerId, row.CashRegisterId);
        Assert.Equal(customerId, row.CustomerId);
        Assert.Equal(result.Payment!.TotalAmount, row.TotalAmount);
        Assert.True(row.TotalAmount > 0);
        Assert.False(string.IsNullOrWhiteSpace(row.ReceiptNumber));
        Assert.StartsWith("AT-KASSE-01-", row.ReceiptNumber, StringComparison.Ordinal);
        Assert.Equal(result.Payment.ReceiptNumber, row.ReceiptNumber);
        Assert.Equal(StoredSignature, row.TseSignature);
        Assert.Equal(StoredPrevious, row.PrevSignatureValueUsed);
        Assert.Equal(StoredThumbprint, row.CertificateThumbprint);
        Assert.Equal("ATU12345678", row.Steuernummer);
    }

    [Fact]
    public async Task CreatePayment_StoresCompactJwsWithThreeSegments()
    {
        var jws = SignMachineCodeJws("AT-AXIS3-JWS-1", previousCompactJws: null);
        Assert.Equal(3, jws.Split('.').Length);

        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Tse = new TseOptions { TseMode = "Demo" },
                TseMock = CreateTseMock(jws, string.Empty, StoredThumbprint),
            });

        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(result.Success, result.Message + ": " + string.Join("; ", result.Errors));
        var row = await ctx.PaymentDetails.AsNoTracking().SingleAsync();
        Assert.Equal(jws, row.TseSignature);
        var parts = row.TseSignature.Split('.');
        Assert.Equal(3, parts.Length);
        Assert.All(parts, part => Assert.False(string.IsNullOrEmpty(part)));
        Assert.Equal("eyJhbGciOiJFUzI1NiJ9", parts[0]);
    }

    [Fact]
    public async Task CreatePayment_ReceiptQrAndPreviousSignatureAreContinuous()
    {
        var firstJws = SignMachineCodeJws("AT-AXIS3-CHAIN-1", previousCompactJws: null);
        var secondJws = SignMachineCodeJws("AT-AXIS3-CHAIN-2", previousCompactJws: firstJws);
        var planned = new[]
        {
            new TseSignatureResult(firstJws, string.Empty, StoredThumbprint, SigningProvider: "local"),
            new TseSignatureResult(secondJws, firstJws, StoredThumbprint, SigningProvider: "local"),
        };

        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var sut = PaymentServiceCoverageHarness.CreatePaymentService(
            ctx,
            new PaymentServiceCoverageHarness.Options
            {
                Tse = new TseOptions { TseMode = "Demo" },
                TseMock = CreateSequentialTseMock(planned),
            });

        var first = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);
        var second = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.True(first.Success, first.Message + ": " + string.Join("; ", first.Errors));
        Assert.True(second.Success, second.Message + ": " + string.Join("; ", second.Errors));

        var payments = await ctx.PaymentDetails.AsNoTracking()
            .OrderBy(p => p.ReceiptNumber)
            .ToListAsync();
        Assert.Equal(2, payments.Count);
        Assert.Equal(firstJws, payments[0].TseSignature);
        Assert.Equal(string.Empty, payments[0].PrevSignatureValueUsed);
        Assert.Equal(secondJws, payments[1].TseSignature);
        Assert.Equal(firstJws, payments[1].PrevSignatureValueUsed);

        Assert.Equal(2, await ctx.Invoices.CountAsync());
        var receipts = await ctx.Receipts.AsNoTracking().ToListAsync();
        Assert.Equal(2, receipts.Count);
        var firstReceipt = Assert.Single(receipts, r => r.PaymentId == payments[0].Id);
        var secondReceipt = Assert.Single(receipts, r => r.PaymentId == payments[1].Id);
        Assert.Equal(firstJws, firstReceipt.SignatureValue);
        Assert.Equal(string.Empty, firstReceipt.PrevSignatureValue);
        Assert.Equal(secondJws, secondReceipt.SignatureValue);
        Assert.Equal(firstJws, secondReceipt.PrevSignatureValue);
        Assert.StartsWith("_R1-AT1_", firstReceipt.QrCodePayload, StringComparison.Ordinal);
        Assert.StartsWith("_R1-AT1_", secondReceipt.QrCodePayload, StringComparison.Ordinal);

        var firstQr = await sut.GetQrPayloadForPaymentAsync(payments[0].Id);
        var secondQr = await sut.GetQrPayloadForPaymentAsync(payments[1].Id);
        Assert.NotNull(firstQr);
        Assert.NotNull(secondQr);
        Assert.StartsWith("_R1-AT1_", firstQr.Value.QrPayload, StringComparison.Ordinal);
        Assert.StartsWith("_R1-AT1_", secondQr.Value.QrPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DepExport_GroupsByThumbprint_AndReportsLegacyJws()
    {
        const string thumbA = "AXIS3-THUMB-A";
        const string thumbB = "AXIS3-THUMB-B";
        var f5A1 = SignMachineCodeJws("AT-AXIS3-DEP-0001", previousCompactJws: null);
        var f5A2 = SignMachineCodeJws("AT-AXIS3-DEP-0002", previousCompactJws: f5A1);
        var f5B = SignMachineCodeJws("AT-AXIS3-DEP-0003", previousCompactJws: f5A2);
        var legacy = LegacyJsonJws();

        await using var db = CreateDepDb();
        var registerId = await SeedDepRegisterAsync(db);
        var issued = new DateTime(2026, 1, 10, 9, 0, 0, DateTimeKind.Utc);
        db.PaymentDetails.AddRange(
            CreateDepPayment(registerId, issued, "AT-AXIS3-DEP-0001", thumbA, f5A1),
            CreateDepPayment(registerId, issued.AddHours(1), "AT-AXIS3-DEP-0002", thumbA, f5A2),
            CreateDepPayment(registerId, issued.AddHours(2), "AT-AXIS3-DEP-0004", thumbA, legacy),
            CreateDepPayment(registerId, issued.AddHours(3), "AT-AXIS3-DEP-0003", thumbB, f5B));
        await db.SaveChangesAsync();

        var service = CreateDepExportService(db, CreateTwoThumbprintKeyProvider());
        var export = await service.GenerateDepExportWithValidationAsync(
            registerId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(2, export.BelegeGruppeCount);
        Assert.Equal(2, export.Root.BelegeGruppe.Count);
        Assert.Equal(3, export.F5CompliantJwsCount);
        Assert.Equal(1, export.LegacyJwsCount);
        Assert.NotNull(export.LegacyJwsWarning);
        Assert.Contains("legacy", export.LegacyJwsWarning, StringComparison.OrdinalIgnoreCase);

        var groupA = Assert.Single(export.Root.BelegeGruppe, g => g.BelegeKompakt.Contains(legacy));
        Assert.Equal(3, groupA.BelegeKompakt.Count);
        Assert.Contains(f5A1, groupA.BelegeKompakt);
        Assert.Contains(f5A2, groupA.BelegeKompakt);
        Assert.False(string.IsNullOrWhiteSpace(groupA.Signaturzertifikat));

        var groupB = Assert.Single(export.Root.BelegeGruppe, g => g.BelegeKompakt.Contains(f5B));
        Assert.Single(groupB.BelegeKompakt);
        Assert.False(string.IsNullOrWhiteSpace(groupB.Signaturzertifikat));
        Assert.Equal(4, export.Root.BelegeGruppe.Sum(g => g.BelegeKompakt.Count));
    }

    [Fact]
    public async Task TenantBackup_IncludesPaymentDetailsAndReceiptRows()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        const string receiptNumber = "AT-AXIS3-BACKUP-20261028-1";
        const string tseSignature = "hdr.payload.sig";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Axis3Backup_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Axis3 Cafe",
            Slug = "axis3",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "KASSE-AXIS3",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = paymentId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Gast",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 12.40m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            ReceiptNumber = receiptNumber,
            TseSignature = tseSignature,
            CertificateThumbprint = StoredThumbprint,
            CreatedAt = DateTime.UtcNow,
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
        });
        db.Receipts.Add(new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            TenantId = tenantId,
            PaymentId = paymentId,
            CashRegisterId = registerId,
            ReceiptNumber = receiptNumber,
            IssuedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            SubTotal = 12.40m,
            TaxTotal = 0m,
            GrandTotal = 12.40m,
            SignatureValue = tseSignature,
        });
        await db.SaveChangesAsync();

        var zipPath = Path.Combine(Path.GetTempPath(), $"axis3-tenant-backup-{Guid.NewGuid():N}.zip");
        try
        {
            var exporter = new TenantScopedBackupExporter();
            var result = await exporter.ExportAsync(db, tenantId, "axis3", zipPath);

            Assert.Equal(1, result.Manifest.TableRowCounts["payment_details.json"]);
            Assert.Equal(1, result.Manifest.TableRowCounts["receipts.json"]);

            await using var zip = ZipFile.OpenRead(zipPath);
            await using var paymentsStream = zip.GetEntry("payment_details.json")!.Open();
            using var payments = await JsonDocument.ParseAsync(paymentsStream);
            Assert.Equal(1, payments.RootElement.GetArrayLength());
            Assert.Equal(tseSignature, payments.RootElement[0].GetProperty("tseSignature").GetString());
            Assert.Equal(receiptNumber, payments.RootElement[0].GetProperty("receiptNumber").GetString());

            await using var receiptsStream = zip.GetEntry("receipts.json")!.Open();
            using var receipts = await JsonDocument.ParseAsync(receiptsStream);
            Assert.Equal(1, receipts.RootElement.GetArrayLength());
            Assert.Equal(receiptNumber, receipts.RootElement[0].GetProperty("receiptNumber").GetString());
            Assert.Equal(tseSignature, receipts.RootElement[0].GetProperty("signatureValue").GetString());
            Assert.Equal(paymentId, receipts.RootElement[0].GetProperty("paymentId").GetGuid());
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
    }

    private static Mock<ITseService> CreateTseMock(string compactJws, string previous, string thumbprint)
    {
        var tse = new Mock<ITseService>();
        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult(compactJws, previous, thumbprint, SigningProvider: "local"));
        return tse;
    }

    private static Mock<ITseService> CreateSequentialTseMock(IReadOnlyList<TseSignatureResult> planned)
    {
        var index = 0;
        var tse = new Mock<ITseService>();
        tse.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(() => planned[index++]);
        return tse;
    }

    private static string SignMachineCodeJws(string belegnummer, string? previousCompactJws)
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-AXIS3",
            belegnummer,
            new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 10.00m },
            turnoverCounterCents: 1000,
            previousCompactJws,
            keyProvider.GetCertificateSerialNumber()!,
            keyProvider.GetTurnoverCounterAesKeyBytes()!);
        return pipeline.Sign(payload, "axis3-fiscal-smoke");
    }

    private static string LegacyJsonJws()
    {
        var jsonPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"Kassen-ID":"1"}"""))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"eyJhbGciOiJFUzI1NiJ9.{jsonPayload}.c2ln";
    }

    private static AppDbContext CreateDepDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Axis3Dep_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(SystemTenantIds.Platform));
    }

    private static async Task<Guid> SeedDepRegisterAsync(AppDbContext db)
    {
        TenantTestDoubles.EnsurePlatformTenant(db);
        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = registerId,
            RegisterNumber = "KASSE-AXIS3",
            Location = "Axis3 DEP",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return registerId;
    }

    private static PaymentDetails CreateDepPayment(
        Guid registerId,
        DateTime issuedAt,
        string receiptNumber,
        string thumbprint,
        string tseSignature) =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = tseSignature,
            TseTimestamp = issuedAt,
            ReceiptNumber = receiptNumber,
            CreatedAt = issuedAt,
            CertificateThumbprint = thumbprint,
            TaxDetails = JsonDocument.Parse("{}"),
            PaymentItems = JsonDocument.Parse("[]"),
        };

    private static ITseKeyProvider CreateTwoThumbprintKeyProvider()
    {
        var signingKey = new SoftwareTseKeyProvider();
        var certificate = X509CertificateLoader.LoadCertificate(signingKey.GetCertificateBytes()!);
        var keys = new Mock<ITseKeyProvider>();
        keys.Setup(k => k.GetCurrentCertificateThumbprint()).Returns("AXIS3-THUMB-A");
        keys.Setup(k => k.GetCertificateBytes()).Returns(signingKey.GetCertificateBytes());
        keys.Setup(k => k.GetTurnoverCounterAesKeyBytes()).Returns(signingKey.GetTurnoverCounterAesKeyBytes());
        keys.Setup(k => k.GetCertificateByThumbprintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(certificate);
        keys.Setup(k => k.GetCertificateChainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<X509Certificate2>());
        return keys.Object;
    }

    private static RksvDepExportService CreateDepExportService(AppDbContext db, ITseKeyProvider keyProvider)
    {
        var env = new Mock<IRksvEnvironmentService>();
        env.Setup(x => x.IsDemoMode()).Returns(true);
        env.Setup(x => x.IsProductionMode()).Returns(false);
        env.Setup(x => x.IsTseSimulated()).Returns(true);
        return new RksvDepExportService(
            db,
            keyProvider,
            env.Object,
            Mock.Of<IRksvDepPrueftoolRunner>(),
            NullLogger<RksvDepExportService>.Instance);
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }
}
