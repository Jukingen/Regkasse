using System.Reflection;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminPeppolSubmissionsControllerTests
{
    [Fact]
    public async Task List_SuperAdmin_ReturnsRows_AndCanFilterByTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var older = Row(tenantA, EinvoiceSubmissionStatuses.Queued, "peppol-reserved", DateTime.UtcNow.AddMinutes(-10));
        var newer = Row(tenantB, EinvoiceSubmissionStatuses.Sent, null, DateTime.UtcNow);
        await using var db = await SeedAsync(tenantA, older, newer);
        var controller = Controller(db, tenantA, Roles.SuperAdmin);

        var all = await controller.List(null, null, null, null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(all.Result);
        var body = Assert.IsType<PeppolSubmissionListResponse>(ok.Value);
        Assert.Equal(2, body.Total);
        Assert.Equal(newer.Id, body.Items[0].Id);
        Assert.Equal(tenantB, body.Items[0].TenantId);
        Assert.Equal("sc-guid", body.Items[0].ProviderMessageId);
        Assert.DoesNotContain(PropertyNames(typeof(PeppolSubmissionRowDto)), name =>
            name.Contains("ubl", StringComparison.OrdinalIgnoreCase)
            || name.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ApiKey", StringComparison.Ordinal));

        var filtered = await controller.List(tenantA, null, 50, 0, CancellationToken.None);
        var filteredOk = Assert.IsType<OkObjectResult>(filtered.Result);
        var filteredBody = Assert.IsType<PeppolSubmissionListResponse>(filteredOk.Value);
        Assert.Equal(1, filteredBody.Total);
        Assert.Equal(older.Id, Assert.Single(filteredBody.Items).Id);
    }

    [Fact]
    public async Task List_FilterByStatus_ReturnsMatchingRows()
    {
        var tenantA = Guid.NewGuid();
        var queued = Row(tenantA, EinvoiceSubmissionStatuses.Queued, "peppol-reserved", DateTime.UtcNow.AddMinutes(-3));
        var failed = Row(tenantA, EinvoiceSubmissionStatuses.Failed, "peppol-ack-error", DateTime.UtcNow.AddMinutes(-2));
        var acked = Row(tenantA, EinvoiceSubmissionStatuses.Ack, null, DateTime.UtcNow.AddMinutes(-1));
        await using var db = await SeedAsync(tenantA, queued, failed, acked);
        var controller = Controller(db, tenantA, Roles.SuperAdmin);

        var result = await controller.List(null, EinvoiceSubmissionStatuses.Failed, 50, 0, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<PeppolSubmissionListResponse>(ok.Value);
        var row = Assert.Single(body.Items);
        Assert.Equal(EinvoiceSubmissionStatuses.Failed, row.Status);
        Assert.Equal("peppol-ack-error", row.FailureReason);
        Assert.Equal(1, body.Total);
    }

    [Fact]
    public async Task List_ManagerOtherTenant_Returns404()
    {
        var ambient = Guid.NewGuid();
        var other = Guid.NewGuid();
        var hidden = Row(other, EinvoiceSubmissionStatuses.Sent, null, DateTime.UtcNow);
        await using var db = await SeedAsync(ambient, hidden);
        var controller = Controller(db, ambient, Roles.Manager);

        var result = await controller.List(other, null, 50, 0, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Get_SuperAdmin_ReturnsInvoiceHref()
    {
        var tenantId = Guid.NewGuid();
        var row = Row(tenantId, EinvoiceSubmissionStatuses.Ack, null, DateTime.UtcNow);
        await using var db = await SeedAsync(tenantId, row);
        var controller = Controller(db, tenantId, Roles.SuperAdmin);

        var result = await controller.Get(row.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<PeppolSubmissionDetailDto>(ok.Value);
        Assert.Equal(row.InvoiceId, body.InvoiceId);
        Assert.Equal(AdminPeppolSubmissionsController.InvoicePathPrefix + row.InvoiceId, body.InvoiceHref);
        Assert.Equal(EinvoiceSubmissionStatuses.Ack, body.Status);
        Assert.Null(body.FailureReason);
        Assert.DoesNotContain(PropertyNames(typeof(PeppolSubmissionDetailDto)), name =>
            name.Contains("ubl", StringComparison.OrdinalIgnoreCase)
            || name.Contains("apiKey", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<AppDbContext> SeedAsync(Guid ambient, params EinvoiceSubmission[] rows)
    {
        var db = CreateDb(ambient);
        db.Tenants.Add(Tenant(ambient));
        foreach (var tenantId in rows.Select(row => row.TenantId).Distinct().Where(id => id != ambient))
            db.Tenants.Add(Tenant(tenantId));
        db.EinvoiceSubmissions.AddRange(rows);
        await db.SaveChangesAsync();
        return db;
    }

    private static EinvoiceSubmission Row(Guid tenantId, string status, string? reason, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        InvoiceId = Guid.NewGuid(),
        Status = status,
        CorrelationId = Guid.NewGuid(),
        FailureReason = reason,
        ProviderMessageId = status == EinvoiceSubmissionStatuses.Queued ? null : "sc-guid",
        ProviderStatus = status == EinvoiceSubmissionStatuses.Ack ? "DELIVERED" : null,
        AttemptedAtUtc = status == EinvoiceSubmissionStatuses.Queued ? null : createdAt,
        AckedAtUtc = status == EinvoiceSubmissionStatuses.Ack ? createdAt : null,
        CreatedAtUtc = createdAt,
    };

    private static Tenant Tenant(Guid id) => new()
    {
        Id = id,
        Name = "mandant",
        Slug = id.ToString("N")[..12],
        IsActive = true,
    };

    private static AdminPeppolSubmissionsController Controller(AppDbContext db, Guid ambient, string role)
    {
        var controller = new AdminPeppolSubmissionsController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, "operator"),
                        new Claim(ClaimTypes.Role, role),
                    ],
                    "test")),
            },
        };
        return controller;
    }

    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PeppolQueue_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static IEnumerable<string> PropertyNames(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name);
}
