using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Ensures audit diff only contains whitelisted safe fields; no sensitive data (Notes, TaxNumber, EmployeeNumber).
/// </summary>
public class UserAuditDiffHelperTests
{
    [Fact]
    public void AllowedKeys_ContainsOnlySafeFields()
    {
        var allowed = UserAuditDiffHelper.AllowedKeys;
        Assert.Contains("FirstName", allowed);
        Assert.Contains("LastName", allowed);
        Assert.Contains("Email", allowed);
        Assert.Contains("UserName", allowed);
        Assert.Contains("Role", allowed);
        Assert.Contains("IsActive", allowed);
        Assert.Contains("IsDemo", allowed);
        Assert.DoesNotContain("Password", allowed);
        Assert.DoesNotContain("Notes", allowed);
        Assert.DoesNotContain("TaxNumber", allowed);
        Assert.DoesNotContain("EmployeeNumber", allowed);
    }

    [Fact]
    public void CreateSafeSnapshot_ReturnsOnlyWhitelistedProperties()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            UserName = "jdoe",
            Email = "j@example.com",
            FirstName = "John",
            LastName = "Doe",
            Role = "Cashier",
            IsActive = true,
            IsDemo = false,
            EmployeeNumber = "E-12345",
            TaxNumber = "ATU12345678",
            Notes = "Confidential note"
        };

        var snapshot = UserAuditDiffHelper.CreateSafeSnapshot(user);
        Assert.NotNull(snapshot);

        var json = JsonSerializer.Serialize(snapshot);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("FirstName", out _));
        Assert.True(root.TryGetProperty("LastName", out _));
        Assert.True(root.TryGetProperty("Email", out _));
        Assert.True(root.TryGetProperty("UserName", out _));
        Assert.True(root.TryGetProperty("Role", out _));
        Assert.True(root.TryGetProperty("IsActive", out _));
        Assert.True(root.TryGetProperty("IsDemo", out _));

        Assert.False(root.TryGetProperty("EmployeeNumber", out _));
        Assert.False(root.TryGetProperty("TaxNumber", out _));
        Assert.False(root.TryGetProperty("Notes", out _));
        Assert.False(root.TryGetProperty("PasswordHash", out _));
    }

    [Fact]
    public void CreateSafeSnapshot_WhenUserNull_ReturnsEmptyObject()
    {
        var snapshot = UserAuditDiffHelper.CreateSafeSnapshot(null!);
        Assert.NotNull(snapshot);
        var json = JsonSerializer.Serialize(snapshot);
        Assert.Equal("{}", json);
    }
}
