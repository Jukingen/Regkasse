using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public class InvoiceServiceGenerateTests
{
    private static InvoiceService CreateService(AppDbContext db) => new(
        db,
        TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions
        {
            CompanyName = "Live GmbH",
            TaxNumber = "ATU99999999",
            Street = "Live Str",
            ZipCode = "1020",
            City = "Wien",
        }),
        TenantTestDoubles.PrimaryTenantResolver);

    [Fact]
    public async Task GenerateInvoiceAsync_UsesPaymentCompanySnapshot()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"InvoiceGen_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsureDefaultTenant(db);

        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CompanyName = "Live GmbH",
            CompanyAddress = "Live Str, 1020 Wien",
            CompanyTaxNumber = "ATU99999999",
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
            TotalAmount = 12m,
            TaxAmount = 2m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CompanyName = "Snapshot GmbH",
            CompanyAddress = "Snapshot Gasse 2, 1010 Wien",
            CashRegisterId = regId,
            TseSignature = "eyJ.eyJ.sign",
            ReceiptNumber = "AT-KASSE-01-20260612-9",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var dto = await CreateService(db).GenerateInvoiceAsync(payment);

        Assert.Equal("Snapshot GmbH", dto.SellerName);
        Assert.Equal("Snapshot Gasse 2, 1010 Wien", dto.SellerAddress);
        Assert.Equal("ATU12345678", dto.SellerTaxNumber);
        Assert.Equal(payment.Id, dto.PaymentId);
        Assert.Equal("DerivedFromPayment", dto.DataProvenance);
    }

    [Fact]
    public async Task GenerateInvoiceAsync_PrefersPersistedInvoiceRow()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"InvoiceGen_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsureDefaultTenant(db);

        var paymentId = Guid.NewGuid();
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
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            SourcePaymentId = paymentId,
            InvoiceNumber = "INV-1",
            InvoiceDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow,
            Status = InvoiceStatus.Paid,
            Subtotal = 8m,
            TaxAmount = 2m,
            TotalAmount = 10m,
            PaidAmount = 10m,
            RemainingAmount = 0,
            CompanyName = "Persisted GmbH",
            CompanyTaxNumber = "ATU11111111",
            CompanyAddress = "Persisted 1",
            TseSignature = "sig",
            KassenId = "KASSE-01",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = regId,
            TaxDetails = JsonDocument.Parse("{}"),
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var payment = new PaymentDetails
        {
            Id = paymentId,
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 10m,
            TaxAmount = 2m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CompanyName = "Snapshot GmbH",
            CashRegisterId = regId,
            TseSignature = "sig",
            ReceiptNumber = "INV-1",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var dto = await CreateService(db).GenerateInvoiceAsync(payment);

        Assert.Equal("Persisted GmbH", dto.SellerName);
        Assert.Equal("ATU11111111", dto.SellerTaxNumber);
        Assert.Equal("Persisted", dto.DataProvenance);
    }
}
