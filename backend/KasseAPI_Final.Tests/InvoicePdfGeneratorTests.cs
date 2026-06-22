using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Billing;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class InvoicePdfGeneratorTests
{
    private readonly InvoicePdfGenerator _generator = new();

    [Fact]
    public void Generate_ProducesValidPdfWithDisclaimer()
    {
        var sale = CreateSale();
        var pdf = _generator.Generate(new LicenseSaleInvoiceDocument(
            Sale: sale,
            TenantName: "Cafe Demo",
            TenantSlug: "cafe",
            TenantAddress: "Hauptstraße 1, 1010 Wien",
            TenantVatId: "ATU12345678",
            TenantEmail: "billing@cafe.test",
            Seller: new CompanyProfileOptions
            {
                CompanyName = "Regkasse Platform GmbH",
                TaxNumber = "ATU99999999",
                Street = "Platformweg 1",
                ZipCode = "1010",
                City = "Wien",
                Country = "AT",
                Email = "billing@regkasse.at",
                Website = "https://regkasse.at",
            }));

        Assert.NotEmpty(pdf);
        Assert.Equal(0x25, pdf[0]); // %
        Assert.Equal(0x50, pdf[1]); // P
        Assert.Equal(0x44, pdf[2]); // D
        Assert.Equal(0x46, pdf[3]); // F
    }

    [Fact]
    public void TryLoadLogoBytes_ReturnsNullForMissingRelativePath()
    {
        var bytes = InvoicePdfGenerator.TryLoadLogoBytes("missing/logo.png", Path.GetTempPath());
        Assert.Null(bytes);
    }

    [Fact]
    public void NonRksvDisclaimer_IsExplicit()
    {
        Assert.Contains("Nicht RKSV-belegt", InvoicePdfGenerator.NonRksvDisclaimer, StringComparison.Ordinal);
    }

    private static LicenseSale CreateSale() =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LicenseKey = "REGK-20261231-cafe-A7F3K2D9",
            LicensePlan = LicenseSalePlans.TwelveMonths,
            ValidFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ValidUntilUtc = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            PriceNet = 100m,
            VatRate = 20m,
            VatAmount = 20m,
            PriceGross = 120m,
            Currency = "EUR",
            SoldAtUtc = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            SoldByUserId = Guid.NewGuid(),
            InvoiceNumber = "RE2026081",
            Status = LicenseSaleStatuses.Active,
        };
}
