using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using Xunit;

namespace KasseAPI_Final.Tests;

public class UsernameChangePolicyTests
{
    [Fact]
    public void GetNewAccountRestrictionError_WhenAccountYoungerThan24Hours_ReturnsMessage()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        };

        var error = UsernameChangePolicy.GetNewAccountRestrictionError(user);

        Assert.NotNull(error);
        Assert.Contains("24", error, StringComparison.Ordinal);
    }

    [Fact]
    public void GetNewAccountRestrictionError_WhenAccountOlderThan24Hours_ReturnsNull()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            CreatedAt = DateTime.UtcNow.AddHours(-25),
        };

        Assert.Null(UsernameChangePolicy.GetNewAccountRestrictionError(user));
    }

    [Fact]
    public void GetNewAccountRestrictionError_WhenBypassed_ReturnsNull()
    {
        var user = new ApplicationUser
        {
            Id = "u1",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        };

        Assert.Null(UsernameChangePolicy.GetNewAccountRestrictionError(user, bypassRestrictions: true));
    }

    [Fact]
    public void GetNewAccountRestrictionError_WhenCreatedAtDefaultsToNow_ReturnsMessage()
    {
        var user = new ApplicationUser { Id = "u1" };

        Assert.NotNull(UsernameChangePolicy.GetNewAccountRestrictionError(user));
    }
}
