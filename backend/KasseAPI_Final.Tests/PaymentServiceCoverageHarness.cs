using System.Security.Claims;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Shared in-memory PaymentService factory for license, core create, benefit, and voucher-storno coverage.
/// </summary>
internal static class PaymentServiceCoverageHarness
{
    public const string CashierId = "cashier1";

    public const string FiskalyMachineCode =
        "_R1-AT1_KASSE-01_1_2026-08-16T12:00:00_10,00_0,00_0,00_0,00_0,00_abc_123_0_sig";

    public sealed class Options
    {
        public string UserId { get; init; } = CashierId;
        public string Role { get; init; } = "Cashier";
        public TseOptions? Tse { get; init; }
        public IHostEnvironment? Host { get; init; }
        public ILicenseService? License { get; init; }
        public LicenseOptions? LicenseOptions { get; init; }
        public INtpTimeSyncStatus? Ntp { get; init; }
        public NtpSettings? NtpSettings { get; init; }
        public ICardPaymentService? Card { get; init; }
        public Mock<IAuditLogService>? Audit { get; init; }
        public Mock<ITseService>? TseMock { get; init; }
        public Mock<IFinanzOnlineService>? Finanz { get; init; }
        public IFinanzOnlineMetrics? FinanzMetrics { get; init; }
        public ILogger<PaymentService>? Logger { get; init; }
        public Mock<IReceiptSequenceService>? ReceiptSeq { get; init; }
        public InventoryOptions? Inventory { get; init; }
        public bool DemoUser { get; init; }
        public KasseAPI_Final.Services.FeatureFlags.IFeatureFlagService? FeatureFlags { get; init; }
        public CompanyProfileOptions? CompanyProfile { get; init; }
    }

