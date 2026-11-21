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
    public void Search_TranslatesToPlainToTsQueryOnTheStoredVector()
    {
        using var db = CreateContext();
        var sql = db.AuditLogs
            .ApplyFilters(new AuditLogQueryFilters { Search = "Kasse" }, db)
            .ToQueryString();

        Assert.Contains("plainto_tsquery", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search_vector", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("simple", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ILIKE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StoredVector_UsesToTsVectorOverActionEntityTypeDescriptionAndEmail()
    {
        Assert.Contains("to_tsvector", AuditLogSearchVector.StoredExpression, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("action", AuditLogSearchVector.StoredExpression, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("entity_type", AuditLogSearchVector.StoredExpression, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("description", AuditLogSearchVector.StoredExpression, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user_email", AuditLogSearchVector.StoredExpression, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlankSearch_DoesNotAddFullTextQuery()
    {
        using var db = CreateContext();
        var sql = db.AuditLogs
            .ApplyFilters(new AuditLogQueryFilters { Search = "   " }, db)
            .ToQueryString();

        Assert.DoesNotContain("plainto_tsquery", sql, StringComparison.OrdinalIgnoreCase);
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
