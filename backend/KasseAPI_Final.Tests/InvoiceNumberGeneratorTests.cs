using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class InvoiceNumberGeneratorTests
{
    [Fact]
    public void FormatInvoiceNumber_MatchesDocumentedExample()
    {
        Assert.Equal("RE20260841", InvoiceNumberGenerator.FormatInvoiceNumber(2026, 8, 41));
    }

    [Fact]
    public void FormatInvoiceNumber_StartsSequenceWithoutPadding()
    {
        Assert.Equal("RE2026081", InvoiceNumberGenerator.FormatInvoiceNumber(2026, 8, 1));
    }

    [Theory]
    [InlineData("RE20260841", "RE202608", 41, true)]
    [InlineData("RE2026081", "RE202608", 1, true)]
    [InlineData("RE2026079", "RE202608", 0, false)]
    [InlineData("INV-20260841", "RE202608", 0, false)]
    public void TryParseSequence_ParsesSuffix(string invoiceNumber, string prefix, int expected, bool success)
    {
        var parsed = InvoiceNumberGenerator.TryParseSequence(invoiceNumber, prefix, out var sequence);

        Assert.Equal(success, parsed);
        if (success)
            Assert.Equal(expected, sequence);
    }

    [Fact]
    public void GenerateInvoiceNumber_ReturnsFirstSequenceForMonth()
    {
        using var db = CreateDb();
        var generator = new InvoiceNumberGenerator(db);

        var invoiceNumber = generator.GenerateInvoiceNumber(new DateTime(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("RE2026081", invoiceNumber);
    }

    [Fact]
    public void GenerateInvoiceNumber_IncrementsWithinSameMonth()
    {
        using var db = CreateDb();
        db.LicenseSales.Add(CreateSale("RE20260841"));
        db.LicenseSales.Add(CreateSale("RE2026087"));
        db.SaveChanges();

        var generator = new InvoiceNumberGenerator(db);
        var invoiceNumber = generator.GenerateInvoiceNumber(new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("RE20260842", invoiceNumber);
    }

    [Fact]
    public void GenerateInvoiceNumber_ResetsSequenceForNewMonth()
    {
        using var db = CreateDb();
        db.LicenseSales.Add(CreateSale("RE20260841"));
        db.SaveChanges();

        var generator = new InvoiceNumberGenerator(db);
        var invoiceNumber = generator.GenerateInvoiceNumber(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal("RE2026091", invoiceNumber);
    }

    [Theory]
    [InlineData("RE20260841", 2026, 8, 41)]
    [InlineData("RE2026081", 2026, 8, 1)]
    [InlineData("RE202612999", 2026, 12, 999)]
    public void ParseInvoiceNumber_ParsesValidNumbers(string invoiceNumber, int year, int month, int sequence)
    {
        using var db = CreateDb();
        var generator = new InvoiceNumberGenerator(db);

        var parsed = generator.ParseInvoiceNumber(invoiceNumber);

        Assert.Equal((year, month, sequence), parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("INV-20260841")]
    [InlineData("RE202607")]
    [InlineData("RE2026131")]
    public void ParseInvoiceNumber_ThrowsForInvalidNumbers(string invoiceNumber)
    {
        using var db = CreateDb();
        var generator = new InvoiceNumberGenerator(db);

        Assert.Throws<ArgumentException>(() => generator.ParseInvoiceNumber(invoiceNumber));
    }

    [Fact]
    public void ParseInvoiceNumber_RoundTripsWithFormat()
    {
        var formatted = InvoiceNumberGenerator.FormatInvoiceNumber(2026, 8, 41);
        using var db = CreateDb();
        var generator = new InvoiceNumberGenerator(db);

        var (year, month, sequence) = generator.ParseInvoiceNumber(formatted);

        Assert.Equal(2026, year);
        Assert.Equal(8, month);
        Assert.Equal(41, sequence);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"InvoiceNumberGen_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static LicenseSale CreateSale(string invoiceNumber) =>
        new()
        {
            TenantId = Guid.NewGuid(),
            LicenseKey = "REGK-20261231-dev-A7F3K2D9",
            LicensePlan = LicenseSalePlans.TwelveMonths,
            ValidFromUtc = DateTime.UtcNow,
            ValidUntilUtc = DateTime.UtcNow.AddYears(1),
            PriceNet = 100m,
            VatRate = 20m,
            VatAmount = 20m,
            PriceGross = 120m,
            SoldAtUtc = DateTime.UtcNow,
            SoldByUserId = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber,
        };
}
