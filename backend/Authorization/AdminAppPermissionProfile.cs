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
/// <item><description><see cref="AppPermissions.UserView"/> — user list, /admin/users, staff hub (Mandanten-Admin)</description></item>
/// <item><description><see cref="AppPermissions.UserManage"/> — tenant user create/edit/deactivate (Mandanten-Admin)</description></item>
/// <item><description><see cref="AppPermissions.UserResetPassword"/> — tenant-scoped password reset</description></item>
/// <item><description><see cref="AppPermissions.PaymentView"/> — payment lists, admin payment APIs, signature forensics</description></item>
/// <item><description><see cref="AppPermissions.SaleView"/> — receipts / Belege (no separate receipt.view catalog key)</description></item>
/// <item><description><see cref="AppPermissions.ReportView"/>, <see cref="AppPermissions.ReportExport"/> — reporting and RKSV oversight exports</description></item>
/// <item><description><see cref="AppPermissions.OrderView"/>, <see cref="AppPermissions.TableView"/>, <see cref="AppPermissions.CashRegisterView"/> — tenant-wide operational visibility</description></item>
/// <item><description><see cref="AppPermissions.CashRegisterManage"/>, <see cref="AppPermissions.CashRegisterDecommission"/> — Kassenverwaltung (not in <see cref="PosTerminalOperationalWriteStrip"/>)</description></item>
/// </list>
/// <para><b>Cashier (whitelist):</b> strict FA menu subset via <see cref="CashierAdminAllowlist"/>.</para>
/// <para><b>Stripped for admin (write / floor ops only):</b> see <see cref="PosTerminalOperationalWriteStrip"/>
/// (POS mutations + <see cref="AppPermissions.TseSign"/>). RKSV Sonderbeleg create keys are <b>not</b> stripped —
/// Mandanten-Admin uses them on FA <c>/rksv/sonderbelege</c>.</para>
/// </remarks>
public static class AdminAppPermissionProfile
{
    /// <summary>
    /// POS floor mutations and register signing — excluded from admin JWT for non-SuperAdmin roles.
    /// Does <b>not</b> include read-only oversight keys (payment.view, sale.view, report.view, cart.view, kitchen.view, …).
    /// Does <b>not</b> strip RKSV Sonderbeleg create keys (<c>rksv.*.create</c>) — those are FA back-office
    /// operations on <c>/rksv/sonderbelege</c> (Mandanten-Admin / Manager must retain them).
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
        AppPermissions.LicenseView,
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// POS Cashier permission keys for <c>app_context=pos</c> (mirrors <see cref="Roles.Cashier"/> matrix).
    /// Includes <see cref="AppPermissions.LicenseView"/> for POST-login mandant license gate.
    /// </summary>
    public static readonly IReadOnlySet<string> CashierPosPermissions =
        RolePermissionMatrix.GetPermissionsForRole(Roles.Cashier);

    /// <summary>
    /// Manager admin oversight reads that must remain <em>effectively</em> available after <see cref="Filter"/>
    /// (direct JWT claim or via <see cref="PermissionImplication"/>).
    /// Used by contract tests; not an allowlist (Manager keeps all non-stripped matrix permissions).
    /// </summary>
    public static readonly IReadOnlyList<string> ManagerOversightViewPermissions =
    [
        AppPermissions.UserView,
        AppPermissions.UserManage,
        AppPermissions.UserResetPassword, // implied by UserManage when not embedded
        AppPermissions.RoleView,
        AppPermissions.PaymentView,
        AppPermissions.SaleView,
        AppPermissions.ReportView,
        AppPermissions.ReportExport,
        AppPermissions.DailyClosingView, // FA /tagesabschluss sidebar + route (keep explicit in JWT)
        AppPermissions.AuditView,
        AppPermissions.SettingsView,
        AppPermissions.CashRegisterView, // implied by CashRegisterManage when not embedded
        AppPermissions.FinanzOnlineView,
        AppPermissions.FinanzOnlineManage,
    ];

    /// <summary>
    /// Manager FA Kassenverwaltung keys that must remain effectively available after <see cref="Filter"/>.
    /// <see cref="AppPermissions.CashRegisterView"/> may be implied by manage/decommission.
    /// Contract-tested; mirror in <c>frontend-admin</c> <c>MANAGER_ADMIN_PERMISSIONS</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> ManagerCashRegisterAdminPermissions =
    [
        AppPermissions.CashRegisterView,
        AppPermissions.CashRegisterManage,
        AppPermissions.CashRegisterDecommission,
    ];

    /// <summary>
    /// Manager FA Sonderbelege create keys that must survive <see cref="Filter"/> (not POS-terminal strip).
    /// Contract-tested; mirror in <c>frontend-admin</c> <c>MANAGER_ADMIN_PERMISSIONS</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> ManagerRksvSonderbelegCreatePermissions =
    [
        AppPermissions.RksvNullbelegCreate,
        AppPermissions.RksvStartbelegCreate,
        AppPermissions.RksvMonatsbelegCreate,
        AppPermissions.RksvJahresbelegCreate,
        AppPermissions.RksvSchlussbelegCreate,
    ];

    /// <summary>
    /// Applies admin-app scoping. POS Cashier sessions always embed the full <see cref="CashierPosPermissions"/> set.
    /// Other POS / legacy (null) contexts return <paramref name="effectivePermissions"/> unchanged.
    /// </summary>
    public static IReadOnlySet<string> Filter(
        string? appContext,
        IReadOnlyList<string> canonicalRoles,
        IReadOnlySet<string> effectivePermissions)
    {
        if (string.Equals(appContext, ClientAppPolicy.Pos, StringComparison.OrdinalIgnoreCase)
            && canonicalRoles.Any(r => string.Equals(r, Roles.Cashier, StringComparison.OrdinalIgnoreCase))
            && !canonicalRoles.Any(r => string.Equals(r, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            var posCashier = new HashSet<string>(effectivePermissions, StringComparer.OrdinalIgnoreCase);
            foreach (var p in CashierPosPermissions)
                posCashier.Add(p);
            return posCashier;
        }

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
