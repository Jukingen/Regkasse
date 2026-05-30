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

    public static bool IsSatisfied(string required, IReadOnlySet<string> effective)
    {
        if (string.IsNullOrWhiteSpace(required) || effective.Count == 0)
            return false;

        if (effective.Contains(required))
            return true;

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
