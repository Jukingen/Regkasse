using System.Collections.Frozen;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Filters effective permissions for <see cref="ClientAppPolicy.Admin"/> JWT and <c>/api/Auth/me</c>.
/// POS-terminal permissions (cart, payment.take, TSE at register, etc.) stay in the role matrix for POS
/// <c>app_context=pos</c> but are not embedded in admin sessions — except SuperAdmin (full catalog).
/// Cashier admin login uses a strict view-only allowlist aligned with FA menu matrix.
/// </summary>
public static class AdminAppPermissionProfile
{
    /// <summary>POS floor / register operations — excluded from admin JWT for non-SuperAdmin roles.</summary>
    private static readonly FrozenSet<string> PosTerminalOnly = new[]
    {
        AppPermissions.PaymentTake,
        AppPermissions.CartView,
        AppPermissions.CartManage,
        AppPermissions.CashdrawerOpen,
        AppPermissions.CashdrawerClose,
        AppPermissions.ShiftOpen,
        AppPermissions.ShiftClose,
        AppPermissions.KitchenView,
        AppPermissions.KitchenUpdate,
        AppPermissions.VoucherIssue,
        AppPermissions.SaleCreate,
        AppPermissions.SaleCancel,
        AppPermissions.OrderCreate,
        AppPermissions.OrderUpdate,
        AppPermissions.OrderCancel,
        AppPermissions.TableManage,
        AppPermissions.ReceiptReprint,
        AppPermissions.PriceOverride,
        AppPermissions.DiscountApply,
        AppPermissions.RksvStartbelegCreate,
        AppPermissions.RksvMonatsbelegCreate,
        AppPermissions.RksvJahresbelegCreate,
        AppPermissions.TseSign,
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>FA menus for Cashier: dashboard + products/payments/reports (view only).</summary>
    private static readonly FrozenSet<string> CashierAdminAllowlist = new[]
    {
        AppPermissions.ProductView,
        AppPermissions.CategoryView,
        AppPermissions.ModifierView,
        AppPermissions.PaymentView,
        AppPermissions.ReportView,
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Applies admin-app scoping. POS and legacy (null) contexts return <paramref name="effectivePermissions"/> unchanged.
    /// </summary>
    public static IReadOnlySet<string> Filter(
        string? appContext,
        IReadOnlyList<string> canonicalRoles,
        IReadOnlySet<string> effectivePermissions)
    {
        if (!string.Equals(appContext, ClientAppPolicy.Admin, StringComparison.OrdinalIgnoreCase))
            return effectivePermissions;

        if (canonicalRoles.Any(r => string.Equals(r, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
            return effectivePermissions;

        if (canonicalRoles.Any(r => string.Equals(r, Roles.Cashier, StringComparison.OrdinalIgnoreCase)))
        {
            var cashier = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in effectivePermissions)
            {
                if (CashierAdminAllowlist.Contains(p))
                    cashier.Add(p);
            }

            return cashier;
        }

        var admin = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in effectivePermissions)
        {
            if (!PosTerminalOnly.Contains(p))
                admin.Add(p);
        }

        return admin;
    }

    /// <summary>Sorted list for stable JSON serialization (login / me).</summary>
    public static List<string> FilterToSortedList(
        string? appContext,
        IReadOnlyList<string> canonicalRoles,
        IReadOnlySet<string> effectivePermissions) =>
        Filter(appContext, canonicalRoles, effectivePermissions)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
