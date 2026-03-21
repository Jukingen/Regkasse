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
/// In-memory integration: real <see cref="CashRegisterResolutionService"/> proves the payment <strong>commit gate</strong>
/// re-reads register state inside an EF transaction after a successful pre-check snapshot could have been taken.
/// Full <see cref="PaymentService"/> temporal T1/T2 gap without sleeps is covered by <see cref="PaymentRegisterCommitGateTests"/> (Moq).
/// </summary>
public class CashRegisterPaymentCommitGateOrderingIntegrationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PayCommitOrder_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static CashRegisterResolutionService CreateService(AppDbContext ctx) =>
        new(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());

    [Fact]
    public async Task PreCheck_AllowsOpenRegister_ThenClose_BeforeCommitGate_CommitGate_RejectsClosed()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            RegisterNumber = "K01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = new ClaimsPrincipal();

        var pre = await svc.ValidatePaymentRegisterAsync("u1", regId, principal);
        Assert.True(pre.Ok);

        var reg = await ctx.CashRegisters.FirstAsync(r => r.Id == regId);
        reg.Status = RegisterStatus.Closed;
        reg.CurrentUserId = null;
        reg.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        await using (var tx = await ctx.Database.BeginTransactionAsync())
        {
            var commit = await svc.ValidatePaymentRegisterForCommitAsync("u1", regId, principal);
            await tx.RollbackAsync();

            Assert.False(commit.Ok);
            Assert.Equal(CashRegisterResolutionCodes.Closed, commit.Code);
        }
    }

    [Fact]
    public async Task PreCheck_AllowsShiftOwner_ThenShiftMovesToOtherUser_CommitGate_RejectsForbidden()
    {
        await using var ctx = CreateContext();
        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            RegisterNumber = "K01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CurrentUserId = "u1",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var principal = new ClaimsPrincipal();

        var pre = await svc.ValidatePaymentRegisterAsync("u1", regId, principal);
        Assert.True(pre.Ok);

        var reg = await ctx.CashRegisters.FirstAsync(r => r.Id == regId);
        reg.CurrentUserId = "u2";
        reg.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        await using (var tx = await ctx.Database.BeginTransactionAsync())
        {
            var commit = await svc.ValidatePaymentRegisterForCommitAsync("u1", regId, principal);
            await tx.RollbackAsync();

            Assert.False(commit.Ok);
            Assert.Equal(CashRegisterResolutionCodes.Forbidden, commit.Code);
        }
    }
}
