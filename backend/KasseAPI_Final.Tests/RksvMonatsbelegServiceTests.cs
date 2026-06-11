using KasseAPI_Final.Constants;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.FinanzOnlineIntegration;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class RksvMonatsbelegServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Monatsbeleg_{Guid.NewGuid():N}")
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
            TenantTestDoubles.PrimaryTenantResolver);

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
            new FinanzOnlineOutboxService(context, new Mock<ILogger<FinanzOnlineOutboxService>>().Object));
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
                new TseSignatureResult("eyJhbGciOiJFUzI1NiJ9.eyJ.test.monat", prev ?? "chain-root"));
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
                return $"AT-KASSE-01-{DateTime.UtcNow:yyyyMMdd}-{n}";
            });

        var service = CreateService(context, tseMock, receiptSeqMock);
        return (regId, service, receiptSeqMock);
    }

    [Fact]
    public async Task CreateMonatsbelegAsync_PersistsSignedZeroReceipt_AndKind_AndPeriod()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var (y, m) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();

        var resp = await service.CreateMonatsbelegAsync(
            new CreateMonatsbelegRequest
            {
                CashRegisterId = regId,
                Year = y,
                Month = m,
                Reason = "RKSV Monat"
            },
            "cashier-1");

        Assert.NotEqual(Guid.Empty, resp.PaymentId);
        Assert.False(string.IsNullOrEmpty(resp.ReceiptNumber));

        var payment = await context.PaymentDetails.AsNoTracking().FirstAsync(p => p.Id == resp.PaymentId);
        if (m == 12)
        {
            Assert.Equal(RksvSpecialReceiptKinds.Jahresbeleg, payment.RksvSpecialReceiptKind);
            Assert.Equal(y, payment.RksvSpecialReceiptYear);
            Assert.Null(payment.RksvSpecialReceiptMonth);
        }
        else
        {
            Assert.Equal(RksvSpecialReceiptKinds.Monatsbeleg, payment.RksvSpecialReceiptKind);
            Assert.Equal(y, payment.RksvSpecialReceiptYear);
            Assert.Equal(m, payment.RksvSpecialReceiptMonth);
        }

        Assert.Equal(0m, payment.TotalAmount);
        Assert.False(string.IsNullOrEmpty(payment.PrevSignatureValueUsed));
    }

    [Fact]
    public async Task CreateMonatsbelegAsync_DuplicateMonth_Throws()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var (y, m) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var req = new CreateMonatsbelegRequest { CashRegisterId = regId, Year = y, Month = m };
        await service.CreateMonatsbelegAsync(req, "u1");

        var ex = await Assert.ThrowsAsync<RksvOperationGuardException>(() => service.CreateMonatsbelegAsync(req, "u1"));
        if (m == 12)
        {
            Assert.Equal(RksvGuardErrorCodes.DuplicateJahresbeleg, ex.ErrorCode);
            Assert.Contains("Jahresbeleg", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Equal(RksvGuardErrorCodes.DuplicateMonatsbeleg, ex.ErrorCode);
            Assert.Contains("Monatsbeleg already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CreateMonatsbelegAsync_PastMonthWithoutForce_ThrowsRequiresForce()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var (year, month) = GetPreviousViennaMonth();

        var ex = await Assert.ThrowsAsync<RksvOperationGuardException>(() =>
            service.CreateMonatsbelegAsync(
                new CreateMonatsbelegRequest { CashRegisterId = regId, Year = year, Month = month },
                "u1"));
        Assert.Equal(RksvGuardErrorCodes.MonatsbelegPastMonthRequiresForce, ex.ErrorCode);
    }

    [Fact]
    public async Task CreateMonatsbelegAsync_PastMonthWithForce_Succeeds()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var (year, month) = GetPreviousViennaMonth();

        var resp = await service.CreateMonatsbelegAsync(
            new CreateMonatsbelegRequest { CashRegisterId = regId, Year = year, Month = month, Reason = "Admin catch-up" },
            "u1",
            forcePastMonth: true);

        Assert.NotEqual(Guid.Empty, resp.PaymentId);
        var row = await context.PaymentDetails.AsNoTracking()
            .SingleAsync(p => p.Id == resp.PaymentId);
        Assert.Equal(RksvSpecialReceiptKinds.Monatsbeleg, row.RksvSpecialReceiptKind);
        Assert.Equal(year, row.RksvSpecialReceiptYear);
        Assert.Equal(month, row.RksvSpecialReceiptMonth);
    }

    [Fact]
    public async Task CreateMonatsbelegAsync_FutureMonth_ThrowsEvenWithForce()
    {
        await using var context = CreateContext();
        var (regId, service, _) = await SeedAsync(context);
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var futureMonth = month == 12 ? 1 : month + 1;
        var futureYear = month == 12 ? year + 1 : year;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateMonatsbelegAsync(
                new CreateMonatsbelegRequest { CashRegisterId = regId, Year = futureYear, Month = futureMonth },
                "u1",
                forcePastMonth: true));
        Assert.Contains("future Vienna calendar month", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static (int Year, int Month) GetPreviousViennaMonth()
    {
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        if (month == 1)
            return (year - 1, 12);
        return (year, month - 1);
    }

    [Fact]
    public async Task GetStaffPerformanceAsync_ExcludesMonatsbelegFromRowCount()
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
            TseSignature = "sig-m",
            TseTimestamp = t0,
            TaxDetails = System.Text.Json.JsonDocument.Parse("{}"),
            PaymentItems = System.Text.Json.JsonDocument.Parse("[]"),
            ReceiptNumber = "R-M-1",
            CreatedAt = t0,
            CreatedBy = "c1",
            IsActive = true,
            RksvSpecialReceiptKind = RksvSpecialReceiptKinds.Monatsbeleg,
            RksvSpecialReceiptYear = t0.Year,
            RksvSpecialReceiptMonth = t0.Month
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
