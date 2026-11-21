using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class AuditLogFullTextSearchPostgreSqlTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public AuditLogFullTextSearchPostgreSqlTests(PostgreSqlReplayFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Search_ReturnsTheMatchingActionDescriptionAndEmail()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        var tenantId = Guid.NewGuid();
        var userId = "fts-" + Guid.NewGuid().ToString("N");
        await using var db = CreateContext(tenantId);
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Search Cafe",
            Slug = "fts-" + tenantId.ToString("N")[..8],
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = "max.drawer@test.at",
            NormalizedEmail = "MAX.DRAWER@TEST.AT",
            FirstName = "Max",
            LastName = "Drawer",
            IsActive = true,
        });
        db.AuditLogs.Add(Row(tenantId, userId, "USER_LOGIN", "opened the cash drawer"));
        db.AuditLogs.Add(Row(tenantId, "other-actor", "PAYMENT", "cash sale", "erika@other.at"));
        await db.SaveChangesAsync();

        var byDescription = await db.AuditLogs
            .ApplyFilters(new AuditLogQueryFilters { Search = "drawer" }, db)
            .ToListAsync();
        Assert.Single(byDescription);
        Assert.Equal("USER_LOGIN", byDescription[0].Action);

        var byAction = await db.AuditLogs
            .ApplyFilters(new AuditLogQueryFilters { Search = "login" }, db)
            .ToListAsync();
        Assert.Contains(byAction, row => row.Action == "USER_LOGIN");
        Assert.DoesNotContain(byAction, row => row.Action == "PAYMENT");

        var byEmail = await db.AuditLogs
            .ApplyFilters(new AuditLogQueryFilters { Search = "max.drawer@test.at" }, db)
            .ToListAsync();
        Assert.Contains(byEmail, row => row.UserId == userId);
        Assert.DoesNotContain(byEmail, row => row.Action == "PAYMENT");
    }

    private AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static AuditLog Row(
        Guid tenantId,
        string userId,
        string action,
        string description,
        string? email = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserEmail = email,
            UserRole = "Manager",
            Action = action,
            EntityType = "User",
            Description = description,
            Status = AuditLogStatus.Success,
            Timestamp = DateTime.UtcNow,
            SessionId = Guid.NewGuid().ToString("N"),
        };
}