    public static AppDbContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"PayCore_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    public static Mock<ITseService> CreateTseMock()
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
            .ReturnsAsync(new TseSignatureResult(
                FiskalyMachineCode,
                "prev-fiskaly",
                CertificateThumbprint: "fiskaly-thumb",
                FiskalyQrCodeData: FiskalyMachineCode,
                SigningProvider: "Fiskaly"));
        tse.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "fiskaly-cert" });
        tse.Setup(x => x.GetDeviceStatusAsync())
            .ReturnsAsync(new TseStatus { IsConnected = true, IsReady = true, Status = "Connected" });
        return tse;
    }

    public static Mock<ILicenseService> CreateLicenseService(LicenseStatusResponse deployment)
    {
        var mock = new Mock<ILicenseService>(MockBehavior.Loose);
        mock.Setup(x => x.GetCurrentDeploymentStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(deployment);
        return mock;
    }

    public static LicenseStatusResponse ValidDeployment(int daysRemaining = 30) =>
        new(
            IsValid: true,
            IsTrial: false,
            IsExpired: false,
            DaysRemaining: daysRemaining,
            ExpiryDate: DateTime.UtcNow.AddDays(daysRemaining),
            MachineHash: "test-hash");

    public static LicenseStatusResponse ExpiredDeployment(int daysAgo, bool isTrial = false) =>
        new(
            IsValid: false,
            IsTrial: isTrial,
            IsExpired: true,
            DaysRemaining: -daysAgo,
            ExpiryDate: DateTime.UtcNow.AddDays(-daysAgo),
            MachineHash: "test-hash");

    public static PaymentService CreatePaymentService(AppDbContext context, Options? options = null)
    {
        options ??= new Options();
        var tseMock = options.TseMock ?? CreateTseMock();
        var audit = options.Audit ?? CreateAuditMock();

        var paymentRepo = new GenericRepository<PaymentDetails>(context, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(context, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(context, Mock.Of<ILogger<GenericRepository<Customer>>>());

        var finanzMock = options.Finanz ?? new Mock<IFinanzOnlineService>();
        if (options.Finanz == null)
        {
            finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
                .ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true, Status = "Submitted" });
        }

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser
            {
                Id = options.UserId,
                UserName = options.UserId,
                Role = options.Role,
                IsDemo = options.DemoUser
            });

        var companyProfile = options.CompanyProfile ?? new CompanyProfileOptions
        {
            CompanyName = "Test GmbH",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var receiptSeqMock = options.ReceiptSeq ?? new Mock<IReceiptSequenceService>();
        if (options.ReceiptSeq == null)
        {
            var seq = 0;
            receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                    It.IsAny<IDbContextTransaction>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) =>
                    $"AT-{reg}-{d:yyyyMMdd}-{++seq}");
        }

        var receiptService = new ReceiptService(
            context,
            Mock.Of<ILogger<ReceiptService>>(),
            tseMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            userMock.Object,
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.ProductionHostEnvironment);

        var cashRegResolver = new CashRegisterResolutionService(
            context,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, options.UserId),
            new Claim(ClaimTypes.Role, options.Role),
        };
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(http);

        var tseOptions = options.Tse ?? new TseOptions { TseMode = "Demo" };
        var ntpSettings = options.NtpSettings ?? new NtpSettings { Enabled = false };

        return new PaymentService(
            context,
            paymentRepo,
            productRepo,
            customerRepo,
            tseMock.Object,
            finanzMock.Object,
            userMock.Object,
            new NoOpProductModifierValidationService(),
            receiptSeqMock.Object,
            receiptService,
            audit.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Microsoft.Extensions.Options.Options.Create(tseOptions),
            Microsoft.Extensions.Options.Options.Create(options.Inventory ?? new InventoryOptions { EnforceStockOnSales = false }),
            options.Logger ?? Mock.Of<ILogger<PaymentService>>(),
            cashRegResolver,
            accessor.Object,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver,
            finanzOnlineMetrics: options.FinanzMetrics,
            ntpSettings: Microsoft.Extensions.Options.Options.Create(ntpSettings),
            ntpTimeSyncStatus: options.Ntp,
            hostEnvironment: options.Host,
            licenseService: options.License,
            licenseOptions: options.LicenseOptions == null
                ? null
                : Microsoft.Extensions.Options.Options.Create(options.LicenseOptions),
            cardPaymentService: options.Card,
            featureFlags: options.FeatureFlags);
    }

    public static Mock<IAuditLogService> CreateAuditMock()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        audit.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    public static async Task<(Guid CustomerId, Guid ProductId, Guid CashRegisterId, Guid CategoryId)> SeedCatalogAsync(
        AppDbContext context,
        decimal unitPrice = 10m,
        decimal customerDiscountPercentage = 0m)
    {
        TenantTestDoubles.EnsurePlatformTenant(context);
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        context.Categories.Add(new Category
        {
            TenantId = SystemTenantIds.Platform,
            Id = categoryId,
            Name = "Speisen",
            VatRate = 10m
        });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = SystemTenantIds.Platform,
            Name = "Schnitzel",
            Price = unitPrice,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 100,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            TaxRate = TaxTypes.GetTaxRate(2),
            Barcode = $"t-{productId:N}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        context.Customers.Add(new Customer
        {
            Id = customerId,
            Name = "Gast",
            Email = "g@t.com",
            Phone = "1",
            IsActive = true,
            DiscountPercentage = customerDiscountPercentage
        });
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = SystemTenantIds.Platform,
            Id = cashRegisterId,
            RegisterNumber = "KASSE-01",
            Location = "Wien",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();
        return (customerId, productId, cashRegisterId, categoryId);
    }

    public static async Task<Guid> AddProductAsync(
        AppDbContext context,
        Guid categoryId,
        string name,
        decimal price,
        int taxType)
    {
        var productId = Guid.NewGuid();
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = SystemTenantIds.Platform,
            Name = name,
            Price = price,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 100,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = taxType,
            TaxRate = TaxTypes.GetTaxRate(taxType),
            Barcode = $"t-{productId:N}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        await context.SaveChangesAsync();
        return productId;
    }

    public static async Task SetTenantLicenseValidUntilAsync(AppDbContext context, DateTime? validUntilUtc)
    {
        var tenant = await context.Tenants.FirstAsync(t => t.Id == SystemTenantIds.Platform);
        tenant.LicenseValidUntilUtc = validUntilUtc;
        await context.SaveChangesAsync();
    }

    public static CreatePaymentRequest SaleRequest(
        Guid customerId,
        Guid productId,
        Guid registerId,
        decimal total = 10m,
        string method = "cash",
        int quantity = 1,
        string? idempotencyKey = null,
        Guid? cardPaymentIntentId = null) =>
        new()
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = total,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            Payment = new PaymentMethodRequest
            {
                Method = method,
                TseRequired = true,
                CardPaymentIntentId = cardPaymentIntentId
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = quantity, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString("N")
        };

    public static Options EnforcedLicenseOptions(ILicenseService license, string role = "Cashier") =>
        new()
        {
            Role = role,
            License = license,
            LicenseOptions = new LicenseOptions { Enabled = true },
            Tse = new TseOptions { TseMode = "Device" },
            Host = TenantTestDoubles.ProductionHostEnvironment
        };
}
