using System.Reflection;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Contract: PaymentController auth split — read GETs use payment.view; mutations keep floor permissions.
/// </summary>
public sealed class PaymentControllerAuthorizationTests
{
    private static MethodInfo? FindAction(string name) =>
        typeof(PaymentController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SingleOrDefault(m => m.Name == name);

    private static IEnumerable<string> GetHasPermissionValues(MethodInfo method) =>
        method.GetCustomAttributes<HasPermissionAttribute>()
            .Select(a => a.Permission);

    [Fact]
    public void Controller_HasNoClassLevelPaymentTake()
    {
        var classAttrs = typeof(PaymentController)
            .GetCustomAttributes<HasPermissionAttribute>()
            .Select(a => a.Permission)
            .ToList();

        Assert.DoesNotContain(AppPermissions.PaymentTake, classAttrs);
    }

    [Fact]
    public void ReadEndpoints_RequirePaymentView_NotPaymentTake()
    {
        foreach (var name in new[]
                 {
                     "GetPaymentMethods",
                     "GetPaymentHistory",
                     "GetPayment",
                     "GetSignatureDebug",
                     "VerifySignature",
                     "GetReceipt",
                 })
        {
            var method = FindAction(name);
            Assert.NotNull(method);

            var perms = GetHasPermissionValues(method!).ToList();
            Assert.Contains(AppPermissions.PaymentView, perms);
            Assert.DoesNotContain(AppPermissions.PaymentTake, perms);
            Assert.DoesNotContain(AppPermissions.TseDiagnostics, perms);
        }
    }

    [Fact]
    public void CreatePayment_RequiresPaymentTake()
    {
        var method = FindAction("CreatePayment");
        Assert.NotNull(method);

        var perms = GetHasPermissionValues(method!).ToList();
        Assert.Contains(AppPermissions.PaymentTake, perms);
        Assert.DoesNotContain(AppPermissions.PaymentView, perms);
    }

    [Fact]
    public void TseSignature_RequiresTseSign_NotPaymentView()
    {
        var method = FindAction("GenerateTseSignature");
        Assert.NotNull(method);

        var perms = GetHasPermissionValues(method!).ToList();
        Assert.Contains(AppPermissions.TseSign, perms);
    }

    [Fact]
    public void Controller_InheritsAuthorizeFromBase()
    {
        Assert.True(typeof(PaymentController).IsSubclassOf(typeof(BaseController)));
        Assert.Contains(
            typeof(BaseController).GetCustomAttributes<AuthorizeAttribute>(),
            _ => true);
    }
}
