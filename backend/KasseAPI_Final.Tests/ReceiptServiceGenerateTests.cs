using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
            TenantTestDoubles.PrimaryTenantResolver, TenantTestDoubles.ProductionHostEnvironment);
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
        Assert.Equal("RKSV-konform", dto.RksvFooterLabel);
        Assert.Equal(payment.ReceiptNumber, dto.ReceiptNumber);
        Assert.Equal(payment.TseSignature, dto.Signature?.SignatureValue);
    }

    [Fact]
    public async Task GenerateReceiptAsync_UsesCompanySettings_WhenPaymentSnapshotMissing()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptCompanyDb_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsureDefaultTenant(db);

        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CompanyName = "DB Firma GmbH",
            CompanyAddress = "DB Gasse 9, 4020 Linz",
            CompanyTaxNumber = "ATU11111111",
            CompanyDescription = "Willkommen",
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
            RegisterNumber = "KASSE-DB",
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
            CashierId = "cashier-1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            CashRegisterId = regId,
            TseSignature = "eyJ.eyJ.sign",
            ReceiptNumber = "AT-KASSE-DB-20260712-1",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var dto = await CreateService(db).GenerateReceiptAsync(payment);

        Assert.Equal("DB Firma GmbH", dto.Company.Name);
        Assert.Equal("DB Gasse 9, 4020 Linz", dto.Company.Address);
        Assert.Equal("ATU11111111", dto.Company.TaxNumber);
        Assert.Equal("Willkommen", dto.FooterText);
    }

    [Fact]
    public void GetRksvFooter_ReturnsDemoLabel_InDevelopment()
    {
        var service = CreateService(new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"ReceiptRksvFooter_{Guid.NewGuid()}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)));

        var result = service.GetRksvFooter(TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        Assert.Equal("DEMO / NICHT FISKAL", result);
    }

    [Fact]
    public void GetRksvFooter_ReturnsProductionLabel_InProduction()
    {
        var service = CreateService(new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"ReceiptRksvFooter_{Guid.NewGuid()}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)));

        var result = service.GetRksvFooter(TenantTestDoubles.ProductionHostEnvironment);

        Assert.Equal("RKSV-konform", result);
    }

    [Fact]
    public void GetRksvFooter_ReturnsDemoLabel_WhenRksvModeDemoOnProductionHost()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" })
            .Build();
        var rksvEnv = new RksvEnvironmentService(config, TenantTestDoubles.ProductionHostEnvironment);
        var service = new ReceiptService(
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase($"ReceiptRksvFooterDemoMode_{Guid.NewGuid()}")
                    .Options,
                TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)),
            NullLogger<ReceiptService>.Instance,
            Mock.Of<ITseService>(),
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions()),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.ProductionHostEnvironment,
            rksvEnv,
            config);

        var result = service.GetRksvFooter(TenantTestDoubles.ProductionHostEnvironment);

        Assert.Equal("DEMO / NICHT FISKAL", result);
    }

    [Fact]
    public void GetTseSignatureDisplay_ReturnsUnavailable_WhenMissing()
    {
        var service = CreateService(new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"ReceiptTse_{Guid.NewGuid()}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)));

        var result = service.GetTseSignatureDisplay(new PaymentDetails { TseSignature = null });

        Assert.Equal("TSE-Signatur: nicht verfügbar", result);
    }

    [Fact]
    public void GetTseSignatureDisplay_ShortensLongSignature()
    {
        var service = CreateService(new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"ReceiptTse_{Guid.NewGuid()}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)));

        var longSig = new string('a', 60);
        var result = service.GetTseSignatureDisplay(new PaymentDetails { TseSignature = longSig });

        Assert.StartsWith("TSE-Signatur:", result);
        Assert.Contains(new string('a', 50) + "...", result);
    }

    [Fact]
    public async Task MapToDto_UsesCashierUserName_WhenNameEmpty()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptCashier_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsureDefaultTenant(db);

        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-02",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync("cashier-uuid"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = "cashier-uuid",
                UserName = "kassier1",
                FirstName = "",
                LastName = "",
            });

        var tse = new Mock<ITseService>();
        tse.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert-1" });

        var service = new ReceiptService(
            db,
            NullLogger<ReceiptService>.Instance,
            tse.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions()),
            userMock.Object,
            TenantTestDoubles.PrimaryTenantResolver, TenantTestDoubles.ProductionHostEnvironment);

        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            TableNumber = 1,
            CashierId = "cashier-uuid",
            TotalAmount = 5m,
            TaxAmount = 0.5m,
            PaymentMethodRaw = "0",
            CashRegisterId = regId,
            TseSignature = "eyJ.eyJ.sign",
            ReceiptNumber = "AT-KASSE-02-20260712-1",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var dto = await service.GenerateReceiptAsync(payment);

        Assert.Equal("kassier1", dto.CashierDisplayName);
    }

    [Fact]
    public async Task MapToDto_UsesCashierEmail_WhenUserNameEmpty()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptCashierEmail_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsureDefaultTenant(db);

        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-03",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync("cashier-uuid"))
            .ReturnsAsync(new ApplicationUser
            {
                Id = "cashier-uuid",
                UserName = null,
                Email = "kassier@example.com",
            });

        var tse = new Mock<ITseService>();
        tse.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert-1" });

        var service = new ReceiptService(
            db,
            NullLogger<ReceiptService>.Instance,
            tse.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions()),
            userMock.Object,
            TenantTestDoubles.PrimaryTenantResolver, TenantTestDoubles.ProductionHostEnvironment);

        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            TableNumber = 1,
            CashierId = "cashier-uuid",
            TotalAmount = 5m,
            TaxAmount = 0.5m,
            PaymentMethodRaw = "0",
            CashRegisterId = regId,
            TseSignature = "eyJ.eyJ.sign",
            ReceiptNumber = "AT-KASSE-03-20260712-1",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var dto = await service.GenerateReceiptAsync(payment);

        Assert.Equal("kassier@example.com", dto.CashierDisplayName);
    }
}
