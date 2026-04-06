using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Tests;

public class OfflineTransactionReplayIntegrationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"OfflineReplay_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static (PaymentService paymentService, OfflineTransactionService offlineService) CreateServices(
        AppDbContext context,
        CompanyProfileOptions companyProfile,
        Mock<IAuditLogService> auditMock,
        Mock<ITseService> tseMock,
        Mock<IReceiptSequenceService> receiptSeqMock)
    {
        var loggerPayment = new Mock<ILogger<PaymentService>>().Object;
        var loggerRepo = new Mock<ILogger<GenericRepository<PaymentDetails>>>().Object;
        var loggerProd = new Mock<ILogger<GenericRepository<Product>>>().Object;
        var loggerCust = new Mock<ILogger<GenericRepository<Customer>>>().Object;

        var paymentRepo = new GenericRepository<PaymentDetails>(context, loggerRepo);
        var productRepo = new GenericRepository<Product>(context, loggerProd);
        var customerRepo = new GenericRepository<Customer>(context, loggerCust);

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
            Options.Create(companyProfile),
            userMock.Object,
            TenantTestDoubles.PrimaryTenantResolver);

        var cashRegResolver = new CashRegisterResolutionService(context, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver);
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
            Options.Create(companyProfile),
            Options.Create(tseOptions),
            Options.Create(new InventoryOptions()),
            loggerPayment,
            cashRegResolver,
            httpAccessor,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);

        var offlineService = new OfflineTransactionService(
            context,
            paymentService,
            auditMock.Object,
            new Mock<ILogger<OfflineTransactionService>>().Object);

        return (paymentService, offlineService);
    }

    private static string CreatePayloadJson(CreatePaymentRequest request)
        => JsonSerializer.Serialize(request);

    [Fact]
    public async Task ReplayOfflineTransaction_SyncedLinksOfflineOrigin_AndExportShowsMetadata()
    {
        await using var context = CreateContext();

        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        // Seed minimal fiscal domain.
        TenantTestDoubles.EnsureDefaultTenant(context);
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
            Name = "Döner",
            Price = 6.90m,
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

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

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var offlineCreatedAt = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
        var offlineId = Guid.NewGuid();

        var paymentRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };

        using var doc = JsonDocument.Parse(CreatePayloadJson(paymentRequest));
        var payloadElement = doc.RootElement.Clone();

        var replayReq = new ReplayOfflineTransactionsRequest
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
        };

        var resp = await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");
        Assert.Single(resp.Items);
        Assert.Equal("Synced", resp.Items[0].Status);
        Assert.NotNull(resp.Items[0].SyncedPaymentId);

        var offlineRow = await context.OfflineTransactions.FirstAsync(x => x.Id == offlineId);
        Assert.Equal(OfflineTransactionStatus.Synced, offlineRow.Status);
        Assert.NotNull(offlineRow.SyncedPaymentId);

        var paymentRow = await context.PaymentDetails.FirstAsync(p => p.Id == offlineRow.SyncedPaymentId);
        Assert.Equal(offlineId, paymentRow.OfflineTransactionId);

        var receiptRow = await context.Receipts.FirstOrDefaultAsync(r => r.PaymentId == paymentRow.Id);
        Assert.NotNull(receiptRow);

        var exportService = new FiscalExportService(context, new Mock<ILogger<FiscalExportService>>().Object);
        var fromUtc = offlineCreatedAt.AddHours(-1);
        var toUtc = DateTime.UtcNow.AddHours(1);
        var export = await exportService.BuildExportAsync(cashRegisterId, fromUtc, toUtc, includeCsv: false);

        var exportedReceipt = export.Receipts.Single(r => r.ReceiptId == receiptRow!.ReceiptId);
        Assert.True(exportedReceipt.HasOfflineOrigin);
        Assert.NotNull(exportedReceipt.OfflineCreatedAtUtc);
        Assert.Equal(offlineCreatedAt, exportedReceipt.OfflineCreatedAtUtc.Value);
        Assert.NotNull(exportedReceipt.FiscalizedAtUtc);
        Assert.NotNull(offlineRow.FiscalizedAtUtc);
        Assert.True(offlineRow.RetryCount >= 1);
    }

    [Fact]
    public async Task ReplayOfflineTransaction_PayloadMismatch_DoesNotOverwritePayload()
    {
        await using var context = CreateContext();
        var cashRegisterId = Guid.NewGuid();
        var offlineId = Guid.NewGuid();
        var originalPayload = $"{{\"totalAmount\":1,\"cashRegisterId\":\"{cashRegisterId:D}\"}}";
        context.OfflineTransactions.Add(new OfflineTransaction
        {
            Id = offlineId,
            CashRegisterId = cashRegisterId,
            CreatedAt = DateTime.UtcNow,
            OfflineCreatedAtUtc = DateTime.UtcNow.AddHours(-1),
            PayloadJson = originalPayload,
            Status = OfflineTransactionStatus.Pending,
            IsActive = true,
            RetryCount = 0
        });
        await context.SaveChangesAsync();

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        var tseMock = new Mock<ITseService>();
        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var companyProfile = new CompanyProfileOptions { CompanyName = "T", TaxNumber = "ATU12345678", Street = "", ZipCode = "", City = "", FooterText = "" };
        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var tamperedPayload = $"{{\"totalAmount\":99,\"cashRegisterId\":\"{cashRegisterId:D}\"}}";
        using var doc2 = JsonDocument.Parse(tamperedPayload);
        var resp = await offlineService.ReplayOfflineTransactionsAsync(new ReplayOfflineTransactionsRequest
        {
            Transactions =
            {
                new ReplayOfflineTransactionItem
                {
                    OfflineTransactionId = offlineId,
                    CreatedAtUtc = DateTime.UtcNow,
                    CashRegisterId = cashRegisterId,
                    Payload = doc2.RootElement.Clone()
                }
            }
        }, "u1", "Cashier");

        Assert.Equal("PAYLOAD_IMMUTABLE_MISMATCH", resp.Items[0].ErrorCode);
        var row = await context.OfflineTransactions.AsNoTracking().FirstAsync(x => x.Id == offlineId);
        Assert.Equal(originalPayload, row.PayloadJson);
        var batch = resp.ReplayBatchCorrelationId!.Value;
        Assert.Equal(batch, resp.Items[0].ReplayBatchCorrelationId);
        auditMock.Verify(x => x.LogSystemOperationAsync(
            "PAYLOAD_IMMUTABLE_MISMATCH",
            "OfflineTransaction",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            batch.ToString("N")), Times.Once);
    }

    [Fact]
    public async Task ReplayOfflineTransaction_BatchCorrelation_SameOnSuccessAcrossPaymentAndAudits()
    {
        await using var context = CreateContext();

        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Döner",
            Price = 6.90m,
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");

        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var offlineId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var paymentRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(paymentRequest));
        context.OfflineTransactions.Add(new OfflineTransaction
        {
            Id = offlineId,
            CashRegisterId = cashRegisterId,
            PayloadJson = doc.RootElement.GetRawText(),
            PayloadHash = OfflinePayloadHashing.ComputeRuntimeCanonicalHashHex(doc.RootElement.GetRawText()),
            ServerReceivedAtUtc = DateTime.UtcNow,
            OfflineCreatedAtUtc = DateTime.UtcNow,
            Status = OfflineTransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            RetryCount = 0
        });
        await context.SaveChangesAsync();

        var resp = await offlineService.ReplayOfflineTransactionsAsync(new ReplayOfflineTransactionsRequest
        {
            Transactions =
            {
                new ReplayOfflineTransactionItem
                {
                    OfflineTransactionId = offlineId,
                    CreatedAtUtc = DateTime.UtcNow,
                    CashRegisterId = cashRegisterId,
                    Payload = doc.RootElement.Clone()
                }
            }
        }, "u1", "Cashier");

        var batch = resp.ReplayBatchCorrelationId!.Value;
        Assert.Single(resp.Items);
        Assert.Equal(batch, resp.Items[0].ReplayBatchCorrelationId);
        var payment = await context.PaymentDetails.FirstAsync();
        Assert.Equal(batch, payment.OfflineReplayBatchCorrelationId);

        auditMock.Verify(x => x.LogSystemOperationAsync(
            "OFFLINE_SYNCED",
            "OfflineTransaction",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            batch.ToString("N")), Times.Once);

        auditMock.Verify(x => x.LogPaymentOperationAsync(
            "PaymentCreated",
            "Payment",
            It.IsAny<Guid?>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<decimal?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            batch.ToString("N"),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<double?>()), Times.Once);
    }

    [Fact]
    public async Task ReplayOfflineTransaction_FailedDoesNotCreateReceipt()
    {
        await using var context = CreateContext();

        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Döner",
            Price = 6.90m,
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

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
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");

        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var offlineCreatedAt = new DateTime(2026, 3, 18, 10, 5, 0, DateTimeKind.Utc);
        var offlineId = Guid.NewGuid();

        // ProductId does not exist -> payment fails before fiscal artifact creation.
        var missingProductId = Guid.NewGuid();
        var paymentRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = missingProductId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };

        using var doc = JsonDocument.Parse(CreatePayloadJson(paymentRequest));
        var payloadElement = doc.RootElement.Clone();

        var replayReq = new ReplayOfflineTransactionsRequest
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
        };

        var resp = await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");
        Assert.Single(resp.Items);
        Assert.Equal("Pending", resp.Items[0].Status);

        var offlineRow = await context.OfflineTransactions.FirstAsync(x => x.Id == offlineId);
        Assert.Equal(OfflineTransactionStatus.Pending, offlineRow.Status);
        Assert.Null(offlineRow.SyncedPaymentId);
        Assert.NotNull(offlineRow.LastErrorCode);
        Assert.NotNull(offlineRow.LastReplayAttemptAt);
        Assert.True(offlineRow.RetryCount >= 1);

        Assert.Empty(context.Receipts.ToList());
    }

    [Fact]
    public async Task ReplayOfflineTransaction_ClockDriftWarning_FlagAndAuditLogged()
    {
        await using var context = CreateContext();

        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());

        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

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
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");

        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var offlineId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var missingProductId = Guid.NewGuid();

        var payloadRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = missingProductId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };

        var replayReq = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineId,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(10),
                    CashRegisterId = cashRegisterId,
                    Payload = JsonDocument.Parse(JsonSerializer.Serialize(payloadRequest)).RootElement.Clone(),
                    DeviceId = "dev1",
                    ClientSequenceNumber = 10
                }
            }
        };

        await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");

        var offlineRow = await context.OfflineTransactions.FirstAsync(x => x.Id == offlineId);
        Assert.True(offlineRow.ClockDriftWarning);

        auditMock.Verify(x => x.LogSystemOperationAsync(
            "CLOCK_DRIFT_WARNING",
            "OfflineTransaction",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReplayOfflineTransaction_ClientSequenceGap_DetectedAndFlagged()
    {
        await using var context = CreateContext();

        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();
        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = Guid.NewGuid(), Name = "Speisen", VatRate = 10m });
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());

        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

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
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");

        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var offlineId1 = Guid.NewGuid();
        var offlineId2 = Guid.NewGuid();
        var missingProductId = Guid.NewGuid();
        var idempotencyKey1 = Guid.NewGuid().ToString("N");
        var idempotencyKey2 = Guid.NewGuid().ToString("N");

        var baseRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = missingProductId, Quantity = 1, TaxType = TaxType.Reduced }
            }
        };

        var payload1 = new CreatePaymentRequest
        {
            CustomerId = baseRequest.CustomerId,
            TableNumber = baseRequest.TableNumber,
            Steuernummer = baseRequest.Steuernummer,
            CashRegisterId = baseRequest.CashRegisterId,
            Payment = baseRequest.Payment,
            Items = baseRequest.Items,
            TotalAmount = 6.90m,
            IdempotencyKey = idempotencyKey1
        };

        var payload2 = new CreatePaymentRequest
        {
            CustomerId = baseRequest.CustomerId,
            TableNumber = baseRequest.TableNumber,
            Steuernummer = baseRequest.Steuernummer,
            CashRegisterId = baseRequest.CashRegisterId,
            Payment = baseRequest.Payment,
            Items = baseRequest.Items,
            TotalAmount = 6.90m,
            IdempotencyKey = idempotencyKey2
        };

        using var doc1 = JsonDocument.Parse(CreatePayloadJson(payload1));
        using var doc2 = JsonDocument.Parse(CreatePayloadJson(payload2));

        var replayReq = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineId1,
                    CreatedAtUtc = DateTime.UtcNow,
                    CashRegisterId = cashRegisterId,
                    Payload = doc1.RootElement.Clone(),
                    DeviceId = "dev1",
                    ClientSequenceNumber = 10
                },
                new()
                {
                    OfflineTransactionId = offlineId2,
                    CreatedAtUtc = DateTime.UtcNow,
                    CashRegisterId = cashRegisterId,
                    Payload = doc2.RootElement.Clone(),
                    DeviceId = "dev1",
                    ClientSequenceNumber = 12
                }
            }
        };

        await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");

        var row1 = await context.OfflineTransactions.FirstAsync(x => x.Id == offlineId1);
        var row2 = await context.OfflineTransactions.FirstAsync(x => x.Id == offlineId2);
        Assert.False(row1.SequenceGapDetected);
        Assert.True(row2.SequenceGapDetected);

        auditMock.Verify(x => x.LogSystemOperationAsync(
            "SEQUENCE_GAP_DETECTED",
            "OfflineTransaction",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReplayOfflineTransaction_PayloadHashDeduplication_ReturnsCanonicalOfflineId()
    {
        await using var context = CreateContext();

        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());

        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

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
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");

        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var offlineId1 = Guid.NewGuid();
        var offlineId2 = Guid.NewGuid();
        var offlineCreatedAt = DateTime.UtcNow;
        var missingProductId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var paymentRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = missingProductId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };

        using var doc = JsonDocument.Parse(CreatePayloadJson(paymentRequest));
        var payloadElement = doc.RootElement.Clone();

        var replayReq = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineId1,
                    CreatedAtUtc = offlineCreatedAt,
                    CashRegisterId = cashRegisterId,
                    Payload = payloadElement,
                    DeviceId = "dev1",
                    ClientSequenceNumber = 10
                },
                new()
                {
                    OfflineTransactionId = offlineId2,
                    CreatedAtUtc = offlineCreatedAt,
                    CashRegisterId = cashRegisterId,
                    Payload = payloadElement,
                    DeviceId = "dev1",
                    ClientSequenceNumber = 10
                }
            }
        };

        var resp = await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");

        Assert.Equal(2, resp.Items.Count);
        Assert.Equal(1, context.OfflineTransactions.Count());

        var row = await context.OfflineTransactions.FirstAsync();
        Assert.Equal(row.RetryCount, 2);

        Assert.Equal(offlineId1, resp.Items[0].RequestedOfflineTransactionId);
        Assert.Equal(offlineId1, resp.Items[0].OfflineTransactionId);
        Assert.Equal(offlineId2, resp.Items[1].RequestedOfflineTransactionId);
        Assert.Equal(offlineId1, resp.Items[1].OfflineTransactionId);

        auditMock.Verify(x => x.LogSystemOperationAsync(
            "PAYLOAD_HASH_DEDUPLICATED",
            "OfflineTransaction",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Legacy migration used digest(PayloadJson::text) without key ordering; runtime uses sorted JSON.
    /// Recompute-hash path must still dedupe and align stored payload_hash after successful payload match.
    /// </summary>
    [Fact]
    public async Task ReplayOfflineTransaction_LegacyWrongPayloadHash_RecomputesHashAndDedupes()
    {
        await using var context = CreateContext();

        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

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
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) => $"AT-{reg}-{d:yyyyMMdd}-1");

        var (_, offlineService) = CreateServices(context, companyProfile, auditMock, tseMock, receiptSeqMock);

        var offlineIdCanonical = Guid.NewGuid();
        var offlineIdClient = Guid.NewGuid();
        var missingProductId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString("N");

        var paymentRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = missingProductId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };

        var payloadJson = CreatePayloadJson(paymentRequest);
        var (_, runtimeHash) = OfflinePayloadHashing.NormalizeAndHash(payloadJson);

        context.OfflineTransactions.Add(new OfflineTransaction
        {
            Id = offlineIdCanonical,
            CashRegisterId = cashRegisterId,
            PayloadJson = payloadJson,
            PayloadHash = new string('0', 64),
            ServerReceivedAtUtc = DateTime.UtcNow,
            OfflineCreatedAtUtc = DateTime.UtcNow,
            Status = OfflineTransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await context.SaveChangesAsync();

        using var doc = JsonDocument.Parse(payloadJson);
        var replayReq = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineIdClient,
                    CreatedAtUtc = DateTime.UtcNow,
                    CashRegisterId = cashRegisterId,
                    Payload = doc.RootElement.Clone(),
                    DeviceId = "dev1",
                    ClientSequenceNumber = 10
                }
            }
        };

        await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");

        Assert.Single(context.OfflineTransactions);
        var row = await context.OfflineTransactions.FirstAsync();
        Assert.Equal(offlineIdCanonical, row.Id);
        Assert.Equal(runtimeHash, row.PayloadHash, StringComparer.OrdinalIgnoreCase);

        auditMock.Verify(x => x.LogSystemOperationAsync(
            "PAYLOAD_HASH_DEDUPLICATED",
            "OfflineTransaction",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<AuditLogStatus>(),
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReplayOfflineTransaction_ReplayIdempotency_SyncedSkipped_NoExtraReceipts()
    {
        await using var context = CreateContext();

        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(context);
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
            Name = "Döner",
            Price = 6.90m,
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

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

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var offlineCreatedAt = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
        var offlineId = Guid.NewGuid();

        var paymentRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };

        using var doc = JsonDocument.Parse(CreatePayloadJson(paymentRequest));
        var payloadElement = doc.RootElement.Clone();

        var replayReq = new ReplayOfflineTransactionsRequest
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
        };

        await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");

        var receiptCount1 = context.Receipts.Count();
        var paymentCount1 = context.PaymentDetails.Count();

        await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");

        Assert.Equal(receiptCount1, context.Receipts.Count());
        Assert.Equal(paymentCount1, context.PaymentDetails.Count());

        var offlineRow = await context.OfflineTransactions.FirstAsync(x => x.Id == offlineId);
        Assert.Equal(OfflineTransactionStatus.Synced, offlineRow.Status);
    }

    [Fact]
    public async Task ReplayOfflineTransaction_ConcurrentSafety_DuplicateEntriesInSameRequest_NoExtraReceipts()
    {
        await using var context = CreateContext();

        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Döner",
            Price = 6.90m,
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

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };

        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        auditMock.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

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

        var idempotencyKey = Guid.NewGuid().ToString("N");
        var offlineCreatedAt = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc);
        var offlineId = Guid.NewGuid();

        var paymentRequest = new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };

        using var doc = JsonDocument.Parse(CreatePayloadJson(paymentRequest));
        var payloadElement = doc.RootElement.Clone();

        var replayReq = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineId,
                    CreatedAtUtc = offlineCreatedAt,
                    CashRegisterId = cashRegisterId,
                    Payload = payloadElement
                },
                new()
                {
                    OfflineTransactionId = offlineId,
                    CreatedAtUtc = offlineCreatedAt,
                    CashRegisterId = cashRegisterId,
                    Payload = payloadElement
                }
            }
        };

        await offlineService.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");

        Assert.Single(context.Receipts.ToList());
    }
}

