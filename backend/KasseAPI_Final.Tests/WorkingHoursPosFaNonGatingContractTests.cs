using System.Text.RegularExpressions;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Contract: working-hours intake gates must stay on public website/app surfaces only.
/// POS and FA/admin controllers must never call IsAcceptingOnlineOrders / EvaluateWebsiteStatus.
/// </summary>
public sealed class WorkingHoursPosFaNonGatingContractTests
{
    private static readonly Regex ForbiddenGateCalls = new(
        @"\b(IsAcceptingOnlineOrders|EvaluateWebsiteStatus|GetWebsiteStatusAsync)\b",
        RegexOptions.Compiled);

    private static readonly string[] AllowedRelativePaths =
    [
        Path.Combine("Services", "Website", "PublicTenantCatalogService.cs"),
        Path.Combine("Services", "Order", "OnlineOrderIntakeService.cs"),
        Path.Combine("Sites", "Controllers", "WebsiteStatusController.cs"),
        Path.Combine("Controllers", "PublicTenantsController.cs"),
        Path.Combine("Controllers", "PublicSitesController.cs"),
        Path.Combine("Controllers", "PublicOnlineOrdersController.cs"),
        Path.Combine("Models", "WorkingHours.cs"),
    ];

    [Fact]
    public void Pos_and_admin_controllers_do_not_call_online_order_hours_gates()
    {
        var backendRoot = FindBackendRoot();
        var scanRoots = new[]
        {
            Path.Combine(backendRoot, "Controllers"),
            Path.Combine(backendRoot, "Services", "Order"),
            Path.Combine(backendRoot, "Services", "Payment"),
            Path.Combine(backendRoot, "Services", "PaymentGateway"),
        };

        var allowed = new HashSet<string>(
            AllowedRelativePaths.Select(p => Path.GetFullPath(Path.Combine(backendRoot, p))),
            StringComparer.OrdinalIgnoreCase);

        var violations = new List<string>();
        foreach (var root in scanRoots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var full = Path.GetFullPath(file);
                if (allowed.Contains(full))
                    continue;

                // Public customer website controllers may expose status via catalog — still OK if they
                // only forward profile fields; forbid direct gate evaluation helpers.
                var name = Path.GetFileName(full);
                if (name.StartsWith("Public", StringComparison.OrdinalIgnoreCase)
                    && full.Contains($"{Path.DirectorySeparatorChar}Controllers{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    // PublicTenantsController is allowed (profile includes status fields from catalog).
                    if (name is "PublicTenantsController.cs" or "PublicSitesController.cs"
                        or "PublicOnlineOrdersController.cs" or "PublicCustomerController.cs")
                    {
                        continue;
                    }
                }

                var source = File.ReadAllText(full);
                if (!ForbiddenGateCalls.IsMatch(source))
                    continue;

                // SettingsController may mention WorkingHours DTO but must not evaluate intake gates.
                if (name is "SettingsController.cs" or "PosCompanyController.cs"
                    or "CompanySettingsController.cs")
                {
                    Assert.DoesNotMatch(ForbiddenGateCalls, source);
                    continue;
                }

                violations.Add(Path.GetRelativePath(backendRoot, full));
            }
        }

        Assert.True(
            violations.Count == 0,
            "Working-hours online gates must not appear outside website/public catalog surfaces:\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void WebsiteStatusController_documents_website_only_scope()
    {
        var path = Path.Combine(FindBackendRoot(), "Sites", "Controllers", "WebsiteStatusController.cs");
        Assert.True(File.Exists(path), path);
        var source = File.ReadAllText(path);
        Assert.Contains("never gates POS or FA", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api/sites/{tenantSlug}/status", source);
    }

    private static string FindBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var csproj = Path.Combine(dir.FullName, "KasseAPI_Final.csproj");
            if (File.Exists(csproj))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate backend project root.");
    }
}
