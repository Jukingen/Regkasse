using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RksvJahresbelegServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Jahresbeleg_{Guid.NewGuid():N}")
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

    private static async Task<(Guid RegisterId, RksvSpecialReceiptService Service, Mock<IReceiptSequenceService> Seq)> SeedAsync(AppDbContext context)
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
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()))
            .ReturnsAsync((Guid _, string _, decimal _, string _, string? prev, DateTime? _, string? _, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _) =>
                new TseSignatureResult(RksvTestSignatures.CreateDemoCompactJws(), prev ?? "chain-root"));
        tseMock.Setup(x => x.GetTseCertificateInfoAsync(It.IsAny<string>()))
            .ReturnsAsync(new TseCertificateInfo { CertificateNumber = "cert-test" });

        var receiptSeqMock = new Mock<IReceiptSequenceService>();
        var n = 0;
        receiptSeqMock
            .Setup(x => x.AllocateNextBelegNrInTransactionAsync(
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .ReturnsAsync(() =>
            {
                n++;
                return $"AT-KASSE-01-{DateTime.UtcNow:yyyyMMdd}-J{n}";
            });

        var service = CreateService(context, tseMock, receiptSeqMock);
        return (regId, service, receiptSeqMock);
    }

    [Fact]
    public async Task CreateJahresbelegAsync_PersistsSignedZeroReceipt_AndKind_AndYearOnly()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var (y, _) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();

        var resp = await service.CreateJahresbelegAsync(
            new CreateJahresbelegRequest
            {
                CashRegisterId = regId,
                Year = y,
                Reason = "Test Jahresbeleg",
                EarlyReason = null,
            },
            "u1");

        Assert.NotEqual(Guid.Empty, resp.PaymentId);
        Assert.False(string.IsNullOrEmpty(resp.ReceiptNumber));

        var payment = await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == resp.PaymentId);
        Assert.Equal(RksvSpecialReceiptKinds.Jahresbeleg, payment.RksvSpecialReceiptKind);
        Assert.Equal(y, payment.RksvSpecialReceiptYear);
        Assert.Null(payment.RksvSpecialReceiptMonth);
        Assert.True(payment.RksvNullbelegActsAsJahresbeleg);
        Assert.Equal(0m, payment.TotalAmount);
        Assert.False(string.IsNullOrEmpty(payment.PrevSignatureValueUsed));

        var fonRow = await context.RksvSpecialReceiptFinanzOnlineSubmissions.AsNoTracking()
            .SingleAsync(x => x.PaymentId == resp.PaymentId);
        Assert.Equal(RksvSpecialReceiptKinds.Jahresbeleg, fonRow.Kind);
        Assert.Equal(RksvSpecialReceiptFinanzOnlineSubmissionStatuses.Pending, fonRow.Status);
        Assert.Equal(regId, fonRow.CashRegisterId);
        Assert.Equal(resp.ReceiptId, fonRow.ReceiptId);

        var outbox = await context.FinanzOnlineOutboxMessages.AsNoTracking()
            .SingleAsync(x => x.MessageType == FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvJahresbelegSubmission);
        Assert.Equal(FinanzOnlineOutboxStatuses.Pending, outbox.Status);
        Assert.Equal("RksvSpecialReceipt", outbox.AggregateType);
        Assert.Equal(resp.ReceiptId, outbox.AggregateId);
        Assert.Contains("Jahresbeleg", outbox.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateJahresbelegAsync_DuplicateYear_Throws()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var (y, _) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var req = new CreateJahresbelegRequest { CashRegisterId = regId, Year = y };
        await service.CreateJahresbelegAsync(req, "u1");

        var ex = await Assert.ThrowsAsync<RksvOperationGuardException>(() => service.CreateJahresbelegAsync(req, "u1"));
        Assert.Equal(RksvGuardErrorCodes.DuplicateJahresbeleg, ex.ErrorCode);
        Assert.Contains("Jahresbeleg", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateJahresbelegAsync_BlocksWhenLegacyDecemberMonatsbelegExists()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var y = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth().Year;
        context.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "Gast",
            TableNumber = 0,
            CashierId = "u1",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "legacy",
            TseTimestamp = DateTime.UtcNow,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = "LEG-M12",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "u1",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Monatsbeleg,
            RksvSpecialReceiptYear = y,
            RksvSpecialReceiptMonth = 12,
        });
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<RksvOperationGuardException>(() =>
            service.CreateJahresbelegAsync(new CreateJahresbelegRequest { CashRegisterId = regId, Year = y }, "u1"));
        Assert.Equal(RksvGuardErrorCodes.DuplicateJahresbeleg, ex.ErrorCode);
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateJahresbelegAsync_YearOutOfWindow_Throws()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var vy = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth().Year;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateJahresbelegAsync(
                new CreateJahresbelegRequest { CashRegisterId = regId, Year = vy + 2 },
                "u1"));
        Assert.Contains("Vienna year", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetStaffPerformanceAsync_ExcludesJahresbelegFromRowCount()
    {
        await using var context = CreateContext();
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
            RegisterNumber = "R1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        var t0 = DateTime.UtcNow.AddMinutes(-5);
        context.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "C",
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "sig-sale",
            TseTimestamp = t0,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = "R-SALE-1",
            CreatedAt = t0,
            CreatedBy = "c1",
            IsActive = true,
            RksvSpecialReceiptKind = null
        });
        context.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = WalkInCustomerConstants.GuestCustomerId,
            CustomerName = "C",
            TableNumber = 0,
            CashierId = "c1",
            TotalAmount = 0m,
            TaxAmount = 0m,
            PaymentMethodRaw = "0",
            Steuernummer = "ATU12345678",
            CashRegisterId = regId,
            TseSignature = "sig-j",
            TseTimestamp = t0,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = "R-J-1",
            CreatedAt = t0,
            CreatedBy = "c1",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Jahresbeleg,
            RksvSpecialReceiptYear = t0.Year,
            RksvSpecialReceiptMonth = null
        });
        await context.SaveChangesAsync();

        var reporting = new OperationalReportingService(
            context,
            new Mock<ILogger<OperationalReportingService>>().Object,
            Mock.Of<IAuditLogService>());

        // ResolveRange uses Austria calendar dates only (time ignored). UtcNow ± hours can miss t0 when Vienna local date is ahead of the UTC calendar date of those bounds.
        var reportDay = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var staff = await reporting.GetStaffPerformanceAsync(reportDay, reportDay, regId, "c1", null, true, false, CancellationToken.None);
        Assert.Single(staff.ByStaff);
        Assert.Equal(1, staff.ByStaff[0].SaleTransactionCount);
        Assert.Equal(10m, staff.ByStaff[0].GrossSalesAmount);
    }
}
