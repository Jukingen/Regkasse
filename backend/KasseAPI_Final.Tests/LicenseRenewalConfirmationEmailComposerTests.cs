using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseRenewalConfirmationEmailComposerTests
{
    [Fact]
    public void BuildSubject_IncludesTenantName()
    {
        var model = LicenseRenewalConfirmationEmailComposer.CreateModel(
            "Cafe Test",
            new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            "REGK-20270115-cafe-ABCDEF12");
        var subject = LicenseRenewalConfirmationEmailComposer.BuildSubject(model);

        Assert.Contains("verlängert", subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cafe Test", subject, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildHtmlBody_ContainsTenantExpiryDashboardAndMaskedKey()
    {
        var model = LicenseRenewalConfirmationEmailComposer.CreateModel(
            "Cafe Test",
            new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            "REGK-20270301-cafe-ABCDEF12XYZ",
            adminName: "Anna Admin",
            dashboardLink: "https://admin.regkasse.at/dashboard");

        var html = LicenseRenewalConfirmationEmailComposer.BuildHtmlBody(model);

        Assert.Contains("Cafe Test", html, StringComparison.Ordinal);
        Assert.Contains("01.03.2027", html, StringComparison.Ordinal);
        Assert.Contains("Anna Admin", html, StringComparison.Ordinal);
        Assert.Contains("https://admin.regkasse.at/dashboard", html, StringComparison.Ordinal);
        Assert.Contains("REGK-20270301-ca…", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ABCDEF12XYZ", html, StringComparison.Ordinal);
        Assert.Contains("#52c41a", html, StringComparison.Ordinal);
    }

    [Fact]
    public void MaskLicenseKey_LeavesShortKeysIntact()
    {
        Assert.Equal("SHORT-KEY", LicenseRenewalConfirmationEmailComposer.MaskLicenseKey("SHORT-KEY"));
        Assert.Equal("—", LicenseRenewalConfirmationEmailComposer.MaskLicenseKey(null));
    }
}
