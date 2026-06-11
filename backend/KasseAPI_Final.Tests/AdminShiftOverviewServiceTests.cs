using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminShiftOverviewServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminShiftOverview_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task GetOverview_SplitsActiveHistoryAndClosings()
    {
        await using var ctx = CreateContext();
        var tenantId = LegacyDefaultTenantIds.Primary;
        var regId = Guid.NewGuid();
        var closingId = Guid.NewGuid();

        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        ctx.CashierShifts.Add(new CashierShift
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            CashierId = "c1",
            CashierName = "Active Cashier",
            StartBalance = 50m,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            Status = CashierShiftStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        ctx.CashierShifts.Add(new CashierShift
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = regId,
            CashierId = "c2",
            CashierName = "Done Cashier",
            StartBalance = 40m,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            EndedAt = DateTime.UtcNow.AddHours(-20),
            TotalSales = 100m,
            Status = CashierShiftStatuses.Completed,
            DailyClosingId = closingId,
            CashCount = 90m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        ctx.DailyClosings.Add(new DailyClosing
        {
            Id = closingId,
            TenantId = tenantId,
            CashRegisterId = regId,
            UserId = "c2",
            ClosingDate = DateTime.UtcNow.Date,
            ClosingType = "Daily",
            TotalAmount = 100m,
            TotalTaxAmount = 16.67m,
            TransactionCount = 5,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });

        await ctx.SaveChangesAsync();

        var svc = new AdminShiftOverviewService(ctx);
        var overview = await svc.GetOverviewAsync(tenantId, null, null, null);

        Assert.Single(overview.ActiveShifts);
        Assert.Equal("Active Cashier", overview.ActiveShifts[0].CashierName);
        Assert.Single(overview.ShiftHistory);
        Assert.Equal("Done Cashier", overview.ShiftHistory[0].CashierName);
        Assert.Single(overview.DailyClosings);
        Assert.Equal(closingId, overview.DailyClosings[0].DailyClosingId);
        Assert.Equal("K1", overview.DailyClosings[0].RegisterNumber);
        Assert.True(overview.DailyClosings[0].HasTseSignature);
    }
}
