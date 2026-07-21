using FluentAssertions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
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
/// RKSV Sonderbelege integration-style coverage (matches production <see cref="RksvSpecialReceiptService"/> behavior).
/// </summary>
public class RksvSpecialReceiptTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RksvSpecialReceipt_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(
            options,
            TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static RksvSpecialReceiptService CreateService(
        AppDbContext context,
        Mock<ITseService> tseMock,
        Mock<IReceiptSequenceService> receiptSeqMock)
    {
        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test GmbH",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };
        var tseOptions = new TseOptions { TseMode = "Demo" };

        var receiptService = new ReceiptService(
            context,
            new Mock<ILogger<ReceiptService>>().Object,
            tseMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver, TenantTestDoubles.ProductionHostEnvironment);

        return new RksvSpecialReceiptService(
            context,
            tseMock.Object,
            receiptSeqMock.Object,
            receiptService,
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Options.Create(tseOptions),
            new Mock<ILogger<RksvSpecialReceiptService>>().Object,
            new RksvSpecialReceiptFinanzOnlineSubmissionTracker(context),
            new FinanzOnlineOutboxService(context, new Mock<ILogger<FinanzOnlineOutboxService>>().Object),
            Mock.Of<IReportPdfCaptureService>());
    }

    private static async Task<(Guid RegisterId, RksvSpecialReceiptService Service)> SeedRegisterAsync(AppDbContext context)
    {
        TenantTestDoubles.EnsureDefaultTenant(context);
        context.Customers.Add(new Customer
        {
            Id = WalkInCustomerConstants.GuestCustomerId,
            Name = "Gast",
            Email = "gast@test",
            Phone = "0",
            IsActive = true
        });
        var regId = Guid.NewGuid();
        context.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "KASSE-01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

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
            .ReturnsAsync(new TseSignatureResult(RksvTestSignatures.CreateDemoCompactJws(), "prev-chain"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert-test" });

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var n = 0;
        receiptSeqMock
            .Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(() => $"AT-KASSE-01-{DateTime.UtcNow:yyyyMMdd}-{++n}");

        var service = CreateService(context, tseMock, receiptSeqMock);
        return (regId, service);
    }

    private static PaymentService CreatePaymentService(AppDbContext context)
    {
        var paymentRepo = new GenericRepository<PaymentDetails>(context, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(context, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(context, Mock.Of<ILogger<GenericRepository<Customer>>>());

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult("eyJ.eyJ.sign", "prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert123" });

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync("u1"))
            .ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", Role = "Cashier", IsDemo = false });

        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test GmbH",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };
        var tseOptions = new TseOptions { TseMode = "Demo" };

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var seqCallCount = 0;
        receiptSeqMock.Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((IDbContextTransaction _, Guid _, string reg, DateTime d) =>
                $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");

        var receiptService = new ReceiptService(
            context,
            Mock.Of<ILogger<ReceiptService>>(),
            tseMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            userMock.Object,
            TenantTestDoubles.PrimaryTenantResolver, TenantTestDoubles.ProductionHostEnvironment);
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());

        var resolution = new CashRegisterResolutionService(
            context,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        var httpAccessorMock = new Mock<IHttpContextAccessor>();
        httpAccessorMock.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

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
            auditMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Options.Create(tseOptions),
            Options.Create(new InventoryOptions()),
            Mock.Of<ILogger<PaymentService>>(),
            resolution,
            httpAccessorMock.Object,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);
    }

    private static async Task<(Guid RegisterId, PaymentService Payment, Guid ProductId)> SeedDecommissionedPaymentPipelineAsync(AppDbContext context)
    {
        var (regId, rksv) = await SeedRegisterAsync(context);
        await rksv.CreateSchlussbelegAsync(new CreateSchlussbelegRequest { CashRegisterId = regId, Reason = "Test decommission" }, "manager-1");

        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        context.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Soup",
            Price = 100m,
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
        await context.SaveChangesAsync();

        return (regId, CreatePaymentService(context), productId);
    }

    private static async Task<bool> HasJahresbelegCoverageAsync(
        AppDbContext db,
        Guid registerId,
        int year,
        bool decemberMonatsbelegCountsAsJahresbeleg)
    {
        if (decemberMonatsbelegCountsAsJahresbeleg)
        {
            return await db.PaymentDetails.AsNoTracking().AnyAsync(p =>
                p.CashRegisterId == registerId &&
                p.IsActive &&
                (
                    (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg && p.RksvSpecialReceiptYear == year) ||
                    (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg &&
                     p.RksvSpecialReceiptYear == year &&
                     p.RksvSpecialReceiptMonth == 12)
                ));
        }

        return await db.PaymentDetails.AsNoTracking().AnyAsync(p =>
            p.CashRegisterId == registerId &&
            p.IsActive &&
            p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg &&
            p.RksvSpecialReceiptYear == year);
    }

    [Fact]
    public async Task CreateStartbeleg_OnlyOncePerRegister()
    {
        await using var context = CreateContext();
        var (registerId, service) = await SeedRegisterAsync(context);

        var first = await service.CreateStartbelegAsync(new CreateStartbelegRequest { CashRegisterId = registerId }, "u1");
        var exception = await Record.ExceptionAsync(() =>
            service.CreateStartbelegAsync(new CreateStartbelegRequest { CashRegisterId = registerId }, "u1"));

        first.Should().NotBeNull();
        exception.Should().BeOfType<RksvOperationGuardException>();
        exception!.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateMonatsbeleg_OnlyOncePerMonth()
    {
        await using var context = CreateContext();
        var (registerId, service) = await SeedRegisterAsync(context);
        var (y, m) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        if (m == 1)
        {
            y -= 1;
            m = 12;
        }
        else
        {
            m -= 1;
        }

        var first = await service.CreateMonatsbelegAsync(
            new CreateMonatsbelegRequest { CashRegisterId = registerId, Year = y, Month = m },
            "u1",
            forcePastMonth: true);
        var exception = await Record.ExceptionAsync(() =>
            service.CreateMonatsbelegAsync(
                new CreateMonatsbelegRequest { CashRegisterId = registerId, Year = y, Month = m },
                "u1",
                forcePastMonth: true));

        first.Should().NotBeNull();
        exception.Should().BeOfType<RksvOperationGuardException>();
        var guard = (RksvOperationGuardException)exception!;
        if (m == 12)
        {
            guard.ErrorCode.Should().Be(RksvGuardErrorCodes.DuplicateJahresbeleg);
            guard.Message.Should().Contain("Jahresbeleg");
        }
        else
        {
            guard.ErrorCode.Should().Be(RksvGuardErrorCodes.DuplicateMonatsbeleg);
            guard.Message.Should().Contain("already exists");
            guard.Message.Should().Contain($"{y}-{m:00}");
        }
    }

    [Fact]
    public async Task Jahresbeleg_DecemberMonatsbelegCanActAsJahresbeleg()
    {
        await using var context = CreateContext();
        var (registerId, _) = await SeedRegisterAsync(context);
        const int year = 2026;
        const int december = 12;

        context.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "u",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            TseSignature = "sig",
            TseTimestamp = DateTime.UtcNow,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = "M-12",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "u",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Monatsbeleg,
            RksvSpecialReceiptYear = year,
            RksvSpecialReceiptMonth = december
        });
        await context.SaveChangesAsync();

        var hasJahresbeleg = await HasJahresbelegCoverageAsync(context, registerId, year, decemberMonatsbelegCountsAsJahresbeleg: true);
        hasJahresbeleg.Should().BeTrue();
    }

    [Fact]
    public async Task Schlussbeleg_PreventsNewPayments()
    {
        await using var context = CreateContext();
        var (registerId, paymentService, productId) = await SeedDecommissionedPaymentPipelineAsync(context);

        var result = await paymentService.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                CustomerId = WalkInCustomerConstants.GuestCustomerId,
                TableNumber = 1,
                TotalAmount = 100m,
                Steuernummer = "ATU12345678",
                CashRegisterId = registerId,
                Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
                Items = new List<PaymentItemRequest>
                {
                    new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
                }
            },
            "u1");

        result.Success.Should().BeFalse();
        result.DiagnosticCode.Should().Be(RksvGuardErrorCodes.RegisterDecommissioned);
    }
}
