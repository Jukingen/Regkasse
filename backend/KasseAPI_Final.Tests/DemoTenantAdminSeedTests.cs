using System.IO;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DemoTenantAdminSeedTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DemoTenantSeed_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    private static async Task SeedRolesAsync(AppDbContext db)
    {
        foreach (var role in Roles.Canonical)
        {
            db.Roles.Add(new IdentityRole
            {
                Id = Guid.NewGuid().ToString("D"),
                Name = role,
                NormalizedName = role.ToUpperInvariant(),
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedAsync_Creates_Tenants_Admins_And_Owner_Memberships()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var provisioner = new UserTenantMembershipProvisioner(db);

        await DemoTenantAdminSeed.SeedAsync(db, userManager, provisioner);

        foreach (var slug in new[] { "dev", "cafe", "bar" })
        {
            var tenant = await db.Tenants.SingleAsync(t => t.Slug == slug);
            var email = $"admin@{slug}.regkasse.at";
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.Equal(Roles.Manager, user!.Role);

            var membership = await db.UserTenantMemberships.SingleAsync(m =>
                m.UserId == user.Id && m.TenantId == tenant.Id && m.IsActive);
            Assert.True(membership.IsOwner);
        }
    }

    [Fact]
    public async Task SeedAsync_Is_Idempotent()
    {
        await using var db = CreateDb();
        await SeedRolesAsync(db);
        var userManager = CreateUserManager(db);
        var provisioner = new UserTenantMembershipProvisioner(db);

        await DemoTenantAdminSeed.SeedAsync(db, userManager, provisioner);
        await DemoTenantAdminSeed.SeedAsync(db, userManager, provisioner);

        Assert.Equal(3, await db.Tenants.CountAsync(t => DemoTenantIds.All.Contains(t.Id) || t.Slug == "dev" || t.Slug == "cafe" || t.Slug == "bar"));
        Assert.Equal(3, await db.UserTenantMemberships.CountAsync(m => m.IsActive && m.IsOwner));
    }

    [Fact]
    public void Seed_Sql_Script_Exists_And_Is_Idempotent()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "scripts", "seed-demo-tenant-admins.sql");
        Assert.True(File.Exists(path), $"Expected {path}");

        var text = File.ReadAllText(path);
        Assert.Contains("admin@cafe.regkasse.at", text, StringComparison.Ordinal);
        Assert.Contains("user_tenant_memberships", text, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(DemoTenantIds.Cafe.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CashRegister_Seed_Sql_Script_Exists_And_Targets_Demo_Tenants()
    {
        var repoRoot = FindRepoRoot();
        var path = Path.Combine(repoRoot, "scripts", "seed-demo-cash-registers.sql");
        Assert.True(File.Exists(path), $"Expected {path}");

        var text = File.ReadAllText(path);
        Assert.Contains("cash_registers", text, StringComparison.Ordinal);
        Assert.Contains("'default'", text, StringComparison.Ordinal);
        Assert.Contains("'cafe'", text, StringComparison.Ordinal);
        Assert.Contains("'bar'", text, StringComparison.Ordinal);
        Assert.Contains("'test'", text, StringComparison.Ordinal);
        Assert.Contains("'dev'", text, StringComparison.Ordinal);
        Assert.Contains("'KASSE-001'", text, StringComparison.Ordinal);
        Assert.Contains("'Hauptkasse'", text, StringComparison.Ordinal);
        Assert.Contains("\"Status\"", text, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", text, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var script = Path.Combine(dir.FullName, "scripts", "seed-demo-tenant-admins.sql");
                if (File.Exists(script))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
