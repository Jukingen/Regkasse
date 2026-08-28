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
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptGen_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
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
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "Filiale Wien",
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
        Assert.Equal(ReceiptThankYouMessage.Default, dto.FooterText);
        Assert.Equal(ReceiptThankYouMessage.Default, dto.ThankYouMessage);
        Assert.Equal("Live footer", dto.Company.Description);
        Assert.Equal("KASSE-01", dto.KassenID);
        Assert.Equal("KASSE-01", dto.DisplayRegisterNumber);
        Assert.Equal("Filiale Wien", dto.BranchName);
        Assert.Equal("RKSV-konform", dto.RksvFooterLabel);
        Assert.False(dto.ShowDemoLabel);
        Assert.Equal(payment.ReceiptNumber, dto.ReceiptNumber);
        Assert.Equal(payment.TseSignature, dto.Signature?.SignatureValue);
    }

    [Fact]
    public async Task GenerateReceiptAsync_UsesCompanySettings_WhenPaymentSnapshotMissing()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptCompanyDb_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
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
            TenantId = SystemTenantIds.Platform,
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
        Assert.Equal(ReceiptThankYouMessage.Default, dto.FooterText);
        Assert.Equal(ReceiptThankYouMessage.Default, dto.ThankYouMessage);
        Assert.Equal("Willkommen", dto.Company.Description);
        Assert.Equal("KASSE-DB", dto.KassenID);
    }

    [Fact]
    public void GetRksvFooter_ReturnsDemoLabel_InDevelopment()
    {
        var host = TenantTestDoubles.HostEnvironmentReturning(Environments.Development);
        var service = new ReceiptService(
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase($"ReceiptRksvFooter_{Guid.NewGuid()}")
                    .Options,
                TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform)),
            NullLogger<ReceiptService>.Instance,
            Mock.Of<ITseService>(),
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions()),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver,
            host);

        var result = service.GetRksvFooter(host);

        Assert.Equal("DEMO / NICHT FISKAL", result);
    }

    [Fact]
    public void GetRksvFooter_ReturnsProductionLabel_InProduction()
    {
        var service = CreateService(new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"ReceiptRksvFooter_{Guid.NewGuid()}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform)));

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
                TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform)),
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
    public void GetRksvFooter_HidesDemoLabel_WhenShowDemoLabelFalseInDevelopment()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RKSV:Mode"] = "Production",
                ["RKSV:ShowDemoLabel"] = "false"
            })
            .Build();
        var host = TenantTestDoubles.HostEnvironmentReturning(Environments.Development);
        var rksvEnv = new RksvEnvironmentService(config, host);
        var service = new ReceiptService(
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase($"ReceiptRksvFooterHideDemo_{Guid.NewGuid()}")
                    .Options,
                TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform)),
            NullLogger<ReceiptService>.Instance,
            Mock.Of<ITseService>(),
            TenantTestDoubles.CompanyProfileProviderReturning(new CompanyProfileOptions()),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver,
            host,
            rksvEnv,
            config);

        var result = service.GetRksvFooter(host);

        Assert.Equal("RKSV-konform", result);
    }

    [Fact]
    public void GetTseSignatureDisplay_ReturnsUnavailable_WhenMissing()
    {
        var service = CreateService(new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"ReceiptTse_{Guid.NewGuid()}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform)));

        var result = service.GetTseSignatureDisplay(new PaymentDetails { TseSignature = null });

        Assert.Equal("TSE-Signatur: nicht verfügbar", result);
    }

    [Fact]
    public void GetTseSignatureDisplay_ReturnsFullSignature_WithoutTruncation()
    {
        var service = CreateService(new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"ReceiptTse_{Guid.NewGuid()}")
                .Options,
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform)));

        var longSig = new string('a', 60);
        var result = service.GetTseSignatureDisplay(new PaymentDetails { TseSignature = longSig });

        Assert.StartsWith("TSE-Signatur:", result);
        Assert.Contains(longSig, result);
        Assert.DoesNotContain("...", result);
    }

    [Fact]
    public async Task MapToDto_UsesCashierUserName_WhenNameEmpty()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptCashier_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptCashierEmail_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
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

    [Fact]
    public async Task GenerateReceiptAsync_PrefersThankYouMessage_OverCompanyDescription()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptThanks_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
            CompanyName = "Cafe",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            CompanyDescription = "Legacy footer",
            ThankYouMessage = "Danke und auf Wiedersehen!",
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
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "KASSE-THX",
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
            ReceiptNumber = "AT-KASSE-THX-1",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var dto = await CreateService(db).GenerateReceiptAsync(payment);

        Assert.Equal("Danke und auf Wiedersehen!", dto.FooterText);
        Assert.Equal("Danke und auf Wiedersehen!", dto.ThankYouMessage);
        Assert.Equal("Legacy footer", dto.Company.Description);
    }

    [Fact]
    public async Task GenerateReceiptAsync_UsesDefaultThankYou_WhenFooterUnset()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptThanksDefault_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        db.CompanySettings.Add(new CompanySettings
        {
            TenantId = SystemTenantIds.Platform,
            CompanyName = "Cafe",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
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
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "KASSE-DEF",
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
            ReceiptNumber = "AT-KASSE-DEF-1",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var dto = await CreateService(db).GenerateReceiptAsync(payment);

        Assert.Equal(ReceiptThankYouMessage.Default, dto.FooterText);
        Assert.Equal(ReceiptThankYouMessage.Default, dto.ThankYouMessage);
    }

    [Theory]
    [InlineData(13.64, 15.00, 1.36, 13.64)]
    [InlineData(0, 15.00, 1.36, 13.64)]
    [InlineData(0, 0, 0, 0)]
    public void ResolveNetTotal_UsesStoredNet_OrGrossMinusTax(
        decimal subTotal,
        decimal grandTotal,
        decimal taxTotal,
        decimal expected)
    {
        Assert.Equal(expected, ReceiptService.ResolveNetTotal(subTotal, grandTotal, taxTotal));
    }

    [Fact]
    public async Task GenerateReceiptAsync_ComputesNetFromGrossMinusTax_WhenLineNetMissing()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptNet_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "KASSE-NET",
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
            TotalAmount = 15m,
            TaxAmount = 1.36m,
            PaymentMethodRaw = "0",
            CashRegisterId = regId,
            TseSignature = "eyJ.eyJ.sign",
            ReceiptNumber = "AT-KASSE-NET-1",
            PaymentItems = JsonDocument.Parse(
                """[{"ProductId":"00000000-0000-0000-0000-000000000001","ProductName":"Schnitzel","Quantity":1,"UnitPrice":15,"TotalPrice":15,"TaxType":2,"TaxRate":0.1,"TaxAmount":1.36,"LineNet":0}]"""),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        var dto = await CreateService(db).GenerateReceiptAsync(payment);

        Assert.Equal(13.64m, dto.SubTotal);
        Assert.Equal(13.64m, dto.NetTotal);
        Assert.Equal(13.64m, dto.Totals?.TotalNet);
        Assert.Equal(1.36m, dto.TaxAmount);
        Assert.Equal(15.00m, dto.GrandTotal);
    }

    [Fact]
    public async Task GetReceiptByPaymentIdAsync_ComputesNetFromGrossMinusTax_WhenStoredSubTotalIsZero()
    {
        var tenantAccessor = TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"ReceiptNetStored_{Guid.NewGuid()}")
            .Options;
        await using var db = new AppDbContext(options, tenantAccessor);
        TenantTestDoubles.EnsurePlatformTenant(db);

        var regId = Guid.NewGuid();
        db.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = regId,
            RegisterNumber = "KASSE-NET2",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        var payment = new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Guest",
            CashierId = "cashier-1",
            TotalAmount = 15m,
            TaxAmount = 1.36m,
            PaymentMethodRaw = "0",
            CashRegisterId = regId,
            TseSignature = "eyJ.eyJ.sign",
            ReceiptNumber = "AT-KASSE-NET2-1",
            PaymentItems = JsonDocument.Parse("[]"),
            TaxDetails = JsonDocument.Parse("{}"),
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        };
        db.PaymentDetails.Add(payment);
        db.Receipts.Add(new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            PaymentId = payment.Id,
            ReceiptNumber = payment.ReceiptNumber!,
            IssuedAt = DateTime.UtcNow,
            CashierId = payment.CashierId,
            CashRegisterId = regId,
            SubTotal = 0,
            TaxTotal = 1.36m,
            GrandTotal = 15.00m,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var dto = await CreateService(db).GetReceiptByPaymentIdAsync(payment.Id);

        Assert.NotNull(dto);
        Assert.Equal(13.64m, dto.SubTotal);
        Assert.Equal(13.64m, dto.NetTotal);
        Assert.Equal(13.64m, dto.Totals?.TotalNet);
        Assert.Equal(1.36m, dto.TaxAmount);
        Assert.Equal(15.00m, dto.GrandTotal);
    }
}
