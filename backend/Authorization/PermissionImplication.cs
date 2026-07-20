using System.Collections.Frozen;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Composite permission implication: coarse permissions (e.g. user.manage) satisfy granular endpoints,
/// and granular grants satisfy composite checks when all children are present.
/// </summary>
public static class PermissionImplication
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> ParentToChildren = BuildParentToChildren();
    private static readonly FrozenDictionary<string, string> ChildToParent = BuildChildToParent();
    /// <summary>One-way: holder permission satisfies required read (e.g. manage → view), not the reverse.</summary>
    private static readonly FrozenDictionary<string, FrozenSet<string>> HolderToImpliedReads = BuildHolderToImpliedReads();

    public static bool IsSatisfied(string required, IReadOnlySet<string> effective)
    {
        if (string.IsNullOrWhiteSpace(required) || effective.Count == 0)
            return false;

        // Compact SuperAdmin JWT emits only system.critical; treat it as full catalog access.
        if (effective.Contains(AppPermissions.SystemCritical))
            return true;

        if (effective.Contains(required))
            return true;

        foreach (var held in effective)
        {
            if (HolderToImpliedReads.TryGetValue(held, out var implied) && implied.Contains(required))
                return true;
        }

        if (ParentToChildren.TryGetValue(required, out var children)
            && children.All(c => effective.Contains(c)))
            return true;

        if (ChildToParent.TryGetValue(required, out var parent) && effective.Contains(parent))
            return true;

        return false;
    }

    public static bool IsSatisfied(string required, IEnumerable<string> effective)
    {
        var set = effective as IReadOnlySet<string>
            ?? effective.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return IsSatisfied(required, set);
    }

    private static FrozenDictionary<string, FrozenSet<string>> BuildParentToChildren()
    {
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [AppPermissions.UserManage] =
            [
                AppPermissions.UserCreate,
                AppPermissions.UserEdit,
                AppPermissions.UserDelete,
                AppPermissions.UserChangeRole,
                AppPermissions.UserChangeUsername,
                AppPermissions.UserResetPassword,
            ],
            [AppPermissions.ProductManage] =
            [
                AppPermissions.ProductCreate,
                AppPermissions.ProductEdit,
                AppPermissions.ProductDelete,
                AppPermissions.ProductUpdateStock,
            ],
            [AppPermissions.TenantManage] =
            [
                AppPermissions.TenantView,
                AppPermissions.TenantCreate,
                AppPermissions.TenantEdit,
                AppPermissions.TenantDelete,
                AppPermissions.TenantImpersonate,
            ],
            [AppPermissions.SettingsManage] =
            [
                AppPermissions.SettingsBackup,
                AppPermissions.BackupManage,
                AppPermissions.WebsiteManage,
            ],
            // Super Admin override: digital.manage satisfies all digital service checks.
            [AppPermissions.DigitalManage] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalPreview,
                AppPermissions.DigitalRequest,
                AppPermissions.DigitalCreate,
                AppPermissions.DigitalPublish,
                AppPermissions.DigitalEdit,
                AppPermissions.DigitalDelete,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalWebPreview,
                AppPermissions.DigitalWebRequest,
                AppPermissions.DigitalWebCreate,
                AppPermissions.DigitalWebPublish,
                AppPermissions.DigitalWebDelete,
                AppPermissions.DigitalWebUse,
                AppPermissions.DigitalAppView,
                AppPermissions.DigitalAppPreview,
                AppPermissions.DigitalAppRequest,
                AppPermissions.DigitalAppCreate,
                AppPermissions.DigitalAppPublish,
                AppPermissions.DigitalAppDelete,
                AppPermissions.DigitalAppUse,
                AppPermissions.DigitalPricingManage,
                AppPermissions.DigitalActivate,
                AppPermissions.DigitalOrdersView,
                AppPermissions.DigitalOrdersManage,
                AppPermissions.DigitalOrdersApprove,
            ],
            [AppPermissions.AuditCleanup] =
            [
                AppPermissions.AuditDelete,
            ],
        };

        return map.ToFrozenDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<string, FrozenSet<string>> BuildHolderToImpliedReads()
    {
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [AppPermissions.CashRegisterManage] = [AppPermissions.CashRegisterView],
            [AppPermissions.TableManage] = [AppPermissions.TableView],
            [AppPermissions.LicenseManage] = [AppPermissions.LicenseView],
            // Domains/customization — view/preview/request only (not create/publish).
            [AppPermissions.WebsiteManage] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalPreview,
                AppPermissions.DigitalRequest,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalWebPreview,
                AppPermissions.DigitalWebRequest,
                AppPermissions.DigitalAppView,
                AppPermissions.DigitalAppPreview,
                AppPermissions.DigitalAppRequest,
            ],
            // Simplified digital.* → legacy web/app checks + related actions.
            [AppPermissions.DigitalView] =
            [
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalAppView,
            ],
            [AppPermissions.DigitalPreview] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalAppView,
                AppPermissions.DigitalWebPreview,
                AppPermissions.DigitalAppPreview,
            ],
            [AppPermissions.DigitalRequest] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalAppView,
                AppPermissions.DigitalWebRequest,
                AppPermissions.DigitalAppRequest,
            ],
            [AppPermissions.DigitalCreate] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalPreview,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalAppView,
                AppPermissions.DigitalWebPreview,
                AppPermissions.DigitalAppPreview,
                AppPermissions.DigitalWebCreate,
                AppPermissions.DigitalAppCreate,
                AppPermissions.DigitalWebUse,
                AppPermissions.DigitalAppUse,
            ],
            [AppPermissions.DigitalPublish] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalAppView,
                AppPermissions.DigitalWebPublish,
                AppPermissions.DigitalAppPublish,
            ],
            [AppPermissions.DigitalEdit] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalPreview,
                AppPermissions.WebsiteManage,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalAppView,
            ],
            [AppPermissions.DigitalDelete] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalWebDelete,
                AppPermissions.DigitalAppDelete,
            ],
            [AppPermissions.DigitalWebCreate] =
            [
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalWebPreview,
                AppPermissions.DigitalWebUse,
            ],
            [AppPermissions.DigitalAppCreate] =
            [
                AppPermissions.DigitalAppView,
                AppPermissions.DigitalAppPreview,
                AppPermissions.DigitalAppUse,
            ],
            [AppPermissions.DigitalWebPublish] = [AppPermissions.DigitalWebView],
            [AppPermissions.DigitalAppPublish] = [AppPermissions.DigitalAppView],
            [AppPermissions.DigitalWebUse] = [AppPermissions.DigitalWebView],
            [AppPermissions.DigitalAppUse] = [AppPermissions.DigitalAppView],
            [AppPermissions.DigitalWebPreview] = [AppPermissions.DigitalWebView],
            [AppPermissions.DigitalAppPreview] = [AppPermissions.DigitalAppView],
            [AppPermissions.DigitalWebRequest] = [AppPermissions.DigitalWebView],
            [AppPermissions.DigitalAppRequest] = [AppPermissions.DigitalAppView],
            [AppPermissions.DigitalPricingManage] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalAppView,
            ],
            [AppPermissions.DigitalActivate] =
            [
                AppPermissions.DigitalView,
                AppPermissions.DigitalWebView,
                AppPermissions.DigitalAppView,
            ],
            [AppPermissions.DigitalOrdersManage] =
            [
                AppPermissions.DigitalOrdersView,
            ],
            [AppPermissions.DigitalOrdersApprove] =
            [
                AppPermissions.DigitalOrdersView,
                AppPermissions.DigitalOrdersManage,
            ],
            [AppPermissions.DailyClosingExecute] = [AppPermissions.DailyClosingView],
        };

        return map.ToFrozenDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<string, string> BuildChildToParent()
    {
        var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (parent, children) in ParentToChildren)
        {
            foreach (var child in children)
                reverse.TryAdd(child, parent);
        }

        return reverse.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
