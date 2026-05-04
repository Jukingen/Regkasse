using System.ComponentModel.DataAnnotations;
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

public class RksvNullbelegServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Nullbeleg_{Guid.NewGuid():N}")
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

    private static async Task<(Guid RegisterId, RksvSpecialReceiptService Service, Mock<ITseService> Tse, Mock<IReceiptSequenceService> Seq)> SeedAndBuildAsync(AppDbContext context)
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
            Status = RegisterStatus.Open,
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
            .ReturnsAsync(new TseSignatureResult("eyJhbGciOiJFUzI1NiJ9.eyJ.test.sign", "chain-prev"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert-test" });

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        receiptSeqMock
            .Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync("AT-KASSE-01-20260131-42");

        var service = CreateService(context, tseMock, receiptSeqMock);
        return (regId, service, tseMock, receiptSeqMock);
    }

    [Fact]
    public async Task CreateNullbelegAsync_PersistsPaymentInvoiceReceipt_AndMarksKind()
    {
        await using var context = CreateContext();
        var (regId, service, tseMock, receiptSeqMock) = await SeedAndBuildAsync(context);

        var anchor = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Unspecified);
        var req = new CreateNullbelegRequest
        {
            CashRegisterId = regId,
            Year = 2026,
            Month = 1,
            Reason = "Monats-Nullbeleg"
        };

        var resp = await service.CreateNullbelegAsync(req, "manager-1");

        Assert.NotEqual(Guid.Empty, resp.PaymentId);
        Assert.NotEqual(Guid.Empty, resp.InvoiceId);
        Assert.NotEqual(Guid.Empty, resp.ReceiptId);
        Assert.Equal("AT-KASSE-01-20260131-42", resp.ReceiptNumber);
        Assert.False(resp.ActsAsJahresbeleg);

        var payment = await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == resp.PaymentId);
        Assert.Equal(RksvSpecialReceiptKinds.Nullbeleg, payment.RksvSpecialReceiptKind);
        Assert.Equal(2026, payment.RksvSpecialReceiptYear);
        Assert.Equal(1, payment.RksvSpecialReceiptMonth);
        Assert.Equal(0m, payment.TotalAmount);
        Assert.Equal(0m, payment.TaxAmount);
        Assert.Equal("[]", payment.PaymentItems.RootElement.GetRawText());

        var invoice = await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == resp.InvoiceId);
        Assert.Equal(payment.Id, invoice.SourcePaymentId);
        Assert.Equal(0m, invoice.TotalAmount);

        var receipt = await context.Receipts.AsNoTracking().FirstAsync(r => r.ReceiptId == resp.ReceiptId);
        Assert.Equal("AT-KASSE-01-20260131-42", receipt.ReceiptNumber);
        Assert.False(string.IsNullOrEmpty(receipt.QrCodePayload));
        Assert.Equal("chain-prev", receipt.PrevSignatureValue);

        receiptSeqMock.Verify(
            x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                regId,
                "KASSE-01",
                anchor),
            Times.Once);

        tseMock.Verify(
            x => x.CreateInvoiceSignatureAsync(
                regId,
                "AT-KASSE-01-20260131-42",
                0m,
                "KASSE-01",
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateNullbelegAsync_December_DefaultsActsAsJahresbeleg()
    {
        await using var context = CreateContext();
        var (regId, service, _, receiptSeqMock) = await SeedAndBuildAsync(context);
        receiptSeqMock
            .Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync("AT-KASSE-01-20261231-1");

        var resp = await service.CreateNullbelegAsync(
            new CreateNullbelegRequest { CashRegisterId = regId, Year = 2026, Month = 12 },
            "manager-1");

        Assert.True(resp.ActsAsJahresbeleg);
        var payment = await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == resp.PaymentId);
        Assert.True(payment.RksvNullbelegActsAsJahresbeleg);
    }

    [Fact]
    public async Task CreateNullbelegAsync_DuplicateMonth_Throws()
    {
        await using var context = CreateContext();
        var (regId, service, _, _) = await SeedAndBuildAsync(context);

        var req = new CreateNullbelegRequest { CashRegisterId = regId, Year = 2026, Month = 3 };
        await service.CreateNullbelegAsync(req, "u1");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateNullbelegAsync(req, "u1"));
        Assert.Contains("Nullbeleg already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreatePaymentRequest_ZeroTotalAmount_FailsDataAnnotations()
    {
        var req = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 1, TaxType = TaxType.Standard }
            },
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            TableNumber = 1,
            TotalAmount = 0m,
            CashRegisterId = Guid.NewGuid(),
            Steuernummer = "ATU12345678"
        };
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(req, new ValidationContext(req), results, validateAllProperties: true);
        Assert.False(ok);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreatePaymentRequest.TotalAmount)));
    }
}
