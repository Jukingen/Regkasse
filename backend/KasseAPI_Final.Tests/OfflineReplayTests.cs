using System.Text.Json;
using FluentAssertions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Offline replay queue semantics: voucher plaintext must not appear in persisted <see cref="OfflineTransaction.PayloadJson"/>;
/// batch replay preserves client item order for successful fiscalizations.
/// </summary>
public class OfflineReplayTests
{
    private const string VoucherPlainCode = "TEST-VOUCHER-12345";

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"OfflineReplayPack_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static (PaymentService PaymentService, OfflineTransactionService OfflineService) CreateServices(
        AppDbContext context,
        CompanyProfileOptions companyProfile,
        Mock<IAuditLogService> auditMock,
        Mock<ITseService> tseMock,
        Mock<IReceiptSequenceService> receiptSeqMock)
    {
        var paymentRepo = new GenericRepository<PaymentDetails>(context, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(context, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(context, Mock.Of<ILogger<GenericRepository<Customer>>>());

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", Role = "Cashier", IsDemo = false });

        var modifierValidation = new NoOpProductModifierValidationService();
        var tseOptions = new TseOptions { TseMode = "Demo" };
        var receiptService = new ReceiptService(
            context,
            new Mock<ILogger<ReceiptService>>().Object,
            tseMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            userMock.Object,
            TenantTestDoubles.PrimaryTenantResolver);

        var cashRegResolver = new CashRegisterResolutionService(
            context,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());
        var httpAccessor = Mock.Of<IHttpContextAccessor>();
        var paymentService = new PaymentService(
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
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Options.Create(tseOptions),
            Options.Create(new InventoryOptions()),
            Mock.Of<ILogger<PaymentService>>(),
            cashRegResolver,
            httpAccessor,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);

        var dataProtection = new EphemeralDataProtectionProvider();
        var offlineService = new OfflineTransactionService(
            context,
            paymentService,
            auditMock.Object,
            new Mock<ILogger<OfflineTransactionService>>().Object,
            dataProtection);

        return (paymentService, offlineService);
    }

    private static string CreatePayloadJson(CreatePaymentRequest request) => JsonSerializer.Serialize(request);

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<decimal?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return auditMock;
    }

    private static async Task<(Guid CustomerId, Guid ProductId, Guid CashRegisterId)> SeedCashSaleAsync(AppDbContext context, decimal unitPrice)
    {
        TenantTestDoubles.EnsureDefaultTenant(context);
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        context.Categories.Add(new Category
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = categoryId,
            Name = "Speisen",
            VatRate = 10m
        });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Item",
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
        context.Customers.Add(new Customer { Id = customerId, Name = "Test", Email = "t@t.com", Phone = "1", IsActive = true });
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = cashRegisterId,
            RegisterNumber = "KASSE-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();
        return (customerId, productId, cashRegisterId);
    }

    private static async Task<(Guid CustomerId, Guid ProductId, Guid CashRegisterId, Guid VoucherId)> SeedVoucherSaleAsync(AppDbContext context)
    {
        var (customerId, productId, cashRegisterId) = await SeedCashSaleAsync(context, 50m);
        var voucherId = Guid.NewGuid();
        var hash = VoucherCodeHasher.HashNormalized(VoucherCodeHasher.NormalizeCode(VoucherPlainCode));
        context.Vouchers.Add(new Voucher
        {
            Id = voucherId,
            TenantId = LegacyDefaultTenantIds.Primary,
            CodeHash = hash,
            MaskedCode = "****2345",
            InitialAmount = 100m,
            RemainingAmount = 100m,
            Currency = "EUR",
            Status = VoucherStatus.Active,
            ValidFromUtc = DateTime.UtcNow.AddDays(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            CreatedByUserId = "u1",
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return (customerId, productId, cashRegisterId, voucherId);
    }

    [Fact]
    public async Task OfflineQueue_DoesNotStoreVoucherCodes_InPayloadJson()
    {
        await using var context = CreateContext();
        var (customerId, productId, cashRegisterId, _) = await SeedVoucherSaleAsync(context);

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var auditMock = CreateAuditMock();

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var seq = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seq}");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seq}");

        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var offlineId = Guid.NewGuid();
        var offlineCreatedAt = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
        var paymentRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 50m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest
            {
                Method = "voucher",
                TseRequired = false,
                VoucherCode = VoucherPlainCode
            },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

        using var doc = JsonDocument.Parse(CreatePayloadJson(paymentRequest));
        var payloadElement = doc.RootElement.Clone();

        var resp = await offlineService.ReplayOfflineTransactionsAsync(
            new ReplayOfflineTransactionsRequest
            {
                Transactions = new List<ReplayOfflineTransactionItem>
                {
                    new()
                    {
                        OfflineTransactionId = offlineId,
                        CreatedAtUtc = offlineCreatedAt,
                        CashRegisterId = cashRegisterId,
                        Payload = payloadElement
                    }
                }
            },
            "u1",
            "Cashier");

        resp.Items.Should().ContainSingle();
        resp.Items[0].Status.Should().Be("Synced");

        var row = await context.OfflineTransactions.AsNoTracking().FirstAsync(x => x.Id == offlineId);
        row.PayloadJson.Should().NotContain(VoucherPlainCode);
        row.PayloadSecretsProtected.Should().NotBeNullOrWhiteSpace();
        row.PayloadSecretsProtected.Should().MatchRegex("^[A-Za-z0-9+/=]+$");
    }

    [Fact]
    public async Task OfflineReplay_ProcessesInOrder()
    {
        await using var context = CreateContext();

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        TenantTestDoubles.EnsureDefaultTenant(context);
        var categoryId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        context.Categories.Add(new Category
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = categoryId,
            Name = "Speisen",
            VatRate = 10m
        });

        var product10 = Guid.NewGuid();
        var product20 = Guid.NewGuid();
        var product30 = Guid.NewGuid();
        foreach (var (pid, price) in new[] { (product10, 10m), (product20, 20m), (product30, 30m) })
        {
            context.Products.Add(new Product
            {
                Id = pid,
                TenantId = LegacyDefaultTenantIds.Primary,
                Name = $"P-{price}",
                Price = price,
                CategoryId = categoryId,
                Category = "Speisen",
                StockQuantity = 100,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = 2,
                TaxRate = TaxTypes.GetTaxRate(2),
                Barcode = $"t-{pid:N}",
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsActive = true
            });
        }

        context.Customers.Add(new Customer { Id = customerId, Name = "Test", Email = "t@t.com", Phone = "1", IsActive = true });
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = cashRegisterId,
            RegisterNumber = "KASSE-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var auditMock = CreateAuditMock();

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var seq = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seq}");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-{++seq}");

        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var baseUtc = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        var items = new List<ReplayOfflineTransactionItem>();
        var amounts = new[] { (10m, product10), (20m, product20), (30m, product30) };
        for (var i = 0; i < amounts.Length; i++)
        {
            var (amount, pid) = amounts[i];
            var paymentRequest = new CreatePaymentRequest
            {
                CustomerId = customerId,
                TableNumber = 1,
                TotalAmount = amount,
                Steuernummer = "ATU12345678",
                CashRegisterId = cashRegisterId,
                Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
                Items = new List<PaymentItemRequest>
                {
                    new() { ProductId = pid, Quantity = 1, TaxType = TaxType.Reduced }
                },
                IdempotencyKey = Guid.NewGuid().ToString("N")
            };
            using var doc = JsonDocument.Parse(CreatePayloadJson(paymentRequest));
            items.Add(new ReplayOfflineTransactionItem
            {
                OfflineTransactionId = Guid.NewGuid(),
                CreatedAtUtc = baseUtc.AddMinutes(i),
                CashRegisterId = cashRegisterId,
                Payload = doc.RootElement.Clone()
            });
        }

        var resp = await offlineService.ReplayOfflineTransactionsAsync(
            new ReplayOfflineTransactionsRequest { Transactions = items },
            "u1",
            "Cashier");

        resp.Items.Should().HaveCount(3);
        resp.Items.Should().OnlyContain(x => x.Status == "Synced");

        var totals = new List<decimal>();
        foreach (var r in resp.Items)
        {
            r.SyncedPaymentId.Should().NotBeNull();
            var pay = await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == r.SyncedPaymentId!.Value);
            totals.Add(pay.TotalAmount);
        }

        totals.Should().Equal(10m, 20m, 30m);
    }
}
