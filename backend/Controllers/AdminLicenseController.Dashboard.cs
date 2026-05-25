using System.Globalization;
using System.Text;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Aggregated license metrics and activity for the admin dashboard.</summary>
public sealed partial class AdminLicenseController
{
    private sealed record TenantLicenseStatRow(DateTime? LicenseValidUntilUtc, bool IsActive, string Status);

    private sealed record DeploymentLicenseStatRow(DateTime ExpiryAtUtc, bool IsRevoked, bool IsCancelled, Guid? SupersededByLicenseId, Guid? TransferredToLicenseId);

    private bool IsActorSuperAdmin() => User.IsInRole(Roles.SuperAdmin);

    private string? ActorUserId => User.GetActorUserId();

    /// <summary>
    /// Mandantenlizenz + deployment license KPIs. Super Admin: all tenants; Manager: membership-scoped tenants only.
    /// </summary>
    [HttpGet("dashboard-stats")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(LicenseDashboardStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseDashboardStatsDto>> GetDashboardStats(CancellationToken cancellationToken)
    {
        return Ok(await BuildDashboardStatsAsync(recentActivityTake: 5, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Issued-license counts and activated device cardinality (no JWT).</summary>
    [HttpGet("dashboard/summary")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(LicenseDashboardSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseDashboardSummaryResponse>> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var stats = await BuildDashboardStatsAsync(recentActivityTake: 0, cancellationToken).ConfigureAwait(false);
        return Ok(new LicenseDashboardSummaryResponse(
            stats.ActiveDeploymentLicenses,
            stats.ExpiringDeploymentLicenses,
            stats.ExpiredDeploymentLicenses,
            stats.ActivatedDevices));
    }

    /// <summary>Activation counts grouped by calendar day or ISO week (UTC).</summary>
    [HttpGet("dashboard/activation-series")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(LicenseDashboardActivationSeriesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseDashboardActivationSeriesResponse>> GetActivationSeries(
        [FromQuery] string granularity = "day",
        [FromQuery] int lookbackDays = 30,
        CancellationToken cancellationToken = default)
    {
        lookbackDays = Math.Clamp(lookbackDays, 7, 90);
        var isWeek = string.Equals(granularity, "week", StringComparison.OrdinalIgnoreCase);
        var fromUtc = DateTime.UtcNow.Date.AddDays(-lookbackDays);

        var stamps = await _db.ActivatedLicenses.AsNoTracking()
            .Where(a => a.ActivatedAtUtc >= fromUtc)
            .Select(a => a.ActivatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<LicenseActivationSeriesPointDto> points;
        if (isWeek)
        {
            points = stamps
                .GroupBy(GetUtcIsoWeekStart)
                .OrderBy(g => g.Key)
                .Select(g => new LicenseActivationSeriesPointDto(g.Key, g.Count()))
                .ToList();
        }
        else
        {
            points = stamps
                .GroupBy(ToUtcCalendarDate)
                .OrderBy(g => g.Key)
                .Select(g => new LicenseActivationSeriesPointDto(g.Key, g.Count()))
                .ToList();
        }

        return Ok(new LicenseDashboardActivationSeriesResponse(isWeek ? "week" : "day", points));
    }

    /// <summary>Recent license-related events (activations + lifecycle audit).</summary>
    [HttpGet("dashboard/recent-activity")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(LicenseDashboardRecentActivityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseDashboardRecentActivityResponse>> GetRecentActivity(
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 5, 100);
        var items = await BuildRecentLicenseActivitiesAsync(take, cancellationToken).ConfigureAwait(false);
        return Ok(new LicenseDashboardRecentActivityResponse(items));
    }

    /// <summary>CSV export of non-deleted issued licenses (masked keys, no JWT).</summary>
    [HttpGet("dashboard/report.csv")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> ExportDashboardCsv(CancellationToken cancellationToken)
    {
        var rows = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => !il.IsDeleted)
            .OrderByDescending(il => il.IssuedAtUtc)
            .Select(il => new
            {
                il.CustomerName,
                il.LicenseKey,
                il.ExpiryAtUtc,
                il.IssuedAtUtc,
                il.IsRevoked,
                il.IsCancelled,
                il.RequireFingerprint,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sb = new StringBuilder(4096);
        sb.AppendLine("customer_name,license_key_masked,issued_at_utc,expiry_at_utc,is_revoked,is_cancelled,require_fingerprint");
        foreach (var r in rows)
        {
            sb.Append(EscapeCsvField(r.CustomerName)).Append(',');
            sb.Append(EscapeCsvField(MaskIssuedLicenseKey(r.LicenseKey))).Append(',');
            sb.Append(EscapeCsvField(r.IssuedAtUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsvField(r.ExpiryAtUtc.ToString("o", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(r.IsRevoked ? '1' : '0').Append(',');
            sb.Append(r.IsCancelled ? '1' : '0').Append(',');
            sb.Append(r.RequireFingerprint ? '1' : '0');
            sb.AppendLine();
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"license-dashboard-report_{DateTime.UtcNow:yyyyMMdd_HHmmss}_UTC.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private async Task<LicenseDashboardStatsDto> BuildDashboardStatsAsync(
        int recentActivityTake,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysLater = now.AddDays(30);

        var tenantLicenseStats = await LoadVisibleTenantLicenseStatsAsync(cancellationToken).ConfigureAwait(false);
        var deploymentLicenseStats = await LoadDeploymentLicenseStatsAsync(cancellationToken).ConfigureAwait(false);

        var activatedDevices = await _db.ActivatedLicenses.AsNoTracking()
            .Where(a => a.IsActive && a.ValidUntilUtc >= now && a.MachineFingerprint != null)
            .Select(a => a.MachineFingerprint)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return new LicenseDashboardStatsDto
        {
            ActiveTenantLicenses = tenantLicenseStats.Count(t =>
                t.LicenseValidUntilUtc.HasValue
                && t.LicenseValidUntilUtc.Value > now
                && t.IsActive
                && string.Equals(t.Status, TenantStatuses.Active, StringComparison.OrdinalIgnoreCase)),
            ExpiringTenantLicenses = tenantLicenseStats.Count(t =>
                t.LicenseValidUntilUtc.HasValue
                && t.LicenseValidUntilUtc.Value > now
                && t.LicenseValidUntilUtc.Value <= thirtyDaysLater
                && t.IsActive),
            ExpiredTenantLicenses = tenantLicenseStats.Count(t =>
                t.LicenseValidUntilUtc.HasValue
                && t.LicenseValidUntilUtc.Value <= now),
            ActiveDeploymentLicenses = deploymentLicenseStats.Count(l =>
                IsActiveDeploymentLicense(l, now)),
            ExpiringDeploymentLicenses = deploymentLicenseStats.Count(l =>
                IsActiveDeploymentLicense(l, now)
                && l.ExpiryAtUtc <= thirtyDaysLater),
            ExpiredDeploymentLicenses = deploymentLicenseStats.Count(l =>
                !l.IsRevoked
                && !l.IsCancelled
                && l.ExpiryAtUtc < now),
            ActivatedDevices = activatedDevices,
            RecentActivities = recentActivityTake > 0
                ? await GetRecentLicenseActivities(recentActivityTake, cancellationToken).ConfigureAwait(false)
                : [],
        };
    }

    private async Task<List<TenantLicenseStatRow>> LoadVisibleTenantLicenseStatsAsync(CancellationToken cancellationToken)
    {
        var tenantsQuery = _db.Tenants.AsNoTracking()
            .Where(t => t.Status != TenantStatuses.Deleted);

        if (!IsActorSuperAdmin())
        {
            var actorUserId = ActorUserId;
            if (string.IsNullOrWhiteSpace(actorUserId))
                return [];

            var userTenantIds = await _db.UserTenantMemberships.AsNoTracking()
                .Where(m => m.UserId == actorUserId && m.IsActive)
                .Select(m => m.TenantId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (userTenantIds.Count == 0)
                return [];

            tenantsQuery = tenantsQuery.Where(t => userTenantIds.Contains(t.Id));
        }

        return await tenantsQuery
            .Select(t => new TenantLicenseStatRow(t.LicenseValidUntilUtc, t.IsActive, t.Status))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<DeploymentLicenseStatRow>> LoadDeploymentLicenseStatsAsync(CancellationToken cancellationToken)
    {
        return await _db.IssuedLicenses.AsNoTracking()
            .Where(l => !l.IsDeleted)
            .Select(l => new DeploymentLicenseStatRow(
                l.ExpiryAtUtc,
                l.IsRevoked,
                l.IsCancelled,
                l.SupersededByLicenseId,
                l.TransferredToLicenseId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsActiveDeploymentLicense(DeploymentLicenseStatRow license, DateTime nowUtc) =>
        !license.IsRevoked
        && !license.IsCancelled
        && license.SupersededByLicenseId == null
        && license.TransferredToLicenseId == null
        && license.ExpiryAtUtc >= nowUtc;

    /// <summary>Recent license events from audit log (primary) plus issuance/activation rows not yet audited.</summary>
    private async Task<List<LicenseActivityDto>> GetRecentLicenseActivities(
        int count,
        CancellationToken cancellationToken)
    {
        var fetch = Math.Min(80, count * 3);
        var entityType = nameof(IssuedLicense);

        var auditRows = await _db.AuditLogs.AsNoTracking()
            .Where(a =>
                EF.Functions.Like(a.Action, "LIC_%")
                || a.Action == "ACTIVATED"
                || a.Action == "GENERATED"
                || (a.EntityType == entityType && a.EntityId != null))
            .OrderByDescending(a => a.Timestamp)
            .Take(fetch)
            .Select(a => new
            {
                a.Timestamp,
                a.Action,
                a.EntityId,
                a.UserId,
                a.NewValues,
                a.Metadata,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entityIds = auditRows
            .Select(a => a.EntityId)
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var issuedById = entityIds.Count == 0
            ? new Dictionary<Guid, (string LicenseKey, string? MachineHashHex)>()
            : await _db.IssuedLicenses.AsNoTracking()
                .Where(il => entityIds.Contains(il.Id))
                .Select(il => new { il.Id, il.LicenseKey, il.MachineHashHex })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => (x.LicenseKey, x.MachineHashHex),
                    cancellationToken)
                .ConfigureAwait(false);

        var userIds = auditRows
            .Select(a => a.UserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .ToList();

        var emailsByUserId = userIds.Count == 0
            ? new Dictionary<string, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, Email = u.Email ?? u.UserName ?? string.Empty })
                .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken)
                .ConfigureAwait(false);

        var fromAudit = new List<LicenseActivityDto>(auditRows.Count);
        foreach (var row in auditRows)
        {
            var licenseKey = TryReadJsonStringProperty(row.NewValues, "licenseKeyMasked")
                ?? TryReadJsonStringProperty(row.NewValues, "licenseKey");
            var machineHash = FormatShortMachineFingerprint(
                TryReadJsonStringProperty(row.Metadata, "MachineHash")
                ?? TryReadJsonStringProperty(row.Metadata, "machineHash"));

            if (row.EntityId.HasValue && issuedById.TryGetValue(row.EntityId.Value, out var issued))
            {
                licenseKey ??= MaskIssuedLicenseKey(issued.LicenseKey);
                machineHash ??= FormatShortMachineFingerprint(issued.MachineHashHex);
            }

            fromAudit.Add(new LicenseActivityDto
            {
                Timestamp = DateTime.SpecifyKind(row.Timestamp, DateTimeKind.Utc),
                LicenseKey = licenseKey ?? "REGK-****-****-*****",
                MachineHash = machineHash ?? string.Empty,
                Action = MapAuditRowToActivityCode(row.Action),
                UserEmail = ResolveUserEmail(row.UserId, emailsByUserId),
            });
        }

        // Issuance/activation paths do not write LIC_* audit rows yet — supplement from operational tables.
        var supplemented = await SupplementUndocumentedLicenseActivitiesAsync(
            fetch,
            emailsByUserId,
            cancellationToken).ConfigureAwait(false);

        return fromAudit
            .Concat(supplemented)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToList();
    }

    private async Task<List<LicenseActivityDto>> SupplementUndocumentedLicenseActivitiesAsync(
        int fetch,
        IReadOnlyDictionary<string, string> emailsByUserId,
        CancellationToken cancellationToken)
    {
        var issuedRows = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => !il.IsDeleted)
            .OrderByDescending(il => il.IssuedAtUtc)
            .Take(fetch)
            .Select(il => new
            {
                il.IssuedAtUtc,
                il.LicenseKey,
                il.MachineHashHex,
                il.IssuedByUserId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activations = await _db.ActivatedLicenses.AsNoTracking()
            .OrderByDescending(x => x.ActivatedAtUtc)
            .Take(fetch)
            .Select(a => new { a.ActivatedAtUtc, a.LicenseKey, a.MachineFingerprint })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var supplementalUserIds = issuedRows
            .Select(r => r.IssuedByUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id) && !emailsByUserId.ContainsKey(id!))
            .Distinct()
            .ToList();

        var supplementalEmails = supplementalUserIds.Count == 0
            ? new Dictionary<string, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => supplementalUserIds.Contains(u.Id))
                .Select(u => new { u.Id, Email = u.Email ?? u.UserName ?? string.Empty })
                .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken)
                .ConfigureAwait(false);

        var emails = emailsByUserId
            .Concat(supplementalEmails)
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.First().Value);

        var generated = issuedRows.Select(r => new LicenseActivityDto
        {
            Timestamp = DateTime.SpecifyKind(r.IssuedAtUtc, DateTimeKind.Utc),
            LicenseKey = MaskIssuedLicenseKey(r.LicenseKey),
            MachineHash = FormatShortMachineFingerprint(r.MachineHashHex) ?? string.Empty,
            Action = "GENERATED",
            UserEmail = ResolveUserEmail(r.IssuedByUserId, emails),
        });

        var activated = activations.Select(a => new LicenseActivityDto
        {
            Timestamp = DateTime.SpecifyKind(a.ActivatedAtUtc, DateTimeKind.Utc),
            LicenseKey = MaskIssuedLicenseKey(a.LicenseKey),
            MachineHash = FormatShortMachineFingerprint(a.MachineFingerprint) ?? string.Empty,
            Action = "ACTIVATED",
            UserEmail = string.Empty,
        });

        return generated.Concat(activated).ToList();
    }

    private async Task<IReadOnlyList<LicenseDashboardActivityRowDto>> BuildRecentLicenseActivitiesAsync(
        int take,
        CancellationToken cancellationToken)
    {
        var items = await GetRecentLicenseActivities(take, cancellationToken).ConfigureAwait(false);
        return items.Select(MapActivityDtoToDashboardRow).ToList();
    }

    private static LicenseDashboardActivityRowDto MapActivityDtoToDashboardRow(LicenseActivityDto dto) =>
        new(
            dto.Timestamp,
            dto.LicenseKey,
            string.IsNullOrEmpty(dto.MachineHash) ? null : dto.MachineHash,
            MapActivityCodeToDashboardAction(dto.Action),
            dto.Action);

    private static string ResolveUserEmail(string? userId, IReadOnlyDictionary<string, string> emailsByUserId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return string.Empty;
        return emailsByUserId.TryGetValue(userId, out var email) ? email : string.Empty;
    }

    private static string MapAuditRowToActivityCode(string action) =>
        action switch
        {
            "ACTIVATED" => "ACTIVATED",
            "GENERATED" => "GENERATED",
            _ => MapAuditActionToActivityCode(action),
        };

    private static string? TryReadJsonStringProperty(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(propertyName, out var direct) && !TryGetCaseInsensitive(doc.RootElement, propertyName, out direct))
                return null;

            return direct.ValueKind switch
            {
                JsonValueKind.String => direct.GetString(),
                JsonValueKind.Number => direct.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetCaseInsensitive(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string MapAuditActionToActivityCode(string action) =>
        action switch
        {
            "LIC_EXTEND" => "EXTENDED",
            "LIC_REVOKE" => "REVOKED",
            "LIC_CANCEL" => "CANCELLED",
            "LIC_SOFT_DELETE" => "DELETED",
            "LIC_UNREGISTER_MACHINE" => "UNREGISTERED",
            "LIC_DETAILS_VIEW" => "DETAILS_VIEWED",
            "LIC_FORCE_DEACTIVATE_ATTEMPT" => "FORCE_DEACTIVATED",
            _ => "OTHER",
        };

    private static string MapActivityCodeToDashboardAction(string actionCode) =>
        actionCode switch
        {
            "ACTIVATED" => "activate",
            "GENERATED" => "generate",
            "REVOKED" => "revoke",
            "EXTENDED" => "extend",
            "CANCELLED" => "cancel",
            "DELETED" => "delete",
            "UNREGISTERED" => "unregister",
            "DETAILS_VIEWED" => "details",
            "FORCE_DEACTIVATED" => "force_deactivate",
            _ => "other",
        };

    private static DateTime GetUtcIsoWeekStart(DateTime utcInstant)
    {
        var d = ToUtcCalendarDate(utcInstant);
        var dow = d.DayOfWeek;
        var daysFromMonday = dow == DayOfWeek.Sunday ? 6 : (int)dow - (int)DayOfWeek.Monday;
        return d.AddDays(-daysFromMonday);
    }

    private static DateTime ToUtcCalendarDate(DateTime dt)
    {
        var asUtc = dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        };
        return new DateTime(asUtc.Year, asUtc.Month, asUtc.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static string EscapeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";
        var s = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{s}\"";
    }
}

public sealed record LicenseDashboardSummaryResponse(
    int ActiveLicenses,
    int ExpiringWithin30Days,
    int ExpiredLicenses,
    int UniqueActivatedDevices);

public sealed record LicenseActivationSeriesPointDto(DateTime PeriodStartUtc, int Count);

public sealed record LicenseDashboardActivationSeriesResponse(string Granularity, IReadOnlyList<LicenseActivationSeriesPointDto> Points);

public sealed record LicenseDashboardActivityRowDto(
    DateTime TimestampUtc,
    string LicenseKeyMasked,
    string? MachineFingerprintShort,
    string Action,
    string SourceCode);

public sealed record LicenseDashboardRecentActivityResponse(IReadOnlyList<LicenseDashboardActivityRowDto> Items);
