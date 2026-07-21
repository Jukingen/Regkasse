using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RksvStartbelegServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Startbeleg_{Guid.NewGuid():N}")
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

    private static async Task<(Guid RegisterId, RksvSpecialReceiptService Service)> SeedAsync(AppDbContext context)
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
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()))
            .ReturnsAsync(new TseSignatureResult(RksvTestSignatures.CreateDemoCompactJws(), "prev-sb"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert-test" });

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        receiptSeqMock
            .Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync("AT-KASSE-01-20260503-1");

        var service = CreateService(context, tseMock, receiptSeqMock);
        return (regId, service);
    }

    [Fact]
    public async Task CreateStartbelegAsync_PersistsSignedZeroReceipt_AndKind()
    {
        await using var context = CreateContext();
        var (regId, service) = await SeedAsync(context);

        var resp = await service.CreateStartbelegAsync(
            new CreateStartbelegRequest
            {
                CashRegisterId = regId,
                Reason = "RKSV Start",
                CorrelationId = "pos-session-1"
            },
            "cashier-1");

        Assert.NotEqual(Guid.Empty, resp.PaymentId);
        Assert.False(string.IsNullOrEmpty(resp.ReceiptNumber));
        Assert.False(string.IsNullOrEmpty(resp.QrData));

        var payment = await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == resp.PaymentId);
        Assert.Equal(RksvSpecialReceiptKinds.Startbeleg, payment.RksvSpecialReceiptKind);
        Assert.Null(payment.RksvSpecialReceiptYear);
        Assert.Null(payment.RksvSpecialReceiptMonth);
        Assert.Equal(0m, payment.TotalAmount);
        Assert.Equal("pos-session-1", payment.CorrelationId);

        var fonRow = await context.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking()
            .SingleAsync(x => x.PaymentId == resp.PaymentId);
        Assert.Equal(RksvSpecialReceiptKinds.Startbeleg, fonRow.Kind);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending, fonRow.Status);
        Assert.Equal(regId, fonRow.CashRegisterId);
        Assert.Equal(resp.ReceiptId, fonRow.ReceiptId);
        Assert.Equal(0, fonRow.AttemptCount);

        var outbox = await context.FinanzOnlineOutboxMessages.AsNoTracking()
            .SingleAsync(x => x.MessageType == FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission);
        Assert.Equal(FinanzOnlineOutboxStatuses.Pending, outbox.Status);
        Assert.Equal("RksvSpecialReceipt", outbox.AggregateType);
        Assert.Equal(resp.ReceiptId, outbox.AggregateId);
        Assert.Contains(resp.ReceiptNumber, outbox.PayloadJson, StringComparison.Ordinal);
        Assert.Contains(resp.PaymentId.ToString("N"), outbox.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateStartbelegAsync_Duplicate_Throws()
    {
        await using var context = CreateContext();
        var (regId, service) = await SeedAsync(context);
        var req = new CreateStartbelegRequest { CashRegisterId = regId };
        await service.CreateStartbelegAsync(req, "u1");

        var ex = await Assert.ThrowsAsync<RksvOperationGuardException>(() => service.CreateStartbelegAsync(req, "u1"));
        Assert.Equal(RksvGuardErrorCodes.DuplicateStartbeleg, ex.ErrorCode);
        Assert.Contains("Startbeleg already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
