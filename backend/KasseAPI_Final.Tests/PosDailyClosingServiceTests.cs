using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosDailyClosingServiceTests
{
    private static IRksvEnvironmentService CreateRksvEnvironment() =>
        new RksvEnvironmentService(
            new ConfigurationBuilder().Build(),
            TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

    private static Mock<UserManager<ApplicationUser>> CreateUserManager(ApplicationUser user)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mgr = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mgr.Setup(m => m.FindByIdAsync(user.Id)).ReturnsAsync(user);
        return mgr;
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosDailyClose_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task GetStatus_NoActiveShift_ReturnsFalse()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var status = await svc.GetStatusAsync("cashier-1");

        Assert.False(status.HasActiveShift);
        Assert.False(status.CanClose);
    }

    [Fact]
    public async Task PerformDailyClosing_NoActiveShift_Throws()
    {
        await using var ctx = CreateContext();
        var svc = CreateService(ctx);

        var ex = await Assert.ThrowsAsync<PosDailyClosingException>(() =>
            svc.PerformDailyClosingAsync("cashier-1", Roles.Cashier, new PosDailyClosingRequest { CashCount = 100 }));

        Assert.Equal(PosDailyClosingFailureKind.NoActiveShift, ex.Kind);
    }

    [Fact]
    public async Task PerformDailyClosing_AlreadyClosedToday_Throws()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var today = DateTime.UtcNow.Date;

        ctx.CashierShifts.Add(new CashierShift
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            CashierId = userId,
            CashierName = "Max",
            StartBalance = 100m,
            StartedAt = DateTime.UtcNow.AddHours(-4),
            Status = CashierShiftStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            UserId = userId,
            ClosingDate = today,
            ClosingType = "Daily",
            TotalAmount = 10m,
            TotalTaxAmount = 2m,
            TransactionCount = 1,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var tagesabschluss = new Mock<ITagesabschlussService>();
        tagesabschluss.Setup(t => t.CanPerformClosingAsync(regId)).ReturnsAsync(false);
        tagesabschluss.Setup(t => t.GetPaymentsWithoutInvoiceCountAsync(regId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var svc = CreateService(ctx, tagesabschluss.Object);
        var ex = await Assert.ThrowsAsync<PosDailyClosingException>(() =>
            svc.PerformDailyClosingAsync(userId, Roles.Cashier, new PosDailyClosingRequest { CashCount = 100 }));

        Assert.Equal(PosDailyClosingFailureKind.AlreadyClosed, ex.Kind);
    }

    [Fact]
    public async Task PerformDailyClosing_Success_LinksShiftAndFiscalClosing()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var shiftId = Guid.NewGuid();
        var closingId = Guid.NewGuid();
        var startedAt = DateTime.UtcNow.AddHours(-3);

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        ctx.CashierShifts.Add(new CashierShift
        {
            Id = shiftId,
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            CashierId = userId,
            CashierName = "Max",
            StartBalance = 100m,
            StartedAt = startedAt,
            Status = CashierShiftStatuses.Active,
            CreatedAt = startedAt,
            IsActive = true,
        });
        ctx.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CashRegisterId = regId,
            TotalAmount = 25m,
            PaymentMethodRaw = ((int)PaymentMethod.Cash).ToString(),
            CreatedAt = startedAt.AddMinutes(5),
            IsActive = true,
            ReceiptNumber = "R-1",
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            Id = closingId,
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            UserId = userId,
            ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(
                PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified()),
            ClosingType = "Daily",
            TotalAmount = 25m,
            TotalTaxAmount = 5m,
            TransactionCount = 1,
            TseSignature = "jws.sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var tagesabschluss = new Mock<ITagesabschlussService>();
        tagesabschluss.Setup(t => t.CanPerformClosingAsync(regId)).ReturnsAsync(true);
        tagesabschluss.Setup(t => t.PerformDailyClosingAsync(userId, regId))
            .ReturnsAsync(new TagesabschlussResult
            {
                Success = true,
                ClosingId = closingId,
                ClosingDate = DateTime.UtcNow,
                TotalAmount = 25m,
                TotalTaxAmount = 5m,
                TransactionCount = 1,
                TseSignature = "jws.sig",
                Status = "Completed",
            });

        var dailySummary = new Mock<IDailyClosingService>();
        dailySummary.Setup(d => d.GenerateClosingSummaryAsync(
                LegacyDefaultTenantIds.Primary,
                regId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyClosingSummaryDto { BusinessDate = DateTime.UtcNow });

        var shiftSvc = new Mock<IPosShiftService>();
        shiftSvc.Setup(s => s.GetShiftTotalsAsync(regId, startedAt, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShiftTotalsDto { Sales = 25m, Cash = 25m, Card = 0m });

        var cashRegisterShift = new CashRegisterShiftService(
            ctx,
            CreateUserManager(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                Email = $"{userId}@test",
                FirstName = "Max",
                LastName = "Test",
            }).Object,
            Mock.Of<ILogger<CashRegisterShiftService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        var svc = new PosDailyClosingService(
            ctx,
            tagesabschluss.Object,
            shiftSvc.Object,
            cashRegisterShift,
            dailySummary.Object,
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<PosDailyClosingService>>(),
            TenantTestDoubles.HostEnvironmentReturning(Environments.Development),
            Options.Create(new TseOptions { Mode = "Fake", TseMode = "Demo" }),
            new ConfigurationBuilder().Build(),
            CreateRksvEnvironment(),
            CreateReportEnricherMock());

        var result = await svc.PerformDailyClosingAsync(
            userId,
            Roles.Cashier,
            new PosDailyClosingRequest { CashCount = 25m });

        Assert.True(result.Success);
        Assert.Equal(closingId, result.DailyClosingId);
        Assert.Equal(0m, result.Report!.Difference);
        Assert.Equal(CashierShiftStatuses.Completed, result.Shift!.Status);
        Assert.Equal(closingId, result.Shift.DailyClosingId);

        var persisted = await ctx.CashierShifts.FindAsync(shiftId);
        Assert.NotNull(persisted!.EndedAt);
        Assert.Equal(25m, persisted.CashCount);

        var register = await ctx.CashRegisters.FindAsync(regId);
        Assert.Equal(RegisterStatus.Closed, register!.Status);
        Assert.Null(register.CurrentUserId);
    }

    [Fact]
    public async Task GetStatus_AlreadyClosedToday_ReturnsBlockReason()
    {
        await using var ctx = CreateContext();
        const string userId = "cashier-1";
        var regId = Guid.NewGuid();
        var viennaToday = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = regId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        ctx.CashierShifts.Add(new CashierShift
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            CashierId = userId,
            CashierName = "Max",
            StartBalance = 0m,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            Status = CashierShiftStatuses.Active,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = regId,
            UserId = userId,
            ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaToday),
            ClosingType = "Daily",
            TotalAmount = 10m,
            TotalTaxAmount = 2m,
            TransactionCount = 1,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var tagesabschluss = new Mock<ITagesabschlussService>();
        tagesabschluss.Setup(t => t.GetLastClosingDateAsync(regId))
            .ReturnsAsync(PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(viennaToday));
        tagesabschluss.Setup(t => t.GetLastClosingPerformedAtForTypeAsync(regId, "Daily"))
            .ReturnsAsync(DateTime.UtcNow);
        tagesabschluss.Setup(t => t.GetPaymentsWithoutInvoiceCountAsync(regId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(0);

        var svc = CreateService(ctx, tagesabschluss.Object);
        var status = await svc.GetStatusAsync(userId);

        Assert.True(status.HasActiveShift);
        Assert.False(status.CanClose);
        Assert.Equal(PosDailyClosingBlockReasons.AlreadyClosedToday, status.BlockReason);
        Assert.NotNull(status.LastClosingPerformedAt);
    }

    private static PosDailyClosingService CreateService(AppDbContext ctx, ITagesabschlussService? tagesabschluss = null, string cashierUserId = "cashier-1")
    {
        var tages = tagesabschluss ?? CreateDefaultTagesabschlussService();
        var cashRegisterShift = new CashRegisterShiftService(
            ctx,
            CreateUserManager(new ApplicationUser
            {
                Id = cashierUserId,
                UserName = cashierUserId,
                Email = $"{cashierUserId}@test",
                FirstName = "Max",
                LastName = "Test",
            }).Object,
            Mock.Of<ILogger<CashRegisterShiftService>>(),
            TenantTestDoubles.PrimaryTenantResolver,
            RksvStartbelegTestDoubles.GateOff(),
            RksvMonatsbelegTestDoubles.GateOff());

        return new PosDailyClosingService(
            ctx,
            tages,
            Mock.Of<IPosShiftService>(),
            cashRegisterShift,
            Mock.Of<IDailyClosingService>(),
            TenantTestDoubles.PrimaryTenantResolver,
            Mock.Of<IAuditLogService>(),
            Mock.Of<ILogger<PosDailyClosingService>>(),
            TenantTestDoubles.HostEnvironmentReturning(Environments.Development),
            Options.Create(new TseOptions { Mode = "Fake", TseMode = "Demo" }),
            new ConfigurationBuilder().Build(),
            CreateRksvEnvironment(),
            CreateReportEnricherMock());
    }

    private static ITagesabschlussReportEnricher CreateReportEnricherMock()
    {
        var mock = new Mock<ITagesabschlussReportEnricher>();
        mock.Setup(e => e.BuildContextAsync(It.IsAny<DailyClosing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TagesabschlussCloudContext
            {
                CompanyName = "Test GmbH",
                CompanyAddress = "Wien",
                CompanyVatId = "ATU99999999",
                TseProviderLabel = "fiskaly Cloud-HSM",
                DepExportStatusLabel = "Ausstehend",
            });
        return mock.Object;
    }

    private static ITagesabschlussService CreateDefaultTagesabschlussService()
    {
        var mock = new Mock<ITagesabschlussService>();
        mock.Setup(t => t.GetLastClosingPerformedAtForTypeAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((DateTime?)null);
        return mock.Object;
    }
}
