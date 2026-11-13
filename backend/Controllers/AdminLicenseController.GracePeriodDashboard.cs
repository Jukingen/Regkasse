using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.License;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>
    /// Super Admin overview of mandants currently in the license grace window
    /// (bucket KPIs + sortable tenant list).
    /// </summary>
    [HttpGet("grace-period")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(GracePeriodDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GracePeriodDashboardDto>> GetGracePeriodDashboard(
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var graceDays = Math.Max(
            1,
            LicenseGracePeriodConfig.GracePeriodDays > 0
                ? LicenseGracePeriodConfig.GracePeriodDays
                : LicenseGracePeriodConfig.DefaultGracePeriodDays);
        var graceStartedCutoff = now.AddDays(-graceDays);

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t =>
                t.DeletedAtUtc == null
                && t.Status == TenantStatuses.Active
                && t.LicenseValidUntilUtc != null
                && t.LicenseValidUntilUtc <= now
                && t.LicenseValidUntilUtc >= graceStartedCutoff)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Slug,
                ExpiredAtUtc = t.LicenseValidUntilUtc!.Value,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<GracePeriodTenantRowDto>(tenants.Count);
        foreach (var tenant in tenants)
        {
            var remaining = GracePeriodReminderMilestones.ResolveGraceDaysRemaining(
                tenant.ExpiredAtUtc,
                now,
                graceDays);
            if (remaining is null)
                continue;

            rows.Add(new GracePeriodTenantRowDto(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                DateTime.SpecifyKind(tenant.ExpiredAtUtc, DateTimeKind.Utc),
                remaining.Value,
                GracePeriodReminderMilestones.ResolveLockdownDateUtc(tenant.ExpiredAtUtc, graceDays)));
        }

        rows.Sort((a, b) =>
        {
            var byDays = a.DaysRemaining.CompareTo(b.DaysRemaining);
            return byDays != 0
                ? byDays
                : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        var critical = rows.Count(r => r.DaysRemaining <= 2);
        var medium = rows.Count(r => r.DaysRemaining is >= 3 and <= 5);
        var good = rows.Count(r => r.DaysRemaining >= 6);

        return Ok(new GracePeriodDashboardDto(
            rows.Count,
            critical,
            medium,
            good,
            rows));
    }
}
