using System.Security.Claims;
using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RksvSchlussbelegServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Schlussbeleg_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
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

        var loggerReceipt = new Mock<ILogger<ReceiptService>>().Object;
        var receiptService = new ReceiptService(
            context,
            loggerReceipt,
            tseMock.Object,
            Options.Create(companyProfile),
            Mock.Of<IUserService>(),
            TenantTestDoubles.PrimaryTenantResolver);

        return new RksvSpecialReceiptService(
            context,
            tseMock.Object,
            receiptSeqMock.Object,
            receiptService,
            TenantTestDoubles.PrimaryTenantResolver,
            Options.Create(companyProfile),
            Options.Create(tseOptions),
            new Mock<ILogger<RksvSpecialReceiptService>>().Object,
            new RksvSpecialReceiptFinanzOnlineSubmissionTracker(context),
            new FinanzOnlineOutboxService(context, new Mock<ILogger<FinanzOnlineOutboxService>>().Object));
    }

    private static async Task<(Guid RegisterId, RksvSpecialReceiptService Service, Mock<ITseService> Tse, Mock<IReceiptSequenceService> Seq)> SeedAsync(
        AppDbContext context,
        RegisterStatus status,
        string? currentUserId = null)
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
            Status = status,
            CurrentUserId = currentUserId,
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
            .ReturnsAsync(new TseSignatureResult("eyJhbGciOiJFUzI1NiJ9.eyJ.test.schluss", "chain-prev-schluss"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert-test" });

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        receiptSeqMock
            .Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync("AT-KASSE-01-20260503-99");

        var service = CreateService(context, tseMock, receiptSeqMock);
        return (regId, service, tseMock, receiptSeqMock);
    }

    [Fact]
    public async Task CreateSchlussbelegAsync_OpenShift_Throws()
    {
        await using var context = CreateContext();
        var (regId, service, _, _) = await SeedAsync(context, RegisterStatus.Open, currentUserId: "u1");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSchlussbelegAsync(new CreateSchlussbelegRequest { CashRegisterId = regId }, "manager-1"));

        Assert.Contains("open shift", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSchlussbelegAsync_Closed_CreatesSignedZero_AndDecommissionsRegister()
    {
        await using var context = CreateContext();
        var (regId, service, tseMock, receiptSeqMock) = await SeedAsync(context, RegisterStatus.Closed);

        var resp = await service.CreateSchlussbelegAsync(
            new CreateSchlussbelegRequest { CashRegisterId = regId, Reason = "Außerbetriebnahme" },
            "manager-1");

        Assert.NotEqual(Guid.Empty, resp.PaymentId);
        Assert.Equal("AT-KASSE-01-20260503-99", resp.ReceiptNumber);
        Assert.False(string.IsNullOrEmpty(resp.QrData));

        var reg = await context.CashRegisters.AsNoTracking().FirstAsync(r => r.Id == regId);
        Assert.Equal(RegisterStatus.Decommissioned, reg.Status);
        Assert.Null(reg.CurrentUserId);

        var payment = await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == resp.PaymentId);
        Assert.Equal(RksvSpecialReceiptKinds.Schlussbeleg, payment.RksvSpecialReceiptKind);
        Assert.Equal(0m, payment.TotalAmount);
        Assert.False(string.IsNullOrEmpty(payment.TseSignature));

        var receipt = await context.Receipts.AsNoTracking().FirstAsync(r => r.PaymentId == resp.PaymentId);
        Assert.Equal("chain-prev-schluss", receipt.PrevSignatureValue);

        tseMock.Verify(
            x => x.CreateInvoiceSignatureAsync(
                regId,
                "AT-KASSE-01-20260503-99",
                0m,
                "KASSE-01",
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);

        receiptSeqMock.Verify(
            x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                regId,
                "KASSE-01",
                It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSchlussbelegAsync_SecondCall_Rejected_AlreadyDecommissioned()
    {
        await using var context = CreateContext();
        var (regId, service, _, _) = await SeedAsync(context, RegisterStatus.Closed);

        await service.CreateSchlussbelegAsync(new CreateSchlussbelegRequest { CashRegisterId = regId }, "manager-1");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSchlussbelegAsync(new CreateSchlussbelegRequest { CashRegisterId = regId }, "manager-1"));

        Assert.Contains("decommissioned", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateSchlussbelegAsync_DuplicateSchlussbelegRow_Rejected()
    {
        await using var context = CreateContext();
        var (regId, service, _, _) = await SeedAsync(context, RegisterStatus.Closed);

        await service.CreateSchlussbelegAsync(new CreateSchlussbelegRequest { CashRegisterId = regId }, "manager-1");

        // Simulate inconsistent state: Schlussbeleg row exists but register not yet decommissioned (guarded in service).
        var reg = await context.CashRegisters.FirstAsync(r => r.Id == regId);
        reg.Status = RegisterStatus.Closed;
        reg.CurrentUserId = null;
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateSchlussbelegAsync(new CreateSchlussbelegRequest { CashRegisterId = regId }, "manager-1"));

        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AfterSchlussbeleg_ValidatePaymentRegisterAsync_RejectedDecommissioned()
    {
        await using var context = CreateContext();
        var (regId, service, _, _) = await SeedAsync(context, RegisterStatus.Closed);
        await service.CreateSchlussbelegAsync(new CreateSchlussbelegRequest { CashRegisterId = regId }, "manager-1");

        var resolution = new CashRegisterResolutionService(
            context,
            Mock.Of<ILogger<CashRegisterResolutionService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        var gate = await resolution.ValidatePaymentRegisterAsync("u1", regId, new ClaimsPrincipal(), CancellationToken.None);

        Assert.False(gate.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Decommissioned, gate.Code);
    }
}
