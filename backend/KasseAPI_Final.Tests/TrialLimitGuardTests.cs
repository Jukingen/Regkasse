using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Trial;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TrialLimitGuardTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"trial_limit_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static TrialLimitGuard CreateGuard(AppDbContext db, TrialOptions? opts = null)
    {
        var monitor = Mock.Of<IOptionsMonitor<TrialOptions>>(m =>
            m.CurrentValue == (opts ?? new TrialOptions { MaxRegistersInTrial = 1, MaxUsersInTrial = 2 }));
        return new TrialLimitGuard(db, monitor);
    }

    [Fact]
    public async Task EnsureCanCreateCashRegisterAsync_Throws_WhenAtLimit()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T",
            Slug = "t-limit",
            Status = TenantStatuses.Active,
            IsActive = true,
            TrialStatus = TrialStatuses.Active,
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(5),
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegisterNumber = "KASSE-001",
            Location = "Main",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<TrialLimitExceededException>(() =>
            CreateGuard(db).EnsureCanCreateCashRegisterAsync(tenantId));
        Assert.Equal(TrialLimitExceededException.ErrorCodeValue, ex.ErrorCode);
        Assert.Equal("registers", ex.LimitKind);
    }

    [Fact]
    public async Task EnsureCanCreateUserAsync_Allows_WhenUnderLimit()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T2",
            Slug = "t2-limit",
            Status = TenantStatuses.Active,
            IsActive = true,
            TrialStatus = TrialStatuses.Active,
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(5),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await CreateGuard(db, new TrialOptions { MaxUsersInTrial = 3 })
            .EnsureCanCreateUserAsync(tenantId);
    }
}
