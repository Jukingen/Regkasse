using System.Security.Claims;
using System.Runtime.CompilerServices;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Appointments;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosAppointmentApiTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task Create_ThenList_ReturnsTenantScopedAppointment()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();

        var controller = CreateController(db, "cashier-1");
        var start = new DateTime(2026, 10, 10, 8, 30, 0, DateTimeKind.Utc);
        var created = await controller.Create(
            new CreatePosAppointmentRequest
            {
                CustomerName = "Anna Beispiel",
                StaffId = "staff-1",
                StartUtc = start,
                EndUtc = start.AddMinutes(45),
                ExpectedVersion = 0,
            },
            CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(created.Result);
        var dto = Assert.IsType<AppointmentDto>(createdResult.Value);
        Assert.Equal(1, dto.Version);
        Assert.Equal(AppointmentStatus.Booked, dto.Status);
        Assert.Equal("Anna Beispiel", dto.Notes);

        var list = await controller.List(start, start.AddHours(1), "staff-1", CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(list.Result);
        var items = Assert.IsAssignableFrom<IReadOnlyList<AppointmentDto>>(listOk.Value);
        Assert.Single(items);
        Assert.Equal(dto.Id, items[0].Id);
    }

    [Fact]
    public async Task Update_WithStaleVersion_Returns409WithCurrentRow()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();

        var controller = CreateController(db, "cashier-1");
        var start = new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);
        var created = await controller.Create(
            new CreatePosAppointmentRequest { StaffId = "staff-1", StartUtc = start },
            CancellationToken.None);
        var dto = Assert.IsType<AppointmentDto>(Assert.IsType<CreatedResult>(created.Result).Value);

        var stale = await controller.Update(
            dto.Id,
            new UpdatePosAppointmentRequest { ExpectedVersion = 0, Notes = "stale" },
            CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(stale.Result);
        var body = Assert.IsType<AppointmentConflictDto>(conflict.Value);
        Assert.Equal(AppointmentConflictCodes.Conflict, body.Code);
        Assert.Equal(dto.Id, body.Appointment.Id);
        Assert.Equal(1, body.Appointment.Version);

        var updated = await controller.Update(
            dto.Id,
            new UpdatePosAppointmentRequest { ExpectedVersion = 1, Notes = "ok" },
            CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(updated.Result);
        var saved = Assert.IsType<AppointmentDto>(ok.Value);
        Assert.Equal(2, saved.Version);
        Assert.Equal("ok", saved.Notes);
    }

    [Fact]
    public async Task Create_SameStaffSlot_SecondClientGets409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();

        var first = CreateController(db, "cashier-1");
        var second = CreateController(db, "cashier-2");
        var start = new DateTime(2026, 10, 10, 10, 0, 0, DateTimeKind.Utc);

        var created = await first.Create(
            new CreatePosAppointmentRequest { StaffId = "staff-1", StartUtc = start, ExpectedVersion = 0 },
            CancellationToken.None);
        var original = Assert.IsType<AppointmentDto>(Assert.IsType<CreatedResult>(created.Result).Value);

        var clash = await second.Create(
            new CreatePosAppointmentRequest { StaffId = "staff-1", StartUtc = start, ExpectedVersion = 0 },
            CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(clash.Result);
        var body = Assert.IsType<AppointmentConflictDto>(conflict.Value);
        Assert.Equal(AppointmentConflictCodes.Conflict, body.Code);
        Assert.Equal(original.Id, body.Appointment.Id);
        Assert.Equal(original.Version, body.Appointment.Version);
    }

    [Fact]
    public async Task CrossTenant_GetUpdateDelete_Return404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantA);
        SeedTenant(db, tenantA);
        SeedTenant(db, tenantB);
        await db.SaveChangesAsync();

        var actorA = CreateController(db, "cashier-a", tenantA);
        var start = new DateTime(2026, 10, 10, 11, 0, 0, DateTimeKind.Utc);
        var created = await actorA.Create(
            new CreatePosAppointmentRequest { StaffId = "staff-1", StartUtc = start },
            CancellationToken.None);
        var dto = Assert.IsType<AppointmentDto>(Assert.IsType<CreatedResult>(created.Result).Value);

        var actorB = CreateController(db, "cashier-b", tenantB);
        var list = await actorB.List(null, null, null, CancellationToken.None);
        var listOk = Assert.IsType<OkObjectResult>(list.Result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<AppointmentDto>>(listOk.Value));

        var patch = await actorB.Update(
            dto.Id,
            new UpdatePosAppointmentRequest { ExpectedVersion = 1 },
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(patch.Result);

        var delete = await actorB.Cancel(dto.Id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(delete.Result);
    }

    [Fact]
    public async Task MissingTenant_Returns404()
    {
        await using var db = await CreateDbAsync(null);
        var controller = CreateController(db, "cashier-1", tenantId: null);
        var list = await controller.List(null, null, null, CancellationToken.None);
        Assert.IsType<NotFoundResult>(list.Result);

        var created = await controller.Create(
            new CreatePosAppointmentRequest { StartUtc = DateTime.UtcNow },
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(created.Result);
    }

    [Fact]
    public async Task CreateAndUpdate_WriteAuditEvents()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(tenantId);
        SeedTenant(db, tenantId);
        await db.SaveChangesAsync();

        var audit = CreateAuditMock();
        var controller = CreateController(db, "cashier-1", tenantId, audit.Object);
        var start = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        var created = await controller.Create(
            new CreatePosAppointmentRequest { StaffId = "staff-1", StartUtc = start },
            CancellationToken.None);
        var dto = Assert.IsType<AppointmentDto>(Assert.IsType<CreatedResult>(created.Result).Value);

        await controller.Update(
            dto.Id,
            new UpdatePosAppointmentRequest { ExpectedVersion = 1, Notes = "changed" },
            CancellationToken.None);
        await controller.Cancel(dto.Id, CancellationToken.None);

        VerifyAudit(audit, AuditEventType.AppointmentCreated, Times.Once());
        VerifyAudit(audit, AuditEventType.AppointmentUpdated, Times.Once());
        VerifyAudit(audit, AuditEventType.AppointmentCancelled, Times.Once());
        Assert.Equal(120, (int)AuditEventType.AppointmentCreated);
        Assert.Equal(121, (int)AuditEventType.AppointmentUpdated);
        Assert.Equal(122, (int)AuditEventType.AppointmentCancelled);
    }

    private static async Task<AppDbContext> CreateDbAsync(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"Appointments_{Guid.NewGuid():N}")
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static PosAppointmentsController CreateController(
        AppDbContext db,
        string actorId,
        Guid? tenantId = null,
        IAuditLogService? audit = null)
    {
        var accessor = Accessors.GetValue(
            db,
            _ => throw new InvalidOperationException("Tenant accessor was not registered for the test context."));
        if (accessor is TenantTestDoubles.MutableTenantAccessor mutable)
            mutable.TenantId = tenantId ?? mutable.TenantId;

        var service = new AppointmentService(
            db,
            accessor,
            audit ?? CreateAuditMock().Object,
            Mock.Of<ILogger<AppointmentService>>());
        var controller = new PosAppointmentsController(service, accessor, new PermissiveVerticalProfileGuard())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [
                            new Claim(ClaimTypes.NameIdentifier, actorId),
                            new Claim(ClaimTypes.Role, Roles.Cashier),
                        ],
                        authenticationType: "test")),
                },
            },
        };
        return controller;
    }

    private static void SeedTenant(AppDbContext db, Guid tenantId)
    {
        if (db.Tenants.IgnoreQueryFilters().Any(tenant => tenant.Id == tenantId))
            return;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {tenantId:N}",
            Slug = $"t-{tenantId:N}"[..20],
            Status = TenantStatuses.Active,
            IsActive = true,
        });
    }

    private static Mock<IAuditLogService> CreateAuditMock()
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(logger => logger.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
        return audit;
    }

    private static void VerifyAudit(Mock<IAuditLogService> audit, AuditEventType actionType, Times times)
    {
        audit.Verify(
            logger => logger.LogSystemOperationAsync(
                It.IsAny<string>(),
                nameof(Appointment),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                AuditLogStatus.Success,
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                actionType,
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()),
            times);
    }
}
