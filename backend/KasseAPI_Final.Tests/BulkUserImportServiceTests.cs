using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BulkUserImportServiceTests
{
    private static readonly Guid CafeTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"BulkImport_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    private static async Task SeedTenantAndRolesAsync(AppDbContext db, RoleManager<IdentityRole> roleManager)
    {
        db.Tenants.Add(new Tenant
        {
            Id = CafeTenantId,
            Name = "Cafe",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        foreach (var role in new[] { Roles.Manager, Roles.Cashier, Roles.Accountant })
        {
            if (await roleManager.FindByNameAsync(role).ConfigureAwait(false) == null)
                await roleManager.CreateAsync(new IdentityRole(role)).ConfigureAwait(false);
        }
    }

    private static BulkUserImportService CreateService(AppDbContext db)
    {
        var userManager = CreateUserManager(db);
        var roleManager = new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(db),
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!);

        var uniqueness = new UserUniquenessValidationService(userManager);
        var userCreation = new UserCreationService(db, userManager, uniqueness);
        var membershipProvisioner = new Mock<IUserTenantMembershipProvisioner>();
        membershipProvisioner
            .Setup(m => m.ProvisionActiveMembershipAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tenantUsers = new TenantUserService(
            db,
            userManager,
            membershipProvisioner.Object,
            uniqueness,
            Mock.Of<IUserSessionInvalidation>(),
            Mock.Of<IQuickUserGeneratorService>(),
            userCreation,
            Mock.Of<IAuditLogService>(),
            Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(),
            NullCurrentTenantAccessor.Instance,
            ActivityEventTestSupport.CreateRecorder(),
            Mock.Of<IUserRoleChangeService>(),
            Mock.Of<ILogger<TenantUserService>>());

        var resultStore = new Mock<IBulkUserImportResultStore>();
        resultStore
            .Setup(r => r.SaveResultCsvAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("result123");

        return new BulkUserImportService(
            db,
            roleManager,
            tenantUsers,
            uniqueness,
            resultStore.Object,
            Mock.Of<ILogger<BulkUserImportService>>());
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            new List<IUserValidator<ApplicationUser>>(),
            new List<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());
    }

    [Fact]
    public async Task RunJobAsync_ValidCsv_CreatesUser()
    {
        await using var db = CreateContext();
        var roleManager = new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(db),
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!);
        await SeedTenantAndRolesAsync(db, roleManager);

        var service = CreateService(db);
        var csv = "email,username,firstName,lastName,role,tenantSlug\n" +
                  "import.test@example.com,,Anna,Test,Cashier,cafe\n";
        var (rows, _) = BulkUserImportFileParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "users.csv");

        var job = new BulkImportJobEntry
        {
            JobId = "test",
            TotalRows = rows.Count,
            Rows = rows,
            Actor = new BulkImportActorContext("actor-1", Roles.SuperAdmin, true, null),
        };

        await service.RunJobAsync(job);

        Assert.Equal(BulkImportJobStatus.Completed, job.Status);
        Assert.Equal(1, job.SuccessCount);
        Assert.Empty(job.AllErrors());
    }

    [Fact]
    public void Preview_ReturnsFirstRows()
    {
        var csv = "email,role,tenantSlug\n" +
                  string.Join('\n', Enumerable.Range(1, 15).Select(i => $"user{i}@test.com,Cashier,cafe"));
        using var db = CreateContext();
        var service = CreateService(db);

        var preview = service.Preview(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "users.csv", 10);

        Assert.Equal(15, preview.TotalRows);
        Assert.Equal(10, preview.PreviewRows.Count);
    }

    [Fact]
    public async Task RunJobAsync_DuplicateEmailInFile_ReportsError()
    {
        await using var db = CreateContext();
        var roleManager = new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(db),
            null!,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!);
        await SeedTenantAndRolesAsync(db, roleManager);

        var service = CreateService(db);
        var csv = "email,role,tenantSlug\n" +
                  "dup@example.com,Cashier,cafe\n" +
                  "dup@example.com,Manager,cafe\n";
        var (rows, _) = BulkUserImportFileParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "users.csv");

        var job = new BulkImportJobEntry
        {
            JobId = "dup",
            TotalRows = rows.Count,
            Rows = rows,
            Actor = new BulkImportActorContext("actor-1", Roles.SuperAdmin, true, null),
        };

        await service.RunJobAsync(job);

        Assert.Equal(1, job.SuccessCount);
        Assert.Contains(job.AllErrors(), e => e.Error.Contains("Duplicate email", StringComparison.OrdinalIgnoreCase));
    }
}
