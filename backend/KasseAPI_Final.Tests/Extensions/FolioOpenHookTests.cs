using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Extensions;
using KasseAPI_Final.Services.Lodging;
using KasseAPI_Final.Services.VerticalProfiles;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Extensions;

public sealed class FolioOpenHookTests
{
    [Fact]
    public async Task FolioOpened_Rejection_RollsBackTheFolio()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"FolioOpenHook_{Guid.NewGuid():N}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        TenantTestDoubles.EnsureTenant(db, tenantId);
        var room = new Room
        {
            TenantId = tenantId,
            Number = "4",
            Type = "EZ",
            Capacity = 1,
            Status = RoomStatus.Available,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        var customer = new Customer
        {
            TenantId = tenantId,
            Name = "Gast",
            CustomerNumber = $"C-{Guid.NewGuid():N}"[..12],
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Rooms.Add(room);
        db.Customers.Add(customer);
        db.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = TenantExtensionSettings.EnabledKey,
            Value = "[\"folio_open_reject\"]",
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var registry = new TenantExtensionRegistry(
            [new RejectingOpenExtension()],
            TenantTestDoubles.DbContextFactoryForTests(options, TenantTestDoubles.TenantAccessorReturning(null)),
            new MemoryCache(new MemoryCacheOptions()),
            new EmptyCache(),
            Options.Create(new CacheSettings()),
            Mock.Of<IAuditLogService>(),
            NullLogger<TenantExtensionRegistry>.Instance);
        var controller = new PosRoomsController(
            new LodgingService(db, Mock.Of<IAuditLogService>(), registry),
            TenantTestDoubles.TenantAccessorReturning(tenantId),
            new PermissiveVerticalProfileGuard())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Role, Roles.Cashier)],
                        authenticationType: "test")),
                },
            },
        };

        var created = await controller.CreateFolio(
            new CreateGuestFolioRequest { CustomerId = customer.Id, RoomId = room.Id, Notes = "breakfast" },
            CancellationToken.None);

        var blocked = Assert.IsType<BadRequestObjectResult>(created.Result);
        var error = Assert.IsType<LodgingErrorDto>(blocked.Value);
        Assert.Equal(ExtensionFailedException.ErrorCode, error.Code);
        Assert.Equal(0, await db.GuestFolios.CountAsync());
        Assert.Equal(0, await db.GuestFolioItems.CountAsync());
        Assert.Equal(RoomStatus.Available, (await db.Rooms.SingleAsync()).Status);
    }

    [TenantExtensionHook(TenantExtensionHooks.FolioOpened)]
    private sealed class RejectingOpenExtension : ITenantFeatureExtension
    {
        public string FeatureName => "folio_open_reject";

        public bool AppliesTo(Guid tenantId) => tenantId != Guid.Empty;

        public Task<ExtensionResult> ExecuteAsync(ExtensionContext context, CancellationToken ct) =>
            Task.FromResult(new ExtensionResult(false, "OPEN_REJECTED"));
    }

    private sealed class EmptyCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearAllAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
