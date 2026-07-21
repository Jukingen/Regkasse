using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PaymentStornoDuplicateGuardTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"StornoDup_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static PaymentService CreatePaymentService(AppDbContext context, IHttpContextAccessor httpAccessor)
    {
        var paymentRepo = new GenericRepository<PaymentDetails>(context, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(context, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(context, Mock.Of<ILogger<GenericRepository<Customer>>>());
        var tseMock = new Mock<ITseService>();
        var finanzMock = new Mock<IFinanzOnlineService>();
        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync("cashier1"))
            .ReturnsAsync(new ApplicationUser { Id = "cashier1", UserName = "cashier1", Role = "Cashier" });
        var companyProfile = new CompanyProfileOptions
        {
            CompanyName = "Test Co",
            TaxNumber = "ATU12345678",
            Street = "S1",
            ZipCode = "1010",
            City = "Wien",
            FooterText = ""
        };
        var receiptService = new ReceiptService(
            context,
            Mock.Of<ILogger<ReceiptService>>(),
            tseMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            userMock.Object,
            TenantTestDoubles.PrimaryTenantResolver,
            TenantTestDoubles.ProductionHostEnvironment);
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(), It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        var cashRegResolver = new CashRegisterResolutionService(
            context,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        return new PaymentService(
            context,
            paymentRepo,
            productRepo,
            customerRepo,
            tseMock.Object,
            finanzMock.Object,
            userMock.Object,
            new NoOpProductModifierValidationService(),
            Mock.Of<IReceiptSequenceService>(),
            receiptService,
            auditMock.Object,
            TenantTestDoubles.CompanyProfileProviderReturning(companyProfile),
            Options.Create(new TseOptions { TseMode = "Demo" }),
            Options.Create(new InventoryOptions()),
            Mock.Of<ILogger<PaymentService>>(),
            cashRegResolver,
            httpAccessor,
            new PaymentMethodCatalogService(context, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(context, TenantTestDoubles.PrimaryTenantResolver),
            TenantTestDoubles.PrimaryTenantResolver);
    }

    private static IHttpContextAccessor HttpAccessorForCashier()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "cashier1"),
            new Claim(ClaimTypes.Role, "Cashier"),
        };
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
        };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(http);
        return accessor.Object;
    }

    private static async Task<(Guid registerId, string receiptNumber)> SeedSaleWithExistingStornoAsync(AppDbContext ctx)
    {
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        if (!await ctx.Customers.AnyAsync(c => c.Id == WalkInCustomerConstants.GuestCustomerId))
        {
            ctx.Customers.Add(new Customer
            {
                Id = WalkInCustomerConstants.GuestCustomerId,
                Name = "Walk-in Customer",
                Email = "walkin@system.local",
                Phone = "",
                IsActive = true,
                IsSystem = true,
            });
        }

        var registerId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = registerId,
            RegisterNumber = "KASSE-TEST",
            Location = "Test",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        var saleId = Guid.NewGuid();
        const string receiptNumber = "AT-SALE-STORNO-DUP";
        var itemsJson = JsonDocument.Parse("""[{"ProductId":"00000000-0000-0000-0000-000000000099","ProductName":"Item","Quantity":1,"UnitPrice":10,"TotalPrice":10,"TaxType":2,"TaxRate":0.1,"TaxAmount":0.91,"LineNet":9.09,"Modifiers":[]}]""");
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = saleId,
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Walk-in Customer",
            PaymentItems = itemsJson,
            TaxDetails = JsonDocument.Parse("""{"2":0.91}"""),
            TotalAmount = 10m,
            TaxAmount = 0.91m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "sig",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = registerId,
            CashierId = "cashier1",
            ReceiptNumber = receiptNumber,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
            TableNumber = 1,
        });

        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Walk-in Customer",
            PaymentItems = itemsJson,
            TaxDetails = JsonDocument.Parse("""{"2":-0.91}"""),
            TotalAmount = -10m,
            TaxAmount = -0.91m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            TseSignature = "sig-storno",
            TseTimestamp = DateTime.UtcNow,
            CashRegisterId = registerId,
            CashierId = "cashier1",
            ReceiptNumber = "AT-SALE-STORNO-DUP-S1",
            OriginalPaymentId = saleId,
            IsStorno = true,
            StornoReason = StornoReason.KundeStorniert,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
            TableNumber = 1,
        });

        await ctx.SaveChangesAsync();
        return (registerId, receiptNumber);
    }

    [Fact]
    public async Task CreatePaymentAsync_Storno_WhenReversalAlreadyExists_ReturnsDeterministicFailure()
    {
        await using var ctx = CreateContext();
        var (registerId, receiptNumber) = await SeedSaleWithExistingStornoAsync(ctx);
        var svc = CreatePaymentService(ctx, HttpAccessorForCashier());

        var request = new CreatePaymentRequest
        {
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            Items = [],
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 10m,
            CashRegisterId = registerId,
            IsStorno = true,
            OriginalReceiptNumber = receiptNumber,
            StornoReason = StornoReason.KundeStorniert,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
        };

        var result = await svc.CreatePaymentAsync(request, "cashier1");

        Assert.False(result.Success);
        Assert.Equal("STORNO_ALREADY_EXISTS", result.DiagnosticCode);
        Assert.Contains("already cancelled", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
