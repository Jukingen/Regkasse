using System.Collections.Frozen;
using System.Linq;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Static role-to-permissions mapping. Single source of truth for default permission sets per role.
/// SuperAdmin: full set including system.critical, tse.diagnostics, audit.cleanup, inventory.delete.
/// Other canonical roles: explicit sets. Admin role removed; former Admin users migrated to SuperAdmin.
/// </summary>
public static class RolePermissionMatrix
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> RolePermissions = BuildMatrix();

    /// <summary>
    /// Returns true if the role has the given permission. Role name is case-insensitive.
    /// </summary>
    public static bool RoleHasPermission(string roleName, string permission)
    {
        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(permission))
            return false;

        if (RolePermissions.TryGetValue(roleName, out var set))
            return set.Contains(permission);

        return false;
    }

    /// <summary>
    /// Returns the union of all permissions for the given roles. Used by handler to check permission.
    /// </summary>
    public static IReadOnlySet<string> GetPermissionsForRoles(IEnumerable<string> roleNames)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleNames == null)
            return result;

        foreach (var role in roleNames)
        {
            if (string.IsNullOrEmpty(role)) continue;
            if (RolePermissions.TryGetValue(role, out var set))
            {
                foreach (var p in set)
                    result.Add(p);
            }
        }
        return result;
    }

    private static FrozenDictionary<string, FrozenSet<string>> BuildMatrix()
    {
        var all = PermissionCatalog.All.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        var matrix = new Dictionary<string, FrozenSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Roles.SuperAdmin] = all,

            [Roles.Manager] = new[]
            {
                AppPermissions.UserView, AppPermissions.RoleView,
                AppPermissions.ProductView, AppPermissions.ProductManage,
                AppPermissions.CategoryView, AppPermissions.CategoryManage,
                AppPermissions.ModifierView, AppPermissions.ModifierManage,
                AppPermissions.OrderView, AppPermissions.OrderCreate, AppPermissions.OrderUpdate, AppPermissions.OrderCancel,
                AppPermissions.TableView, AppPermissions.TableManage,
                AppPermissions.CartView,
                AppPermissions.SaleView,
                AppPermissions.PaymentView, AppPermissions.PaymentCancel,
                AppPermissions.RefundCreate,
                AppPermissions.DiscountApply, AppPermissions.PriceOverride,
                AppPermissions.CashRegisterView, AppPermissions.CashRegisterManage,
                AppPermissions.ShiftView, AppPermissions.ShiftManage,
                AppPermissions.InventoryView, AppPermissions.InventoryManage,
                AppPermissions.CustomerView, AppPermissions.CustomerManage,
                AppPermissions.InvoiceView, AppPermissions.InvoiceManage, AppPermissions.InvoiceExport,
                AppPermissions.ReportView, AppPermissions.ReportExport,
                AppPermissions.AuditView, AppPermissions.AuditExport,
                AppPermissions.SettingsView,
                AppPermissions.KitchenView, AppPermissions.KitchenUpdate,
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),

            [Roles.Cashier] = new[]
            {
                AppPermissions.ProductView, AppPermissions.CategoryView, AppPermissions.ModifierView,
                AppPermissions.OrderView, AppPermissions.OrderCreate, AppPermissions.OrderUpdate,
                AppPermissions.TableView, AppPermissions.TableManage,
                AppPermissions.CartView, AppPermissions.CartManage,
                AppPermissions.SaleView, AppPermissions.SaleCreate,
                AppPermissions.PaymentView, AppPermissions.PaymentTake, AppPermissions.PaymentCancel,
                AppPermissions.RefundCreate,
                AppPermissions.DiscountApply, AppPermissions.PriceOverride,
                AppPermissions.CashRegisterView, AppPermissions.CashdrawerOpen, AppPermissions.CashdrawerClose,
                AppPermissions.ShiftView, AppPermissions.ShiftOpen, AppPermissions.ShiftClose, AppPermissions.ShiftManage,
                AppPermissions.InventoryView,
                AppPermissions.CustomerView, AppPermissions.CustomerManage,
                AppPermissions.InvoiceView,
                AppPermissions.ReceiptReprint,
                AppPermissions.KitchenView,
                AppPermissions.TseSign,
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),

            [Roles.Waiter] = new[]
            {
                AppPermissions.ProductView, AppPermissions.CategoryView, AppPermissions.ModifierView,
                AppPermissions.OrderView, AppPermissions.OrderCreate, AppPermissions.OrderUpdate, AppPermissions.OrderCancel,
                AppPermissions.TableView, AppPermissions.TableManage,
                AppPermissions.CartView,
                AppPermissions.SaleView, AppPermissions.SaleCreate,
                AppPermissions.PaymentView, AppPermissions.PaymentTake,
                AppPermissions.ShiftView, AppPermissions.ShiftClose,
                AppPermissions.CustomerView, AppPermissions.CustomerManage,
                AppPermissions.KitchenView,
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),

            [Roles.Kitchen] = new[]
            {
                AppPermissions.OrderView, AppPermissions.OrderUpdate,
                AppPermissions.ProductView, AppPermissions.CategoryView,
                AppPermissions.KitchenView, AppPermissions.KitchenUpdate,
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),

            [Roles.ReportViewer] = new[]
            {
                AppPermissions.ReportView, AppPermissions.ReportExport,
                AppPermissions.AuditView,
                AppPermissions.SettingsView,
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),

            [Roles.Accountant] = new[]
            {
                AppPermissions.ReportView, AppPermissions.ReportExport,
                AppPermissions.AuditView,
                AppPermissions.FinanzOnlineView,
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        };

        return matrix.ToFrozenDictionary();
    }
}
