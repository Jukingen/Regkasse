using KasseAPI_Final.Helpers;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ReservedUsernamesTests
{
    [Theory]
    [InlineData("admin")]
    [InlineData("ROOT")]
    [InlineData("Support")]
    [InlineData("superuser")]
    public void IsReserved_Blocks_Listed_Names(string userName)
    {
        Assert.True(ReservedUsernames.IsReserved(userName));
    }

    [Theory]
    [InlineData("manager1")]
    [InlineData("cashier2")]
    [InlineData("admin1")]
    [InlineData("my_support")]
    public void IsReserved_Allows_Non_Exact_Matches(string userName)
    {
        Assert.False(ReservedUsernames.IsReserved(userName));
    }
}
