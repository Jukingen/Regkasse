using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class ReceiptServiceGenerateTests
{
    private static ReceiptService CreateService(AppDbContext db, CompanyProfileOptions? profile = null)
    {
        var tse = new Mock<ITseService>();
        tse.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert-1" });

        profile ??= new CompanyProfileOptions
        {
            CompanyName = "Live GmbH",
            TaxNumber = "ATU99999999",
            Street = "Live Str",
            ZipCode = "1020",
            City = "Wien",
            FooterText = "Live footer",
        };

        return new ReceiptService(
            db,
            NullLogger<ReceiptService>.Instance,
            tse.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(profile),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver);
    }

    [Fact]
    public async Task GenerateReceiptAsync_UsesPaymentCompanySnapshot_NotLiveSettings()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptGen_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsureDefaultTenant(db);

        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CompanyName = "Live GmbH",
            CompanyAddress = "Live Str, 1020 Wien",
            CompanyTaxNumber = "ATU99999999",
            CompanyDescription = "Live footer",
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

        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            TableNumber = 1,
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CompanyName = "Snapshot GmbH",
            CompanyAddress = "Snapshot Gasse 1, 1010 Wien",
            CashRegisterId = regId,
            TseSignature = "eyJ.eyJ.sign",
            ReceiptNumber = "AT-KASSE-01-20260612-1",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var service = CreateService(db);
        var dto = await service.GenerateReceiptAsync(payment);

        Assert.Equal("Snapshot GmbH", dto.Company.Name);
        Assert.Equal("Snapshot Gasse 1, 1010 Wien", dto.Company.Address);
        Assert.Equal("ATU12345678", dto.Company.TaxNumber);
        Assert.Equal("Live footer", dto.FooterText);
        Assert.Equal(payment.ReceiptNumber, dto.ReceiptNumber);
        Assert.Equal(payment.TseSignature, dto.Signature?.SignatureValue);
    }
}
