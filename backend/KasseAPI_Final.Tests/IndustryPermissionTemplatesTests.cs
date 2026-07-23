using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class IndustryPermissionTemplatesTests
{
    [Fact]
    public void All_ContainsRestaurantRetailHotel()
    {
        Assert.Contains(IndustryPermissionTemplates.All, t => t.Id == IndustryPermissionTemplates.Restaurant);
        Assert.Contains(IndustryPermissionTemplates.All, t => t.Id == IndustryPermissionTemplates.Retail);
        Assert.Contains(IndustryPermissionTemplates.All, t => t.Id == IndustryPermissionTemplates.Hotel);
    }

    [Fact]
    public void Get_ReturnsNull_ForNoneOrUnknown()
    {
        Assert.Null(IndustryPermissionTemplates.Get(IndustryPermissionTemplates.None));
        Assert.Null(IndustryPermissionTemplates.Get("unknown-template"));
        Assert.Null(IndustryPermissionTemplates.Get(null));
    }

    [Fact]
    public void Get_ReturnsRestaurant()
    {
        var t = IndustryPermissionTemplates.Get("Restaurant");
        Assert.NotNull(t);
        Assert.Equal(IndustryPermissionTemplates.Restaurant, t!.Id);
        Assert.Contains(t.Slots, s => s.SystemRole == Roles.Cashier);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("none", true)]
    [InlineData("restaurant", true)]
    [InlineData("nope", false)]
    public void IsValidId(string? id, bool expected)
    {
        Assert.Equal(expected, IndustryPermissionTemplates.IsValidId(id));
    }
}
