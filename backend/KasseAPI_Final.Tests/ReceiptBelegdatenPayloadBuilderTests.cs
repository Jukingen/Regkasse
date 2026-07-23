using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReceiptBelegdatenPayloadBuilderTests
{
    private static readonly byte[] DevAesKey = new SoftwareTseKeyProvider().GetTurnoverCounterAesKeyBytes()!;

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptBelegdaten_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(LegacyDefaultTenantIds.Primary));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
    public string? TenantSlug { get; set; }
    }

    [Fact]
    public async Task GetMachineCodeForReceiptAsync_UsesStoredCompactJwsMachineCode()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsureDefaultTenant(db);
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(
            keyProvider,
            NullLogger<SignaturePipeline>.Instance,
            new ReceiptBelegdatenPayloadBuilder(db, keyProvider));

        var registerId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = registerId,
            RegisterNumber = "KASSE-001",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        var beleg = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE-001-20260228-00000001",
            new DateTime(2026, 2, 28, 16, 52, 48, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 10.00m },
            1000,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            DevAesKey);
        var jws = pipeline.Sign(beleg, "builder-test");

        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            TableNumber = 1,
            CashierId = "cashier",
            TotalAmount = 10m,
            TaxAmount = 1.67m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = jws,
            TseTimestamp = new DateTime(2026, 2, 28, 16, 52, 48, DateTimeKind.Utc),
            ReceiptNumber = "AT-KASSE-001-20260228-00000001",
            CreatedAt = new DateTime(2026, 2, 28, 16, 52, 48, DateTimeKind.Utc),
            TaxDetails = JsonDocument.Parse("""{"1":10.00}"""),
            PaymentItems = JsonDocument.Parse("[]"),
        });
        await db.SaveChangesAsync();

        var machineCode = await pipeline.GetMachineCodeForReceiptAsync(
            registerId,
            "AT-KASSE-001-20260228-00000001",
            new DateTime(2026, 2, 28, 16, 52, 48, DateTimeKind.Utc));

        Assert.Equal(SignaturePipeline.GetMachineCode(beleg), machineCode);
        Assert.Contains("_10,00_", machineCode, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetMachineCodeFromCompactJws_MatchesSignedPayload()
    {
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, NullLogger<SignaturePipeline>.Instance);
        var payload = BelegdatenPayloadBuilder.Build(
            "KASSE-001",
            "AT-KASSE-001-20260115-42",
            new DateTime(2026, 1, 15, 14, 30, 0, DateTimeKind.Utc),
            new RksvTaxSetAmounts { Normal = 100.00m },
            10000,
            null,
            keyProvider.GetCertificateSerialNumber()!,
            DevAesKey);
        var jws = pipeline.Sign(payload, "extract-test");

        Assert.True(SignaturePipeline.TryGetMachineCodeFromCompactJws(jws, out var machineCode));
        Assert.Equal(SignaturePipeline.GetMachineCode(payload), machineCode);
    }
}
