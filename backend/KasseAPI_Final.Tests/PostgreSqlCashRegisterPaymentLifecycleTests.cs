using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PostgreSQL-only proofs for row-level serialization between payment commit authorization and shift close/open.
/// Requires Docker or REGKASSE_TEST_POSTGRES (same fixture as offline replay tests).
/// <para><see cref="CreatePaymentAsync_WhenRegisterAlreadyClosed"/> exercises the <strong>pre-check</strong> path (register already closed before <see cref="PaymentService.CreatePaymentAsync"/> runs).
/// In-memory <see cref="PaymentRegisterCommitGateTests"/> uses Moq so pre-check passes while the <strong>commit gate</strong> fails (temporal gap without sleeps).</para>
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class PostgreSqlCashRegisterPaymentLifecycleTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public PostgreSqlCashRegisterPaymentLifecycleTests(PostgreSqlReplayFixture fixture) =>
        _fixture = fixture;

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options);

    private static async Task AddMinimalUserAsync(AppDbContext ctx, string id)
    {
        if (await ctx.Users.AnyAsync(u => u.Id == id))
            return;

        ctx.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            Email = $"{id}@x.test",
            NormalizedEmail = $"{id}@x.test".ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            FirstName = "T",
            LastName = "U"
        });
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(params ApplicationUser[] users)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        foreach (var u in users)
            mgr.Setup(m => m.FindByIdAsync(u.Id)).ReturnsAsync(u);
        return mgr;
    }

    [SkippableFact]
    public async Task PaymentCommitGate_RowLock_BlocksClose_UntilPaymentTransactionRollsBack()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var regId = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            await AddMinimalUserAsync(seed, "u1");
            seed.CashRegisters.Add(new CashRegister
            {
                TenantId = LegacyDefaultTenantIds.Primary,
                Id = regId,
                RegisterNumber = "PG-LOCK-1",
                Location = "T",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CurrentUserId = "u1",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        await using var holdCtx = CreateContext();
        await using var tx = await holdCtx.Database.BeginTransactionAsync();
        var resolution = new CashRegisterResolutionService(holdCtx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver);
        var gateOk = await resolution.ValidatePaymentRegisterForCommitAsync("u1", regId, new ClaimsPrincipal());
        Assert.True(gateOk.Ok);

        var cs = _fixture.ConnectionString;
        var closeTask = Task.Run(async () =>
        {
            await using var closeCtx = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(cs).Options);
            var mgr = CreateUserManagerMock();
            var shift = new CashRegisterShiftService(closeCtx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);
            return await shift.TryCloseCashRegisterAsync(regId, "u1", 0m, CancellationToken.None);
        });

        await Task.Delay(400);
        Assert.False(closeTask.IsCompleted);

        await tx.RollbackAsync();

        var closeResult = await closeTask.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(CashRegisterCloseKind.Success, closeResult.Kind);
    }

    [SkippableFact]
    public async Task ValidatePaymentRegisterForCommit_AfterClose_ReturnsClosed()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var regId = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            await AddMinimalUserAsync(seed, "u1");
            seed.CashRegisters.Add(new CashRegister
            {
                TenantId = LegacyDefaultTenantIds.Primary,
                Id = regId,
                RegisterNumber = "PG-CLOSE-1",
                Location = "T",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CurrentUserId = "u1",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        await using (var closeCtx = CreateContext())
        {
            var mgr = CreateUserManagerMock();
            var shift = new CashRegisterShiftService(closeCtx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);
            var closed = await shift.TryCloseCashRegisterAsync(regId, "u1", 0m, CancellationToken.None);
            Assert.Equal(CashRegisterCloseKind.Success, closed.Kind);
        }

        await using var payCtx = CreateContext();
        await using var tx = await payCtx.Database.BeginTransactionAsync();
        var resolution = new CashRegisterResolutionService(payCtx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver);
        var gate = await resolution.ValidatePaymentRegisterForCommitAsync("u1", regId, new ClaimsPrincipal());
        Assert.False(gate.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Closed, gate.Code);
    }

    [SkippableFact]
    public async Task ValidatePaymentRegisterForCommit_AfterShiftMovesToOtherUser_ReturnsForbidden()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var u1 = new ApplicationUser
        {
            Id = "life-u1",
            UserName = "life-u1",
            NormalizedUserName = "LIFE-U1",
            Email = "life-u1@x.test",
            NormalizedEmail = "LIFE-U1@X.TEST",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            FirstName = "A",
            LastName = "One"
        };
        var u2 = new ApplicationUser
        {
            Id = "life-u2",
            UserName = "life-u2",
            NormalizedUserName = "LIFE-U2",
            Email = "life-u2@x.test",
            NormalizedEmail = "LIFE-U2@X.TEST",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            FirstName = "B",
            LastName = "Two"
        };

        await using (var seed = CreateContext())
        {
            seed.Users.Add(u1);
            seed.Users.Add(u2);
            seed.CashRegisters.Add(new CashRegister
            {
                TenantId = LegacyDefaultTenantIds.Primary,
                Id = r1,
                RegisterNumber = "PG-R1",
                Location = "T",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CurrentUserId = u1.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            seed.CashRegisters.Add(new CashRegister
            {
                TenantId = LegacyDefaultTenantIds.Primary,
                Id = r2,
                RegisterNumber = "PG-R2",
                Location = "T",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Closed,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            seed.UserSettings.Add(new UserSettings
            {
                Id = Guid.NewGuid(),
                UserId = u1.Id,
                CashRegisterId = r1.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using (var step = CreateContext())
        {
            var mgr = CreateUserManagerMock(u1, u2);
            var shift = new CashRegisterShiftService(step, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);
            var closeR = await shift.TryCloseCashRegisterAsync(r1, u1.Id, 0m, CancellationToken.None);
            Assert.Equal(CashRegisterCloseKind.Success, closeR.Kind);
            var openR = await shift.TryOpenCashRegisterAsync(
                r1,
                u2.Id,
                0m,
                "test open",
                allowIdempotentSameUser: true,
                CancellationToken.None);
            Assert.Equal(CashRegisterOpenKind.SuccessOpened, openR.Kind);
        }

        await using var payCtx = CreateContext();
        await using var tx = await payCtx.Database.BeginTransactionAsync();
        var resolution = new CashRegisterResolutionService(payCtx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver);
        var gate = await resolution.ValidatePaymentRegisterForCommitAsync(u1.Id, r1, new ClaimsPrincipal());
        Assert.False(gate.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, gate.Code);
    }

    [SkippableFact]
    public async Task CreatePaymentAsync_WhenRegisterAlreadyClosed_RejectsWithClosedDiagnostic()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        await using (var seed = CreateContext())
        {
            await AddMinimalUserAsync(seed, "u1");
            TenantTestDoubles.EnsureDefaultTenant(seed);
            seed.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = categoryId, Name = "Speisen", VatRate = 10m });
            seed.Products.Add(new Product
            {
                Id = productId,
                TenantId = LegacyDefaultTenantIds.Primary,
                Name = "Item",
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
            seed.Customers.Add(new Customer
            {
                Id = customerId,
                Name = "C",
                Email = "c@test",
                Phone = "1",
                IsActive = true
            });
            seed.CashRegisters.Add(new CashRegister
            {
                TenantId = LegacyDefaultTenantIds.Primary,
                Id = cashRegisterId,
                RegisterNumber = "PG-PAY-1",
                Location = "T",
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                Status = RegisterStatus.Open,
                CurrentUserId = "u1",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        await using (var closeCtx = CreateContext())
        {
            var mgr = CreateUserManagerMock();
            var shift = new CashRegisterShiftService(closeCtx, mgr.Object, Mock.Of<ILogger<CashRegisterShiftService>>(), TenantTestDoubles.PrimaryTenantResolver);
            await shift.TryCloseCashRegisterAsync(cashRegisterId, "u1", 0m, CancellationToken.None);
        }

        await using var payCtx = CreateContext();
        var paymentService = CreatePaymentServiceForLifecycleTest(payCtx);

        var request = new CreatePaymentRequest
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
            }
        };

        var result = await paymentService.CreatePaymentAsync(request, "u1");
        Assert.False(result.Success);
        Assert.Equal(CashRegisterResolutionCodes.Closed, result.DiagnosticCode);
    }

    /// <summary>
    /// Payment wiring for lifecycle tests (TSE demo mode skips device checks; FinanzOnline mock).
    /// </summary>
    private static PaymentService CreatePaymentServiceForLifecycleTest(AppDbContext ctx)
    {
        var paymentRepo = new GenericRepository<PaymentDetails>(ctx, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(ctx, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(ctx, Mock.Of<ILogger<GenericRepository<Customer>>>());

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()))
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
            CompanyName = "PG Test",
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
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(),
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _, Guid _, string reg, DateTime d) =>
                $"AT-{reg}-{d:yyyyMMdd}-{++seqCallCount}");

        var receiptService = new ReceiptService(
            ctx,
            Mock.Of<ILogger<ReceiptService>>(),
            tseMock.Object,
            Options.Create(companyProfile),
            Mock.Of<IUserService>());
        var auditMock = new Mock<IAuditLogService>();
        auditMock.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(), It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());

        var cashRegResolver = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), TenantTestDoubles.PrimaryTenantResolver);
        var httpAccessorMock = new Mock<IHttpContextAccessor>();
        httpAccessorMock.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

        return new PaymentService(
            ctx,
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
            Options.Create(companyProfile),
            Options.Create(tseOptions),
            Mock.Of<ILogger<PaymentService>>(),
            cashRegResolver,
            httpAccessorMock.Object,
            new PaymentMethodCatalogService(ctx, TenantTestDoubles.PrimaryTenantResolver),
            new PricingRuleResolver(ctx),
            TenantTestDoubles.PrimaryTenantResolver);
    }
}
