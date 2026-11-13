using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public class AuditLogSearchFilterTests
{
    [Fact]
    public void Search_TranslatesToIlikeAcrossActionEntityTypeDescriptionAndEmail()
    {
        using var db = CreateContext();
        var sql = db.AuditLogs
            .ApplyFilters(new AuditLogQueryFilters { Search = "Kasse" }, db)
            .ToQueryString();

        Assert.Contains("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.action", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.entity_type", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a.description", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Email\"", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BlankSearch_DoesNotAddIlike()
    {
        using var db = CreateContext();
        var sql = db.AuditLogs
            .ApplyFilters(new AuditLogQueryFilters { Search = "   " }, db)
            .ToQueryString();

        Assert.DoesNotContain("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=regkasse_audit_search;Username=u;Password=p")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }
}
