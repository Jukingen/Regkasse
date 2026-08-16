using System.Reflection;
using KasseAPI_Final.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Hard-remove contract: Cart/Payment/Product controllers serve only canonical <c>/api/pos/*</c> routes.
/// Legacy aliases <c>/api/Cart</c>, <c>/api/Payment</c>, <c>/api/Product</c> must not be reintroduced.
/// </summary>
public sealed class LegacyAliasRemovalContractTests
{
    [Theory]
    [InlineData(typeof(PaymentController), "api/pos/payment")]
    [InlineData(typeof(CartController), "api/pos/cart")]
    [InlineData(typeof(ProductController), "api/pos")]
    public void Controller_HasOnlyCanonicalRoute_AndIsNotObsolete(Type controllerType, string expectedRoute)
    {
        var routes = controllerType
            .GetCustomAttributes<RouteAttribute>(inherit: false)
            .Select(r => r.Template)
            .ToList();

        Assert.Equal(new[] { expectedRoute }, routes);
        Assert.Null(controllerType.GetCustomAttribute<ObsoleteAttribute>());
    }

    [Theory]
    [InlineData(typeof(PaymentController))]
    [InlineData(typeof(CartController))]
    [InlineData(typeof(ProductController))]
    public void Controller_DoesNotRegisterLegacyDeprecationFilter(Type controllerType)
    {
        var filterTypes = controllerType
            .GetCustomAttributes<ServiceFilterAttribute>(inherit: false)
            .Select(f => f.ServiceType.Name);

        Assert.DoesNotContain(filterTypes, name =>
            name.Contains("LegacyRoute", StringComparison.OrdinalIgnoreCase));
    }
}
