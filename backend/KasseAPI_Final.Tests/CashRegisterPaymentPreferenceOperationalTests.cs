using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Preference (<see cref="UserSettings.CashRegisterId"/>) can point at a row that is not operationally usable for payment
/// (closed register, or open on another user&apos;s shift). Distinct from snapshot &quot;assignment API allowed persist&quot; tests.
/// </summary>
public class CashRegisterPaymentPreferenceOperationalTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PrefVsOp_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ValidatePaymentRegister_PersistedPreferenceClosed_RegisterClosed_PreCheckFails()
    {
        await using var ctx = CreateContext();
        const string userId = "u1";
        var closedPref = Guid.NewGuid();
        var openOther = Guid.NewGuid();

        ctx.CashRegisters.Add(new CashRegister
        {
            Id = closedPref,
            RegisterNumber = "K-CLOSED",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = openOther,
            RegisterNumber = "K-OPEN",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CashRegisterId = closedPref.ToString(),
            Language = "de-DE",
            Currency = "EUR",
            DateFormat = "DD.MM.YYYY",
            TimeFormat = "24h",
            DefaultTaxRate = 20,
            EnableDiscounts = true,
            EnableCoupons = true,
            AutoPrintReceipts = false,
            ReceiptHeader = "h",
            ReceiptFooter = "f",
            FinanzOnlineEnabled = false,
            SessionTimeout = 30,
            RequirePinForRefunds = true,
            MaxDiscountPercentage = 50,
            Theme = "light",
            CompactMode = false,
            ShowProductImages = true,
            EnableNotifications = true,
            LowStockAlert = true,
            DefaultPaymentMethod = "mixed",
            DefaultTableNumber = "1",
            DefaultWaiterName = "K",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());
        var r = await svc.ValidatePaymentRegisterAsync(userId, closedPref, new ClaimsPrincipal());

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Closed, r.Code);
    }

    [Fact]
    public async Task ValidatePaymentRegister_PreferenceMatchesButOtherUserShift_OperationallyForbidden()
    {
        await using var ctx = CreateContext();
        const string userId = "waiter-1";
        var regId = Guid.NewGuid();

        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "cashier-2",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        ctx.UserSettings.Add(new UserSettings
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CashRegisterId = regId.ToString(),
            Language = "de-DE",
            Currency = "EUR",
            DateFormat = "DD.MM.YYYY",
            TimeFormat = "24h",
            DefaultTaxRate = 20,
            EnableDiscounts = true,
            EnableCoupons = true,
            AutoPrintReceipts = false,
            ReceiptHeader = "h",
            ReceiptFooter = "f",
            FinanzOnlineEnabled = false,
            SessionTimeout = 30,
            RequirePinForRefunds = true,
            MaxDiscountPercentage = 50,
            Theme = "light",
            CompactMode = false,
            ShowProductImages = true,
            EnableNotifications = true,
            LowStockAlert = true,
            DefaultPaymentMethod = "mixed",
            DefaultTableNumber = "1",
            DefaultWaiterName = "K",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var svc = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());
        var r = await svc.ValidatePaymentRegisterAsync(userId, regId, new ClaimsPrincipal());

        Assert.False(r.Ok);
        Assert.Equal(CashRegisterResolutionCodes.Forbidden, r.Code);
    }
}
