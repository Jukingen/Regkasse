using System.Collections.Frozen;

namespace KasseAPI_Final.Authorization;

/// <summary>
/// Filters effective permissions for <see cref="ClientAppPolicy.Admin"/> JWT and <c>/api/Auth/me</c>.
/// POS-terminal <em>write</em> permissions (payment.take, sale.create, TSE sign at register, etc.) stay in the role
/// matrix for POS <c>app_context=pos</c> but are not embedded in admin sessions — except SuperAdmin (full catalog).
/// </summary>
/// <remarks>
/// <para><b>Manager / back-office (blacklist):</b> effective matrix minus <see cref="PosTerminalOperationalWriteStrip"/>.
/// Oversight <em>read</em> keys are preserved, including:</para>
/// <list type="bullet">
/// <item><description><see cref="AppPermissions.PaymentView"/> — payment lists, admin payment APIs, signature forensics</description></item>
/// <item><description><see cref="AppPermissions.SaleView"/> — receipts / Belege (no separate receipt.view catalog key)</description></item>
/// <item><description><see cref="AppPermissions.ReportView"/>, <see cref="AppPermissions.ReportExport"/> — reporting and RKSV oversight exports</description></item>
/// <item><description><see cref="AppPermissions.OrderView"/>, <see cref="AppPermissions.TableView"/>, <see cref="AppPermissions.CashRegisterView"/> — tenant-wide operational visibility</description></item>
/// </list>
/// <para><b>Cashier (whitelist):</b> strict FA menu subset via <see cref="CashierAdminAllowlist"/>.</para>
/// <para><b>Stripped for admin (write / floor ops only):</b> see <see cref="PosTerminalOperationalWriteStrip"/>.</para>
/// </remarks>
public static class AdminAppPermissionProfile
{
    /// <summary>
    /// POS floor mutations and register signing — excluded from admin JWT for non-SuperAdmin roles.
    /// Does <b>not</b> include read-only oversight keys (payment.view, sale.view, report.view, cart.view, kitchen.view, …).
    /// </summary>
    private static readonly FrozenSet<string> PosTerminalOperationalWriteStrip = new[]
    {
        AppPermissions.PaymentTake,
        AppPermissions.CartManage,
        AppPermissions.CashdrawerOpen,
        AppPermissions.CashdrawerClose,
        AppPermissions.ShiftOpen,
        AppPermissions.ShiftClose,
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
    /// Manager admin oversight reads that must survive <see cref="Filter"/> when present in the role matrix.
    /// Used by contract tests; not an allowlist (Manager keeps all non-stripped matrix permissions).
    /// </summary>
    public static readonly IReadOnlyList<string> ManagerOversightViewPermissions =
    [
        AppPermissions.UserView,
        AppPermissions.RoleView,
        AppPermissions.PaymentView,
        AppPermissions.SaleView,
        AppPermissions.ReportView,
        AppPermissions.ReportExport,
        AppPermissions.AuditView,
        AppPermissions.SettingsView,
        AppPermissions.CashRegisterView,
        AppPermissions.FinanzOnlineView,
        AppPermissions.FinanzOnlineManage,
    ];

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
            if (!PosTerminalOperationalWriteStrip.Contains(p))
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
