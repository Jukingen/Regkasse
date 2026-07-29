using System.Globalization;
using System.Text;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Tse;

public interface ISignaturkarteProgramService
{
    Task<SignaturkarteProgramStatusDto> GetStatusAsync(
        Guid? scopeTenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SignaturkarteProgramDeviceDto>> ListDevicesAsync(
        Guid? scopeTenantId,
        string? statusFilter,
        Guid? filterTenantId,
        CancellationToken cancellationToken = default);

    Task<SignaturkarteProgramMarkCompliantResponse> MarkCompliantAsync(
        Guid deviceId,
        Guid? scopeTenantId,
        bool isSuperAdmin,
        string actorUserId,
        string actorRole,
        string? note,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string FileName)> ExportCsvAsync(
        Guid? scopeTenantId,
        string? statusFilter,
        Guid? filterTenantId,
        CancellationToken cancellationToken = default);
}

public sealed class SignaturkarteProgramService : ISignaturkarteProgramService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOptionsMonitor<SignaturkarteProgramOptions> _options;
    private readonly IAuditLogService _auditLog;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SignaturkarteProgramService> _logger;

    public SignaturkarteProgramService(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptionsMonitor<SignaturkarteProgramOptions> options,
        IAuditLogService auditLog,
        TimeProvider timeProvider,
        ILogger<SignaturkarteProgramService> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _auditLog = auditLog;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<SignaturkarteProgramStatusDto> GetStatusAsync(
        Guid? scopeTenantId,
        CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var days = SignaturkarteProgramMilestones.DaysUntilDeadline(opt.DeadlineUtc, now);
        var devices = await LoadDevicesAsync(scopeTenantId, cancellationToken).ConfigureAwait(false);
        var totals = ComputeTotals(devices, opt.ExcludeDemoAndSoftDevices);

        return new SignaturkarteProgramStatusDto
        {
            Enabled = opt.Enabled,
            DisplayName = opt.DisplayName,
            DeadlineUtc = opt.DeadlineUtc.ToUniversalTime(),
            DaysRemaining = days,
            BannerSeverity = opt.Enabled
                ? SignaturkarteProgramMilestones.BannerSeverity(days, totals.NonCompliant)
                : null,
            Totals = totals,
            MilestonesNext = ResolveNextMilestone(days, opt.ReminderDaysBefore),
            IsCertificateExpiry = false,
        };
    }

    public async Task<IReadOnlyList<SignaturkarteProgramDeviceDto>> ListDevicesAsync(
        Guid? scopeTenantId,
        string? statusFilter,
        Guid? filterTenantId,
        CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var days = SignaturkarteProgramMilestones.DaysUntilDeadline(opt.DeadlineUtc, now);

        // Manager: scopeTenantId set. Super Admin: optional filterTenantId.
        Guid? loadTenant = scopeTenantId ?? filterTenantId;
        var devices = await LoadDevicesAsync(loadTenant, cancellationToken).ConfigureAwait(false);

        var mapped = devices.Select(d => MapDevice(d, days, opt)).ToList();
        if (!string.IsNullOrWhiteSpace(statusFilter)
            && !string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            mapped = mapped
                .Where(d => string.Equals(d.Status, statusFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return mapped;
    }

    public async Task<SignaturkarteProgramMarkCompliantResponse> MarkCompliantAsync(
        Guid deviceId,
        Guid? scopeTenantId,
        bool isSuperAdmin,
        string actorUserId,
        string actorRole,
        string? note,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var device = await db.TseDevices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == deviceId, cancellationToken)
            .ConfigureAwait(false);

        if (device is null)
        {
            return new SignaturkarteProgramMarkCompliantResponse
            {
                Success = false,
                DeviceId = deviceId,
                Message = "Device not found.",
            };
        }

        if (!isSuperAdmin)
        {
            if (scopeTenantId is null || device.TenantId != scopeTenantId)
            {
                return new SignaturkarteProgramMarkCompliantResponse
                {
                    Success = false,
                    DeviceId = deviceId,
                    Message = "Device not found.",
                };
            }
        }

        var opt = _options.CurrentValue;
        var status = SignaturkarteProgramClassifier.Classify(device, opt.ExcludeDemoAndSoftDevices);
        if (status is SignaturkarteProgramStatuses.Excluded or SignaturkarteProgramStatuses.Revoked)
        {
            return new SignaturkarteProgramMarkCompliantResponse
            {
                Success = false,
                DeviceId = deviceId,
                Message = "Device is excluded or revoked and cannot be marked program-compliant.",
            };
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > 500 })
            trimmedNote = trimmedNote[..500];

        device.SignaturkarteProgramCompliantAtUtc = now;
        device.SignaturkarteProgramCompliantBy = actorUserId;
        device.SignaturkarteProgramNote = trimmedNote;
        device.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _auditLog.LogSystemOperationAsync(
                    action: "SIGNATURKARTE_PROGRAM_MARKED_COMPLIANT",
                    entityType: nameof(TseDevice),
                    userId: actorUserId,
                    userRole: actorRole,
                    description: "Marked TSE device compliant for Mai 2027 Signaturkarte program.",
                    notes: trimmedNote,
                    actionType: AuditEventType.SignaturkarteProgramMarkedCompliant,
                    entityId: device.Id,
                    tenantId: device.TenantId,
                    newValues: new
                    {
                        compliantAtUtc = now,
                        note = trimmedNote,
                        serialNumber = device.SerialNumber,
                    })
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Audit failed after Signaturkarte mark-compliant for device {DeviceId}", deviceId);
        }

        return new SignaturkarteProgramMarkCompliantResponse
        {
            Success = true,
            DeviceId = device.Id,
            CompliantAtUtc = now,
        };
    }

    public async Task<(byte[] Content, string FileName)> ExportCsvAsync(
        Guid? scopeTenantId,
        string? statusFilter,
        Guid? filterTenantId,
        CancellationToken cancellationToken = default)
    {
        var devices = await ListDevicesAsync(scopeTenantId, statusFilter, filterTenantId, cancellationToken)
            .ConfigureAwait(false);
        var sb = new StringBuilder();
        sb.AppendLine(
            "TenantSlug,TenantName,DeviceId,SerialNumber,Provider,DeviceType,CertificateStatus,ExpiresAtUtc,ProgramCompliantAtUtc,ProgramCompliantBy,Status,DaysToDeadline,CertificateExpiresBeforeDeadline,Note");

        foreach (var d in devices)
        {
            sb.Append(Csv(d.TenantSlug)).Append(',')
                .Append(Csv(d.TenantName)).Append(',')
                .Append(d.DeviceId.ToString("D")).Append(',')
                .Append(Csv(d.SerialNumber)).Append(',')
                .Append(Csv(d.Provider)).Append(',')
                .Append(Csv(d.DeviceType)).Append(',')
                .Append(Csv(d.CertificateStatus)).Append(',')
                .Append(d.ExpiresAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "").Append(',')
                .Append(d.ProgramCompliantAtUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "").Append(',')
                .Append(Csv(d.ProgramCompliantBy)).Append(',')
                .Append(Csv(d.Status)).Append(',')
                .Append(d.DaysToDeadline.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(d.CertificateExpiresBeforeDeadline ? "true" : "false").Append(',')
                .Append(Csv(d.ProgramNote))
                .AppendLine();
        }

        var stamp = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return (bytes, $"signaturkarte-program-{stamp}.csv");
    }

    private static int? ResolveNextMilestone(int daysRemaining, int[] anchors)
    {
        if (anchors.Length == 0 || daysRemaining < 0)
            return null;

        var upcoming = anchors.Where(a => a < daysRemaining).OrderByDescending(a => a).Cast<int?>().FirstOrDefault();
        if (upcoming is > 0)
            return upcoming;

        if (anchors.Contains(daysRemaining))
            return daysRemaining;

        return anchors.Where(a => a > daysRemaining).OrderBy(a => a).Cast<int?>().FirstOrDefault();
    }

    private async Task<List<TseDevice>> LoadDevicesAsync(
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var query = db.TseDevices
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(d => d.Tenant)
            .AsQueryable();

        if (tenantId is { } tid)
            query = query.Where(d => d.TenantId == tid);

        return await query
            .OrderBy(d => d.Tenant != null ? d.Tenant.Slug : "")
            .ThenBy(d => d.SerialNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static SignaturkarteProgramTotalsDto ComputeTotals(
        IReadOnlyList<TseDevice> devices,
        bool excludeDemoAndSoft)
    {
        var compliant = 0;
        var open = 0;
        var excluded = 0;
        var revoked = 0;
        foreach (var device in devices)
        {
            switch (SignaturkarteProgramClassifier.Classify(device, excludeDemoAndSoft))
            {
                case SignaturkarteProgramStatuses.Compliant:
                    compliant++;
                    break;
                case SignaturkarteProgramStatuses.Open:
                    open++;
                    break;
                case SignaturkarteProgramStatuses.Excluded:
                    excluded++;
                    break;
                case SignaturkarteProgramStatuses.Revoked:
                    revoked++;
                    break;
            }
        }

        return new SignaturkarteProgramTotalsDto
        {
            Compliant = compliant,
            NonCompliant = open,
            Excluded = excluded,
            Revoked = revoked,
            Total = devices.Count,
        };
    }

    private static SignaturkarteProgramDeviceDto MapDevice(
        TseDevice d,
        int daysToDeadline,
        SignaturkarteProgramOptions opt)
    {
        var status = SignaturkarteProgramClassifier.Classify(d, opt.ExcludeDemoAndSoftDevices);
        var expiresBefore = d.ExpiresAt is { } exp
            && exp.ToUniversalTime() < opt.DeadlineUtc.ToUniversalTime()
            && status == SignaturkarteProgramStatuses.Open;

        return new SignaturkarteProgramDeviceDto
        {
            DeviceId = d.Id,
            TenantId = d.TenantId,
            TenantSlug = d.Tenant?.Slug,
            TenantName = d.Tenant?.Name,
            SerialNumber = d.SerialNumber,
            Provider = d.Provider,
            DeviceType = d.DeviceType,
            CertificateStatus = d.CertificateStatus,
            ExpiresAt = d.ExpiresAt,
            ProgramCompliantAtUtc = d.SignaturkarteProgramCompliantAtUtc,
            ProgramCompliantBy = d.SignaturkarteProgramCompliantBy,
            ProgramNote = d.SignaturkarteProgramNote,
            Status = status,
            DaysToDeadline = daysToDeadline,
            CertificateExpiresBeforeDeadline = expiresBefore,
        };
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        if (escaped.Contains(',') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r'))
            return $"\"{escaped}\"";
        return escaped;
    }
}
