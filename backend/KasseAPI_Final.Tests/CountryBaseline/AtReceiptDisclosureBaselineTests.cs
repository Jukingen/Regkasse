using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.CountryBaseline;

/// <summary>
/// Freezes the mandatory disclosures printed on an Austrian receipt (§11 UStG / RKSV §8):
/// seller identity and UID, Belegnummer, issue date, totals, per-rate VAT breakdown and the
/// RKSV signature block. Captured before any country layer exists.
/// </summary>
public sealed class AtReceiptDisclosureBaselineTests
{
    private const string FixtureFileName = "at-receipt-disclosures.baseline.json";

    private static readonly DateTime IssuedAtUtc = new(2026, 1, 12, 8, 15, 0, DateTimeKind.Utc);
    private static readonly Guid CashRegisterId = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task AtReceiptDisclosures_MatchCommittedBaseline()
    {
        var dto = await GenerateReceiptAsync();

        BaselineFixtureFile.AssertMatchesFixture(FixtureFileName, Normalize(dto));
    }

    [Fact]
    public async Task AtReceiptDisclosures_AreReproducibleAcrossRuns()
    {
        var first = BaselineFixtureFile.Serialize(Normalize(await GenerateReceiptAsync()));
        var second = BaselineFixtureFile.Serialize(Normalize(await GenerateReceiptAsync()));

        Assert.Equal(first, second);
    }

    private static async Task<ReceiptDTO> GenerateReceiptAsync()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AtReceiptDisclosureBaseline_{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
            CompanyName = "Baseline Gastro GmbH",
            CompanyAddress = "Baselinegasse 1, 1010 Wien",
            CompanyTaxNumber = "ATU12345678",
            CompanyDescription = "Baseline Betrieb",
            BusinessHours = new Dictionary<string, string>(),
            Currency = "EUR",
            Language = "de-DE",
            TimeZone = "Europe/Vienna",
            DateFormat = "dd.MM.yyyy",
            TimeFormat = "HH:mm:ss",
            TaxCalculationMethod = "Standard",
            InvoiceNumbering = "Sequential",
            ReceiptNumbering = "Sequential",
            DefaultPaymentMethod = "Cash",
        });

        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = CashRegisterId,
            RegisterNumber = "KASSE-BASELINE-01",
            Location = "Filiale Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = IssuedAtUtc,
            Status = RegisterStatus.Open,
            CreatedAt = IssuedAtUtc,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var standardLine = CartMoneyHelper.ComputeLine(2.50m, 2, 20m);
        var reducedLine = CartMoneyHelper.ComputeLine(6.90m, 1, 10m);

        var items = new List<PaymentItem>
        {
            new()
            {
                ProductId = new Guid("22222222-2222-2222-2222-222222222222"),
                ProductName = "Cola 0,33",
                Quantity = 2,
                UnitPrice = 2.50m,
                TotalPrice = standardLine.LineGross,
                LineNet = standardLine.LineNet,
                TaxAmount = standardLine.LineTax,
                TaxType = TaxTypes.Standard,
                TaxRate = 0.20m,
            },
            new()
            {
                ProductId = new Guid("33333333-3333-3333-3333-333333333333"),
                ProductName = "Döner Teller",
                Quantity = 1,
                UnitPrice = 6.90m,
                TotalPrice = reducedLine.LineGross,
                LineNet = reducedLine.LineNet,
                TaxAmount = reducedLine.LineTax,
                TaxType = TaxTypes.Reduced,
                TaxRate = 0.10m,
            },
        };

        var payment = new PaymentDetails
        {
            Id = new Guid("44444444-4444-4444-4444-444444444444"),
            CustomerId = new Guid("55555555-5555-5555-5555-555555555555"),
            CustomerName = "Guest",
            TableNumber = 3,
            CashierId = "cashier-baseline",
            TotalAmount = standardLine.LineGross + reducedLine.LineGross,
            TaxAmount = standardLine.LineTax + reducedLine.LineTax,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CompanyName = "Baseline Gastro GmbH",
            CompanyAddress = "Baselinegasse 1, 1010 Wien",
            CashRegisterId = CashRegisterId,
            TseSignature = "eyJhbGciOiJFUzI1NiJ9.X1IxLUFUMV9CQVNFTElORQ.QkFTRUxJTkVfU0lHTkFUVVJF",
            PrevSignatureValueUsed = "PREV-BASELINE-SIGNATURE",
            ReceiptNumber = "AT-KASSE-BASELINE-01-20260112-2",
            PaymentItems = JsonDocument.Parse(JsonSerializer.Serialize(items)),
            TaxDetails = JsonDocument.Parse(
                $$"""
                  {"{{TaxTypes.Standard}}":{{standardLine.LineTax.ToString(CultureInfo.InvariantCulture)}},
                   "{{TaxTypes.Reduced}}":{{reducedLine.LineTax.ToString(CultureInfo.InvariantCulture)}}}
                  """),
            CreatedAt = IssuedAtUtc,
            IsActive = true,
        };

        return await CreateService(db).GenerateReceiptAsync(payment);
    }

    private static ReceiptService CreateService(AppDbContext db)
    {
        var tse = new Mock<ITseService>();
        tse.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "BASELINE-AT-CERT-0001" });

        var profile = new CompanyProfileOptions
        {
            CompanyName = "Baseline Gastro GmbH",
            TaxNumber = "ATU12345678",
            Street = "Baselinegasse 1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = "Baseline Betrieb",
        };

        return new ReceiptService(
            db,
            NullLogger<ReceiptService>.Instance,
            tse.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(profile),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.ProductionHostEnvironment);
    }

    /// <summary>
    /// Receipt/item ids and persistence timestamps are generated per run; everything a tax auditor
    /// reads off the printed receipt stays comparable.
    /// </summary>
    private static JsonNode Normalize(ReceiptDTO dto)
    {
        var node = JsonSerializer.SerializeToNode(
            dto,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!;

        Redact(node);
        return node;
    }

    private static void Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (IsVolatile(property.Key, property.Value))
                        obj[property.Key] = "<volatile>";
                    else
                        Redact(property.Value);
                }

                break;

            case JsonArray array:
                foreach (var element in array)
                    Redact(element);

                break;
        }
    }

    private static bool IsVolatile(string propertyName, JsonNode? value)
    {
        if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var text))
            return false;

        if (Guid.TryParse(text, out _))
            return true;

        return propertyName.EndsWith("PersistedAtUtc", StringComparison.Ordinal);
    }
}
