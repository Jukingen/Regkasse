using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class VoucherPaymentRedemptionTests
{
    private const string PlainCode = "GUT-TEST-001";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"VoucherPay_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static PaymentService CreatePaymentService(AppDbContext context, string tseMode = "Demo")
    {
        var loggerPayment = new Mock<ILogger<PaymentService>>().Object;
        var loggerRepo = new Mock<ILogger<GenericRepository<PaymentDetails>>>().Object;
        var loggerProd = new Mock<ILogger<GenericRepository<Product>>>().Object;
        var loggerCust = new Mock<ILogger<GenericRepository<Customer>>>().Object;

        var paymentRepo = new GenericRepository<PaymentDetails>(context, loggerRepo);
        var productRepo = new GenericRepository<Product>(context, loggerProd);
        var customerRepo = new GenericRepository<Customer>(context, loggerCust);

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });
        if (string.Equals(tseMode, "Device", StringComparison.OrdinalIgnoreCase))
        {
            tseMock.Setup(x => x.GetDeviceStatusAsync())
                .ReturnsAsync(new TseStatus
                {
                    IsConnected = true,
                    IsReady = true,
                    Status = "Connected",
                    DeviceId = "dev1",
                });
        }

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>())).ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>())).ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", FirstName = "Test", LastName = "User", Role = "Cashier" });

        var companyProfile = new CompanyProfileOptions { CompanyName = "Test", TaxNumber = "ATU12345678", Street = "S1", ZipCode = "1010", City = "Wien", FooterText = "" };
        var tseOptions = new TseOptions { TseMode = tseMode };

        var modifierValidation = new NoOpProductModifierValidationService();

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var seqCallCount = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(It.IsAny<IDbContextTransaction>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");

        var loggerReceipt = new Mock<ILogger<ReceiptService>>().Object;
        var receiptService = new ReceiptService(context, loggerReceipt, tseMock.Object, Options.Create(companyProfile), Mock.Of<IUserService>(), TenantTestDoubles.PrimaryTenantResolver);

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(), It.IsAny<double?>())).ReturnsAsync(new AuditLog());
        var cashRegResolver = new CashRegisterResolutionService(context, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());
        var httpAccessor = Mock.Of<IHttpContextAccessor>();
        return new PaymentService(
            context,
            paymentRepo,
            productRepo,
            customerRepo,
            tseMock.Object,
            finanzMock.Object,
            userMock.Object,
            modifierValidation,
            receiptSeqMock.Object,
            receiptService,
            auditMock.Object,
            Options.Create(companyProfile),
            Options.Create(tseOptions),
            Options.Create(new InventoryOptions()),
            loggerPayment,
            cashRegResolver,
            httpAccessor,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);
    }

    private static async Task<(Guid categoryId, Guid productId, Guid customerId, Guid regId)> SeedSaleCatalogOnlyAsync(AppDbContext context)
    {
        TenantTestDoubles.EnsureDefaultTenant(context);
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var regId = Guid.NewGuid();

        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Soup",
            Price = 5.00m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
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
        context.Customers.Add(new Customer { Id = customerId, Name = "Walk", Email = "t@t.com", Phone = "1", IsActive = true });
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();
        return (categoryId, productId, customerId, regId);
    }

    private static async Task<(Guid categoryId, Guid productId, Guid customerId, Guid regId, Guid voucherId)> SeedCatalogAndVoucherAsync(
        AppDbContext context,
        decimal voucherBalance,
        DateTime expiresAtUtc,
        VoucherStatus status)
    {
        var (categoryId, productId, customerId, regId) = await SeedSaleCatalogOnlyAsync(context);
        var voucherId = Guid.NewGuid();

        var hash = VoucherCodeHasher.HashNormalized(VoucherCodeHasher.NormalizeCode(PlainCode));
        context.Vouchers.Add(new Voucher
        {
            Id = voucherId,
            TenantId = LegacyDefaultTenantIds.Primary,
            CodeHash = hash,
            MaskedCode = "****001",
            InitialAmount = 100m,
            RemainingAmount = voucherBalance,
            Currency = "EUR",
            Status = status,
            ValidFromUtc = DateTime.UtcNow.AddDays(-2),
            ExpiresAtUtc = expiresAtUtc,
            CreatedByUserId = "u1",
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return (categoryId, productId, customerId, regId, voucherId);
    }

    [Fact]
    public async Task VoucherService_Validate_ReturnsMaskedAndRemaining()
    {
        await using var context = CreateContext();
        await SeedCatalogAndVoucherAsync(context, 100m, DateTime.UtcNow.AddDays(10), VoucherStatus.Active);
        var svc = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var r = await svc.ValidateVoucherByCodeAsync(LegacyDefaultTenantIds.Primary, PlainCode, null);
        Assert.True(r.Ok);
        Assert.Equal(100m, r.RemainingAmount);
        Assert.Equal("****001", r.MaskedCode);
    }

    [Fact]
    public async Task CreatePayment_Voucher_Redeems_PartialBalance()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId, voucherId) = await SeedCatalogAndVoucherAsync(context, 100m, DateTime.UtcNow.AddDays(10), VoucherStatus.Active);
        var paymentService = CreatePaymentService(context);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
                VoucherCode = PlainCode
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.True(result.Success, result.Message + string.Join(";", result.Errors));

        var v = await context.Vouchers.AsNoTracking().FirstAsync(x => x.Id == voucherId);
        Assert.Equal(95.00m, v.RemainingAmount);
        Assert.Equal(VoucherStatus.PartiallyRedeemed, v.Status);

        var ledger = await context.VoucherLedgerEntries.AsNoTracking()
            .Where(l => l.PaymentId == result.Payment!.Id && l.Type == VoucherTransactionType.Redeem)
            .ToListAsync();
        Assert.Single(ledger);
        Assert.Equal(-5.00m, ledger[0].Amount);
        Assert.Equal(95.00m, ledger[0].BalanceAfter);
    }

    [Fact]
    public async Task CreatePayment_Voucher_InsufficientBalance_Fails()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId, _) = await SeedCatalogAndVoucherAsync(context, 2m, DateTime.UtcNow.AddDays(10), VoucherStatus.Active);
        var paymentService = CreatePaymentService(context);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest { Method = "voucher", TseRequired = false, VoucherCode = PlainCode },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.False(result.Success);
        Assert.Contains("balance", string.Join(" ", result.Errors).ToLowerInvariant());
    }

    [Fact]
    public async Task CreatePayment_Voucher_MultiRedemptions_SumsToTotal()
    {
        await using var context = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(context);
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();

        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Combo",
            Price = 10.00m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
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
        context.Customers.Add(new Customer { Id = customerId, Name = "Walk", Email = "t@t.com", Phone = "1", IsActive = true });
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });

        void AddV(Guid id, string plain, decimal rem)
        {
            var hash = VoucherCodeHasher.HashNormalized(VoucherCodeHasher.NormalizeCode(plain));
            context.Vouchers.Add(new Voucher
            {
                Id = id,
                TenantId = LegacyDefaultTenantIds.Primary,
                CodeHash = hash,
                MaskedCode = "****",
                InitialAmount = 100m,
                RemainingAmount = rem,
                Currency = "EUR",
                Status = VoucherStatus.Active,
                ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
                CreatedByUserId = "u1",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        AddV(v1, "MULTI-A", 7m);
        AddV(v2, "MULTI-B", 5m);
        await context.SaveChangesAsync();

        var paymentService = CreatePaymentService(context);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 10.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
                VoucherRedemptions = new List<VoucherRedemptionRequestItem>
                {
                    new() { Code = "multi-a", Amount = 7m },
                    new() { Code = "MULTI-B", Amount = 3m }
                }
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.True(result.Success, result.Message);

        var b1 = await context.Vouchers.AsNoTracking().FirstAsync(x => x.Id == v1);
        var b2 = await context.Vouchers.AsNoTracking().FirstAsync(x => x.Id == v2);
        Assert.Equal(0m, b1.RemainingAmount);
        Assert.Equal(2m, b2.RemainingAmount);

        var ledgers = await context.VoucherLedgerEntries.CountAsync(l => l.PaymentId == result.Payment!.Id && l.Type == VoucherTransactionType.Redeem);
        Assert.Equal(2, ledgers);
    }

    [Fact]
    public async Task CreatePayment_Voucher_Expired_Fails()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId, _) = await SeedCatalogAndVoucherAsync(context, 100m, DateTime.UtcNow.AddDays(-1), VoucherStatus.Active);
        var paymentService = CreatePaymentService(context);

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest { Method = "voucher", TseRequired = false, VoucherCode = PlainCode },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task VoucherService_Validate_WrongTenant_ReturnsNotFound()
    {
        await using var context = CreateContext();
        await SeedCatalogAndVoucherAsync(context, 50m, DateTime.UtcNow.AddDays(30), VoucherStatus.Active);
        var svc = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var otherTenant = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var r = await svc.ValidateVoucherByCodeAsync(otherTenant, PlainCode, null);
        Assert.False(r.Ok);
        Assert.Equal(VoucherValidateErrorCodes.NotFound, r.ErrorCode);
    }

    [Fact]
    public async Task VoucherService_Validate_ExpiredByClock_ReturnsExpired()
    {
        await using var context = CreateContext();
        await SeedCatalogAndVoucherAsync(context, 50m, DateTime.UtcNow.AddDays(-1), VoucherStatus.Active);
        var svc = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var r = await svc.ValidateVoucherByCodeAsync(LegacyDefaultTenantIds.Primary, PlainCode, null);
        Assert.False(r.Ok);
        Assert.Equal(VoucherValidateErrorCodes.Expired, r.ErrorCode);
    }

    [Fact]
    public async Task VoucherService_Validate_Cancelled_ReturnsNotFound()
    {
        await using var context = CreateContext();
        await SeedCatalogAndVoucherAsync(context, 50m, DateTime.UtcNow.AddDays(30), VoucherStatus.Cancelled);
        var svc = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var r = await svc.ValidateVoucherByCodeAsync(LegacyDefaultTenantIds.Primary, PlainCode, null);
        Assert.False(r.Ok);
        Assert.Equal(VoucherValidateErrorCodes.NotFound, r.ErrorCode);
        Assert.Equal("Voucher not found or not valid for this location.", r.Message);
    }

    [Fact]
    public async Task VoucherService_Validate_Redeemed_ReturnsNoBalance()
    {
        await using var context = CreateContext();
        await SeedCatalogAndVoucherAsync(context, 0m, DateTime.UtcNow.AddDays(30), VoucherStatus.Redeemed);
        var svc = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var r = await svc.ValidateVoucherByCodeAsync(LegacyDefaultTenantIds.Primary, PlainCode, null);
        Assert.False(r.Ok);
        Assert.Equal(VoucherValidateErrorCodes.NoBalance, r.ErrorCode);
        Assert.Equal("Voucher has no remaining balance.", r.Message);
    }

    [Fact]
    public async Task VoucherService_Validate_NoBalance_ReturnsNoBalance()
    {
        await using var context = CreateContext();
        await SeedCatalogAndVoucherAsync(context, 0m, DateTime.UtcNow.AddDays(30), VoucherStatus.Active);
        var svc = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var r = await svc.ValidateVoucherByCodeAsync(LegacyDefaultTenantIds.Primary, PlainCode, null);
        Assert.False(r.Ok);
        Assert.Equal(VoucherValidateErrorCodes.NoBalance, r.ErrorCode);
    }

    [Fact]
    public async Task VoucherService_Validate_OptionalAmount_WithinRemaining_CapsMaxRedeemable()
    {
        await using var context = CreateContext();
        await SeedCatalogAndVoucherAsync(context, 20m, DateTime.UtcNow.AddDays(30), VoucherStatus.Active);
        var svc = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var r = await svc.ValidateVoucherByCodeAsync(LegacyDefaultTenantIds.Primary, PlainCode, 10m);
        Assert.True(r.Ok);
        Assert.Equal(10m, r.MaxRedeemableAmount);
        Assert.Equal(20m, r.RemainingAmount);
    }

    [Fact]
    public async Task VoucherService_Validate_OptionalAmount_ExceedsRemaining_ReturnsAmountExceedsBalance()
    {
        await using var context = CreateContext();
        await SeedCatalogAndVoucherAsync(context, 20m, DateTime.UtcNow.AddDays(30), VoucherStatus.Active);
        var svc = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var r = await svc.ValidateVoucherByCodeAsync(LegacyDefaultTenantIds.Primary, PlainCode, 999m);
        Assert.False(r.Ok);
        Assert.Equal(VoucherValidateErrorCodes.AmountExceedsBalance, r.ErrorCode);
        Assert.Equal("Voucher redemption exceeds remaining balance.", r.Message);
    }

    [Fact]
    public async Task CreatePayment_Voucher_FullRedemption_LeavesZero_AndRedeemedStatus()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId, voucherId) = await SeedCatalogAndVoucherAsync(context, 10m, DateTime.UtcNow.AddDays(10), VoucherStatus.Active);
        var p = await context.Products.FirstAsync(x => x.Id == productId);
        p.Price = 10.00m;
        await context.SaveChangesAsync();

        var paymentService = CreatePaymentService(context);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 10.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
                VoucherCode = PlainCode,
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced },
            },
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.True(result.Success, result.Message);

        var v = await context.Vouchers.AsNoTracking().FirstAsync(x => x.Id == voucherId);
        Assert.Equal(0m, v.RemainingAmount);
        Assert.Equal(VoucherStatus.Redeemed, v.Status);
    }

    [Fact]
    public async Task CreatePayment_Voucher_SingleCode_FiscalTotalExceedsRemaining_Fails()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId, _) = await SeedCatalogAndVoucherAsync(context, 9m, DateTime.UtcNow.AddDays(10), VoucherStatus.Active);
        var paymentService = CreatePaymentService(context);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 10.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest { Method = "voucher", TseRequired = false, VoucherCode = PlainCode },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 2, TaxType = TaxType.Reduced },
            },
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.False(result.Success);
        Assert.Contains(
            "exceeds",
            string.Join(" ", result.Errors).ToLowerInvariant(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatePayment_Voucher_MultiRedemption_OneLineExceedsThatVouchersBalance_Fails()
    {
        await using var context = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(context);
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var regId = Guid.NewGuid();
        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();

        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Combo",
            Price = 10.00m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 10,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            TaxRate = TaxTypes.GetTaxRate(2),
            Barcode = $"t-{productId:N}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true,
        });
        context.Customers.Add(new Customer { Id = customerId, Name = "Walk", Email = "t@t.com", Phone = "1", IsActive = true });
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        void AddV(Guid id, string plain, decimal rem)
        {
            var hash = VoucherCodeHasher.HashNormalized(VoucherCodeHasher.NormalizeCode(plain));
            context.Vouchers.Add(new Voucher
            {
                Id = id,
                TenantId = LegacyDefaultTenantIds.Primary,
                CodeHash = hash,
                MaskedCode = "****",
                InitialAmount = 20m,
                RemainingAmount = rem,
                Currency = "EUR",
                Status = VoucherStatus.Active,
                ValidFromUtc = DateTime.UtcNow.AddDays(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
                CreatedByUserId = "u1",
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        AddV(v1, "OVER-A", 8m);
        AddV(v2, "OVER-B", 10m);
        await context.SaveChangesAsync();

        var paymentService = CreatePaymentService(context);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 10.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
                VoucherRedemptions = new List<VoucherRedemptionRequestItem>
                {
                    new() { Code = "OVER-A", Amount = 9m },
                    new() { Code = "OVER-B", Amount = 1m },
                },
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced },
            },
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreatePayment_Voucher_ClientTseRequiredFalse_DeviceTseMode_StillProducesTseSignature()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId, _) = await SeedCatalogAndVoucherAsync(context, 100m, DateTime.UtcNow.AddDays(10), VoucherStatus.Active);
        var paymentService = CreatePaymentService(context, "Device");

        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
                VoucherCode = PlainCode,
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced },
            },
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.True(result.Success, result.Message);
        Assert.False(string.IsNullOrWhiteSpace(result.TseSignature));
        Assert.False(string.IsNullOrWhiteSpace(result.Payment?.TseSignature));
    }

    [Fact]
    public async Task AdminVoucher_Create_DefaultOneYearExpiry_IsAboutOneYearOut()
    {
        await using var context = CreateContext();
        var admin = new AdminVoucherService(context, Mock.Of<ILogger<AdminVoucherService>>());
        var (res, err) = await admin.CreateAsync(
            LegacyDefaultTenantIds.Primary,
            "admin1",
            new CreateAdminVoucherRequest
            {
                InitialAmount = 42m,
                Currency = "EUR",
                ExpiryMode = "DefaultOneYear",
            });
        Assert.Null(err);
        Assert.NotNull(res);
        var span = res!.ExpiresAtUtc - res.ValidFromUtc;
        Assert.InRange(span.TotalDays, 360, 370);
    }

    [Fact]
    public async Task AdminVoucher_Create_CustomExpiry_UsesRequestedEndOfValidity()
    {
        await using var context = CreateContext();
        var admin = new AdminVoucherService(context, Mock.Of<ILogger<AdminVoucherService>>());
        var target = DateTime.UtcNow.AddDays(88);
        var (res, err) = await admin.CreateAsync(
            LegacyDefaultTenantIds.Primary,
            "admin1",
            new CreateAdminVoucherRequest
            {
                InitialAmount = 15m,
                Currency = "EUR",
                ExpiryMode = "Custom",
                ExpiresAtUtc = target,
            });
        Assert.Null(err);
        Assert.NotNull(res);
        Assert.Equal(target.Date, res!.ExpiresAtUtc.Date);
    }

    [Fact]
    public async Task AdminVoucher_VerifyCode_MatchesPlaintextFromCreate()
    {
        await using var context = CreateContext();
        var admin = new AdminVoucherService(context, Mock.Of<ILogger<AdminVoucherService>>());
        var (created, err) = await admin.CreateAsync(
            LegacyDefaultTenantIds.Primary,
            "admin1",
            new CreateAdminVoucherRequest
            {
                InitialAmount = 12m,
                Currency = "EUR",
                ExpiryMode = "DefaultOneYear",
            });
        Assert.Null(err);
        Assert.NotNull(created);

        var (ok, codeErr) = await admin.VerifyCodeMatchesAsync(
            LegacyDefaultTenantIds.Primary,
            created!.Id,
            created.PlaintextCode);
        Assert.Null(codeErr);
        Assert.NotNull(ok);
        Assert.True(ok!.Matches);

        var (wrong, _) = await admin.VerifyCodeMatchesAsync(
            LegacyDefaultTenantIds.Primary,
            created.Id,
            "GUT-XXXXXXXXXXXXXX");
        Assert.NotNull(wrong);
        Assert.False(wrong!.Matches);
    }

    [Fact]
    public async Task AdminVoucher_VerifyCode_Empty_ReturnsCodeRequired()
    {
        await using var context = CreateContext();
        var admin = new AdminVoucherService(context, Mock.Of<ILogger<AdminVoucherService>>());
        var (created, _) = await admin.CreateAsync(
            LegacyDefaultTenantIds.Primary,
            "admin1",
            new CreateAdminVoucherRequest
            {
                InitialAmount = 3m,
                Currency = "EUR",
                ExpiryMode = "DefaultOneYear",
            });
        Assert.NotNull(created);

        var (resp, codeErr) = await admin.VerifyCodeMatchesAsync(LegacyDefaultTenantIds.Primary, created!.Id, "   ");
        Assert.Equal("CODE_REQUIRED", codeErr);
        Assert.Null(resp);
    }

    [Fact]
    public async Task AdminVoucher_Cancel_ReasonTooShort_ReturnsReasonTooShort()
    {
        await using var context = CreateContext();
        var admin = new AdminVoucherService(context, Mock.Of<ILogger<AdminVoucherService>>());
        var (res, _) = await admin.CreateAsync(
            LegacyDefaultTenantIds.Primary,
            "admin1",
            new CreateAdminVoucherRequest
            {
                InitialAmount = 5m,
                Currency = "EUR",
                ExpiryMode = "DefaultOneYear",
            });
        Assert.NotNull(res);
        var (ok, code) = await admin.CancelAsync(LegacyDefaultTenantIds.Primary, "admin1", res!.Id, "no");
        Assert.False(ok);
        Assert.Equal("REASON_TOO_SHORT", code);
    }

    [Fact]
    public async Task AdminVoucher_AfterCancel_CannotValidateOrPay()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId) = await SeedSaleCatalogOnlyAsync(context);
        var admin = new AdminVoucherService(context, Mock.Of<ILogger<AdminVoucherService>>());
        var (created, createErr) = await admin.CreateAsync(
            LegacyDefaultTenantIds.Primary,
            "admin1",
            new CreateAdminVoucherRequest
            {
                InitialAmount = 50m,
                Currency = "EUR",
                ExpiryMode = "DefaultOneYear",
            });
        Assert.Null(createErr);
        Assert.NotNull(created);

        var (cancelOk, cancelErr) = await admin.CancelAsync(
            LegacyDefaultTenantIds.Primary,
            "admin1",
            created!.Id,
            "Customer returned goods — cancel per policy.");
        Assert.True(cancelOk);
        Assert.Null(cancelErr);

        var validate = new VoucherService(context, Mock.Of<ILogger<VoucherService>>());
        var vr = await validate.ValidateVoucherByCodeAsync(LegacyDefaultTenantIds.Primary, created.PlaintextCode, null);
        Assert.False(vr.Ok);
        Assert.Equal(VoucherValidateErrorCodes.NotFound, vr.ErrorCode);

        var paymentService = CreatePaymentService(context);
        var pay = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
                VoucherCode = created.PlaintextCode,
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced },
            },
        };
        var pr = await paymentService.CreatePaymentAsync(pay, "u1");
        Assert.False(pr.Success);
    }

    [Fact]
    public async Task CreatePayment_Voucher_CatalogCodeVoucher_NonVoucherLegacy_RejectsWithoutRedeem()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId, voucherId) =
            await SeedCatalogAndVoucherAsync(context, 50m, DateTime.UtcNow.AddDays(10), VoucherStatus.Active);
        var utc = DateTime.UtcNow;
        context.PaymentMethodDefinitions.Add(new PaymentMethodDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = LegacyDefaultTenantIds.Primary,
            Code = "voucher",
            Name = "Gutschein",
            IsActive = true,
            IsDefault = false,
            DisplayOrder = 40,
            LegacyPaymentMethodValue = 1,
            CreatedAtUtc = utc,
        });
        await context.SaveChangesAsync();

        var paymentService = CreatePaymentService(context);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
                VoucherCode = PlainCode,
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced },
            },
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.False(result.Success);
        Assert.Equal("VOUCHER_LEGACY_MISMATCH", result.DiagnosticCode);
        Assert.Contains("legacy", string.Join(" ", result.Errors).ToLowerInvariant());

        var v = await context.Vouchers.AsNoTracking().FirstAsync(x => x.Id == voucherId);
        Assert.Equal(50m, v.RemainingAmount);
        Assert.Equal(
            0,
            await context.VoucherLedgerEntries.CountAsync(l => l.VoucherId == voucherId && l.Type == VoucherTransactionType.Redeem));
    }

    [Fact]
    public async Task CreatePayment_VoucherMethod_WithoutCode_ReturnsRksvVoucherCodeRequired()
    {
        await using var context = CreateContext();
        var (_, productId, customerId, regId) = await SeedSaleCatalogOnlyAsync(context);
        var paymentService = CreatePaymentService(context);
        var request = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 5.00m,
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced },
            },
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.False(result.Success);
        Assert.Equal(RksvGuardErrorCodes.VoucherCodeRequired, result.DiagnosticCode);
        Assert.True(result.IsDeterministicFailure);
    }

}
