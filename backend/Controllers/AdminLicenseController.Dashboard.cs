using System.Globalization;
using System.Text;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Aggregated license metrics and activity for the admin dashboard.</summary>
public sealed partial class AdminLicenseController
{
    /// <summary>Issued-license counts and activated device cardinality (no JWT).</summary>
    [HttpGet("dashboard/summary")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(LicenseDashboardSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseDashboardSummaryResponse>> GetDashboardSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var in30 = now.AddDays(30);

        var baseIssued = _db.IssuedLicenses.AsNoTracking().Where(il => !il.IsDeleted);

        var activeQuery = baseIssued.Where(il =>
            !il.IsCancelled
            && !il.IsRevoked
            && il.SupersededByLicenseId == null
            && il.TransferredToLicenseId == null
            && il.ExpiryAtUtc >= now);

        var activeLicenses = await activeQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var expiringWithin30Days = await activeQuery
            .Where(il => il.ExpiryAtUtc <= in30)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var expiredLicenses = await baseIssued
            .Where(il => il.ExpiryAtUtc < now)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var uniqueActivatedDevices = await _db.ActivatedLicenses.AsNoTracking()
            .Select(a => a.MachineFingerprint)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(new LicenseDashboardSummaryResponse(
            activeLicenses,
            expiringWithin30Days,
            expiredLicenses,
            uniqueActivatedDevices));
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
        var entityType = nameof(IssuedLicense);
        var fetch = Math.Min(80, take * 3);

        var audits = await _db.AuditLogs.AsNoTracking()
            .Where(a =>
                (a.EntityType == entityType && a.EntityId != null)
                || EF.Functions.Like(a.Action, "LIC_%"))
            .OrderByDescending(a => a.Timestamp)
            .Take(fetch)
            .Select(a => new { a.Timestamp, a.Action, a.EntityId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entityIds = audits
            .Select(a => a.EntityId)
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var keyByIssuedId = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => entityIds.Contains(il.Id))
            .Select(il => new { il.Id, il.LicenseKey })
            .ToDictionaryAsync(x => x.Id, x => x.LicenseKey, cancellationToken)
            .ConfigureAwait(false);

        var auditDtos = new List<LicenseDashboardActivityRowDto>(audits.Count);
        foreach (var a in audits)
        {
            var masked = "REGK-****-****-*****";
            if (a.EntityId.HasValue && keyByIssuedId.TryGetValue(a.EntityId.Value, out var fullKey))
                masked = MaskIssuedLicenseKey(fullKey);

            auditDtos.Add(new LicenseDashboardActivityRowDto(
                a.Timestamp,
                masked,
                null,
                MapAuditActionToDashboardAction(a.Action),
                a.Action));
        }

        var activations = await _db.ActivatedLicenses.AsNoTracking()
            .OrderByDescending(x => x.ActivatedAtUtc)
            .Take(fetch)
            .Select(a => new { a.ActivatedAtUtc, a.LicenseKey, a.MachineFingerprint })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activationDtos = activations.Select(a => new LicenseDashboardActivityRowDto(
            DateTime.SpecifyKind(a.ActivatedAtUtc, DateTimeKind.Utc),
            MaskIssuedLicenseKey(a.LicenseKey),
            FormatShortMachineFingerprint(a.MachineFingerprint),
            "activate",
            "ACTIVATION")).ToList();

        var merged = auditDtos
            .Concat(activationDtos)
            .OrderByDescending(r => r.TimestampUtc)
            .Take(take)
            .ToList();

        return Ok(new LicenseDashboardRecentActivityResponse(merged));
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

    private static string MapAuditActionToDashboardAction(string action)
    {
        return action switch
        {
            "LIC_EXTEND" => "extend",
            "LIC_REVOKE" => "revoke",
            "LIC_CANCEL" => "cancel",
            "LIC_SOFT_DELETE" => "delete",
            "LIC_UNREGISTER_MACHINE" => "unregister",
            "LIC_DETAILS_VIEW" => "details",
            _ => "other",
        };
    }

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
