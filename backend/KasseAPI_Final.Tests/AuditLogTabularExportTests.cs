using System.IO.Compression;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AuditLogTabularExportTests
{
    [Fact]
    public async Task CsvAndXlsx_StartWithTheSameHeaders()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext(tenantId);
        db.AuditLogs.Add(Log(tenantId, "USER_LOGIN", "opened the drawer", "max@test.at"));
        await db.SaveChangesAsync();

        var export = new AuditExportService(db, NullLogger<AuditExportService>.Instance);
        var filters = new AuditLogQueryFilters();

        await using var csv = new MemoryStream();
        await export.StreamTabularExportAsync(filters, "csv", csv);
        var csvText = Encoding.UTF8.GetString(csv.ToArray());
        var header = csvText.Split('\n', 2)[0].Trim();
        Assert.StartsWith("Id,SessionId,UserId,UserEmail,UserRole,Action", header, StringComparison.Ordinal);
        Assert.Contains("Description", header, StringComparison.Ordinal);
        Assert.Contains("max@test.at", csvText, StringComparison.Ordinal);

        await using var xlsx = new MemoryStream();
        await export.StreamTabularExportAsync(filters, "xlsx", xlsx);
        xlsx.Position = 0;
        using var zip = new ZipArchive(xlsx, ZipArchiveMode.Read);
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet.Open());
        var xml = await reader.ReadToEndAsync();
        Assert.Contains(">Id<", xml, StringComparison.Ordinal);
        Assert.Contains(">UserEmail<", xml, StringComparison.Ordinal);
        Assert.Contains(">Action<", xml, StringComparison.Ordinal);
        Assert.Contains(">Description<", xml, StringComparison.Ordinal);
        Assert.Contains("max@test.at", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PageSize_LimitsTheExportedRows()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext(tenantId);
        db.AuditLogs.Add(Log(tenantId, "USER_LOGIN", "first", "a@test.at", DateTime.UtcNow));
        db.AuditLogs.Add(Log(tenantId, "PAYMENT", "second", "b@test.at", DateTime.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();

        var export = new AuditExportService(db, NullLogger<AuditExportService>.Instance);
        await using var csv = new MemoryStream();
        await export.StreamTabularExportAsync(
            new AuditLogQueryFilters { Page = 1, PageSize = 1 },
            "csv",
            csv);

        var lines = Encoding.UTF8.GetString(csv.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void ExportEndpoint_RequiresAuditView()
    {
        var method = typeof(AdminAuditLogsController).GetMethod(nameof(AdminAuditLogsController.Export));
        Assert.NotNull(method);
        var permission = Assert.Single(method!.GetCustomAttributes(typeof(HasPermissionAttribute), inherit: true))
            as HasPermissionAttribute;
        Assert.Equal(AppPermissions.AuditView, permission!.Permission);
        var route = typeof(AdminAuditLogsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Mvc.RouteAttribute>()
            .Single();
        Assert.Equal("api/admin/audit-logs", route.Template);
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static AuditLog Log(Guid tenantId, string action, string description, string email, DateTime? timestamp = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = "auditor",
            UserEmail = email,
            UserRole = "Manager",
            Action = action,
            EntityType = "User",
            Description = description,
            Status = AuditLogStatus.Success,
            Timestamp = timestamp ?? DateTime.UtcNow,
            SessionId = Guid.NewGuid().ToString("N"),
        };
}
