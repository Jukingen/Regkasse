using System.Collections.Frozen;
using System.Linq;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Static role-to-permissions mapping. Single source of truth for default permission sets per role.
/// SuperAdmin: full set including system.critical, tse.diagnostics, audit.cleanup, inventory.delete.
/// Other canonical roles: explicit sets. Admin role removed; former Admin users migrated to SuperAdmin.
/// </summary>
/// <remarks>
/// Cash register permissions (code-only; no DB seed):
/// <list type="bullet">
/// <item><description>SuperAdmin — cash_register.view, manage, decommission (via full catalog)</description></item>
/// <item><description>Manager — view, manage, decommission (tenant-scoped at API layer)</description></item>
/// <item><description>Cashier — FA: product/payment/report view; POS: register awareness via cash_register.view (not admin Kassenverwaltung — gate with cash_register.manage on admin routes).</description></item>
/// <item><description>Accountant — view only</description></item>
/// </list>
/// </remarks>
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
    /// Returns permissions for a single role name (system roles only; custom roles return empty).
    /// </summary>
    public static IReadOnlySet<string> GetPermissionsForRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (RolePermissions.TryGetValue(roleName, out var set))
            return set.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// Union of permission sets (case-insensitive). Used when preserving permissions across role changes.
    /// </summary>
    public static IReadOnlySet<string> MergePermissions(
        IEnumerable<string> previousPermissions,
        IEnumerable<string> newPermissions)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (previousPermissions != null)
        {
            foreach (var permission in previousPermissions)
            {
                if (!string.IsNullOrWhiteSpace(permission))
                    result.Add(permission);
            }
        }

        if (newPermissions != null)
        {
            foreach (var permission in newPermissions)
            {
                if (!string.IsNullOrWhiteSpace(permission))
                    result.Add(permission);
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
                // Tenant user lifecycle (Mandanten-Admin): view + manage; password reset also via UserResetPassword.
                AppPermissions.UserView, AppPermissions.UserManage, AppPermissions.UserResetPassword, AppPermissions.RoleView,
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
                AppPermissions.CashRegisterDecommission,
                AppPermissions.ShiftView, AppPermissions.ShiftManage,
                AppPermissions.ShiftOpen, AppPermissions.ShiftClose,
                AppPermissions.CashdrawerOpen, AppPermissions.CashdrawerClose,
                AppPermissions.TseSign,
                AppPermissions.InventoryView, AppPermissions.InventoryManage,
                AppPermissions.InventoryAdjust,
                AppPermissions.CustomerView, AppPermissions.CustomerManage,
                AppPermissions.InvoiceView, AppPermissions.InvoiceManage, AppPermissions.InvoiceExport,
                AppPermissions.ReportView, AppPermissions.ReportExport,
                AppPermissions.DailyClosingView, AppPermissions.DailyClosingExecute,
                AppPermissions.FiscalExportCompliance,
                AppPermissions.FinanzOnlineView,
                AppPermissions.FinanzOnlineManage,
                AppPermissions.FinanzOnlineSubmit,
                AppPermissions.RksvNullbelegCreate,
                AppPermissions.RksvStartbelegCreate,
                AppPermissions.RksvMonatsbelegCreate,
                AppPermissions.RksvMonatsbelegView,
                AppPermissions.RksvJahresbelegCreate,
                AppPermissions.RksvJahresbelegView,
                AppPermissions.RksvSchlussbelegCreate,
                AppPermissions.AuditView, AppPermissions.AuditExport,
                AppPermissions.SettingsView,
                AppPermissions.BackupManage,
                AppPermissions.LicenseManage,
                AppPermissions.KitchenView, AppPermissions.KitchenUpdate,
                AppPermissions.VoucherRead,
                AppPermissions.VoucherCreate,
                AppPermissions.VoucherCancel,
                AppPermissions.VoucherAuditView,
                AppPermissions.VoucherIssue,
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
                AppPermissions.ReportView,
                AppPermissions.DailyClosingView,
                AppPermissions.LicenseView,
                AppPermissions.ReceiptReprint,
                AppPermissions.KitchenView,
                AppPermissions.TseSign,
                AppPermissions.RksvStartbelegCreate,
                AppPermissions.RksvMonatsbelegCreate,
                AppPermissions.RksvMonatsbelegView,
                AppPermissions.RksvJahresbelegCreate,
                AppPermissions.RksvJahresbelegView,
                AppPermissions.VoucherIssue,
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),

            [Roles.Waiter] = new[]
            {
                AppPermissions.ProductView, AppPermissions.CategoryView, AppPermissions.ModifierView,
                AppPermissions.OrderView, AppPermissions.OrderCreate, AppPermissions.OrderUpdate, AppPermissions.OrderCancel,
                AppPermissions.TableView, AppPermissions.TableManage,
                AppPermissions.CartView,
                AppPermissions.SaleView, AppPermissions.SaleCreate,
                AppPermissions.PaymentView, AppPermissions.PaymentTake,
                // POS: list/select registers for self-assignment when multiple tills exist (payment still gated by resolver).
                AppPermissions.CashRegisterView,
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
                AppPermissions.FiscalExportCompliance,
                AppPermissions.AuditView,
                AppPermissions.FinanzOnlineView,
                AppPermissions.FinanzOnlineSubmit,
                AppPermissions.CashRegisterView,
                AppPermissions.RksvNullbelegCreate,
                AppPermissions.RksvStartbelegCreate,
                AppPermissions.RksvMonatsbelegCreate,
                AppPermissions.RksvMonatsbelegView,
                AppPermissions.RksvJahresbelegCreate,
                AppPermissions.RksvJahresbelegView,
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        };

        return matrix.ToFrozenDictionary();
    }
}
